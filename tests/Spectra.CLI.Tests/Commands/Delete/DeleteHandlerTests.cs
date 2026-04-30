using Spectra.CLI.Commands.Delete;
using Spectra.CLI.Infrastructure;
using Spectra.CLI.Options;
using Spectra.CLI.Tests.TestFixtures;

namespace Spectra.CLI.Tests.Commands.Delete;

[Collection("WorkingDirectory")]
public sealed class DeleteHandlerTests : IDisposable
{
    private readonly TempWorkspace _ws;
    private readonly string _originalCwd;

    public DeleteHandlerTests()
    {
        _ws = new TempWorkspace("spectra-delete");
        _originalCwd = Directory.GetCurrentDirectory();
        Directory.SetCurrentDirectory(_ws.Root);
    }

    public void Dispose()
    {
        Directory.SetCurrentDirectory(_originalCwd);
        _ws.Dispose();
    }

    [Fact]
    public async Task DeleteSingle_NoAutomation_NoDependents_Succeeds()
    {
        var path = _ws.AddTestCase("checkout", "TC-100", title: "Test 100");

        var handler = new DeleteHandler(VerbosityLevel.Quiet, OutputFormat.Json);
        var exit = await handler.ExecuteAsync(
            ids: new[] { "TC-100" },
            suiteAlias: null,
            dryRun: false,
            force: true,
            noAutomationCheck: false,
            noInteraction: true);

        Assert.Equal(ExitCodes.Success, exit);
        Assert.False(File.Exists(path));
    }

    [Fact]
    public async Task Delete_DryRun_TouchesNoFiles()
    {
        var path = _ws.AddTestCase("checkout", "TC-100");

        var handler = new DeleteHandler(VerbosityLevel.Quiet, OutputFormat.Json);
        var exit = await handler.ExecuteAsync(
            ids: new[] { "TC-100" },
            suiteAlias: null,
            dryRun: true,
            force: true,
            noAutomationCheck: false,
            noInteraction: true);

        Assert.Equal(ExitCodes.Success, exit);
        Assert.True(File.Exists(path));
    }

    [Fact]
    public async Task Delete_AutomationLinked_RefusesByDefault_Exit5()
    {
        var path = _ws.AddTestCase("checkout", "TC-100", automatedBy: new[] { "tests/foo.cs" });

        var handler = new DeleteHandler(VerbosityLevel.Quiet, OutputFormat.Json);
        var exit = await handler.ExecuteAsync(
            ids: new[] { "TC-100" },
            suiteAlias: null,
            dryRun: false,
            force: false,
            noAutomationCheck: false,
            noInteraction: true);

        Assert.Equal(ExitCodes.AutomationLinked, exit);
        Assert.True(File.Exists(path), "automation-linked test must remain on disk");
    }

    [Fact]
    public async Task Delete_AutomationLinked_WithForce_Proceeds()
    {
        var path = _ws.AddTestCase("checkout", "TC-100", automatedBy: new[] { "tests/foo.cs" });

        var handler = new DeleteHandler(VerbosityLevel.Quiet, OutputFormat.Json);
        var exit = await handler.ExecuteAsync(
            ids: new[] { "TC-100" },
            suiteAlias: null,
            dryRun: false,
            force: true,
            noAutomationCheck: false,
            noInteraction: true);

        Assert.Equal(ExitCodes.Success, exit);
        Assert.False(File.Exists(path));
    }

    [Fact]
    public async Task Delete_NotFound_Exit4()
    {
        _ws.AddTestCase("checkout", "TC-100");

        var handler = new DeleteHandler(VerbosityLevel.Quiet, OutputFormat.Json);
        var exit = await handler.ExecuteAsync(
            ids: new[] { "TC-999" },
            suiteAlias: null,
            dryRun: false,
            force: true,
            noAutomationCheck: false,
            noInteraction: true);

        Assert.Equal(ExitCodes.NotFound, exit);
    }

    [Fact]
    public async Task Delete_CleansUpDependsOn()
    {
        var deletePath = _ws.AddTestCase("checkout", "TC-100");
        var dependentPath = _ws.AddTestCase("checkout", "TC-200", dependsOn: new[] { "TC-100" });

        var handler = new DeleteHandler(VerbosityLevel.Quiet, OutputFormat.Json);
        var exit = await handler.ExecuteAsync(
            ids: new[] { "TC-100" },
            suiteAlias: null,
            dryRun: false,
            force: true,
            noAutomationCheck: false,
            noInteraction: true);

        Assert.Equal(ExitCodes.Success, exit);
        Assert.False(File.Exists(deletePath));
        Assert.True(File.Exists(dependentPath));

        var dependentContent = await File.ReadAllTextAsync(dependentPath);
        Assert.DoesNotContain("- TC-100", dependentContent);
    }

    [Fact]
    public async Task Delete_BulkOfTwo_Succeeds()
    {
        var p1 = _ws.AddTestCase("checkout", "TC-100");
        var p2 = _ws.AddTestCase("checkout", "TC-101");

        var handler = new DeleteHandler(VerbosityLevel.Quiet, OutputFormat.Json);
        var exit = await handler.ExecuteAsync(
            ids: new[] { "TC-100", "TC-101" },
            suiteAlias: null,
            dryRun: false,
            force: true,
            noAutomationCheck: false,
            noInteraction: true);

        Assert.Equal(ExitCodes.Success, exit);
        Assert.False(File.Exists(p1));
        Assert.False(File.Exists(p2));
    }

    [Fact]
    public async Task Delete_NoArguments_ReturnsMissingArguments()
    {
        var handler = new DeleteHandler(VerbosityLevel.Quiet, OutputFormat.Json);
        var exit = await handler.ExecuteAsync(
            ids: Array.Empty<string>(),
            suiteAlias: null,
            dryRun: false,
            force: false,
            noAutomationCheck: false,
            noInteraction: true);

        Assert.Equal(ExitCodes.MissingArguments, exit);
    }
}
