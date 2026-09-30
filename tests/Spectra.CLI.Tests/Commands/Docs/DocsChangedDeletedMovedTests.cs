using System.Text.Json;
using Spectra.CLI.Commands.Docs;
using Spectra.CLI.Infrastructure;
using Spectra.CLI.Output;
using Spectra.CLI.Tests.TestFixtures;

namespace Spectra.CLI.Tests.Commands.Docs;

/// <summary>Spec 079 FR-018/FR-019: <c>docs changed</c> reports deleted and moved documents (read-only).</summary>
public sealed class DocsChangedDeletedMovedTests : IDisposable
{
    private readonly CriteriaTestWorkspace _ws = new();

    public void Dispose() => _ws.Dispose();

    private void Index(params (string Doc, string Hash, int Count)[] sources) =>
        _ws.WriteIndexYaml("version: 1\nsources:\n" + string.Concat(sources.Select(s =>
            $"- file: {Spectra.Core.Criteria.CriteriaPaths.DerivedFile(s.Doc)}\n  source_doc: {s.Doc}\n  source_type: document\n  doc_hash: {s.Hash}\n  criteria_count: {s.Count}\n")));

    private async Task<Dictionary<string, JsonElement>> ChangedAsync()
    {
        var output = new StringWriter();
        Assert.Equal(ExitCodes.Success, await new DocsChangedHandler(OutputFormat.Json, true, false).ExecuteAsync(_ws.Root, output));
        return CriteriaTestWorkspace.Json(output.ToString()).GetProperty("changed").EnumerateArray()
            .ToDictionary(e => e.GetProperty("path").GetString()!);
    }

    [Fact]
    public async Task DeletedDoc_IsReported()
    {
        Index(("docs/gone.md", "abc", 6));

        var entries = await ChangedAsync();

        var gone = entries["docs/gone.md"];
        Assert.Equal("deleted", gone.GetProperty("status").GetString());
        Assert.Equal(6, gone.GetProperty("criteria_count").GetInt32());
        Assert.False(gone.TryGetProperty("current_hash", out _)); // null → omitted
    }

    [Fact]
    public async Task UnchangedRename_IsOneMovedEntry()
    {
        const string content = "# Refunds\nRefunds MUST be issued.\n";
        _ws.WriteDoc("docs/billing/refunds.md", content);
        Index(("docs/refunds.md", FileHasher.ComputeHash(content), 3));

        var entries = await ChangedAsync();

        var moved = Assert.Single(entries.Values);
        Assert.Equal("moved", moved.GetProperty("status").GetString());
        Assert.Equal("docs/billing/refunds.md", moved.GetProperty("path").GetString());
        Assert.Equal("docs/refunds.md", moved.GetProperty("from").GetString());
    }

    [Fact]
    public async Task TwoNewDocsMatchingOneDeleted_IsAmbiguous_NoMove()
    {
        const string content = "# Same\nSame MUST hold.\n";
        _ws.WriteDoc("docs/x/same.md", content);
        _ws.WriteDoc("docs/y/same.md", content);
        Index(("docs/same.md", FileHasher.ComputeHash(content), 1));

        var entries = await ChangedAsync();

        Assert.Equal("deleted", entries["docs/same.md"].GetProperty("status").GetString());
        Assert.Equal("new", entries["docs/x/same.md"].GetProperty("status").GetString());
        Assert.Equal("new", entries["docs/y/same.md"].GetProperty("status").GetString());
    }

    [Fact]
    public async Task TwoDeletedMatchingOneNewDoc_IsAmbiguous_NoMove()
    {
        const string content = "# Same\nSame MUST hold.\n";
        _ws.WriteDoc("docs/z/same.md", content);
        var hash = FileHasher.ComputeHash(content);
        Index(("docs/a.md", hash, 1), ("docs/b.md", hash, 1));

        var entries = await ChangedAsync();

        Assert.Equal("new", entries["docs/z/same.md"].GetProperty("status").GetString());
        Assert.Equal("deleted", entries["docs/a.md"].GetProperty("status").GetString());
        Assert.Equal("deleted", entries["docs/b.md"].GetProperty("status").GetString());
    }

    [Fact]
    public async Task ImportSources_AreNeverDeleted()
    {
        _ws.WriteIndexYaml("""
            version: 1
            sources:
            - file: imported/jira.criteria.yaml
              source_doc: exports/jira.csv
              source_type: import
              criteria_count: 4
            """);

        Assert.Empty(await ChangedAsync());
    }

    [Fact]
    public async Task DuplicateIds_AreReported()
    {
        _ws.WriteCriteriaFile("docs/a.criteria.yaml", "criteria:\n- id: AC-X-001\n  text: a\n");
        _ws.WriteCriteriaFile("docs/b.criteria.yaml", "criteria:\n- id: AC-X-001\n  text: b\n");
        var output = new StringWriter();

        await new DocsChangedHandler(OutputFormat.Json, false, false).ExecuteAsync(_ws.Root, output);

        var dup = Assert.Single(CriteriaTestWorkspace.Json(output.ToString()).GetProperty("duplicate_ids").EnumerateArray());
        Assert.Equal("AC-X-001", dup.GetProperty("id").GetString());
    }
}
