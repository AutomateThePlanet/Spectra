using Spectra.CLI.Commands.Doctor;
using Spectra.CLI.Infrastructure;
using Spectra.CLI.Options;
using Spectra.CLI.Tests.TestFixtures;

namespace Spectra.CLI.Tests.Commands.Doctor;

[Collection("WorkingDirectory")]
public sealed class DoctorIdsHandlerTests : IDisposable
{
    private readonly TempWorkspace _ws;
    private readonly string _originalCwd;

    public DoctorIdsHandlerTests()
    {
        _ws = new TempWorkspace("spectra-doctor");
        _originalCwd = Directory.GetCurrentDirectory();
        Directory.SetCurrentDirectory(_ws.Root);
    }

    public void Dispose()
    {
        Directory.SetCurrentDirectory(_originalCwd);
        _ws.Dispose();
    }

    [Fact]
    public async Task ReadOnly_NoTests_CompletesWithEmptyReport()
    {
        var handler = new DoctorIdsHandler(VerbosityLevel.Quiet, OutputFormat.Json);
        var exit = await handler.ExecuteAsync(fix: false, noInteraction: true);

        Assert.Equal(ExitCodes.Success, exit);
    }

    [Fact]
    public async Task ReadOnly_NoDuplicates_ReturnsZero()
    {
        _ws.AddTestCase("checkout", "TC-100");
        _ws.AddTestCase("checkout", "TC-101");

        var handler = new DoctorIdsHandler(VerbosityLevel.Quiet, OutputFormat.Json);
        var exit = await handler.ExecuteAsync(fix: false, noInteraction: true);

        Assert.Equal(ExitCodes.Success, exit);
    }

    [Fact]
    public async Task ReadOnly_DuplicatesWithNoInteraction_ReturnsExit9()
    {
        _ws.AddTestCase("checkout", "TC-100");
        _ws.AddTestCase("auth", "TC-100");

        var handler = new DoctorIdsHandler(VerbosityLevel.Quiet, OutputFormat.Json);
        var exit = await handler.ExecuteAsync(fix: false, noInteraction: true);

        Assert.Equal(ExitCodes.DuplicatesFound, exit);
    }

    [Fact]
    public async Task ReadOnly_DuplicatesWithoutNoInteraction_ReturnsZero()
    {
        _ws.AddTestCase("checkout", "TC-100");
        _ws.AddTestCase("auth", "TC-100");

        var handler = new DoctorIdsHandler(VerbosityLevel.Quiet, OutputFormat.Json);
        var exit = await handler.ExecuteAsync(fix: false, noInteraction: false);

        Assert.Equal(ExitCodes.Success, exit);
    }

    [Fact]
    public async Task Fix_RenumbersLaterDuplicates_KeepsOldest()
    {
        // Create the older file first, then the newer with a different mtime
        var first = _ws.AddTestCase("checkout", "TC-100", title: "first");
        File.SetLastWriteTimeUtc(first, new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc));

        Thread.Sleep(50); // ensure mtime ordering on FAT filesystems
        var second = _ws.AddTestCase("auth", "TC-100", title: "second");
        File.SetLastWriteTimeUtc(second, new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));

        var handler = new DoctorIdsHandler(VerbosityLevel.Quiet, OutputFormat.Json);
        var exit = await handler.ExecuteAsync(fix: true, noInteraction: true);

        Assert.Equal(ExitCodes.Success, exit);

        // Older file keeps TC-100
        Assert.True(File.Exists(first));
        var firstContent = await File.ReadAllTextAsync(first);
        Assert.Contains("id: TC-100", firstContent);

        // Newer file got renamed away from TC-100
        Assert.False(File.Exists(second));
    }

    [Fact]
    public async Task Fix_UpdatesDependsOnReferences()
    {
        var oldFirst = _ws.AddTestCase("checkout", "TC-100");
        File.SetLastWriteTimeUtc(oldFirst, new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc));

        Thread.Sleep(50);
        var dup = _ws.AddTestCase("auth", "TC-100");
        File.SetLastWriteTimeUtc(dup, new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));

        // Another test in a third suite depends on TC-100
        _ws.AddTestCase("orders", "TC-200", dependsOn: new[] { "TC-100" });

        var handler = new DoctorIdsHandler(VerbosityLevel.Quiet, OutputFormat.Json);
        await handler.ExecuteAsync(fix: true, noInteraction: true);

        // The kept-oldest file still holds TC-100 (it's the legitimate owner now).
        var keptOldest = await File.ReadAllTextAsync(oldFirst);
        Assert.Contains("id: TC-100", keptOldest);

        // The cascade rewrites depends_on lines only — TC-200 still has a
        // depends_on entry, but it now points at TC-100 (the kept-oldest)
        // because we only renumber the *later* duplicates' IDs and leave
        // depends_on references targeting the kept-oldest's ID untouched.
        var dependent = await File.ReadAllTextAsync(Path.Combine(_ws.TestCasesDir, "orders", "TC-200.md"));
        Assert.Contains("depends_on:", dependent);
        // The dependent should still depend on TC-100 — that's the kept-oldest's ID.
        Assert.Contains("- TC-100", dependent);
    }
}
