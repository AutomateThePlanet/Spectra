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

    // ---------- Spec 080 B7: every JSON-mode failure is one JSON document with a `warnings` field ----------

    [Fact]
    public async Task MissingDocument_JsonMode_StderrIsOneJsonDocument()
    {
        var (exit, output, err) = await _ws.RunAsync("ai", "compile-extraction-prompt", "--doc", "docs/missing.md", "--output-format", "json");

        Assert.Equal(1, exit);
        Assert.Equal(string.Empty, output.Trim());
        var failure = CriteriaTestWorkspace.Json(err);
        Assert.Equal("DOCUMENT_NOT_FOUND", failure.GetProperty("error_code").GetString());
        Assert.Contains("docs/missing.md", failure.GetProperty("message").GetString());
        Assert.Equal(0, failure.GetProperty("warnings").GetArrayLength());
    }

    [Fact]
    public async Task UnreadableDocument_JsonMode_StderrIsOneJsonDocument()
    {
        // An exclusive handle blocks the read reliably on Windows only; elsewhere (a) and (d) carry this item.
        if (!OperatingSystem.IsWindows()) return;

        using var hold = new FileStream(_ws.PathOf("docs/checkout.md"), FileMode.Open, FileAccess.Read, FileShare.None);
        var (exit, _, err) = await _ws.RunAsync("ai", "compile-extraction-prompt", "--doc", "docs/checkout.md", "--output-format", "json");

        Assert.Equal(1, exit);
        var failure = CriteriaTestWorkspace.Json(err);
        Assert.Equal("DOCUMENT_UNREADABLE", failure.GetProperty("error_code").GetString());
        Assert.Equal(0, failure.GetProperty("warnings").GetArrayLength());
    }

    [Fact]
    public async Task MissingDocArgument_JsonMode_RefusalIsOneJsonDocument()
    {
        var (exit, _, err) = await _ws.RunAsync("ai", "compile-extraction-prompt", "--output-format", "json");

        Assert.Equal(4, exit);
        var refusal = CriteriaTestWorkspace.Json(err);
        Assert.True(refusal.GetProperty("refused").GetBoolean());
        Assert.Equal("document_path", refusal.GetProperty("missing_input").GetString());
        Assert.Equal(0, refusal.GetProperty("warnings").GetArrayLength());
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void FailureHelper_CarriesCollectedWarnings_InOneJsonDocument(bool refusal)
    {
        var original = Console.Error;
        var captured = new StringWriter();
        try
        {
            Console.SetError(captured);
            string[] warnings = ["first warning", "second warning"];
            if (refusal)
                Spectra.CLI.Commands.Generate.CompileExtractionPromptCommand.WriteRefusal(true, "document_content", "no content", warnings);
            else
                Spectra.CLI.Commands.Generate.CompileExtractionPromptCommand.WriteFailure(true, "DOCUMENT_NOT_FOUND", "gone", warnings);
        }
        finally
        {
            Console.SetError(original);
        }

        var json = CriteriaTestWorkspace.Json(captured.ToString());
        Assert.Equal(["first warning", "second warning"], json.GetProperty("warnings").EnumerateArray().Select(w => w.GetString()));
    }

    [Fact]
    public void FailureHelper_HumanMode_PrintsWarningsThenTheError()
    {
        var original = Console.Error;
        var captured = new StringWriter();
        try
        {
            Console.SetError(captured);
            Spectra.CLI.Commands.Generate.CompileExtractionPromptCommand.WriteFailure(false, "DOCUMENT_NOT_FOUND", "Document not found: x.md", ["w1"]);
        }
        finally
        {
            Console.SetError(original);
        }

        var lines = captured.ToString().Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(l => l.TrimEnd('\r')).ToArray();
        Assert.Equal(["Warning: w1", "Document not found: x.md"], lines);
    }

    [Fact]
    public async Task EmptyDocument_StillShortCircuitsToStdout()
    {
        _ws.WriteDoc("docs/empty.md", "   \n");

        var (exit, output, err) = await _ws.RunAsync("ai", "compile-extraction-prompt", "--doc", "docs/empty.md", "--output-format", "json");

        Assert.True(exit == 0, err);
        Assert.True(CriteriaTestWorkspace.Json(output).GetProperty("short_circuit").GetBoolean());
    }
}
