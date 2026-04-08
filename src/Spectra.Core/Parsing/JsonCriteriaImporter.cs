using System.Text.Json;
using System.Text.Json.Serialization;
using Spectra.Core.Models.Coverage;

namespace Spectra.Core.Parsing;

/// <summary>
/// Imports acceptance criteria from JSON files.
/// </summary>
public sealed class JsonCriteriaImporter
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    /// <summary>
    /// Imports criteria from a JSON file with a "criteria" array.
    /// </summary>
    public async Task<IReadOnlyList<AcceptanceCriterion>> ImportAsync(
        string filePath, string defaultSourceType = "manual", CancellationToken ct = default)
    {
        var json = await File.ReadAllTextAsync(filePath, ct);
        if (string.IsNullOrWhiteSpace(json))
            return [];

        try
        {
            var doc = JsonSerializer.Deserialize<JsonCriteriaDocument>(json, Options);
            if (doc?.Criteria is null)
                return [];

            return doc.Criteria
                .Where(c => !string.IsNullOrWhiteSpace(c.Text))
                .Select(c => new AcceptanceCriterion
                {
                    Id = c.Id ?? string.Empty,
                    Text = c.Text!.Trim(),
                    Rfc2119 = c.Rfc2119,
                    Source = c.Source,
                    SourceType = c.SourceType ?? defaultSourceType,
                    Component = c.Component,
                    Priority = c.Priority ?? "medium",
                    Tags = c.Tags ?? []
                })
                .ToList();
        }
        catch (JsonException)
        {
            throw new InvalidOperationException($"Invalid JSON format in {filePath}. Expected {{\"criteria\": [...]}}.");
        }
    }

    private sealed class JsonCriteriaDocument
    {
        [JsonPropertyName("criteria")]
        public List<JsonCriterionEntry>? Criteria { get; set; }
    }

    private sealed class JsonCriterionEntry
    {
        [JsonPropertyName("id")]
        public string? Id { get; set; }

        [JsonPropertyName("text")]
        public string? Text { get; set; }

        [JsonPropertyName("rfc2119")]
        public string? Rfc2119 { get; set; }

        [JsonPropertyName("source")]
        public string? Source { get; set; }

        [JsonPropertyName("source_type")]
        public string? SourceType { get; set; }

        [JsonPropertyName("component")]
        public string? Component { get; set; }

        [JsonPropertyName("priority")]
        public string? Priority { get; set; }

        [JsonPropertyName("tags")]
        public List<string>? Tags { get; set; }
    }
}
