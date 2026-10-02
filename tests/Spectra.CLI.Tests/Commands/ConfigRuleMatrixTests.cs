using System.Text.Json;
using System.Text.Json.Nodes;
using Spectra.CLI.Commands.Init;
using Spectra.CLI.Tests.TestFixtures;

namespace Spectra.CLI.Tests.Commands;

/// <summary>
/// Spec 083 T1 (FR-001, FR-014, SC-003): the one config rule, as a table. Every row is built from the REAL embedded init
/// template (the config `spectra init` writes), never from a hand-written fixture — the gap that let 080–082 each add a
/// rule no real workspace satisfied. Each row runs one read command (the critic compiler, which reads `source` and
/// `tests`) and one write command (`ingest-criteria`, which reads `tests`) and checks the exit code, the error code,
/// the exact number of config warnings, and where a successful write put the criteria.
/// </summary>
[Collection(CriteriaCommandsCollection.Name)]
public sealed class ConfigRuleMatrixTests : IDisposable
{
    private const string ConfigWarning = "spectra.config.json has no";
    private const string CriteriaFile = "docs/criteria/docs/a.criteria.yaml";

    private readonly CriteriaTestWorkspace _ws = new();

    public ConfigRuleMatrixTests()
    {
        _ws.WriteDoc("docs/a.md", "# A\n\nUsers MUST see A.\n");
        _ws.WriteDoc("test-cases/s/TC-1.md",
            "---\nid: TC-1\npriority: medium\nsource_refs:\n  - docs/a.md\n---\n\n# See A\n\n## Steps\n\n1. open\n\n## Expected Result\n\n- ok\n");
        _ws.WriteDoc("test-cases/s/_index.json",
            "{\"suite\":\"s\",\"generated_at\":\"2026-01-01T00:00:00Z\",\"tests\":[{\"id\":\"TC-1\",\"file\":\"TC-1.md\",\"title\":\"See A\",\"priority\":\"medium\"}]}");
        _ws.WriteDoc(".spectra/criteria.json", "[{\"text\":\"Users MUST see A\",\"rfc2119\":\"MUST\"}]");
    }

    public void Dispose() => _ws.Dispose();

