using System.Text.Json;

namespace Spectra.CLI.Tests.TestFixtures;

/// <summary>
/// Spec 094: a real-init workspace with two documents (storage: "Upload quota" › "Quota settings", "Limits" twice;
/// sharing: "Links") and criteria with <c>source_doc</c> / <c>source_section</c>, plus helpers for the analysis ingest
/// and <c>compile-prompt</c>. Tests using it must be in the <see cref="CriteriaCommandsCollection"/>.
/// </summary>
public static class GenerationPlanWorkspace
{
    public const string StorageDoc =
        "# Storage\n\nIntro.\n\n## Upload quota\n\nEach user gets **30 GB**.\n\n### Quota settings\n\nShown in settings.\n\n"
        + "## Limits\n\nFirst limits.\n\n## Limits\n\nSecond limits.\n";

    public const string SharingDoc = "# Sharing\n\n## Links\n\nA link expires after 7 days.\n";

    /// <summary>AC-Q1 [Upload quota], AC-Q2 [Quota settings], AC-D1 (doc-level), AC-X1 [Nonexistent]; AC-S1 [Links].</summary>
    public const string StorageCriteria =
        "criteria:\n"
        + "  - id: AC-Q1\n    text: \"Uploads above 30 GB MUST be refused\"\n    rfc2119: MUST\n    source_type: document\n    source_doc: docs/storage.md\n    source_section: Upload quota\n    component: storage\n"
        + "  - id: AC-Q2\n    text: \"Usage MUST be shown in settings\"\n    rfc2119: MUST\n    source_type: document\n    source_doc: docs/storage.md\n    source_section: Quota settings\n    component: storage\n"
        + "  - id: AC-D1\n    text: \"Storage MUST be per user\"\n    rfc2119: MUST\n    source_type: document\n    source_doc: docs/storage.md\n    component: storage\n"
        + "  - id: AC-X1\n    text: \"Something from a section that is gone\"\n    rfc2119: MUST\n    source_type: document\n    source_doc: docs/storage.md\n    source_section: Nonexistent\n    component: storage\n";

    public const string SharingCriteria =
        "criteria:\n"
        + "  - id: AC-S1\n    text: \"Links MUST expire after 7 days\"\n    rfc2119: MUST\n    source_type: document\n    source_doc: docs/sharing.md\n    source_section: Links\n    component: storage\n";

    /// <summary>Spec 095: a plan as 094 wrote it: no duplicate_threshold, no covered_by.</summary>
    public const string Plan094 = """
        {
          "version": 1,
          "suite": "storage",
          "focus": null,
          "batch_size": 8,
          "recommended": 2,
          "planned_total": 2,
          "count_rule": "proportional",
          "behaviors": [
            {"id": "B1", "title": "Quota refused", "category": "boundary", "technique": "BVA", "source": "docs/storage.md#upload-quota", "ref": "docs/storage.md#upload-quota", "resolved": true},
            {"id": "B2", "title": "Links expire", "category": "happy_path", "technique": "EP", "source": "docs/sharing.md#links", "ref": "docs/sharing.md#links", "resolved": true}
          ],
          "sections": {},
          "batches": [
            {"batch_id": "1", "behavior_ids": ["B2"], "source_refs": ["docs/sharing.md#links"], "count": 1},
            {"batch_id": "2", "behavior_ids": ["B1"], "source_refs": ["docs/storage.md#upload-quota"], "count": 1}
          ],
          "notes": []
        }
        """;

    /// <summary>The real <c>spectra init</c>, the two documents and their criteria.</summary>
    public static async Task SetUpAsync(BaselineTestWorkspace ws, bool criteria = true)
    {
        await ws.InitWithRealTemplateAsync();
        ws.WriteDoc("docs/storage.md", StorageDoc);
        ws.WriteDoc("docs/sharing.md", SharingDoc);
        if (criteria)
        {
            ws.WriteCriteriaFile("docs/storage.criteria.yaml", StorageCriteria);
            ws.WriteCriteriaFile("docs/sharing.criteria.yaml", SharingCriteria);
        }
    }

    public static string AnalysisJson(params (string Title, string Source)[] behaviors) =>
        JsonSerializer.Serialize(new
        {
            behaviors = behaviors.Select(b => new { category = "happy_path", title = b.Title, source = b.Source, technique = "EP" })
        });

    public static async Task<(int Exit, string Out, string Err)> IngestAnalysisAsync(
        this BaselineTestWorkspace ws, string analysis, string suite = "storage")
    {
        File.WriteAllText(ws.PathOf(".spectra/analysis.json"), analysis);
        return await ws.RunAsync("ai", "ingest-analysis", "--suite", suite, "--from", ".spectra/analysis.json", "--output-format", "json");
    }

    /// <summary>Ingests <paramref name="analysis"/> and fails the test unless it succeeded.</summary>
    public static async Task PlanAsync(this BaselineTestWorkspace ws, string analysis, string suite = "storage")
    {
        var (exit, output, error) = await ws.IngestAnalysisAsync(analysis, suite);
        if (exit != 0)
        {
            throw new Xunit.Sdk.XunitException($"ingest-analysis exited {exit}\nstdout: {output}\nstderr: {error}");
        }
    }

    /// <summary><c>ai compile-prompt … --output-format json</c>, without throwing on a non-zero exit.</summary>
    public static Task<(int Exit, string Out, string Err)> CompilePromptAsync(this BaselineTestWorkspace ws, params string[] args) =>
        ws.RunAsync(["ai", "compile-prompt", .. args, "--output-format", "json"]);

    /// <summary>The stderr line starting <c>{"command":"compile-prompt"</c> (the mode line, or a failure document).</summary>
    public static JsonElement CompileLine(string stderr)
    {
        var line = stderr.Split('\n').Select(l => l.Trim()).FirstOrDefault(l => l.StartsWith("{\"command\":\"compile-prompt\"", StringComparison.Ordinal));
        if (line is null)
        {
            throw new Xunit.Sdk.XunitException($"no compile-prompt line on stderr:\n{stderr}");
        }

        return BaselineTestWorkspace.Json(line);
    }

    public static int Occurrences(string text, string needle) => UpdatePromptWorkspace.Count(text, needle);
}
