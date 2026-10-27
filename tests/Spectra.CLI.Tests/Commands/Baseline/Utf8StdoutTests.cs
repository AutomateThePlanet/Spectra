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
        var (exit, stdout, _) = await RunRawAsync(args);
        Assert.True(exit == 0, Encoding.UTF8.GetString(stdout));
        return Encoding.UTF8.GetString(stdout);
    }

    /// <summary>Runs the built CLI with both streams redirected, returning their raw bytes.</summary>
    private async Task<(int Exit, byte[] Out, byte[] Err)> RunRawAsync(params string[] args)
    {
        var info = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = _ws.Root,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };
        info.ArgumentList.Add(typeof(Program).Assembly.Location);
        foreach (var arg in args) info.ArgumentList.Add(arg);

        using var process = Process.Start(info)!;
        using var stdout = new MemoryStream();
        using var stderr = new MemoryStream();
        await Task.WhenAll(
            process.StandardOutput.BaseStream.CopyToAsync(stdout),
            process.StandardError.BaseStream.CopyToAsync(stderr));
        await process.WaitForExitAsync();
        return (process.ExitCode, stdout.ToArray(), stderr.ToArray());
    }

    private static bool HasBom(byte[] bytes) => bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF;

    // Spec 085 F6 (T8): stderr is UTF-8 without a BOM too, independently of stdout.
    [Fact]
    public async Task RedirectedStderr_IsUtf8()
    {
        var (exit, _, stderr) = await RunRawAsync("ai", "update-triage", "--suite", "nöpe", "--output-format", "json");

        Assert.Equal(4, exit);
        Assert.False(HasBom(stderr));
        var text = new UTF8Encoding(false, throwOnInvalidBytes: true).GetString(stderr);
        Assert.Contains("nöpe", text);
        Assert.Contains("SUITE_NOT_FOUND", text);
    }

    // Spec 085 F6 (T8): assigning Console.OutputEncoding also switches the user's terminal code page, which persists
    // after the command exits — only redirected writers may be replaced (Infrastructure/ConsoleEncoding).
    [Fact]
    public void Program_DoesNotAssignConsoleOutputEncoding()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Spectra.slnx")))
        {
            dir = dir.Parent;
        }

        Assert.NotNull(dir);
        var program = File.ReadAllText(Path.Combine(dir!.FullName, "src", "Spectra.CLI", "Program.cs"));
        Assert.False(System.Text.RegularExpressions.Regex.IsMatch(program, @"Console\.OutputEncoding\s*="),
            "Program.cs must not assign Console.OutputEncoding: it changes the user's terminal code page. "
            + "Use ConsoleEncoding.UseUtf8ForRedirectedStreams().");
        Assert.Contains("ConsoleEncoding.UseUtf8ForRedirectedStreams()", program);
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
