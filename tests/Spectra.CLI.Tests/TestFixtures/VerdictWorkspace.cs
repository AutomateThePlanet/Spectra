using System.Text.Json;
using Spectra.CLI.IO;
using Spectra.Core.Models;
using Spectra.Core.Models.Grounding;

namespace Spectra.CLI.Tests.TestFixtures;

/// <summary>
/// Spec 090: critic-verdict helpers over a <see cref="BaselineTestWorkspace"/>. Uses only APIs that exist on
/// <c>fc82b7a</c>, so the regression-proof tests compile against the sources before 090.
/// </summary>
public static class VerdictWorkspace
{
    /// <summary>Writes <c>.spectra/verdicts/critic-verdict-{id}.json</c> in the critic's JSON shape.</summary>
    public static string WriteVerdict(this BaselineTestWorkspace ws, string id, string verdict, double score = 0.9)
    {
        var path = ws.VerdictPath(id);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var findings = verdict switch
        {
            "grounded" => """[{"element":"Step 1","claim":"step action","status":"grounded","evidence":"doc says so"}]""",
            "partial" => """[{"element":"Step 1","claim":"step action","status":"grounded","evidence":"doc says so"},{"element":"Expected Result","claim":"exact value","status":"unverified","reason":"not in docs"}]""",
            "hallucinated" => """[{"element":"Step 2","claim":"1 KB = 1000 bytes","status":"hallucinated","reason":"contradicts docs"}]""",
            _ => throw new ArgumentException($"Unknown verdict: {verdict}")
        };
        File.WriteAllText(path,
            $$"""{"verdict":"{{verdict}}","score":{{score.ToString(System.Globalization.CultureInfo.InvariantCulture)}},"critic_model":"claude-sonnet-4-6","findings":{{findings}}}""");
        return path;
    }

    public static string VerdictPath(this BaselineTestWorkspace ws, string id) =>
        ws.PathOf($".spectra/verdicts/critic-verdict-{id}.json");

    /// <summary>Spec 090 (FR-A2): where <c>compile-critic-prompt</c> records the fingerprint of the content it judged.</summary>
    public static string VerdictRecordPath(this BaselineTestWorkspace ws, string id) =>
        ws.PathOf($".spectra/verdicts/critic-input-{id}.json");

    public static string TestPath(this BaselineTestWorkspace ws, string suite, string id) =>
        ws.PathOf($"test-cases/{suite}/{id}.md");

    /// <summary>The test's parsed grounding block, or null.</summary>
    public static GroundingMetadata? GroundingOf(this BaselineTestWorkspace ws, string suite, string id) =>
        UpdatePromptWorkspace.Load(ws, suite, id).Grounding;

    /// <summary>Writes a <c>verdict: manual</c> grounding block through the lossless writer.</summary>
    public static async Task WriteManualGroundingAsync(this BaselineTestWorkspace ws, string suite, string id)
    {
        var test = UpdatePromptWorkspace.Load(ws, suite, id) with
        {
            Grounding = new GroundingMetadata
            {
                Verdict = VerificationVerdict.Manual,
                Score = 1.0,
                Generator = "user",
                Critic = "none",
                VerifiedAt = new DateTimeOffset(2026, 10, 1, 0, 0, 0, TimeSpan.Zero),
                Source = "manual",
                Note = "Checked by hand"
            }
        };
        await new TestFileWriter().WriteAsync(ws.TestPath(suite, id), test);
    }

    /// <summary>Writes a critic grounding block (as <c>ingest-grounding</c> would) through the lossless writer.</summary>
    public static async Task WriteCriticGroundingAsync(
        this BaselineTestWorkspace ws, string suite, string id, VerificationVerdict verdict = VerificationVerdict.Grounded,
        bool flagged = false)
    {
        var test = UpdatePromptWorkspace.Load(ws, suite, id) with
        {
            Grounding = new GroundingMetadata
            {
                Verdict = verdict,
                Score = verdict == VerificationVerdict.Grounded ? 0.95 : 0.72,
                Generator = "claude-opus",
                Critic = "claude-sonnet-4-6",
                VerifiedAt = new DateTimeOffset(2026, 10, 1, 0, 0, 0, TimeSpan.Zero),
                FlaggedForReview = flagged
            }
        };
        await new TestFileWriter().WriteAsync(ws.TestPath(suite, id), test);
    }

    public static Task<(int Exit, string Out, string Err)> CompileCriticAsync(this BaselineTestWorkspace ws, string suite, string id) =>
        ws.RunAsync("ai", "compile-critic-prompt", "--suite", suite, "--test", id);

    public static Task<(int Exit, string Out, string Err)> IngestVerdictAllAsync(this BaselineTestWorkspace ws, string suite = "storage") =>
        ws.RunAsync("ai", "ingest-verdict", "--suite", suite, "--all", "--output-format", "json");

    public static Task<(int Exit, string Out, string Err)> IngestGroundingAsync(this BaselineTestWorkspace ws, params string[] args) =>
        ws.RunAsync(["ai", "ingest-grounding", .. args, "--output-format", "json"]);

    public static Task<(int Exit, string Out, string Err)> AuditGroundingJsonAsync(this BaselineTestWorkspace ws, string suite = "storage") =>
        ws.RunAsync("ai", "audit-grounding", "--suite", suite, "--output-format", "json");

    public static JsonElement Json(string text) => BaselineTestWorkspace.Json(text);
}
