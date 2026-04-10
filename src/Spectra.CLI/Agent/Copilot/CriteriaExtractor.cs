using System.Text.Json;
using GitHub.Copilot.SDK;
using Spectra.CLI.Prompts;
using Spectra.Core.Models.Coverage;
using SpectraProviderConfig = Spectra.Core.Models.Config.ProviderConfig;

namespace Spectra.CLI.Agent.Copilot;

/// <summary>
/// Extracts acceptance criteria from a single document using the Copilot SDK.
/// Per-document extraction eliminates truncation issues.
/// </summary>
public sealed class CriteriaExtractor
{
    private readonly SpectraProviderConfig _provider;
    private readonly Action<string>? _onStatus;

    public CriteriaExtractor(SpectraProviderConfig provider, Action<string>? onStatus = null)
    {
        _provider = provider;
        _onStatus = onStatus;
    }

    /// <summary>
    /// Extracts acceptance criteria from a single document.
    /// </summary>
    public async Task<IReadOnlyList<AcceptanceCriterion>> ExtractFromDocumentAsync(
        string documentPath,
        string documentContent,
        string? component,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(documentContent))
            return [];

        var service = await CopilotService.GetInstanceAsync(ct);
        await using var session = await service.CreateGenerationSessionAsync(_provider, ct: ct);

        var prompt = BuildExtractionPrompt(documentPath, documentContent, component);

        _onStatus?.Invoke($"Extracting criteria from {Path.GetFileName(documentPath)}...");

        var response = await session.SendAndWaitAsync(
            new MessageOptions { Prompt = prompt },
            timeout: TimeSpan.FromMinutes(2),
            cancellationToken: ct);

        var responseText = response?.Data?.Content ?? "";
        if (string.IsNullOrWhiteSpace(responseText))
            return [];

        return ParseResponse(responseText, documentPath, component);
    }

    /// <summary>
    /// Splits compound criteria text and normalizes to RFC 2119 (for import flow).
    /// </summary>
    public async Task<IReadOnlyList<AcceptanceCriterion>> SplitAndNormalizeAsync(
        string rawText,
        string? sourceKey,
        string? component,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(rawText))
            return [];

        var service = await CopilotService.GetInstanceAsync(ct);
        await using var session = await service.CreateGenerationSessionAsync(_provider, ct: ct);

        var prompt = BuildSplitPrompt(rawText, sourceKey, component);

        var response = await session.SendAndWaitAsync(
            new MessageOptions { Prompt = prompt },
            timeout: TimeSpan.FromMinutes(1),
            cancellationToken: ct);

        var responseText = response?.Data?.Content ?? "";
        if (string.IsNullOrWhiteSpace(responseText))
            return [];

        return ParseResponse(responseText, sourceKey, component);
    }

    internal static string BuildExtractionPrompt(string docPath, string content, string? component,
        PromptTemplateLoader? templateLoader = null)
    {
        if (templateLoader is not null)
        {
            var template = templateLoader.LoadTemplate("criteria-extraction");
            var values = new Dictionary<string, string>
            {
                ["document_text"] = content,
                ["document_title"] = docPath,
                ["existing_criteria"] = "",
                ["component"] = component ?? ""
            };
            return PromptTemplateLoader.Resolve(template, values);
        }

        var componentHint = component is not null
            ? $"\nThe document belongs to the \"{component}\" component."
            : "";

        return $$"""
            You are a requirements analyst. Extract all testable acceptance criteria from this single document.

            For each criterion:
            - text: Rewrite using RFC 2119 language (MUST, SHOULD, MAY). Preserve original meaning.
            - rfc2119: The primary RFC 2119 keyword used (MUST, MUST NOT, SHALL, SHALL NOT, SHOULD, SHOULD NOT, MAY, REQUIRED, RECOMMENDED, OPTIONAL)
            - source_section: The heading/section where this criterion was found
            - priority: "high" for MUST/SHALL/REQUIRED, "medium" for SHOULD/RECOMMENDED, "low" for MAY/OPTIONAL
            - tags: 1-3 relevant categorization tags
            {{componentHint}}

            Respond ONLY with a JSON array. No markdown, no explanation, no code fences. Example:
            [
              {"text": "System MUST validate IBAN format before payment", "rfc2119": "MUST", "source_section": "Payment Validation", "priority": "high", "tags": ["payment", "validation"]},
              {"text": "System SHOULD display inline error within 500ms", "rfc2119": "SHOULD", "source_section": "UX Requirements", "priority": "medium", "tags": ["ux", "performance"]}
            ]

            Document path: {{docPath}}

            Document content:
            {{content}}
            """;
    }

    private static string BuildSplitPrompt(string rawText, string? sourceKey, string? component)
    {
        return $$"""
            Split the following text into individual testable acceptance criteria. Each criterion should be a single, atomic, testable statement.

            For each criterion:
            - text: Rewrite using RFC 2119 language (MUST, SHOULD, MAY). Preserve original meaning.
            - rfc2119: The primary RFC 2119 keyword used
            - priority: "high" for MUST/SHALL, "medium" for SHOULD, "low" for MAY
            - tags: 1-3 relevant tags

            Respond ONLY with a JSON array. No markdown, no explanation, no code fences.
            {{(sourceKey is not null ? $"\nSource: {sourceKey}" : "")}}
            {{(component is not null ? $"\nComponent: {component}" : "")}}

            Text to split:
            {{rawText}}
            """;
    }

    private static IReadOnlyList<AcceptanceCriterion> ParseResponse(
        string responseText, string? source, string? component)
    {
        try
        {
            var jsonStart = responseText.IndexOf('[');
            var jsonEnd = responseText.LastIndexOf(']');
            if (jsonStart < 0 || jsonEnd <= jsonStart)
                return [];

            var jsonText = responseText[jsonStart..(jsonEnd + 1)];

            var items = JsonSerializer.Deserialize<List<ExtractedCriterion>>(jsonText, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (items is null)
                return [];

            return items
                .Where(i => !string.IsNullOrWhiteSpace(i.Text))
                .Select(i => new AcceptanceCriterion
                {
                    Text = i.Text!.Trim(),
                    Rfc2119 = i.Rfc2119?.Trim().ToUpperInvariant(),
                    SourceDoc = source,
                    SourceSection = i.SourceSection?.Trim(),
                    Component = component,
                    Priority = NormalizePriority(i.Priority),
                    Tags = i.Tags?.Where(t => !string.IsNullOrWhiteSpace(t)).ToList() ?? []
                })
                .ToList();
        }
        catch
        {
            return [];
        }
    }

    private static string NormalizePriority(string? priority) =>
        priority?.Trim().ToLowerInvariant() switch
        {
            "high" => "high",
            "low" => "low",
            _ => "medium"
        };

    private sealed class ExtractedCriterion
    {
        public string? Text { get; set; }
        public string? Rfc2119 { get; set; }
        public string? SourceSection { get; set; }
        public string? Priority { get; set; }
        public List<string>? Tags { get; set; }
    }
}
