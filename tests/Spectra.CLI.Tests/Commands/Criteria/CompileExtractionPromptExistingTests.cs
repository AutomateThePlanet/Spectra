using Spectra.CLI.Tests.TestFixtures;

namespace Spectra.CLI.Tests.Commands.Criteria;

/// <summary>
/// Spec 079 FR-001/FR-016c-d: <c>compile-extraction-prompt</c> offers the doc's existing criteria, read through the
/// index <c>file</c> entry, in either layout — and never moves files.
/// </summary>
[Collection(CriteriaCommandsCollection.Name)]
public sealed class CompileExtractionPromptExistingTests : IDisposable
{
    private readonly CriteriaTestWorkspace _ws = new();

    public CompileExtractionPromptExistingTests()
    {
        _ws.WriteDoc("docs/checkout.md", "# Checkout\n\nThe card is validated.\n");
        _ws.WriteDoc("docs/login.md", "# Login\n\nUsers log in.\n");
    }

    public void Dispose() => _ws.Dispose();

    private void SeedOldLayout()
    {
        // Pre-079 layout: base-name files, index `file` = base name.
        _ws.WriteCriteriaFile("checkout.criteria.yaml", """
            criteria:
            - id: AC-CHECKOUT-001
              text: System MUST validate the card
              source_doc: docs/checkout.md
              source_section: Payment
            """);
        _ws.WriteCriteriaFile("login.criteria.yaml", """
            criteria:
            - id: AC-LOGIN-001
              text: Users MUST log in
              source_doc: docs/login.md
            """);
        _ws.WriteIndexYaml("""
            version: 1
            total_criteria: 2
            sources:
            - file: checkout.criteria.yaml
              source_doc: docs/checkout.md
              source_type: document
              criteria_count: 1
            - file: login.criteria.yaml
              source_doc: docs/login.md
              source_type: document
              criteria_count: 1
            """);
    }

    [Fact]
    public async Task OldLayout_OffersThisDocsCriteriaOnly_WarnsAndMovesNothing()
    {
        SeedOldLayout();
        var before = _ws.Snapshot();

        var (exit, output, err) = await _ws.RunAsync("ai", "compile-extraction-prompt", "--doc", "docs\\checkout.md");

        Assert.Equal(0, exit);
        Assert.Contains("Existing criteria:", output);
        Assert.Contains("\"id\":\"AC-CHECKOUT-001\"", output);
        Assert.DoesNotContain("AC-LOGIN-001", output);
        Assert.Contains("migrate-criteria", err);
        Assert.Equal(before, _ws.Snapshot());
    }

    [Fact]
    public async Task CompilingTwice_IsByteIdentical()
    {
        SeedOldLayout();

        var (_, first, _) = await _ws.RunAsync("ai", "compile-extraction-prompt", "--doc", "docs/checkout.md");
        var (_, second, _) = await _ws.RunAsync("ai", "compile-extraction-prompt", "--doc", "docs/checkout.md");

        Assert.Equal(first, second);
    }

    [Fact]
    public async Task CustomTemplateWithoutExistingCriteria_Warns()
    {
        SeedOldLayout();
        _ws.WriteDoc(".spectra/prompts/criteria-extraction.md", """
            ---
            spectra_version: "1.0"
            template_id: criteria-extraction
            ---
            Extract criteria from {{document_text}} as a JSON array.
            """);

        var (exit, _, err) = await _ws.RunAsync("ai", "compile-extraction-prompt", "--doc", "docs/checkout.md");

        Assert.Equal(0, exit);
        Assert.Contains("prompts reset criteria-extraction", err);
    }
}
