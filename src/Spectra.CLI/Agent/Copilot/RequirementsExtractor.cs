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
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when the AI extraction call fails.</exception>
    public async Task<IReadOnlyList<RequirementDefinition>> ExtractAsync(
        IReadOnlyList<Spectra.Core.Models.DocumentEntry> documents,
        IReadOnlyList<RequirementDefinition> existingRequirements,
        CancellationToken ct = default)
    {
        if (documents.Count == 0)
            return [];

        var service = await CopilotService.GetInstanceAsync(ct);

        _onStatus?.Invoke("Creating extraction session...");
        await using var session = await service.CreateGenerationSessionAsync(
            _provider,
            ct: ct);

        var prompt = await BuildExtractionPromptAsync(documents, existingRequirements, ct);

        _onStatus?.Invoke("Extracting requirements from documentation...");

        // Use Task.WhenAny as a hard timeout — the SDK doesn't always honor CancellationToken
        var sendTask = session.SendAndWaitAsync(
            new MessageOptions { Prompt = prompt },
            timeout: TimeSpan.FromMinutes(2),
            cancellationToken: ct);
        var delayTask = Task.Delay(TimeSpan.FromMinutes(2), ct);

        var completedTask = await Task.WhenAny(sendTask, delayTask);
        if (completedTask == delayTask)
        {
            throw new TimeoutException(
                "AI provider did not respond within 2 minutes. Check your provider configuration and connectivity.");
        }

        var response = await sendTask;
        var responseText = response?.Data?.Content ?? "";

        if (string.IsNullOrWhiteSpace(responseText))
            throw new InvalidOperationException(
                "AI provider returned an empty response. Check your provider configuration and connectivity.");

        var results = ParseResponse(responseText);
        if (results.Count == 0)
        {
            _onStatus?.Invoke("Warning: AI response could not be parsed into requirements.");
        }
        else
        {
            _onStatus?.Invoke($"Extracted {results.Count} requirement(s) from AI response.");
        }

        return results;
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

    private static IReadOnlyList<RequirementDefinition> ParseResponse(string responseText)
    {
        if (string.IsNullOrWhiteSpace(responseText))
            return [];

        try
        {
            // Extract JSON array from response (may have markdown code fences)
            var jsonStart = responseText.IndexOf('[');
            var jsonEnd = responseText.LastIndexOf(']');

            if (jsonStart < 0)
                return [];

            string jsonText;
            if (jsonEnd < 0 || jsonEnd <= jsonStart)
            {
                // Response was truncated before closing ']' — recover partial JSON
                // Find the last complete JSON object (ends with '}')
                var lastBrace = responseText.LastIndexOf('}');
                if (lastBrace <= jsonStart)
                    return [];
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
                return [];

            return items
                .Where(i => !string.IsNullOrWhiteSpace(i.Title))
                .Select(i => new RequirementDefinition
                {
                    Title = i.Title!.Trim(),
                    Source = i.Source?.Trim(),
                    Priority = NormalizePriority(i.Priority)
                })
                .ToList();
        }
        catch
        {
            return [];
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
