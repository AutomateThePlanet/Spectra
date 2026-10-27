using System.Text.Json;
using Spectra.CLI.Tests.TestFixtures;

namespace Spectra.CLI.Tests.Commands.Criteria;

/// <summary>
/// Spec 081 C4 (FR-010–FR-012, SC-004): in JSON mode every failure of the five criteria commands is one JSON document
/// on stderr with <c>command</c>, <c>success</c>, <c>error_code</c>, <c>errors</c> and <c>warnings</c>, plus
/// command-specific extras (<c>outcome</c> on ingest, <c>missing_input</c> on compile refusals).
/// </summary>
[Collection(CriteriaCommandsCollection.Name)]
public sealed class CriteriaFailureShapeTests : IDisposable
{
    private readonly CriteriaTestWorkspace _ws = new();

    public CriteriaFailureShapeTests()
    {
        _ws.WriteDoc("docs/a.md", "# A\n\nUsers MUST see A.\n");
    }

    public void Dispose() => _ws.Dispose();

    public static TheoryData<string, string, int, string?, string?> Failures => new()
    {
        // args, command, exit, error_code, extra field
        { "ai compile-extraction-prompt --doc docs/missing.md", "compile-extraction-prompt", 1, "DOCUMENT_NOT_FOUND", null },
        { "ai compile-extraction-prompt", "compile-extraction-prompt", 4, "MISSING_INPUT", "missing_input" },
        { "ai ingest-criteria --doc docs/a.md --from missing.json", "ingest-criteria", 1, null, null },
        { "ai ingest-criteria --doc docs/a.md --from .spectra/criteria.json", "ingest-criteria", 7, "INVALID_CRITERION_ID", "outcome" },
        { "docs retire --doc docs/none.md", "docs-retire", 4, "NOT_INDEXED", null },
        { "docs apply-move --from docs/none.md --to docs/a.md", "docs-apply-move", 4, "NOT_INDEXED", null },
        { "docs migrate-criteria", "docs-migrate-criteria", 1, "CRITERIA_FILE_UNREADABLE", null }
    };

    [Theory]
    [MemberData(nameof(Failures))]
    public async Task JsonModeFailure_IsOneDocument_WithTheCommonFields(
        string args, string command, int exit, string? errorCode, string? extra)
    {
        // A foreign id for the ingest-rejection row; a corrupt index for the migrate row.
        _ws.Response(new { id = "AC-OTHER-001", text = "Users MUST see A", rfc2119 = "MUST" });
        if (command == "docs-migrate-criteria")
            _ws.WriteIndexYaml("sources: [ this is: not: valid");

        var (actualExit, output, err) = await _ws.RunAsync([.. args.Split(' '), "--output-format", "json"]);

        Assert.True(actualExit == exit, $"exit {actualExit}: {err}");
        Assert.Equal(string.Empty, output.Trim());
        var failure = CriteriaTestWorkspace.Json(err);
        Assert.Equal(command, failure.GetProperty("command").GetString());
        Assert.False(failure.GetProperty("success").GetBoolean());
        if (errorCode is null)
            Assert.Equal(JsonValueKind.Null, failure.GetProperty("error_code").ValueKind);
        else
            Assert.Equal(errorCode, failure.GetProperty("error_code").GetString());
        Assert.True(failure.GetProperty("errors").GetArrayLength() > 0);
        Assert.Equal(JsonValueKind.Array, failure.GetProperty("warnings").ValueKind);

        foreach (var optional in new[] { "outcome", "missing_input" })
            Assert.Equal(optional == extra, failure.TryGetProperty(optional, out _));
        Assert.False(failure.TryGetProperty("refused", out _));
        Assert.False(failure.TryGetProperty("message", out _));
    }
}
