using System.Text.Json;
using Spectra.CLI.Commands.Docs;
using Spectra.CLI.Infrastructure;
using Spectra.CLI.Output;

namespace Spectra.CLI.Tests.Commands.Docs;

/// <summary>
/// Spec 069 (FR-005): <c>spectra docs changed</c> classifies each source doc as new / changed /
/// unchanged by comparing its SHA-256 against the recorded doc_hash in _criteria_index.yaml.
/// Model-free; drives the skill's incremental skip.
/// </summary>
public sealed class DocsChangedTests : IDisposable
{
    private readonly string _dir;

    public DocsChangedTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), $"spectra-docs-changed-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_dir);
        Directory.CreateDirectory(Path.Combine(_dir, "docs"));
        Directory.CreateDirectory(Path.Combine(_dir, "docs", "criteria"));
        File.WriteAllText(Path.Combine(_dir, "spectra.config.json"),
            """{ "source": { "mode": "local", "local_dir": "docs/" }, "tests": { "dir": "test-cases/" }, "ai": { "providers": [] } }""");
    }

    public void Dispose()
    {
        try { if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true); }
        catch (IOException) { }
    }

    [Fact]
    public async Task Classifies_New_Changed_And_Unchanged()
    {
        // unchanged.md → index hash matches; changed.md → index hash stale; new.md → not in index.
        var unchangedContent = "# Unchanged\nThe system MUST do X.\n";
        var changedContent = "# Changed\nThe system MUST do Y now.\n";
        File.WriteAllText(Path.Combine(_dir, "docs", "unchanged.md"), unchangedContent);
        File.WriteAllText(Path.Combine(_dir, "docs", "changed.md"), changedContent);
        File.WriteAllText(Path.Combine(_dir, "docs", "new.md"), "# New\nThe system MUST do Z.\n");

        var unchangedHash = FileHasher.ComputeHash(unchangedContent);

        // Seed the criteria index: unchanged.md with the real hash, changed.md with a stale hash.
        File.WriteAllText(Path.Combine(_dir, "docs", "criteria", "_criteria_index.yaml"),
            $"""
            version: 1
            total_criteria: 0
            sources:
              - file: unchanged.criteria.yaml
                source_doc: docs/unchanged.md
                source_type: document
                doc_hash: {unchangedHash}
                criteria_count: 0
                outcome: extracted
              - file: changed.criteria.yaml
                source_doc: docs/changed.md
                source_type: document
                doc_hash: deadbeefstalehash
                criteria_count: 0
                outcome: extracted
            """);

        var json = await CaptureJsonAsync(includeUnchanged: true);

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        Assert.Equal("docs-changed", root.GetProperty("command").GetString());

        var byPath = root.GetProperty("changed").EnumerateArray()
            .ToDictionary(e => e.GetProperty("path").GetString()!, e => e.GetProperty("status").GetString()!);

        Assert.Equal("unchanged", byPath["docs/unchanged.md"]);
        Assert.Equal("changed", byPath["docs/changed.md"]);
        Assert.Equal("new", byPath["docs/new.md"]);
    }

    [Fact]
    public async Task DefaultOutput_OmitsUnchanged()
    {
        File.WriteAllText(Path.Combine(_dir, "docs", "fresh.md"), "# Fresh\nThe system MUST do A.\n");
        // No criteria index at all → fresh.md is "new".

        var json = await CaptureJsonAsync(includeUnchanged: false);

        using var doc = JsonDocument.Parse(json);
        var changed = doc.RootElement.GetProperty("changed").EnumerateArray().ToList();
        Assert.Single(changed);
        Assert.Equal("new", changed[0].GetProperty("status").GetString());
    }

    private async Task<string> CaptureJsonAsync(bool includeUnchanged)
    {
        var handler = new DocsChangedHandler(OutputFormat.Json, includeUnchanged, includeArchived: false);
        var sw = new StringWriter();
        var exit = await handler.ExecuteAsync(_dir, sw);
        Assert.Equal(ExitCodes.Success, exit);
        return sw.ToString();
    }
}
