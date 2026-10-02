using System.Diagnostics;
using System.Text;
using Spectra.CLI.Tests.TestFixtures;

namespace Spectra.CLI.Tests.Commands.Baseline;

/// <summary>
/// Spec 084 (found while running the quickstart): the real CLI process writes UTF-8 to a redirected stdout. Before the
/// fix, Windows used the legacy console code page, so triage's <c>numbers: 10gb→50gb</c> reached the skill as
/// <c>10gb\u001A50gb</c>. In-process tests capture through a StringWriter and can't see this, so this one runs the
/// built CLI as a child process.
/// </summary>
[Collection(CriteriaCommandsCollection.Name)]
public sealed class Utf8StdoutTests : IDisposable
{
    private readonly BaselineTestWorkspace _ws = new();

    public void Dispose() => _ws.Dispose();

    private async Task<string> RunProcessAsync(params string[] args)
    {
        var info = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = _ws.Root,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            UseShellExecute = false
        };
        info.ArgumentList.Add(typeof(Program).Assembly.Location);
        foreach (var arg in args) info.ArgumentList.Add(arg);

        using var process = Process.Start(info)!;
        var stdout = await process.StandardOutput.ReadToEndAsync();
        await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        Assert.True(process.ExitCode == 0, stdout);
        return stdout;
    }

    [Fact]
    public async Task RedirectedStdout_IsUtf8()
    {
        _ws.WriteDoc("docs/über.md", "# Über\n\n## Quotas\n\nEach user gets 10GB.\n");
        _ws.WriteTest("storage", "TC-101", ["docs/über.md#Quotas"]);
        await _ws.InitBaselineAsync();
        _ws.WriteDoc("docs/über.md", "# Über\n\n## Quotas\n\nEach user gets 50GB.\n");

        var stdout = await RunProcessAsync("ai", "update-triage", "--suite", "storage", "--dry-run", "--output-format", "json");

        Assert.Contains("numbers: 10gb→50gb", stdout);
        Assert.Contains("docs/über.md#Quotas", stdout);
        Assert.False(stdout.StartsWith('﻿'));
    }
}
