using Spectra.CLI.Generation;
using Spectra.CLI.Prompts;
using Spectra.Core.Models;

namespace Spectra.CLI.Tests.Generation;

/// <summary>
/// Spec 063 — the deterministic, model-free update-prompt compiler. Emits an EDIT prompt for one
/// OUTDATED test; refuses when a required input (the test, or any changed context) is missing.
/// </summary>
public sealed class UpdatePromptCompilerTests
{
    private static TestCase Sample(string id = "TC-100") => new()
    {
        Id = id,
        FilePath = $"{id}.md",
        Title = "Checkout with expired card",
        Priority = Priority.High,
        Steps = ["Enter expired card", "Submit"],
        ExpectedResult = "Payment is declined"
    };

    [Fact]
    public void Compile_NullTest_Refuses_NamingOriginalTest()
    {
        var result = UpdatePromptCompiler.Compile(null, sourceContext: "x", criteriaContext: "y");
        Assert.False(result.IsSuccess);
        Assert.Equal("original_test", result.MissingInput);
    }

    [Fact]
    public void Compile_NoChangedContext_Refuses_NamingChangedContext()
    {
        var result = UpdatePromptCompiler.Compile(Sample(), sourceContext: null, criteriaContext: null);
        Assert.False(result.IsSuccess);
        Assert.Equal("changed_context", result.MissingInput);
    }

    [Fact]
    public void Compile_WithCriteria_Succeeds_AndEmitsEditFraming()
    {
        var result = UpdatePromptCompiler.Compile(
            Sample(id: "TC-100"),
            sourceContext: null,
            criteriaContext: "AC-CHECKOUT-007: expired cards must be declined at submit");

        Assert.True(result.IsSuccess);
        var prompt = result.Prompt!;
        Assert.Contains("TC-100", prompt);                       // the test to edit
        Assert.Contains("Checkout with expired card", prompt);   // its current content
        Assert.Contains("AC-CHECKOUT-007", prompt);              // the changed context to reconcile
        Assert.Contains("do not regenerate", prompt, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Compile_IsDeterministic_SameInputsByteIdentical()
    {
        var a = UpdatePromptCompiler.Compile(Sample(), null, "AC-1: rule");
        var b = UpdatePromptCompiler.Compile(Sample(), null, "AC-1: rule");
        Assert.Equal(a.Prompt, b.Prompt);
    }

    [Fact]
    public void Compile_WithRealEmbeddedTemplate_ResolvesEditFramingAndContext()
    {
        // No user override dir → PromptTemplateLoader falls back to the embedded test-update.md.
        var loader = new PromptTemplateLoader(Path.Combine(Path.GetTempPath(), "no-such-" + Guid.NewGuid().ToString("N")));
        var result = UpdatePromptCompiler.Compile(
            Sample(id: "TC-100"),
            sourceContext: "Submit now declines expired cards immediately",
            criteriaContext: "AC-CHECKOUT-007: expired cards declined at submit",
            templateLoader: loader,
            profileFormat: "[ { \"id\": \"TC-XXX\" } ]");

        Assert.True(result.IsSuccess);
        var prompt = result.Prompt!;
        Assert.Contains("TC-100", prompt);
        Assert.Contains("EDIT", prompt, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("AC-CHECKOUT-007", prompt);                          // acceptance_criteria block rendered
        Assert.Contains("Submit now declines expired cards", prompt);        // current_source block rendered
    }

    [Fact]
    public void SerializeTest_ProducesSnakeCaseSchema_WithIdTitlePriority()
    {
        var json = UpdatePromptCompiler.SerializeTest(Sample(id: "TC-100"));
        Assert.Contains("\"id\": \"TC-100\"", json);
        Assert.Contains("\"title\": \"Checkout with expired card\"", json);
        Assert.Contains("\"priority\": \"high\"", json);
        Assert.Contains("\"expected_result\"", json);
    }
}
