using System.Text.Json;
using System.Text.RegularExpressions;
using Spectra.Core.Models.Grounding;

namespace Spectra.CLI.Verification;

/// <summary>
/// The fail-loud verdict-ingest boundary (Spec 055 FR-006/FR-007). Classifies an agent-produced
/// critic response into a typed <see cref="VerdictIngestResult"/>. Reuses the
/// <see cref="Spectra.CLI.Agent.Critic.CriticResponseParser"/> JSON-extraction <i>shape</i>
/// (markdown-fence strip + first<c>{</c>…last<c>}</c> slice) but <b>changes the missing-field
/// defaults</b>: a missing/unparseable <c>verdict</c> or <c>score</c> is a typed
/// <see cref="VerdictIngestOutcome.ParseFailure"/> with a specific error — never the old silent
/// <c>Partial</c>/<c>0.5</c> soft pass.
///
/// <see cref="Classify"/> is total and pure: every input maps to exactly one outcome and it
/// <b>never throws</b>. A critic <i>call</i> failure (exception/timeout) is a separate runtime
/// concern (the retained in-process path's Unverified-style result) and is never routed through
/// this boundary, so damage and failure are never conflated (FR-007).
/// </summary>
public static partial class VerdictIngestor
{
    [GeneratedRegex(@"```json\s*", RegexOptions.IgnoreCase)]
    private static partial Regex JsonStartRegex();

    [GeneratedRegex(@"\s*```", RegexOptions.IgnoreCase)]
    private static partial Regex JsonEndRegex();

    /// <summary>
    /// Classifies a raw critic response. Empty/whitespace → <see cref="VerdictIngestOutcome.EmptyResponse"/>;
    /// non-JSON or missing/unparseable <c>verdict</c>/<c>score</c> → <see cref="VerdictIngestOutcome.ParseFailure"/>
    /// with a specific error; a well-formed response → <see cref="VerdictIngestOutcome.Verdict"/>
    /// carrying a reused <see cref="VerificationResult"/>. Never throws.
    /// </summary>
    public static VerdictIngestResult Classify(string? response)
    {
        if (string.IsNullOrWhiteSpace(response))
            return VerdictIngestResult.Failure(
                VerdictIngestOutcome.EmptyResponse, "critic returned no content");

        try
        {
            var json = ExtractJson(response);
            if (string.IsNullOrWhiteSpace(json))
                return VerdictIngestResult.Failure(
                    VerdictIngestOutcome.ParseFailure, "no JSON object found in the critic response");

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            // FR-006: missing/unparseable verdict or score is DAMAGE — fail loud, no soft default.
            if (!TryReadVerdict(root, out var verdict))
                return VerdictIngestResult.Failure(
                    VerdictIngestOutcome.ParseFailure,
                    "critic response missing or unparseable required 'verdict' field");

            if (!TryReadScore(root, out var score))
                return VerdictIngestResult.Failure(
                    VerdictIngestOutcome.ParseFailure,
                    "critic response missing or unparseable required 'score' field");

            var findings = ReadFindings(root);

            return VerdictIngestResult.FromVerdict(new VerificationResult
            {
                Verdict = verdict,
                Score = score,
                Findings = findings,
                CriticModel = root.TryGetProperty("critic_model", out var m) ? m.GetString() ?? "unknown" : "unknown"
            });
        }
        catch (JsonException ex)
        {
            return VerdictIngestResult.Failure(
                VerdictIngestOutcome.ParseFailure, $"invalid JSON in critic response: {ex.Message}");
        }
        catch (Exception ex)
        {
            return VerdictIngestResult.Failure(
                VerdictIngestOutcome.ParseFailure, $"could not classify critic response: {ex.Message}");
        }
    }

    /// <summary>
    /// Extracts the JSON object from a response that may be wrapped in a markdown code block.
    /// Reuses the <c>CriticResponseParser</c> extraction shape (fence strip + first<c>{</c>…last<c>}</c>).
    /// </summary>
    private static string ExtractJson(string response)
    {
        var json = response.Trim();
        json = JsonStartRegex().Replace(json, "");
        json = JsonEndRegex().Replace(json, "");

        var startIndex = json.IndexOf('{');
        var endIndex = json.LastIndexOf('}');
        if (startIndex >= 0 && endIndex > startIndex)
            json = json[startIndex..(endIndex + 1)];

        return json.Trim();
    }

    private static bool TryReadVerdict(JsonElement root, out VerificationVerdict verdict)
    {
        verdict = default;
        if (!root.TryGetProperty("verdict", out var element) || element.ValueKind != JsonValueKind.String)
            return false;

        switch (element.GetString()?.ToLowerInvariant())
        {
            case "grounded": verdict = VerificationVerdict.Grounded; return true;
            case "partial": verdict = VerificationVerdict.Partial; return true;
            case "hallucinated": verdict = VerificationVerdict.Hallucinated; return true;
            case "manual": verdict = VerificationVerdict.Manual; return true;
            default: return false; // unknown verdict string is damage, NOT a silent coercion to Partial
        }
    }

    private static bool TryReadScore(JsonElement root, out double score)
    {
        score = 0.0;
        if (!root.TryGetProperty("score", out var element))
            return false;

        switch (element.ValueKind)
        {
            case JsonValueKind.Number:
                score = Math.Clamp(element.GetDouble(), 0.0, 1.0);
                return true;
            case JsonValueKind.String when double.TryParse(element.GetString(), out var parsed):
                score = Math.Clamp(parsed, 0.0, 1.0);
                return true;
            default:
                return false;
        }
    }

    private static List<CriticFinding> ReadFindings(JsonElement root)
    {
        var findings = new List<CriticFinding>();
        if (!root.TryGetProperty("findings", out var arr) || arr.ValueKind != JsonValueKind.Array)
            return findings;

        foreach (var el in arr.EnumerateArray())
        {
            var element = el.TryGetProperty("element", out var e) ? e.GetString() : null;
            var claim = el.TryGetProperty("claim", out var c) ? c.GetString() : null;
            if (string.IsNullOrEmpty(element) || string.IsNullOrEmpty(claim))
                continue;

            var status = (el.TryGetProperty("status", out var s) ? s.GetString()?.ToLowerInvariant() : null) switch
            {
                "grounded" => FindingStatus.Grounded,
                "hallucinated" => FindingStatus.Hallucinated,
                _ => FindingStatus.Unverified
            };

            findings.Add(new CriticFinding
            {
                Element = element,
                Claim = claim,
                Status = status,
                Evidence = el.TryGetProperty("evidence", out var ev) && ev.ValueKind != JsonValueKind.Null ? ev.GetString() : null,
                Reason = el.TryGetProperty("reason", out var r) && r.ValueKind != JsonValueKind.Null ? r.GetString() : null
            });
        }

        return findings;
    }
}
