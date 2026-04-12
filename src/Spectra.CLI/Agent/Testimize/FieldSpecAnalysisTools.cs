using System.ComponentModel;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.AI;
using Spectra.Core.Models;
using Spectra.Core.Models.Testimize;

namespace Spectra.CLI.Agent.Testimize;

/// <summary>
/// Local regex-based extractor that turns a snippet of documentation text
/// into structured <see cref="FieldSpec"/> values. Two consumers:
///
/// 1. The generation model can call <c>AnalyzeFieldSpec</c> as an AIFunction
///    to structure prose mid-turn (kept for resilience).
/// 2. TestimizeRunner uses <see cref="Analyze(IReadOnlyList{SourceDocument})"/>
///    as a last-resort regex fallback when the behavior-analysis AI step
///    returned zero <c>field_specs</c>.
///
/// This helper is purely local — no MCP server, no AI call. Output conforms
/// to the shared <see cref="FieldSpec"/> contract in Spectra.Core so both
/// paths produce the same shape.
/// </summary>
public static class FieldSpecAnalysisTools
{
    private const string AnalyzeFieldSpecDescription =
        "Analyze a snippet of documentation text and extract input field " +
        "specifications (type, name, min, max, required). Returns a JSON " +
        "array of field specs. Useful for structuring a paragraph that " +
        "describes one or more input fields into a form the generation " +
        "pipeline can consume.";

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
    /// Runs <see cref="ExtractFields(string)"/> over every document's content
    /// and returns the flattened, de-duplicated result. TestimizeRunner calls
    /// this as the regex fallback when the AI behavior-analysis step did not
    /// emit any field_specs.
    /// </summary>
    public static List<FieldSpec> Analyze(IReadOnlyList<SourceDocument> documents)
    {
        var results = new List<FieldSpec>();
        if (documents is null || documents.Count == 0)
            return results;

        foreach (var doc in documents)
        {
            if (string.IsNullOrWhiteSpace(doc.Content))
                continue;
            results.AddRange(ExtractFields(doc.Content));
        }

        return Deduplicate(results);
    }

    /// <summary>
    /// Heuristic field extraction from a single text snippet. Exposed as
    /// <c>public</c> because unit tests and the AI-callable tool both use it.
    /// </summary>
    public static List<FieldSpec> ExtractFields(string text)
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
                MinLength = int.Parse(m.Groups[1].Value),
                MaxLength = int.Parse(m.Groups[2].Value)
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
                    Required = true,
                    ExpectedInvalidMessage = "This field is required"
                });
            }
            else
            {
                var last = results[^1];
                results[^1] = new FieldSpec
                {
                    Name = last.Name,
                    Type = last.Type,
                    Required = true,
                    Min = last.Min,
                    Max = last.Max,
                    MinDate = last.MinDate,
                    MaxDate = last.MaxDate,
                    MinLength = last.MinLength,
                    MaxLength = last.MaxLength,
                    AllowedValues = last.AllowedValues,
                    ExpectedInvalidMessage = last.ExpectedInvalidMessage ?? "This field is required"
                };
            }
        }

        return results;
    }

    private static List<FieldSpec> Deduplicate(List<FieldSpec> specs)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var result = new List<FieldSpec>(specs.Count);
        foreach (var s in specs)
        {
            var key = $"{s.Type}|{s.Name}|{s.Min}|{s.Max}|{s.MinLength}|{s.MaxLength}";
            if (seen.Add(key))
                result.Add(s);
        }
        return result;
    }
}
