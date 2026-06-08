using Spectra.CLI.Generation;
using Spectra.Core.Models;

namespace Spectra.CLI.Tests.Generation;

/// <summary>
/// Spec 059 (US3) — token-free unit tests for the deterministic behavior-analysis prompt
/// compiler relocated onto the seam. Determinism + refuse-to-emit on missing documents.
/// </summary>
public sealed class AnalysisPromptCompilerTests
{
    private static SourceDocument Doc(string path, string content) => new()
    {
        Path = path,
        Title = path,
        Content = content
    };

    [Fact]
    public void Compile_WithDocuments_EmitsPrompt()
    {
        var docs = new[] { Doc("auth.md", "The login form accepts a username and password.") };

        var result = AnalysisPromptCompiler.Compile(docs, focusArea: null, config: null,
            templateLoader: null, coverageContext: null);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Prompt);
        Assert.Contains("auth.md", result.Prompt);
        Assert.Null(result.MissingInput);
    }

    [Fact]
    public void Compile_IsDeterministic_IdenticalInputsProduceIdenticalPrompt()
    {
        var docs = new[] { Doc("auth.md", "The login form accepts a username and password.") };

        var a = AnalysisPromptCompiler.Compile(docs, "negative", null, null, null);
        var b = AnalysisPromptCompiler.Compile(docs, "negative", null, null, null);

        Assert.True(a.IsSuccess);
        Assert.Equal(a.Prompt, b.Prompt); // byte-identical
    }

    [Fact]
    public void Compile_NoDocuments_RefusesToEmit()
    {
        var result = AnalysisPromptCompiler.Compile([], focusArea: null, config: null,
            templateLoader: null, coverageContext: null);

        Assert.False(result.IsSuccess);
        Assert.Equal("documents", result.MissingInput);
        Assert.Null(result.Prompt);
    }
}
