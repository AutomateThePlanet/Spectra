using Spectra.CLI.Tests.TestFixtures;

namespace Spectra.CLI.Tests.Commands.Criteria;

/// <summary>
/// Spec 079 US1 (FR-004–FR-006, FR-014): a reworded criterion keeps its ID; a no-op or markup-only
/// re-extraction leaves the criteria file byte-identical; invalid IDs fail loud and write nothing.
/// </summary>
[Collection(CriteriaCommandsCollection.Name)]
public sealed class IngestCriteriaIdentityTests : IDisposable
{
    private const string Doc = "docs/checkout.md";
    private const string File = "docs/checkout.criteria.yaml";
    private readonly CriteriaTestWorkspace _ws = new();

    public IngestCriteriaIdentityTests() =>
        _ws.WriteDoc(Doc, "# Checkout\n\n## Payment\n\nThe card is validated.\n");

    public void Dispose() => _ws.Dispose();

    private static object Item(string text, string? id = null, string? section = "Payment", string priority = "high") =>
        id is null
            ? new { text, rfc2119 = "MUST", source_section = section, priority, tags = new[] { "payment" } }
            : new { id, text, rfc2119 = "MUST", source_section = section, priority, tags = new[] { "payment" } };

    private async Task SeedThreeAsync()
    {
        var (exit, _, err) = await _ws.IngestAsync(Doc,
            Item("System MUST validate the card"),
            Item("System **MUST** show an error"),
            Item("System MUST retry once"));
        Assert.True(exit == 0, err);
    }

    [Fact]
    public async Task FirstIngest_IssuesSequentialIds_AndPersistsSourceSection()
    {
        await SeedThreeAsync();

        var criteria = await _ws.ReadCriteriaAsync(File);
        Assert.Equal(["AC-CHECKOUT-001", "AC-CHECKOUT-002", "AC-CHECKOUT-003"], criteria.Select(c => c.Id));
        Assert.All(criteria, c => Assert.Equal("Payment", c.SourceSection));
        Assert.All(criteria, c => Assert.Equal(Doc, c.SourceDoc));
    }

    [Fact]
    public async Task Reworded_KeepsId_MarkupOnly_KeepsStoredText_Dropped_IsGone_New_GetsNextId()
    {
        await SeedThreeAsync();

        var (exit, _, err) = await _ws.IngestAsync(Doc,
            Item("System MUST reject expired and invalid cards", id: "AC-CHECKOUT-001"),
            Item("System MUST show an error"),                       // markup-only change, no id
            Item("System MUST log the decline"));                    // new; 003 dropped
        Assert.True(exit == 0, err);

        var criteria = (await _ws.ReadCriteriaAsync(File)).ToDictionary(c => c.Id);
        Assert.Equal(["AC-CHECKOUT-001", "AC-CHECKOUT-002", "AC-CHECKOUT-004"], criteria.Keys);
        Assert.Equal("System MUST reject expired and invalid cards", criteria["AC-CHECKOUT-001"].Text);
        Assert.Equal("System **MUST** show an error", criteria["AC-CHECKOUT-002"].Text); // previous text verbatim
        Assert.Equal("System MUST log the decline", criteria["AC-CHECKOUT-004"].Text);
    }

    [Fact]
    public async Task SameResponseAgain_LeavesTheCriteriaFileByteIdentical()
    {
        await SeedThreeAsync();
        var before = CriteriaTestWorkspace.Sha256(_ws.PathOf("docs/criteria/" + File));

        var (exit, output, _) = await _ws.IngestAsync(Doc,
            Item("System MUST validate the card"),
            Item("System **MUST** show an error"),
            Item("System MUST retry once"));

        Assert.Equal(0, exit);
        Assert.Equal(before, CriteriaTestWorkspace.Sha256(_ws.PathOf("docs/criteria/" + File)));
        Assert.False(CriteriaTestWorkspace.Json(output).GetProperty("written").GetBoolean());
    }

    [Fact]
    public async Task MarkupOrWhitespaceOnlyRewording_KeepsIds_AndTheFile()
    {
        await SeedThreeAsync();
        var before = CriteriaTestWorkspace.Sha256(_ws.PathOf("docs/criteria/" + File));

        var (exit, _, _) = await _ws.IngestAsync(Doc,
            Item("System   *MUST*  validate the card"),
            Item("System MUST show an error"),
            Item("<b>System</b> MUST retry once"));

        Assert.Equal(0, exit);
        Assert.Equal(before, CriteriaTestWorkspace.Sha256(_ws.PathOf("docs/criteria/" + File)));
    }

    [Theory]
    [InlineData("AC-LOGIN-001")]    // another doc's ID
    [InlineData("AC-CHECKOUT-999")] // unknown
    public async Task ForeignOrUnknownId_Exits7_AndWritesNothing(string badId)
    {
        await SeedThreeAsync();
        var before = _ws.Snapshot();

        var (exit, output, err) = await _ws.IngestAsync(Doc, Item("anything", id: badId));

        Assert.Equal(7, exit);
        Assert.Equal(string.Empty, output.Trim());
        var failure = CriteriaTestWorkspace.Json(err);
        Assert.Equal("INVALID_CRITERION_ID", failure.GetProperty("error_code").GetString());
        Assert.Contains(badId, failure.GetProperty("errors")[0].GetString());
        Assert.Equal(before, _ws.Snapshot());
    }

    [Fact]
    public async Task SameIdTwice_Exits7_AndWritesNothing()
    {
        await SeedThreeAsync();
        var before = _ws.Snapshot();

        var (exit, _, _) = await _ws.IngestAsync(Doc,
            Item("one", id: "AC-CHECKOUT-001"), Item("two", id: "AC-CHECKOUT-001"));

        Assert.Equal(7, exit);
        Assert.Equal(before, _ws.Snapshot());
    }

    [Fact]
    public async Task CorruptExistingCriteriaFile_Exits1_AndWritesNothing()
    {
        await SeedThreeAsync();
        System.IO.File.WriteAllText(_ws.PathOf("docs/criteria/" + File), "criteria:\n  - id: [broken\n    text: : :\n");
        var before = _ws.Snapshot();

        var (exit, _, err) = await _ws.IngestAsync(Doc, Item("System MUST validate the card"));

        Assert.Equal(1, exit);
        Assert.Equal("CRITERIA_FILE_UNREADABLE", CriteriaTestWorkspace.Json(err).GetProperty("error_code").GetString());
        Assert.Equal(before, _ws.Snapshot());
    }

    // ---------- Spec 080 B6: a rejected ingest has its own outcome ----------

    [Fact]
    public async Task RejectedIngest_ReportsOutcomeRejected_NotExtracted()
    {
        await SeedThreeAsync();

        var (exit, _, err) = await _ws.IngestAsync(Doc, Item("anything", id: "AC-LOGIN-001"));

        Assert.Equal(7, exit);
        var failure = CriteriaTestWorkspace.Json(err);
        Assert.False(failure.GetProperty("success").GetBoolean());
        Assert.Equal("Rejected", failure.GetProperty("outcome").GetString());
    }

    [Fact]
    public void RejectedResult_IsNeitherExtractedNorSuccess()
    {
        var rejected = Spectra.CLI.Extraction.CriteriaIngestResult.Rejected("INVALID_CRITERION_ID", "bad id");

        Assert.Equal(Spectra.CLI.Extraction.ExtractionOutcome.Rejected, rejected.Outcome);
        Assert.False(rejected.IsSuccess);
        Assert.Equal("INVALID_CRITERION_ID", rejected.ErrorCode);
    }
}
