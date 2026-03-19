using System.Text.Json;
using System.Text.RegularExpressions;
using Spectra.Core.Models.Grounding;

namespace Spectra.CLI.Agent.Critic;

/// <summary>
/// Parses critic model responses into verification results.
/// </summary>
public sealed partial class CriticResponseParser
{
    [GeneratedRegex(@"```json\s*", RegexOptions.IgnoreCase)]
    private static partial Regex JsonStartRegex();

    [GeneratedRegex(@"\s*```", RegexOptions.IgnoreCase)]
    private static partial Regex JsonEndRegex();

    /// <summary>
    /// Parses a critic response into a verification result.
    /// </summary>
    /// <param name="response">Raw response from the critic model</param>
    /// <param name="modelName">Name of the critic model</param>
    /// <param name="duration">Time taken for verification</param>
    /// <returns>Parsed verification result</returns>
    public VerificationResult Parse(string response, string modelName, TimeSpan duration)
    {
        if (string.IsNullOrWhiteSpace(response))
        {
            return CreateErrorResult(modelName, "Empty response from critic model");
        }

        try
        {
            var json = ExtractJson(response);
            return ParseJson(json, modelName, duration);
        }
        catch (JsonException ex)
        {
            return CreateErrorResult(modelName, $"Invalid JSON response: {ex.Message}");
        }
        catch (Exception ex)
        {
            return CreateErrorResult(modelName, $"Parse error: {ex.Message}");
        }
    }

    /// <summary>
    /// Extracts JSON from a response that may be wrapped in markdown code blocks.
    /// </summary>
    private static string ExtractJson(string response)
    {
        var json = response.Trim();

        // Remove markdown code blocks
        json = JsonStartRegex().Replace(json, "");
        json = JsonEndRegex().Replace(json, "");

        // Find JSON object boundaries
        var startIndex = json.IndexOf('{');
        var endIndex = json.LastIndexOf('}');

        if (startIndex >= 0 && endIndex > startIndex)
        {
            json = json[startIndex..(endIndex + 1)];
        }

        return json.Trim();
    }

    /// <summary>
    /// Parses the JSON into a verification result.
    /// </summary>
    private static VerificationResult ParseJson(string json, string modelName, TimeSpan duration)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        var verdict = ParseVerdict(root);
        var score = ParseScore(root);
        var findings = ParseFindings(root);

        return new VerificationResult
        {
            Verdict = verdict,
            Score = score,
            Findings = findings,
            CriticModel = modelName,
            Duration = duration
        };
    }

    private static VerificationVerdict ParseVerdict(JsonElement root)
    {
        if (!root.TryGetProperty("verdict", out var verdictElement))
            return VerificationVerdict.Partial;

        var verdictStr = verdictElement.GetString()?.ToLowerInvariant();
        return verdictStr switch
        {
            "grounded" => VerificationVerdict.Grounded,
            "partial" => VerificationVerdict.Partial,
            "hallucinated" => VerificationVerdict.Hallucinated,
            _ => VerificationVerdict.Partial
        };
    }

    private static double ParseScore(JsonElement root)
    {
        if (!root.TryGetProperty("score", out var scoreElement))
            return 0.5;

        return scoreElement.ValueKind switch
        {
            JsonValueKind.Number => Math.Clamp(scoreElement.GetDouble(), 0.0, 1.0),
            JsonValueKind.String when double.TryParse(scoreElement.GetString(), out var parsed) =>
                Math.Clamp(parsed, 0.0, 1.0),
            _ => 0.5
        };
    }

    private static List<CriticFinding> ParseFindings(JsonElement root)
    {
        var findings = new List<CriticFinding>();

        if (!root.TryGetProperty("findings", out var findingsElement) ||
            findingsElement.ValueKind != JsonValueKind.Array)
        {
            return findings;
        }

        foreach (var findingElement in findingsElement.EnumerateArray())
        {
            var finding = ParseFinding(findingElement);
            if (finding is not null)
            {
                findings.Add(finding);
            }
        }

        return findings;
    }

    private static CriticFinding? ParseFinding(JsonElement element)
    {
        var elementName = element.TryGetProperty("element", out var e) ? e.GetString() : null;
        var claim = element.TryGetProperty("claim", out var c) ? c.GetString() : null;

        if (string.IsNullOrEmpty(elementName) || string.IsNullOrEmpty(claim))
            return null;

        var statusStr = element.TryGetProperty("status", out var s) ? s.GetString()?.ToLowerInvariant() : null;
        var status = statusStr switch
        {
            "grounded" => FindingStatus.Grounded,
            "unverified" => FindingStatus.Unverified,
            "hallucinated" => FindingStatus.Hallucinated,
            _ => FindingStatus.Unverified
        };

        var evidence = element.TryGetProperty("evidence", out var ev) &&
            ev.ValueKind != JsonValueKind.Null ? ev.GetString() : null;
        var reason = element.TryGetProperty("reason", out var r) &&
            r.ValueKind != JsonValueKind.Null ? r.GetString() : null;

        return new CriticFinding
        {
            Element = elementName,
            Claim = claim,
            Status = status,
            Evidence = evidence,
            Reason = reason
        };
    }

    private static VerificationResult CreateErrorResult(string modelName, string error)
    {
        return new VerificationResult
        {
            Verdict = VerificationVerdict.Partial,
            Score = 0.0,
            Findings = [],
            CriticModel = modelName,
            Errors = [error]
        };
    }
}