    /// <summary>The config `spectra init` writes, read from the CLI assembly's embedded resource.</summary>
    public static string InitTemplate()
    {
        using var stream = typeof(InitHandler).Assembly.GetManifestResourceStream("Spectra.CLI.Templates.spectra.config.json")
            ?? throw new InvalidOperationException("The embedded init template is missing.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    private static string FromTemplate(Action<JsonObject> change)
    {
        var root = JsonNode.Parse(InitTemplate(), documentOptions: new JsonDocumentOptions
        {
            CommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true
        })!.AsObject();
        change(root);
        return root.ToJsonString();
    }

    /// <summary>Config content per row; <c>null</c> means "no spectra.config.json".</summary>
    private static readonly Dictionary<string, string?> Configs = new()
    {
        ["the init template"] = FromTemplate(_ => { }),
        ["no file"] = null,
        ["malformed JSON"] = "{\"source\": {",
        ["root is an array"] = "[]",
        ["root is null"] = "null",
        ["source missing"] = FromTemplate(r => r.Remove("source")),
        ["source null"] = FromTemplate(r => r["source"] = null),
        ["tests missing"] = FromTemplate(r => r.Remove("tests")),
        ["tests null"] = FromTemplate(r => r["tests"] = null),
        ["coverage missing"] = FromTemplate(r => r.Remove("coverage")),
        ["coverage null"] = FromTemplate(r => r["coverage"] = null),
        ["ai null (not read)"] = FromTemplate(r => r["ai"] = null),
        ["generation missing (not read)"] = FromTemplate(r => r.Remove("generation")),
        ["tests / Tests duplicate"] = FromTemplate(r => r["Tests"] = null),
        ["generation / Generation duplicate"] = FromTemplate(r => r["Generation"] = new JsonObject()),
        // Spec 084 FR-029: the optional update-triage keys.
        ["update null"] = FromTemplate(r => r["update"] = null),
        ["update.group_size null"] = FromTemplate(r => r["update"] = new JsonObject { ["group_size"] = null }),
        ["update.group_size 0"] = FromTemplate(r => r["update"] = new JsonObject { ["group_size"] = 0 })
    };

    public static TheoryData<string, int, string?, int, int, string?, int> Rows => new()
    {
        // row,                               critic exit, code, warnings,   ingest exit, code, warnings
        { "the init template",                0, null, 0,                   0, null, 0 },
        { "no file",                          0, null, 0,                   1, "CONFIG_MISSING", 0 },
        { "malformed JSON",                   1, "CONFIG_INVALID", 0,       1, "CONFIG_INVALID", 0 },
        { "root is an array",                 1, "CONFIG_INVALID", 0,       1, "CONFIG_INVALID", 0 },
        { "root is null",                     1, "CONFIG_INVALID", 0,       1, "CONFIG_INVALID", 0 },
        { "source missing",                   0, null, 1,                   0, null, 0 },
        { "source null",                      0, null, 1,                   0, null, 0 },
        { "tests missing",                    0, null, 1,                   0, null, 1 },
        { "tests null",                       0, null, 1,                   0, null, 1 },
        { "coverage missing",                 0, null, 0,                   0, null, 0 },
        { "coverage null",                    0, null, 0,                   0, null, 0 },
        { "ai null (not read)",               0, null, 0,                   0, null, 0 },
        { "generation missing (not read)",    0, null, 0,                   0, null, 0 },
        { "tests / Tests duplicate",          1, "CONFIG_INVALID", 0,       1, "CONFIG_INVALID", 0 },
        { "generation / Generation duplicate", 1, "CONFIG_INVALID", 0,      1, "CONFIG_INVALID", 0 },
        { "update null",                      0, null, 0,                   0, null, 0 },
        { "update.group_size null",           0, null, 0,                   0, null, 0 },
        { "update.group_size 0",              1, "CONFIG_INVALID", 0,       1, "CONFIG_INVALID", 0 }
    };

    private void UseConfig(string row)
    {
        var path = _ws.PathOf("spectra.config.json");
        if (Configs[row] is { } content)
            File.WriteAllText(path, content);
        else
            File.Delete(path);
    }

    private static string? ErrorCode(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        var doc = CriteriaTestWorkspace.Json(json);
        return doc.TryGetProperty("error_code", out var code) && code.ValueKind == JsonValueKind.String ? code.GetString() : null;
    }

    private static int JsonConfigWarnings(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) return 0;
        var doc = CriteriaTestWorkspace.Json(json);
        return doc.TryGetProperty("warnings", out var warnings) && warnings.ValueKind == JsonValueKind.Array
            ? warnings.EnumerateArray().Count(w => w.GetString()?.Contains(ConfigWarning, StringComparison.Ordinal) == true)
            : 0;
    }

    private static int Occurrences(string text) =>
        (text.Length - text.Replace(ConfigWarning, "").Length) / ConfigWarning.Length;

    [Theory]
    [MemberData(nameof(Rows))]
    public async Task ConfigRule(string row,
        int criticExit, string? criticCode, int criticWarnings,
        int ingestExit, string? ingestCode, int ingestWarnings)
    {
        UseConfig(row);

        var critic = await _ws.RunAsync("ai", "compile-critic-prompt", "--suite", "s", "--test", "TC-1", "--output-format", "json");
        var ingest = await _ws.RunAsync("ai", "ingest-criteria", "--doc", "docs/a.md", "--from", ".spectra/criteria.json", "--output-format", "json");

        Assert.True(critic.Exit == criticExit, $"{row}: critic exit {critic.Exit}: {critic.Err}");
        Assert.Equal(criticCode, criticExit == 0 ? null : ErrorCode(critic.Err));
        Assert.True(criticWarnings == Occurrences(critic.Err), $"{row}: critic warnings, stderr: {critic.Err}");

        Assert.True(ingest.Exit == ingestExit, $"{row}: ingest exit {ingest.Exit}: {ingest.Err}");
        Assert.Equal(ingestCode, ingestExit == 0 ? null : ErrorCode(ingest.Err));
        // Ingest reports each warning in its JSON `warnings` (stdout on success, the failure document otherwise) and,
        // because human output is never suppressed in JSON mode, as a stderr line too — count the JSON entries.
        Assert.True(ingestWarnings == JsonConfigWarnings(ingest.Exit == 0 ? ingest.Out : ingest.Err),
            $"{row}: ingest warnings, stdout: {ingest.Out} stderr: {ingest.Err}");

        // A successful write lands in the built-in / template criteria location.
        Assert.Equal(ingestExit == 0, File.Exists(_ws.PathOf(CriteriaFile)));

        if (row.Contains("duplicate", StringComparison.Ordinal))
        {
            var key = row.Split(' ')[0];
            Assert.Contains($"'{key}'", critic.Err, StringComparison.Ordinal);
            Assert.Contains($"'{key}'", ingest.Err, StringComparison.Ordinal);
        }
    }

    /// <summary>An unreadable config file (an exclusive handle blocks reads reliably on Windows only).</summary>
    [Fact]
    public async Task UnreadableConfig_IsInvalid()
    {
        if (!OperatingSystem.IsWindows()) return;

        UseConfig("the init template");
        using var hold = new FileStream(_ws.PathOf("spectra.config.json"), FileMode.Open, FileAccess.Read, FileShare.None);

        var critic = await _ws.RunAsync("ai", "compile-critic-prompt", "--suite", "s", "--test", "TC-1", "--output-format", "json");
        var ingest = await _ws.RunAsync("ai", "ingest-criteria", "--doc", "docs/a.md", "--from", ".spectra/criteria.json", "--output-format", "json");

        Assert.Equal(1, critic.Exit);
        Assert.Equal("CONFIG_INVALID", ErrorCode(critic.Err));
        Assert.Equal(1, ingest.Exit);
        Assert.Equal("CONFIG_INVALID", ErrorCode(ingest.Err));
    }
}
