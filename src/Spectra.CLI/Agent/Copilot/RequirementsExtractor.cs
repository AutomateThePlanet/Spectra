#pragma warning disable CS0618 // RequirementDefinition is obsolete project-wide; the docs-index path still produces it pending the full criteria migration. (Throwing legacy semantics removed in Spec 054.)
using System.Text.Json;
using GitHub.Copilot.SDK;
using Spectra.Core.Models.Coverage;
using SpectraProviderConfig = Spectra.Core.Models.Config.ProviderConfig;

namespace Spectra.CLI.Agent.Copilot;

/// <summary>
/// Extracts testable requirements from documentation using the Copilot SDK.
/// </summary>
public sealed class RequirementsExtractor
{
    private readonly SpectraProviderConfig _provider;
    private readonly Action<string>? _onStatus;

    private readonly string _basePath;

    public RequirementsExtractor(SpectraProviderConfig provider, string basePath, Action<string>? onStatus = null)
    {
        _provider = provider;
        _basePath = basePath;
        _onStatus = onStatus;
    }

    /// <summary>
    /// Extracts testable requirements from source documents.
    /// Spec 047: iterates documents and delegates to the per-document method.
    /// Spec 054: aggregates only the genuinely-extracted requirements of each typed result.
    /// </summary>
    public async Task<IReadOnlyList<RequirementDefinition>> ExtractAsync(
        IReadOnlyList<Spectra.Core.Models.DocumentEntry> documents,
        IReadOnlyList<RequirementDefinition> existingRequirements,
        CancellationToken ct = default)
    {
        if (documents.Count == 0)
            return [];

        var aggregated = new List<RequirementDefinition>();
        foreach (var doc in documents)
        {
            var perDoc = await ExtractFromDocumentAsync(doc, existingRequirements, ct);
            aggregated.AddRange(perDoc.Requirements);
        }
        return aggregated;
    }

    /// <summary>
    /// Spec 047: per-document variant. Sends one prompt containing a single document so the caller
    /// can apply a per-document deadline.
    /// Spec 054 (FR-004): returns a typed <see cref="RequirementsExtractionResult"/> sharing the
    /// <see cref="ExtractionOutcome"/> enum + <c>IsCacheable</c> rule with the criteria path. It no
    /// longer throws on an empty response or on timeout — an empty response becomes
    /// <see cref="ExtractionOutcome.EmptyResponse"/>, and the per-document timeout is owned solely
    /// by the caller's deadline (<c>DocsIndexHandler.ExtractCriteriaLoopAsync</c>), not an internal
    /// throw.
    /// </summary>
    public async Task<RequirementsExtractionResult> ExtractFromDocumentAsync(
        Spectra.Core.Models.DocumentEntry document,
        IReadOnlyList<RequirementDefinition> existingRequirements,
        CancellationToken ct = default)
    {
        var service = await CopilotService.GetInstanceAsync(ct);

        _onStatus?.Invoke($"Creating extraction session for {document.Path}...");
        await using var session = await service.CreateGenerationSessionAsync(
            _provider,
            ct: ct);

        var prompt = await BuildExtractionPromptAsync(new[] { document }, existingRequirements, ct);

        _onStatus?.Invoke($"Extracting acceptance criteria from {document.Path}...");

        var response = await session.SendAndWaitAsync(
            new MessageOptions { Prompt = prompt },
            timeout: TimeSpan.FromMinutes(2),
            cancellationToken: ct);

        var responseText = response?.Data?.Content ?? "";
        var result = ClassifyResponse(responseText);

        switch (result.Outcome)
        {
            case ExtractionOutcome.EmptyResponse:
                _onStatus?.Invoke($"AI returned an empty response for {document.Path}.");
                break;
            case ExtractionOutcome.ParseFailure:
                _onStatus?.Invoke($"Warning: AI response for {document.Path} could not be parsed into acceptance criteria.");
                break;
            default:
                _onStatus?.Invoke($"Extracted {result.Requirements.Count} acceptance criteria from {document.Path}.");
                break;
        }

        return result;
    }

