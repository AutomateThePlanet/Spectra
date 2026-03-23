using Spectra.CLI.Agent.Analysis;
using Spectra.CLI.Agent.Copilot;
using Spectra.Core.Models;

namespace Spectra.CLI.Tests.Agent;

public class BehaviorAnalyzerTests
{
    [Fact]
    public void ParseAnalysisResponse_ValidJson_ReturnsBehaviors()
    {
        var json = """
            {
              "behaviors": [
                {"category": "happy_path", "title": "Successful login", "source": "docs/auth.md"},
                {"category": "negative", "title": "Invalid password rejected", "source": "docs/auth.md"},
                {"category": "edge_case", "title": "Empty username submitted", "source": "docs/auth.md"}
              ]
            }
            """;

        var result = BehaviorAnalyzer.ParseAnalysisResponse(json);

        Assert.NotNull(result);
        Assert.Equal(3, result.Count);
        Assert.Equal(BehaviorCategory.HappyPath, result[0].Category);
        Assert.Equal("Successful login", result[0].Title);
        Assert.Equal("docs/auth.md", result[0].Source);
        Assert.Equal(BehaviorCategory.Negative, result[1].Category);
        Assert.Equal(BehaviorCategory.EdgeCase, result[2].Category);
    }

    [Fact]
    public void ParseAnalysisResponse_WrappedInCodeBlock_ReturnsBehaviors()
    {
        var json = """
            Here is the analysis:
            ```json
            {"behaviors": [{"category": "happy_path", "title": "Test behavior", "source": "docs/api.md"}]}
            ```
            """;

        var result = BehaviorAnalyzer.ParseAnalysisResponse(json);

        Assert.NotNull(result);
        Assert.Single(result);
        Assert.Equal("Test behavior", result[0].Title);
    }

    [Fact]
    public void ParseAnalysisResponse_DirectArray_ReturnsBehaviors()
    {
        // Use a clean JSON string without leading/trailing whitespace issues
        var json = "[{\"category\": \"security\", \"title\": \"Auth check\", \"source\": \"docs/sec.md\"}]";

        var result = BehaviorAnalyzer.ParseAnalysisResponse(json);

        Assert.NotNull(result);
        Assert.Single(result);
        Assert.Equal(BehaviorCategory.Security, result[0].Category);
    }

    [Fact]
    public void ParseAnalysisResponse_MalformedJson_ReturnsNull()
    {
        var result = BehaviorAnalyzer.ParseAnalysisResponse("this is not json at all");
        Assert.Null(result);
    }

    [Fact]
    public void ParseAnalysisResponse_EmptyString_ReturnsNull()
    {
        var result = BehaviorAnalyzer.ParseAnalysisResponse("");
        Assert.Null(result);
    }

