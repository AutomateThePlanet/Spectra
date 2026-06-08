using Spectra.CLI.Commands.Run;
using Spectra.CLI.Commands.Run.WebConsole;
using Spectra.MCP.Storage;

namespace Spectra.CLI.Tests.Commands.Run.WebConsole;

/// <summary>
/// Spec 066 (US3): a browser upload routes through the existing <c>ScreenshotService</c> +
/// <c>AppendScreenshotPathAsync</c> into the result's <c>screenshot_paths</c> — no new storage model and
/// no browser-side authoritative copy (FR-006, SC-004).
/// </summary>
public class ConsoleScreenshotTests
{
    [Fact]
    public async Task Upload_AttachesToCurrentTestResult()
    {
        using var ws = new RunTestWorkspace();
        ws.WriteSuite("s", ("TC-001", "A", "high", null));
        await using var services = new RunServices(ws.Root);
        await services.Engine.StartRunAsync("s", services.IndexLoader("s").ToList());

        // Arbitrary non-empty bytes — ScreenshotService stores them (WebP encode or raw fallback).
        var resp = await new ConsoleEndpoints(services).ScreenshotAsync(new byte[] { 1, 2, 3, 4, 5 });
        Assert.Equal(200, resp.StatusCode);

        var paths = await ReadScreenshotPaths(ws.Root, "TC-001");
        Assert.Single(paths);
    }

    [Fact]
    public async Task Upload_EmptyBytes_Rejected()
    {
        using var ws = new RunTestWorkspace();
        ws.WriteSuite("s", ("TC-001", "A", "high", null));
        await using var services = new RunServices(ws.Root);
        await services.Engine.StartRunAsync("s", services.IndexLoader("s").ToList());

        var resp = await new ConsoleEndpoints(services).ScreenshotAsync(Array.Empty<byte>());

        Assert.Equal(400, resp.StatusCode);
        Assert.Equal("SCREENSHOT_INVALID", ((ConsoleError)resp.Body!).ErrorCode);
        Assert.Empty(await ReadScreenshotPaths(ws.Root, "TC-001"));
    }

    private static async Task<IReadOnlyList<string>> ReadScreenshotPaths(string root, string testId)
    {
        await using var db = new ExecutionDb(Path.Combine(root, ".execution"));
        var runs = await new RunRepository(db).GetAllAsync(limit: 50);
        var resultRepo = new ResultRepository(db);
        foreach (var r in runs)
        {
            var row = (await resultRepo.GetByRunIdAsync(r.RunId)).FirstOrDefault(x => x.TestId == testId);
            if (row is not null) return row.ScreenshotPaths ?? (IReadOnlyList<string>)Array.Empty<string>();
        }
        return Array.Empty<string>();
    }
}
