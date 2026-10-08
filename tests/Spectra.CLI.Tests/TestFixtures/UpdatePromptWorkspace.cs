using System.Text.Json;
using Microsoft.Extensions.Logging;
using Spectra.CLI.Commands.Init;
using Spectra.CLI.Generation;
using Spectra.CLI.Infrastructure;
using Spectra.Core.Models;
using Spectra.Core.Parsing;

namespace Spectra.CLI.Tests.TestFixtures;

/// <summary>Spec 088: update-prompt helpers over a <see cref="BaselineTestWorkspace"/>.</summary>
public static class UpdatePromptWorkspace
{
    /// <summary>Runs <c>ai compile-update-prompt</c> in human mode: the prompt is stdout.</summary>
    public static Task<(int Exit, string Out, string Err)> CompileAsync(this BaselineTestWorkspace ws, params string[] args) =>
        ws.RunAsync(["ai", "compile-update-prompt", .. args]);

    /// <summary>Runs <c>ai compile-update-prompt</c> in JSON mode (refusals are one JSON document on stderr).</summary>
    public static Task<(int Exit, string Out, string Err)> CompileJsonAsync(this BaselineTestWorkspace ws, params string[] args) =>
        ws.RunAsync(["ai", "compile-update-prompt", .. args, "--output-format", "json"]);

    /// <summary>Writes <c>.spectra/updates/{suite}/updated-{id}.json</c>: a JSON array with the test, as the skill stages it.</summary>
    public static string StageEdit(this BaselineTestWorkspace ws, string suite, string fileId, TestCase edited)
    {
        var path = ws.PathOf($".spectra/updates/{suite}/updated-{fileId}.json");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "[\n" + UpdatePromptCompiler.SerializeTest(edited) + "\n]\n");
        return path;
    }

    public static string StageRaw(this BaselineTestWorkspace ws, string suite, string fileId, string content)
    {
        var path = ws.PathOf($".spectra/updates/{suite}/updated-{fileId}.json");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
        return path;
    }

    public static string[] StagedFiles(this BaselineTestWorkspace ws, string suite)
    {
        var dir = ws.PathOf($".spectra/updates/{suite}");
        return Directory.Exists(dir)
            ? Directory.GetFiles(dir).Select(f => Path.GetFileName(f)).OrderBy(n => n, StringComparer.Ordinal).ToArray()
            : [];
    }

    /// <summary>Spec 089: writes <c>.spectra/repairs/{suite}/repaired-{id}.json</c>, as the repair loop stages a repair.</summary>
    public static string StageRepair(this BaselineTestWorkspace ws, string suite, string fileId, TestCase repaired) =>
        ws.StageRepairRaw(suite, fileId, "[\n" + UpdatePromptCompiler.SerializeTest(repaired) + "\n]\n");

    public static string StageRepairRaw(this BaselineTestWorkspace ws, string suite, string fileId, string content)
    {
        var path = ws.PathOf($".spectra/repairs/{suite}/repaired-{fileId}.json");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
        return path;
    }

    public static string[] RepairFiles(this BaselineTestWorkspace ws, string suite)
    {
        var dir = ws.PathOf($".spectra/repairs/{suite}");
        return Directory.Exists(dir)
            ? Directory.GetFiles(dir).Select(f => Path.GetFileName(f)).OrderBy(n => n, StringComparer.Ordinal).ToArray()
            : [];
    }

    /// <summary>Spec 089: <c>ai ingest-update {suite} --all … --output-format json</c>, without throwing on a non-zero exit.</summary>
    public static Task<(int Exit, string Out, string Err)> IngestAllAsync(
        this BaselineTestWorkspace ws, string suite = "storage", params string[] extra) =>
        ws.RunAsync(["ai", "ingest-update", suite, "--all", .. extra, "--output-format", "json"]);

    /// <summary>Spec 089: runs the real <c>spectra init</c> (embedded template, no skills) in the workspace.</summary>
    public static async Task InitWithRealTemplateAsync(this BaselineTestWorkspace ws)
    {
        var logger = LoggingSetup.CreateLoggerFactory(VerbosityLevel.Quiet).CreateLogger<InitHandler>();
        var exit = await new InitHandler(logger, ws.Root).HandleAsync(force: false, skipSkills: true);
        if (exit != ExitCodes.Success)
        {
            throw new Xunit.Sdk.XunitException($"spectra init exited {exit}");
        }
    }

    /// <summary>The test as it is on disk.</summary>
    public static TestCase Load(this BaselineTestWorkspace ws, string suite, string id)
    {
        var file = ws.PathOf($"test-cases/{suite}/{id}.md");
        return new TestCaseParser().Parse(File.ReadAllText(file), $"{id}.md").Value!;
    }

    /// <summary>Non-overlapping occurrences of <paramref name="needle"/>.</summary>
    public static int Count(string text, string needle)
    {
        var count = 0;
        for (var at = text.IndexOf(needle, StringComparison.Ordinal); at >= 0; at = text.IndexOf(needle, at + needle.Length, StringComparison.Ordinal))
        {
            count++;
        }

        return count;
    }

    /// <summary>The fingerprint the suite baseline holds for a test's ref, or null.</summary>
    public static string? RefFingerprint(this BaselineTestWorkspace ws, string suite, string id, string sourceRef)
    {
        var text = ws.ReadBaseline(suite);
        if (text is null) return null;
        var tests = BaselineTestWorkspace.Json(text).GetProperty("tests");
        return tests.TryGetProperty(id, out var entry) && entry.GetProperty("refs").TryGetProperty(sourceRef, out var fp)
            ? fp.GetString()
            : null;
    }

    public static JsonElement Failure(string stderr) => BaselineTestWorkspace.Json(stderr);
}
