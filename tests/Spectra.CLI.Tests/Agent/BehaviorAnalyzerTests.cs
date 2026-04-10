using Spectra.CLI.Agent.Analysis;
using Spectra.CLI.Agent.Copilot;
using Spectra.CLI.Prompts;
using Spectra.Core.Models;
using Spectra.Core.Models.Config;

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
        Assert.Equal("happy_path", result[0].Category);
        Assert.Equal("Successful login", result[0].Title);
        Assert.Equal("docs/auth.md", result[0].Source);
        Assert.Equal("negative", result[1].Category);
        Assert.Equal("edge_case", result[2].Category);
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
        Assert.Equal("security", result[0].Category);
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
            new() { Category = "happy_path", Title = "Login works", Source = "a.md" },
            new() { Category = "negative", Title = "Login fails", Source = "a.md" },
            new() { Category = "edge_case", Title = "Edge case", Source = "a.md" }
        };

        var result = BehaviorAnalyzer.FilterByFocus(behaviors, "negative scenarios");

        Assert.Single(result);
        Assert.Equal("negative", result[0].Category);
    }

    [Fact]
    public void FilterByFocus_EdgeKeyword_FiltersToEdgeCaseCategory()
    {
        var behaviors = new List<IdentifiedBehavior>
        {
            new() { Category = "happy_path", Title = "Works", Source = "a.md" },
            new() { Category = "edge_case", Title = "Boundary", Source = "a.md" }
        };

        var result = BehaviorAnalyzer.FilterByFocus(behaviors, "edge cases");

        Assert.Single(result);
        Assert.Equal("edge_case", result[0].Category);
    }

    [Fact]
    public void FilterByFocus_SecurityPartialMatch_FiltersToSecurityCategory()
    {
        var behaviors = new List<IdentifiedBehavior>
        {
            new() { Category = "happy_path", Title = "Works", Source = "a.md" },
            new() { Category = "security", Title = "Auth check", Source = "a.md" }
        };

        var result = BehaviorAnalyzer.FilterByFocus(behaviors, "sec");

        Assert.Single(result);
        Assert.Equal("security", result[0].Category);
    }

    [Fact]
    public void FilterByFocus_CaseInsensitive()
    {
        var behaviors = new List<IdentifiedBehavior>
        {
            new() { Category = "happy_path", Title = "Works", Source = "a.md" },
            new() { Category = "negative", Title = "Fails", Source = "a.md" }
        };

        var result = BehaviorAnalyzer.FilterByFocus(behaviors, "NEGATIVE");

        Assert.Single(result);
    }

    [Fact]
    public void FilterByFocus_NoMatchingCategory_ReturnsAll()
    {
        var behaviors = new List<IdentifiedBehavior>
        {
            new() { Category = "happy_path", Title = "Works", Source = "a.md" },
            new() { Category = "negative", Title = "Fails", Source = "a.md" }
        };

        var result = BehaviorAnalyzer.FilterByFocus(behaviors, "something unrelated");

        Assert.Equal(2, result.Count);
    }

    [Fact]
    public void CountCoveredBehaviors_NoExistingTests_ReturnsZero()
    {
        var behaviors = new List<IdentifiedBehavior>
        {
            new() { Category = "happy_path", Title = "Successful checkout", Source = "a.md" }
        };

        var result = BehaviorAnalyzer.CountCoveredBehaviors(behaviors, []);

        Assert.Equal(0, result);
    }

    [Fact]
    public void CountCoveredBehaviors_MatchingTest_ReturnsCoveredCount()
    {
        var behaviors = new List<IdentifiedBehavior>
        {
            new() { Category = "happy_path", Title = "Successful checkout with credit card", Source = "a.md" },
            new() { Category = "negative", Title = "Database migration rollback on schema conflict", Source = "a.md" }
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
    public void Category_RawStringPreservedVerbatim()
    {
        // Spec 036 fix: the AI's raw category string is preserved as-is, not collapsed
        // to a fixed enum value.
        var behaviors = new[]
        {
            new IdentifiedBehavior { Category = "happy_path", Title = "A", Source = "a" },
            new IdentifiedBehavior { Category = "keyboard_interaction", Title = "B", Source = "b" },
            new IdentifiedBehavior { Category = "screen_reader_support", Title = "C", Source = "c" },
            new IdentifiedBehavior { Category = "anything_at_all", Title = "D", Source = "d" }
        };

        Assert.Equal("happy_path", behaviors[0].Category);
        Assert.Equal("keyboard_interaction", behaviors[1].Category);
        Assert.Equal("screen_reader_support", behaviors[2].Category);
        Assert.Equal("anything_at_all", behaviors[3].Category);
    }

    // ===== Spec 036 fix: new tests =====

    [Fact]
    public void BuildPrompt_WithTemplateLoader_UsesTemplate()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "spectra-test-" + Guid.NewGuid().ToString("N"));
        var promptsDir = Path.Combine(tempDir, ".spectra", "prompts");
        Directory.CreateDirectory(promptsDir);
        try
        {
            var sentinel = "<<UNIT-TEST-MARKER-7f3a>>";
            var customTemplate =
                "---\n" +
                "name: behavior-analysis\n" +
                "---\n" +
                sentinel + "\n" +
                "{{document_text}}\n" +
                "{{#each categories}}\n" +
                "- {{this.id}}: {{this.description}}\n" +
                "{{/each}}\n";
            File.WriteAllText(Path.Combine(promptsDir, "behavior-analysis.md"), customTemplate);

            var loader = new PromptTemplateLoader(tempDir);
            var docs = new List<SourceDocument>
            {
                new() { Path = "docs/x.md", Title = "X", Content = "content", Sections = [] }
            };

            var prompt = BehaviorAnalyzer.BuildAnalysisPrompt(docs, null, config: null, templateLoader: loader);

            Assert.Contains(sentinel, prompt);
        }
        finally
        {
            try { Directory.Delete(tempDir, recursive: true); } catch { /* best effort */ }
        }
    }

    [Fact]
    public void BuildPrompt_WithCustomCategories_InjectsAll()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "spectra-test-" + Guid.NewGuid().ToString("N"));
        try
        {
            var loader = new PromptTemplateLoader(tempDir); // no user template — uses built-in

            var defaults = SpectraConfig.Default;
            var config = new SpectraConfig
            {
                Source = defaults.Source,
                Tests = defaults.Tests,
                Ai = defaults.Ai,
                Analysis = new AnalysisConfig
                {
                    Categories =
                    [
                        new CategoryDefinition { Id = "keyboard_interaction", Description = "Keyboard nav" },
                        new CategoryDefinition { Id = "screen_reader_support", Description = "ARIA labels" },
                        new CategoryDefinition { Id = "color_contrast", Description = "WCAG contrast" },
                        new CategoryDefinition { Id = "focus_management", Description = "Focus traps" }
                    ]
                }
            };

            var docs = new List<SourceDocument>
            {
                new() { Path = "docs/a11y.md", Title = "A11y", Content = "content", Sections = [] }
            };

            var prompt = BehaviorAnalyzer.BuildAnalysisPrompt(docs, null, config, loader);

            Assert.Contains("keyboard_interaction", prompt);
            Assert.Contains("screen_reader_support", prompt);
            Assert.Contains("color_contrast", prompt);
            Assert.Contains("focus_management", prompt);
        }
        finally
        {
            try { if (Directory.Exists(tempDir)) Directory.Delete(tempDir, recursive: true); } catch { }
        }
    }

    [Fact]
    public void BuildPrompt_WithoutLoader_UsesLegacy()
    {
        var docs = new List<SourceDocument>
        {
            new() { Path = "docs/x.md", Title = "X", Content = "content", Sections = [] }
        };

        var prompt = BehaviorAnalyzer.BuildAnalysisPrompt(docs, null, config: null, templateLoader: null);

        // Spec 037: legacy fallback now contains the ISTQB-enhanced category set
        // (performance was dropped in favour of boundary + error_handling).
        Assert.Contains("happy_path", prompt);
        Assert.Contains("negative", prompt);
        Assert.Contains("edge_case", prompt);
        Assert.Contains("security", prompt);
        Assert.Contains("boundary", prompt);
        Assert.Contains("error_handling", prompt);
    }

    [Fact]
    public void BuildPrompt_WithEmptyCategories_UsesDefaults()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "spectra-test-" + Guid.NewGuid().ToString("N"));
        try
        {
            var loader = new PromptTemplateLoader(tempDir);

            var defaults = SpectraConfig.Default;
            var config = new SpectraConfig
            {
                Source = defaults.Source,
                Tests = defaults.Tests,
                Ai = defaults.Ai,
                Analysis = new AnalysisConfig
                {
                    Categories = []
                }
            };

            var docs = new List<SourceDocument>
            {
                new() { Path = "docs/x.md", Title = "X", Content = "content", Sections = [] }
            };

            var prompt = BehaviorAnalyzer.BuildAnalysisPrompt(docs, null, config, loader);

            // 6 Spec 030 defaults
            Assert.Contains("happy_path", prompt);
            Assert.Contains("negative", prompt);
            Assert.Contains("edge_case", prompt);
            Assert.Contains("boundary", prompt);
            Assert.Contains("error_handling", prompt);
            Assert.Contains("security", prompt);
        }
        finally
        {
            try { if (Directory.Exists(tempDir)) Directory.Delete(tempDir, recursive: true); } catch { }
        }
    }

    [Fact]
    public void FilterByFocus_CustomCategory_Matches()
    {
        var behaviors = new List<IdentifiedBehavior>
        {
            new() { Category = "keyboard_interaction", Title = "Tab nav", Source = "a.md" },
            new() { Category = "color_contrast", Title = "Contrast check", Source = "a.md" },
            new() { Category = "happy_path", Title = "Login works", Source = "a.md" }
        };

        // Custom category: "keyboard" → matches "keyboard_interaction"
        var result = BehaviorAnalyzer.FilterByFocus(behaviors, "keyboard");
        Assert.Single(result);
        Assert.Equal("keyboard_interaction", result[0].Category);

        // Legacy regression: "happy path" still matches happy_path
        var result2 = BehaviorAnalyzer.FilterByFocus(behaviors, "happy path");
        Assert.Single(result2);
        Assert.Equal("happy_path", result2[0].Category);

        // No-match fallback: returns all 3
        var result3 = BehaviorAnalyzer.FilterByFocus(behaviors, "asdfqwerty");
        Assert.Equal(3, result3.Count);
    }

    [Fact]
    public void ParseResponse_CustomCategory_Preserved()
    {
        var json = """{"behaviors":[{"category":"keyboard_interaction","title":"Tab navigation works","source":"docs/a11y.md"}]}""";

        var result = BehaviorAnalyzer.ParseAnalysisResponse(json);

        Assert.NotNull(result);
        Assert.Single(result);
        Assert.Equal("keyboard_interaction", result[0].Category);
        Assert.Equal("Tab navigation works", result[0].Title);
    }
}
