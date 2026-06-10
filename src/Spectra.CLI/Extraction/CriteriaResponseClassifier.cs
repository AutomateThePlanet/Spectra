using System.Text.Json;
using Spectra.Core.Models.Coverage;

namespace Spectra.CLI.Extraction;

/// <summary>
/// Spec 069: the model-free criteria-response classifier — the rescued, verbatim body of the former
/// <c>CriteriaExtractor.ClassifyResponse</c> (+ its priority/technique normalizers). Decoupled from the
/// GitHub Copilot SDK by location so <see cref="CriteriaIngestor"/> and the
/// <c>compile-extraction-prompt</c>/<c>ingest-criteria</c> seam carry no SDK dependency.
///
/// Pure function — no I/O, no model call. Identical behaviour to the pre-069 implementation so the
/// <c>.criteria.yaml</c> / <c>_criteria_index.yaml</c> output stays byte-compatible.
/// </summary>
public static class CriteriaResponseClassifier
{
    /// <summary>
    /// Classify a raw AI response into one of three outcomes (Spec 047 mapping table). The
    /// <paramref name="onException"/> callback fires for parse-time exceptions so the caller can log.
    /// </summary>
    public static CriteriaExtractionResult Classify(
        string? responseText,
        string? source,
        string? component,
        Action<Exception>? onException = null)
    {
        if (string.IsNullOrWhiteSpace(responseText))
            return new CriteriaExtractionResult(ExtractionOutcome.EmptyResponse, []);

        try
        {
            var jsonStart = responseText.IndexOf('[');
            var jsonEnd = responseText.LastIndexOf(']');
            if (jsonStart < 0 || jsonEnd <= jsonStart)
                return new CriteriaExtractionResult(ExtractionOutcome.ParseFailure, []);

            var jsonText = responseText[jsonStart..(jsonEnd + 1)];

            var items = JsonSerializer.Deserialize<List<ExtractedCriterion>>(jsonText, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (items is null)
                return new CriteriaExtractionResult(ExtractionOutcome.ParseFailure, []);

            var criteria = items
                .Where(i => !string.IsNullOrWhiteSpace(i!.Text))
                .Select(i => new AcceptanceCriterion
                {
                    Text = i!.Text!.Trim(),
                    Rfc2119 = i.Rfc2119?.Trim().ToUpperInvariant(),
                    SourceDoc = source,
                    SourceSection = i.SourceSection?.Trim(),
                    Component = component,
                    Priority = NormalizePriority(i.Priority),
                    Tags = i.Tags?.Where(t => !string.IsNullOrWhiteSpace(t)).ToList() ?? [],
                    TechniqueHint = NormalizeTechniqueHint(i.TechniqueHint)
                })
                .ToList();

            return new CriteriaExtractionResult(ExtractionOutcome.Extracted, criteria);
        }
        catch (Exception ex)
        {
            onException?.Invoke(ex);
            return new CriteriaExtractionResult(ExtractionOutcome.ParseFailure, []);
        }
    }

    internal static string NormalizePriority(string? priority) =>
        priority?.Trim().ToLowerInvariant() switch
        {
            "high" => "high",
            "low" => "low",
            _ => "medium"
        };

    /// <summary>
    /// Normalizes ISTQB technique hint codes to canonical uppercase form.
    /// Returns null for empty/whitespace input or unrecognized values so the
    /// YAML serializer omits the field rather than writing junk.
    /// </summary>
    internal static string? NormalizeTechniqueHint(string? hint)
    {
        if (string.IsNullOrWhiteSpace(hint)) return null;
        var upper = hint.Trim().ToUpperInvariant();
        return upper switch
        {
            "BVA" or "EP" or "DT" or "ST" or "EG" or "UC" => upper,
            _ => null
        };
    }

    private sealed class ExtractedCriterion
    {
        public string? Text { get; set; }
        public string? Rfc2119 { get; set; }
        public string? SourceSection { get; set; }
        public string? Priority { get; set; }
        public List<string>? Tags { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("technique_hint")]
        public string? TechniqueHint { get; set; }
    }
}