    [Fact]
    public void ParseAnalysisResponse_EmptyBehaviors_ReturnsEmptyList()
    {
        var json = """{"behaviors": []}""";
        var result = BehaviorAnalyzer.ParseAnalysisResponse(json);
        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    public void FilterByFocus_NegativeKeyword_FiltersToNegativeCategory()
    {
        var behaviors = new List<IdentifiedBehavior>
        {
            new() { CategoryRaw = "happy_path", Title = "Login works", Source = "a.md" },
            new() { CategoryRaw = "negative", Title = "Login fails", Source = "a.md" },
            new() { CategoryRaw = "edge_case", Title = "Edge case", Source = "a.md" }
        };

        var result = BehaviorAnalyzer.FilterByFocus(behaviors, "negative scenarios");

        Assert.Single(result);
        Assert.Equal(BehaviorCategory.Negative, result[0].Category);
    }

    [Fact]
    public void FilterByFocus_EdgeKeyword_FiltersToEdgeCaseCategory()
    {
        var behaviors = new List<IdentifiedBehavior>
        {
            new() { CategoryRaw = "happy_path", Title = "Works", Source = "a.md" },
            new() { CategoryRaw = "edge_case", Title = "Boundary", Source = "a.md" }
        };

        var result = BehaviorAnalyzer.FilterByFocus(behaviors, "edge cases");

        Assert.Single(result);
        Assert.Equal(BehaviorCategory.EdgeCase, result[0].Category);
    }

    [Fact]
    public void FilterByFocus_SecurityPartialMatch_FiltersToSecurityCategory()
    {
        var behaviors = new List<IdentifiedBehavior>
        {
            new() { CategoryRaw = "happy_path", Title = "Works", Source = "a.md" },
            new() { CategoryRaw = "security", Title = "Auth check", Source = "a.md" }
        };

        var result = BehaviorAnalyzer.FilterByFocus(behaviors, "sec");

        Assert.Single(result);
        Assert.Equal(BehaviorCategory.Security, result[0].Category);
    }

    [Fact]
    public void FilterByFocus_CaseInsensitive()
    {
        var behaviors = new List<IdentifiedBehavior>
        {
            new() { CategoryRaw = "happy_path", Title = "Works", Source = "a.md" },
            new() { CategoryRaw = "negative", Title = "Fails", Source = "a.md" }
        };

        var result = BehaviorAnalyzer.FilterByFocus(behaviors, "NEGATIVE");

        Assert.Single(result);
    }

    [Fact]
    public void FilterByFocus_NoMatchingCategory_ReturnsAll()
    {
        var behaviors = new List<IdentifiedBehavior>
        {
            new() { CategoryRaw = "happy_path", Title = "Works", Source = "a.md" },
            new() { CategoryRaw = "negative", Title = "Fails", Source = "a.md" }
        };

        var result = BehaviorAnalyzer.FilterByFocus(behaviors, "something unrelated");

        Assert.Equal(2, result.Count);
    }

    [Fact]
    public void CountCoveredBehaviors_NoExistingTests_ReturnsZero()
    {
        var behaviors = new List<IdentifiedBehavior>
        {
            new() { CategoryRaw = "happy_path", Title = "Successful checkout", Source = "a.md" }
        };

        var result = BehaviorAnalyzer.CountCoveredBehaviors(behaviors, []);

        Assert.Equal(0, result);
    }

    [Fact]
    public void CountCoveredBehaviors_MatchingTest_ReturnsCoveredCount()
    {
        var behaviors = new List<IdentifiedBehavior>
        {
            new() { CategoryRaw = "happy_path", Title = "Successful checkout with credit card", Source = "a.md" },
            new() { CategoryRaw = "negative", Title = "Database migration rollback on schema conflict", Source = "a.md" }
        };

        var existingTests = new List<TestCase>
        {
            new()
            {
                Id = "TC-001",
                Title = "Successful checkout with credit card payment",
                Priority = Priority.Medium,
                Steps = ["Navigate to checkout", "Enter card details", "Click pay"],
                ExpectedResult = "Payment succeeds",
                FilePath = ""
            }
        };

        var result = BehaviorAnalyzer.CountCoveredBehaviors(behaviors, existingTests);

        // First behavior matches (similar title), second does not
        Assert.Equal(1, result);
    }

    [Fact]
    public void BuildAnalysisPrompt_IncludesDocumentContent()
    {
        var docs = new List<SourceDocument>
        {
            new()
            {
                Path = "docs/checkout.md",
                Title = "Checkout Flow",
                Content = "Users can check out with various payment methods.",
                Sections = ["Payment Methods", "Shipping"]
            }
        };

        var prompt = BehaviorAnalyzer.BuildAnalysisPrompt(docs, null);

        Assert.Contains("Checkout Flow", prompt);
        Assert.Contains("checkout.md", prompt);
        Assert.Contains("Payment Methods", prompt);
        Assert.Contains("happy_path", prompt);
    }

    [Fact]
    public void BuildAnalysisPrompt_WithFocus_IncludesFocusArea()
    {
        var docs = new List<SourceDocument>
        {
            new()
            {
                Path = "docs/api.md",
                Title = "API",
                Content = "API documentation.",
                Sections = []
            }
        };

        var prompt = BehaviorAnalyzer.BuildAnalysisPrompt(docs, "negative scenarios");

        Assert.Contains("Focus area: negative scenarios", prompt);
    }

    [Fact]
    public void CategoryParsing_VariousFormats_ParseCorrectly()
    {
        var behaviors = new[]
        {
            new IdentifiedBehavior { CategoryRaw = "happy_path", Title = "A", Source = "a" },
            new IdentifiedBehavior { CategoryRaw = "happypath", Title = "B", Source = "b" },
            new IdentifiedBehavior { CategoryRaw = "negative", Title = "C", Source = "c" },
            new IdentifiedBehavior { CategoryRaw = "error", Title = "D", Source = "d" },
            new IdentifiedBehavior { CategoryRaw = "edge_case", Title = "E", Source = "e" },
            new IdentifiedBehavior { CategoryRaw = "edgecase", Title = "F", Source = "f" },
            new IdentifiedBehavior { CategoryRaw = "security", Title = "G", Source = "g" },
            new IdentifiedBehavior { CategoryRaw = "permission", Title = "H", Source = "h" },
            new IdentifiedBehavior { CategoryRaw = "performance", Title = "I", Source = "i" },
            new IdentifiedBehavior { CategoryRaw = "unknown", Title = "J", Source = "j" }
        };

        Assert.Equal(BehaviorCategory.HappyPath, behaviors[0].Category);
        Assert.Equal(BehaviorCategory.HappyPath, behaviors[1].Category);
        Assert.Equal(BehaviorCategory.Negative, behaviors[2].Category);
        Assert.Equal(BehaviorCategory.Negative, behaviors[3].Category);
        Assert.Equal(BehaviorCategory.EdgeCase, behaviors[4].Category);
        Assert.Equal(BehaviorCategory.EdgeCase, behaviors[5].Category);
        Assert.Equal(BehaviorCategory.Security, behaviors[6].Category);
        Assert.Equal(BehaviorCategory.Security, behaviors[7].Category);
        Assert.Equal(BehaviorCategory.Performance, behaviors[8].Category);
        Assert.Equal(BehaviorCategory.HappyPath, behaviors[9].Category); // Unknown defaults to HappyPath
    }
}
