using Spectra.CLI.Commands.Suite;
using Spectra.CLI.Infrastructure;
using Spectra.CLI.Options;
using Spectra.CLI.Tests.TestFixtures;

namespace Spectra.CLI.Tests.Commands.Suite;

[Collection("WorkingDirectory")]
public sealed class SuiteOpsTests : IDisposable
{
    private readonly TempWorkspace _ws;
    private readonly string _originalCwd;

    public SuiteOpsTests()
    {
        _ws = new TempWorkspace("spectra-suite");
        _originalCwd = Directory.GetCurrentDirectory();
        Directory.SetCurrentDirectory(_ws.Root);
    }

    public void Dispose()
    {
        Directory.SetCurrentDirectory(_originalCwd);
        _ws.Dispose();
    }

    [Fact]
    public async Task List_EmptyWorkspace_ReturnsZeroSuites()
    {
        var handler = new SuiteListHandler(VerbosityLevel.Quiet, OutputFormat.Json);
        var exit = await handler.ExecuteAsync();
        Assert.Equal(ExitCodes.Success, exit);
    }

    [Fact]
    public async Task List_WithSuites_ReportsCounts()
    {
        _ws.AddTestCase("checkout", "TC-100");
        _ws.AddTestCase("checkout", "TC-101", automatedBy: new[] { "tests/foo.cs" });
        _ws.AddTestCase("auth", "TC-200");

        var handler = new SuiteListHandler(VerbosityLevel.Quiet, OutputFormat.Json);
        var exit = await handler.ExecuteAsync();
        Assert.Equal(ExitCodes.Success, exit);
    }

    [Fact]
    public async Task Rename_HappyPath_Succeeds()
    {
        _ws.AddTestCase("checkout", "TC-100");
        _ws.AddTestCase("checkout", "TC-101");

        var handler = new SuiteRenameHandler(VerbosityLevel.Quiet, OutputFormat.Json);
        var exit = await handler.ExecuteAsync("checkout", "payments", dryRun: false, force: true, noInteraction: true);

        Assert.Equal(ExitCodes.Success, exit);
        Assert.False(Directory.Exists(Path.Combine(_ws.TestCasesDir, "checkout")));
        Assert.True(Directory.Exists(Path.Combine(_ws.TestCasesDir, "payments")));
        Assert.True(File.Exists(Path.Combine(_ws.TestCasesDir, "payments", "TC-100.md")));
    }

    [Fact]
    public async Task Rename_TargetExists_Exit6()
    {
        _ws.AddSuite("checkout");
        _ws.AddSuite("payments");

        var handler = new SuiteRenameHandler(VerbosityLevel.Quiet, OutputFormat.Json);
        var exit = await handler.ExecuteAsync("checkout", "payments", dryRun: false, force: true, noInteraction: true);

        Assert.Equal(ExitCodes.SuiteAlreadyExists, exit);
    }

    [Fact]
    public async Task Rename_InvalidName_Exit7()
    {
        _ws.AddSuite("checkout");

        var handler = new SuiteRenameHandler(VerbosityLevel.Quiet, OutputFormat.Json);
        var exit = await handler.ExecuteAsync("checkout", "Invalid Name", dryRun: false, force: true, noInteraction: true);

        Assert.Equal(ExitCodes.InvalidSuiteName, exit);
    }

    [Fact]
    public async Task Rename_NotFound_Exit4()
    {
        var handler = new SuiteRenameHandler(VerbosityLevel.Quiet, OutputFormat.Json);
        var exit = await handler.ExecuteAsync("missing", "payments", dryRun: false, force: true, noInteraction: true);

        Assert.Equal(ExitCodes.NotFound, exit);
    }

    [Fact]
    public async Task Rename_DryRun_NoFilesystemChange()
    {
        _ws.AddTestCase("checkout", "TC-100");

        var handler = new SuiteRenameHandler(VerbosityLevel.Quiet, OutputFormat.Json);
        var exit = await handler.ExecuteAsync("checkout", "payments", dryRun: true, force: true, noInteraction: true);

        Assert.Equal(ExitCodes.Success, exit);
        Assert.True(Directory.Exists(Path.Combine(_ws.TestCasesDir, "checkout")));
        Assert.False(Directory.Exists(Path.Combine(_ws.TestCasesDir, "payments")));
    }

    [Fact]
    public async Task Delete_HappyPath_Succeeds()
    {
        _ws.AddTestCase("checkout", "TC-100");
        _ws.AddTestCase("checkout", "TC-101");

        var handler = new SuiteDeleteHandler(VerbosityLevel.Quiet, OutputFormat.Json);
        var exit = await handler.ExecuteAsync("checkout", dryRun: false, force: true, noInteraction: true);

        Assert.Equal(ExitCodes.Success, exit);
        Assert.False(Directory.Exists(Path.Combine(_ws.TestCasesDir, "checkout")));
    }

    [Fact]
    public async Task Delete_NotFound_Exit4()
    {
        var handler = new SuiteDeleteHandler(VerbosityLevel.Quiet, OutputFormat.Json);
        var exit = await handler.ExecuteAsync("missing", dryRun: false, force: true, noInteraction: true);

        Assert.Equal(ExitCodes.NotFound, exit);
    }

    [Fact]
    public async Task Delete_AutomationLinked_RefusesByDefault()
    {
        _ws.AddTestCase("checkout", "TC-100", automatedBy: new[] { "tests/foo.cs" });

        var handler = new SuiteDeleteHandler(VerbosityLevel.Quiet, OutputFormat.Json);
        var exit = await handler.ExecuteAsync("checkout", dryRun: false, force: false, noInteraction: true);

        Assert.Equal(ExitCodes.AutomationLinked, exit);
        Assert.True(Directory.Exists(Path.Combine(_ws.TestCasesDir, "checkout")));
    }

    [Fact]
    public async Task Delete_ExternalDeps_RefusesByDefault()
    {
        _ws.AddTestCase("checkout", "TC-100");
        _ws.AddTestCase("orders", "TC-200", dependsOn: new[] { "TC-100" });

        var handler = new SuiteDeleteHandler(VerbosityLevel.Quiet, OutputFormat.Json);
        var exit = await handler.ExecuteAsync("checkout", dryRun: false, force: false, noInteraction: true);

        Assert.Equal(ExitCodes.ExternalDependencies, exit);
    }

    [Fact]
    public async Task Delete_DryRun_NoFilesystemChange()
    {
        _ws.AddTestCase("checkout", "TC-100");

        var handler = new SuiteDeleteHandler(VerbosityLevel.Quiet, OutputFormat.Json);
        var exit = await handler.ExecuteAsync("checkout", dryRun: true, force: true, noInteraction: true);

        Assert.Equal(ExitCodes.Success, exit);
        Assert.True(Directory.Exists(Path.Combine(_ws.TestCasesDir, "checkout")));
    }
}
