using System.ComponentModel;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.AI;
using Spectra.Core.Models.Config;

namespace Spectra.CLI.Agent.Testimize;

/// <summary>
/// Spec 038: AIFunction factories for the optional Testimize integration.
/// Two tools are exposed to the generation model:
///
///  - GenerateTestData : forwards to the Testimize MCP server for algorithmic
///                       boundary / equivalence / pairwise / ABC test data
///  - AnalyzeFieldSpec : local heuristic extractor (no MCP call) that turns
///                       a snippet of documentation text into field specs
///
/// Both descriptions explicitly mention "boundary values", "equivalence
/// classes", and "pairwise" so the model knows when to call them (FR-018).
/// </summary>
public static class TestimizeTools
{
    private const string GenerateTestDataDescription =
        "Generate optimized test data using Testimize. Call this when you " +
        "identify input fields with validation rules (min/max length, numeric " +
        "ranges, required fields, format patterns). Returns boundary values, " +
        "equivalence classes, and pairwise combinations that are mathematically " +
        "optimal — better than manually chosen values. Pass an array of fields " +
        "with type, name, and optional min/max/valid_values/invalid_values/" +
        "error_messages. Optionally override the strategy " +
        "(Pairwise|HybridArtificialBeeColony|Combinatorial). Category may be " +
        "Valid or Validation.";

    private const string AnalyzeFieldSpecDescription =
        "Analyze a snippet of documentation text and extract input field " +
        "specifications (type, name, min, max, required, format). Use this " +
        "before calling GenerateTestData when you have a paragraph that " +
        "describes one or more input fields but you need them in structured " +
        "form. Returns a JSON array of field specs.";

    public static AIFunction CreateGenerateTestDataTool(
        TestimizeMcpClient client, TestimizeConfig config)
    {
        async Task<string> GenerateTestData(
            [Description("JSON array of input field specifications")] string fieldsJson,
            [Description("Optional strategy override: Pairwise, HybridArtificialBeeColony, or Combinatorial")] string? strategy = null,
            [Description("Optional category: Valid or Validation")] string? category = null,
            CancellationToken ct = default)
        {
            // Resolve and normalize the strategy. Unknown values fall back to
            // the configured default per FR-007.
            var effectiveStrategy = NormalizeStrategy(strategy ?? config.Strategy);
            var mcpMethod = effectiveStrategy switch
            {
                "Pairwise" or "PairwiseOptimized" => "generate_pairwise_test_cases",
                "Combinatorial" or "CombinatorialOptimized" => "generate_combinatorial_test_cases",
                _ => "generate_hybrid_test_cases"
            };

            JsonElement fieldsElement;
            try
            {
                using var doc = JsonDocument.Parse(fieldsJson);
                fieldsElement = doc.RootElement.Clone();
            }
            catch
            {
                return EmptyResultJson("invalid fields JSON");
            }

            var paramsObj = new Dictionary<string, object?>
            {
                ["fields"] = fieldsElement,
                ["strategy"] = effectiveStrategy,
                ["category"] = category ?? "Validation"
            };
            if (config.AbcSettings is not null)
                paramsObj["abc_settings"] = config.AbcSettings;
            if (config.AbcSettings?.Seed is not null)
                paramsObj["seed"] = config.AbcSettings.Seed;

            var paramsElement = JsonSerializer.SerializeToElement(paramsObj);

            var result = await client.CallToolAsync(mcpMethod, paramsElement, ct);
            if (result is null)
                return EmptyResultJson("Testimize unavailable — fall back to AI-approximated values for these fields");

            return result.Value.GetRawText();
        }

        return AIFunctionFactory.Create(
            GenerateTestData,
            nameof(GenerateTestData),
            GenerateTestDataDescription);
    }

    public static AIFunction CreateAnalyzeFieldSpecTool()
    {
        string AnalyzeFieldSpec(
            [Description("Documentation text snippet to analyze for input field specifications")] string text,
            CancellationToken ct = default)
        {
            var fields = ExtractFields(text ?? "");
            return JsonSerializer.Serialize(new { fields });
        }

        return AIFunctionFactory.Create(
            AnalyzeFieldSpec,
            nameof(AnalyzeFieldSpec),
            AnalyzeFieldSpecDescription);
    }

    /// <summary>Static helper exposed for unit tests.</summary>
    internal static List<FieldSpec> ExtractFields(string text)
    {
        var results = new List<FieldSpec>();
        if (string.IsNullOrWhiteSpace(text))
            return results;

        // "X to Y characters" or "X-Y characters"
        foreach (Match m in Regex.Matches(text,
            @"(\d+)\s*(?:to|-|–|—)\s*(\d+)\s*characters?",
            RegexOptions.IgnoreCase))
        {
            results.Add(new FieldSpec
            {
                Type = "text",
                Name = "field",
                Min = int.Parse(m.Groups[1].Value),
                Max = int.Parse(m.Groups[2].Value)
            });
        }

        // "between X and Y" (numeric — assume integer)
        foreach (Match m in Regex.Matches(text,
            @"between\s+(\d+)\s+and\s+(\d+)",
            RegexOptions.IgnoreCase))
        {
            results.Add(new FieldSpec
            {
                Type = "integer",
                Name = "field",
                Min = int.Parse(m.Groups[1].Value),
                Max = int.Parse(m.Groups[2].Value)
            });
        }

        // "valid email" or "email address"
        if (Regex.IsMatch(text, @"\b(valid\s+)?email(\s+address)?\b", RegexOptions.IgnoreCase))
        {
            results.Add(new FieldSpec { Type = "email", Name = "email" });
        }

        // "phone number"
        if (Regex.IsMatch(text, @"\bphone\s+number\b", RegexOptions.IgnoreCase))
        {
            results.Add(new FieldSpec { Type = "phone", Name = "phone" });
        }

        // "required field" → mark the most recent field (or create a generic one)
        if (Regex.IsMatch(text, @"\brequired\s+field\b", RegexOptions.IgnoreCase))
        {
            if (results.Count == 0)
            {
                results.Add(new FieldSpec
                {
                    Type = "text",
                    Name = "field",
                    ErrorMessages = new Dictionary<string, string> { ["required"] = "This field is required" }
                });
            }
            else
            {
                results[^1].ErrorMessages ??= new Dictionary<string, string>();
                results[^1].ErrorMessages!["required"] = "This field is required";
            }
        }

        return results;
    }

    private static string NormalizeStrategy(string? strategy)
    {
        return strategy switch
        {
            "Pairwise" or "Combinatorial" or "HybridArtificialBeeColony"
                or "PairwiseOptimized" or "CombinatorialOptimized" => strategy,
            _ => "HybridArtificialBeeColony"
        };
    }

    private static string EmptyResultJson(string reason) =>
        JsonSerializer.Serialize(new
        {
            test_combinations = Array.Empty<object>(),
            coverage_summary = new { reason }
        });

    /// <summary>Internal field spec used by the local AnalyzeFieldSpec extractor.</summary>
    public sealed class FieldSpec
    {
        public string Type { get; set; } = "text";
        public string Name { get; set; } = "field";
        public int? Min { get; set; }
        public int? Max { get; set; }
        public Dictionary<string, string>? ErrorMessages { get; set; }
    }
}