    private async Task<string> BuildExtractionPromptAsync(
        IReadOnlyList<Spectra.Core.Models.DocumentEntry> documents,
        IReadOnlyList<RequirementDefinition> existing,
        CancellationToken ct)
    {
        var docParts = new List<string>();
        foreach (var doc in documents)
        {
            var fullPath = Path.Combine(_basePath, doc.Path);
            var content = File.Exists(fullPath)
                ? await File.ReadAllTextAsync(fullPath, ct)
                : doc.Preview;
            docParts.Add($"## Document: {doc.Path}\n\n{content}");
        }
        var docContent = string.Join("\n\n---\n\n", docParts);

        var existingTitles = existing.Count > 0
            ? "Already extracted requirements (DO NOT duplicate these):\n" +
              string.Join("\n", existing.Select(r => $"- {r.Id}: {r.Title}"))
            : "No existing requirements.";

        return $$"""
            You are a requirements analyst. Extract all testable behavioral requirements from the documentation below.

            For each requirement:
            - title: A concise statement of the testable behavior (e.g., "System locks account after 5 failed login attempts")
            - source: The document path where this behavior is described
            - priority: Based on RFC 2119 language:
              - "high" if the document uses MUST, SHALL, REQUIRED, CRITICAL, or similar mandatory language
              - "medium" if the document uses SHOULD, RECOMMENDED, or similar advisory language
              - "low" if the document uses MAY, OPTIONAL, NICE TO HAVE, or similar permissive language
              - Default to "medium" if no clear priority language is present

            {{existingTitles}}

            Respond ONLY with a JSON array. No markdown, no explanation, no code fences. Keep responses under 50 requirements. Example:
            [
              {"title": "User can reset password via email", "source": "docs/auth.md", "priority": "high"},
              {"title": "System displays warning on weak password", "source": "docs/auth.md", "priority": "medium"}
            ]

            Documentation:
            {{docContent}}
            """;
    }

    /// <summary>
    /// Spec 054: classify a raw AI response into a typed <see cref="RequirementsExtractionResult"/>,
    /// paralleling <see cref="CriteriaExtractor.ClassifyResponse"/>. Pure function — exposed
    /// <c>internal</c> so tests can drive each outcome without an AI provider. A valid JSON array
    /// (even an empty one) is <see cref="ExtractionOutcome.Extracted"/>; whitespace is
    /// <see cref="ExtractionOutcome.EmptyResponse"/>; a missing/unparseable array is
    /// <see cref="ExtractionOutcome.ParseFailure"/>. Never throws.
    /// </summary>
    internal static RequirementsExtractionResult ClassifyResponse(string? responseText)
    {
        if (string.IsNullOrWhiteSpace(responseText))
            return new RequirementsExtractionResult(ExtractionOutcome.EmptyResponse, []);

        try
        {
            // Extract JSON array from response (may have markdown code fences)
            var jsonStart = responseText.IndexOf('[');
            var jsonEnd = responseText.LastIndexOf(']');

            if (jsonStart < 0)
                return new RequirementsExtractionResult(ExtractionOutcome.ParseFailure, []);

            string jsonText;
            if (jsonEnd < 0 || jsonEnd <= jsonStart)
            {
                // Response was truncated before closing ']' — recover partial JSON
                // Find the last complete JSON object (ends with '}')
                var lastBrace = responseText.LastIndexOf('}');
                if (lastBrace <= jsonStart)
                    return new RequirementsExtractionResult(ExtractionOutcome.ParseFailure, []);
                jsonText = responseText[jsonStart..(lastBrace + 1)] + "]";
            }
            else
            {
                jsonText = responseText[jsonStart..(jsonEnd + 1)];
            }

            var items = JsonSerializer.Deserialize<List<ExtractedItem>>(jsonText, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (items is null)
                return new RequirementsExtractionResult(ExtractionOutcome.ParseFailure, []);

            var defs = items
                .Where(i => !string.IsNullOrWhiteSpace(i.Title))
                .Select(i => new RequirementDefinition
                {
                    Title = i.Title!.Trim(),
                    Source = i.Source?.Trim(),
                    Priority = NormalizePriority(i.Priority)
                })
                .ToList();

            return new RequirementsExtractionResult(ExtractionOutcome.Extracted, defs);
        }
        catch
        {
            return new RequirementsExtractionResult(ExtractionOutcome.ParseFailure, []);
        }
    }

    private static string NormalizePriority(string? priority)
    {
        if (string.IsNullOrWhiteSpace(priority))
            return "medium";

        return priority.Trim().ToLowerInvariant() switch
        {
            "high" => "high",
            "low" => "low",
            _ => "medium"
        };
    }

    private sealed class ExtractedItem
    {
        public string? Title { get; set; }
        public string? Source { get; set; }
        public string? Priority { get; set; }
    }
}
