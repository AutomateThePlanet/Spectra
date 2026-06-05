using Spectra.CLI.Generation;
using Spectra.Core.Validation;

namespace Spectra.CLI.Tests.Generation;

/// <summary>
/// Spec 053 — US4 (FR-007). The bounded retry loop itself lives in skill/agent choreography,
/// NOT in C#. The only C# obligation is that a fail-loud ingest returns a payload specific
/// enough for a skill to re-prompt the agent against. These tests pin that contract — the
/// exact thing Spec 055's skill depends on.
/// </summary>
public sealed class ChoreographyContractTests
{
    [Fact]
    public void SchemaFailure_CarriesSpecificField_RetryPayload()
    {
        // A skill regenerating must know WHICH test and WHY. The error text references both.
        var content = """[ { "id": "BADID", "title": "T", "expected_result": "ok", "steps": ["a"] } ]""";
        var result = GeneratedTestIngestor.ParseAndValidate(content, new TestValidator());

        Assert.False(result.IsSuccess);
        Assert.Equal(IngestErrorCode.SchemaInvalid, result.ErrorCode);
        Assert.NotEmpty(result.Errors);
        // Specific enough to act on: identifies the offending id and the rule code.
        Assert.Contains(result.Errors, e => e.Contains("BADID") && e.Contains("INVALID_ID_FORMAT"));
    }

    [Theory]
    [InlineData("", IngestErrorCode.EmptyContent)]
    [InlineData("not json", IngestErrorCode.EmptyContent)]
    [InlineData("[ {\"id\": } ]", IngestErrorCode.MalformedJson)]
    [InlineData("[ {\"id\":\"TC-901\",\"title\":\"A\",\"expected_result\":\"ok\"},", IngestErrorCode.Truncated)]
    public void EveryFailure_ExposesAStableErrorCode_AndAtLeastOneMessage(string content, string expectedCode)
    {
        var result = GeneratedTestIngestor.ParseAndValidate(content, new TestValidator());

        Assert.False(result.IsSuccess);
        Assert.Equal(expectedCode, result.ErrorCode);
        Assert.NotEmpty(result.Errors); // a skill always has something to feed back
    }

    [Fact]
    public void NoCSharpRetryLoop_Exists_ContractIsSingleShot()
    {
        // Documents the design: ParseAndValidate is a single evaluation. The loop/limit is the
        // skill's job. Two identical bad inputs yield two identical failures — no internal
        // attempt counter, no hidden state.
        var a = GeneratedTestIngestor.ParseAndValidate("nope", new TestValidator());
        var b = GeneratedTestIngestor.ParseAndValidate("nope", new TestValidator());

        Assert.Equal(a.ErrorCode, b.ErrorCode);
        Assert.False(a.IsSuccess);
        Assert.False(b.IsSuccess);
    }
}
