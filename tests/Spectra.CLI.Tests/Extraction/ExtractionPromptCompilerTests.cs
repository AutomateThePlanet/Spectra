using Spectra.CLI.Extraction;

namespace Spectra.CLI.Tests.Extraction;

/// <summary>
/// Spec 054 — token-free unit tests for the deterministic, model-free extraction-prompt compiler.
/// Covers US1 (determinism, grounded content) and the compiler half of US2 (refuse-to-emit on a
/// missing required input). No model, no network, no I/O.
/// </summary>
public sealed class ExtractionPromptCompilerTests
{
    private const string Doc = "docs/payment.md";
    private const string Content = "The system MUST validate the IBAN format before accepting a payment.";

    // ---------- US1: deterministic, grounded output ----------

    [Fact]
    public void Compile_WithValidInputs_Succeeds_AndEmitsGroundedPrompt()
    {
        var result = ExtractionPromptCompiler.Compile(Doc, Content, component: "payment");

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Prompt);
        Assert.Contains(Content, result.Prompt);
        Assert.Contains("payment", result.Prompt);
        Assert.Contains(Doc, result.Prompt);
        Assert.Null(result.MissingInput);
    }

    [Fact]
    public void Assemble_WithIdenticalInputs_IsByteIdentical()
    {
        var a = ExtractionPromptCompiler.Assemble(Doc, Content, "payment");
        var b = ExtractionPromptCompiler.Assemble(Doc, Content, "payment");

        Assert.Equal(a, b); // determinism (FR-002) — no timestamps/GUIDs/unordered content
    }

    [Fact]
    public void Compile_PerformsNoModelCall()
    {
        // The whole point: compilation is pure. If this returns synchronously without a provider
        // configured, no session/model was involved.
        var result = ExtractionPromptCompiler.Compile(Doc, Content);
        Assert.True(result.IsSuccess);
    }

    // ---------- Refuse-to-emit (FR-002) ----------

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Compile_WithMissingDocPath_Refuses(string? path)
    {
        var result = ExtractionPromptCompiler.Compile(path, Content);

        Assert.False(result.IsSuccess);
        Assert.Equal("document_path", result.MissingInput);
        Assert.Null(result.Prompt);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Compile_WithEmptyContent_Refuses_AndEmitsNoPrompt(string? content)
    {
        // FR-003: an empty/whitespace source is the 'Extracted, []' short-circuit handled BEFORE
        // compilation — so the compiler must never produce a prompt for empty content.
        var result = ExtractionPromptCompiler.Compile(Doc, content);

        Assert.False(result.IsSuccess);
        Assert.Equal("document_content", result.MissingInput);
        Assert.Null(result.Prompt);
    }
}
