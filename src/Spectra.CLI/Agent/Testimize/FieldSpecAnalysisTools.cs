using System.ComponentModel;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.AI;

namespace Spectra.CLI.Agent.Testimize;

/// <summary>
/// Local regex-based extractor that turns a snippet of documentation text
/// into structured input field specifications. Exposed to the generation
/// model as an <see cref="AIFunction"/>. This helper does **not** call any
/// MCP server — it's purely local. The output is meant to be passed to one
/// of the real testimize MCP tools (<c>testimize/generate_hybrid_test_cases</c>
/// or <c>testimize/generate_pairwise_test_cases</c>) by the AI.
///
/// v1.46.0: split out from the old <c>TestimizeTools</c> class (which also
/// contained the broken <c>TestimizeMcpClient</c>-based wrapper). Those have
/// been deleted; testimize's actual tools now flow through the Copilot SDK's
/// native MCP support (<see cref="Spectra.CLI.Agent.Copilot.McpConfigBuilder"/>).
/// </summary>
public static class FieldSpecAnalysisTools
{
    private const string AnalyzeFieldSpecDescription =
        "Analyze a snippet of documentation text and extract input field " +
        "specifications (type, name, min, max, required, format). Use this " +
        "before calling testimize/generate_hybrid_test_cases or " +
        "testimize/generate_pairwise_test_cases when you have a paragraph " +
        "that describes one or more input fields but you need them in " +
        "structured form. Returns a JSON array of field specs.";

    /// <summary>
    /// Factory for the <c>AnalyzeFieldSpec</c> AI function.
    /// </summary>
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

    /// <summary>
    /// Heuristic field extraction used by <see cref="CreateAnalyzeFieldSpecTool"/>.
    /// Exposed as <c>internal</c> for unit tests.
    /// </summary>
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

    /// <summary>
    /// Structured field specification produced by the local extractor.
    /// </summary>
    public sealed class FieldSpec
    {
        public string Type { get; set; } = "text";
        public string Name { get; set; } = "field";
        public int? Min { get; set; }
        public int? Max { get; set; }
        public Dictionary<string, string>? ErrorMessages { get; set; }
    }
}
