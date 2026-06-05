using Spectra.CLI.Verification;
using Spectra.Core.Models;

namespace Spectra.CLI.Tests.Verification;

/// <summary>
/// Spec 055 — token-free unit tests for the deterministic, model-free critic-prompt compiler.
/// Covers US1 (determinism, grounded content, isolation) and the refuse-to-emit boundary (FR-002).
/// No model, no network.
/// </summary>
public sealed class CriticPromptCompilerTests
{
    private static TestCase Test(string id = "TC-900", string title = "Checkout totals update") => new()
    {
        Id = id,
        FilePath = $"{id}.md",
        Title = title,
        Priority = Priority.High,
        Steps = ["Open cart", "Change quantity to 3"],
        ExpectedResult = "The order total reflects the new quantity.",
        SourceRefs = ["docs/checkout.md"]
    };

    private static IReadOnlyList<SourceDocument> Docs() =>
    [
        new SourceDocument { Path = "docs/checkout.md", Title = "Checkout", Content = "Totals MUST update when quantity changes." }
    ];

    // ---------- US1: deterministic, grounded, isolated output ----------

    [Fact]
    public void Compile_WithValidTest_Succeeds_AndEmitsGroundedPrompt()
    {
        var result = CriticPromptCompiler.Compile(Test(), Docs());

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Prompt);
        Assert.Contains("TC-900", result.Prompt);
        Assert.Contains("Checkout totals update", result.Prompt);
        Assert.Contains("Totals MUST update when quantity changes.", result.Prompt);
        Assert.Null(result.MissingInput);
    }

    [Fact]
    public void Assemble_WithIdenticalInputs_IsByteIdentical()
    {
        var a = CriticPromptCompiler.Assemble(Test(), Docs());
        var b = CriticPromptCompiler.Assemble(Test(), Docs());

        Assert.Equal(a, b); // determinism (FR-002) — no timestamps/GUIDs/unordered content
    }

    [Fact]
    public void Compile_PerformsNoModelCall()
    {
        // Pure compilation: returns synchronously with no provider configured.
        var result = CriticPromptCompiler.Compile(Test(), Docs());
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void Compile_DoesNotLeakGeneratorState()
    {
        // The prompt is grounded ONLY in the artifact + docs — never generator reasoning/tokens.
        var prompt = CriticPromptCompiler.Compile(Test(), Docs()).Prompt!;
        Assert.DoesNotContain("token", prompt, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("generator reasoning", prompt, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Compile_WithEmptyDocs_IsNotARefusal()
    {
        var result = CriticPromptCompiler.Compile(Test(), []);
        Assert.True(result.IsSuccess); // empty doc set is allowed (the builder notes "no documentation")
    }

    // ---------- Refuse-to-emit (FR-002) ----------

    [Fact]
    public void Compile_WithNullTest_Refuses()
    {
        var result = CriticPromptCompiler.Compile(null, Docs());

        Assert.False(result.IsSuccess);
        Assert.Equal("test_artifact", result.MissingInput);
        Assert.Null(result.Prompt);
    }

    [Theory]
    [InlineData("", "title")]
    [InlineData("TC-1", "")]
    public void Compile_WithMissingIdOrTitle_Refuses(string id, string title)
    {
        var test = new TestCase
        {
            Id = id,
            FilePath = "x.md",
            Title = title,
            Priority = Priority.Medium,
            ExpectedResult = "ok"
        };

        var result = CriticPromptCompiler.Compile(test, Docs());

        Assert.False(result.IsSuccess);
        Assert.Equal("test_artifact", result.MissingInput);
        Assert.Null(result.Prompt);
    }
}
