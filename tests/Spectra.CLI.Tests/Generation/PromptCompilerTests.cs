using Spectra.CLI.Generation;

namespace Spectra.CLI.Tests.Generation;

/// <summary>
/// Spec 053 — token-free unit tests for the deterministic prompt compiler.
/// Covers US1 (determinism, grounded content) and US2 (refuse-to-emit, FR-004).
/// No model, no network, no I/O.
/// </summary>
public sealed class PromptCompilerTests
{
    private const string Criteria = "- **AC-REPORTING-001** [MUST] Export produces a PDF";

    // ---------- US1: deterministic, grounded output ----------

    [Fact]
    public void Compile_WithValidInputs_Succeeds_AndEmitsGroundedPrompt()
    {
        var result = PromptCompiler.Compile(
            userPrompt: "export to PDF",
            requestedCount: 5,
            criteriaContext: Criteria);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Prompt);
        Assert.Contains("export to PDF", result.Prompt);
        Assert.Contains("5", result.Prompt);
        Assert.Contains("ACCEPTANCE CRITERIA — MANDATORY", result.Prompt);
        Assert.Contains("AC-REPORTING-001", result.Prompt);
        Assert.Null(result.MissingInput);
    }

    [Fact]
    public void Compile_IsDeterministic_IdenticalInputsProduceIdenticalPrompt()
    {
        var a = PromptCompiler.Compile("export to PDF", 5, Criteria);
        var b = PromptCompiler.Compile("export to PDF", 5, Criteria);

        Assert.True(a.IsSuccess);
        Assert.True(b.IsSuccess);
        Assert.Equal(a.Prompt, b.Prompt); // byte-identical
    }

    [Fact]
    public void Assemble_LenientPath_OmitsMandatoryBlock_WhenCriteriaEmpty()
    {
        // The relocated lenient assembly (what GenerationAgent.BuildFullPrompt delegates to)
        // must still emit a prompt with no criteria — preserving from-description behavior.
        var prompt = PromptCompiler.Assemble("describe a thing", 1, criteriaContext: null);

        Assert.Contains("describe a thing", prompt);
        Assert.DoesNotContain("ACCEPTANCE CRITERIA — MANDATORY", prompt);
    }

    // ---------- US2: refuse-to-emit (FR-004) ----------

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Compile_MissingCriteria_RefusesToEmit_AndNamesInput(string? criteria)
    {
        var result = PromptCompiler.Compile("export to PDF", 5, criteria);

        Assert.False(result.IsSuccess);
        Assert.Null(result.Prompt); // no degraded prompt
        Assert.Equal("criteria_context", result.MissingInput);
        Assert.False(string.IsNullOrWhiteSpace(result.Message));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    public void Compile_NonPositiveCount_RefusesToEmit(int count)
    {
        var result = PromptCompiler.Compile("export to PDF", count, Criteria);

        Assert.False(result.IsSuccess);
        Assert.Null(result.Prompt);
        Assert.Equal("count", result.MissingInput);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("   ")]
    public void Compile_MissingUserPrompt_RefusesToEmit(string? userPrompt)
    {
        var result = PromptCompiler.Compile(userPrompt, 5, Criteria);

        Assert.False(result.IsSuccess);
        Assert.Null(result.Prompt);
        Assert.Equal("user_prompt", result.MissingInput);
    }
}
