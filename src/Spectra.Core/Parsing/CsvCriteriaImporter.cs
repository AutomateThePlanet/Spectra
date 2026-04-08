using CsvHelper;
using CsvHelper.Configuration;
using System.Globalization;
using Spectra.Core.Models.Coverage;

namespace Spectra.Core.Parsing;

/// <summary>
/// Imports acceptance criteria from CSV files with auto-detection of column names.
/// </summary>
public sealed class CsvCriteriaImporter
{
    // Priority-ordered column name mappings (case-insensitive, first match wins)
    private static readonly string[] TextColumns = ["text", "summary", "title", "acceptance_criteria", "acceptance criteria", "description"];
    private static readonly string[] SourceColumns = ["source", "key", "id", "work_item_id", "work item id"];
    private static readonly string[] SourceTypeColumns = ["source_type", "source type", "type"];
    private static readonly string[] ComponentColumns = ["component", "area_path", "area path"];
    private static readonly string[] PriorityColumns = ["priority"];
    private static readonly string[] TagsColumns = ["tags", "labels"];

    /// <summary>
    /// Imports criteria from a CSV file. Throws InvalidOperationException if no text column found.
    /// </summary>
    public async Task<IReadOnlyList<AcceptanceCriterion>> ImportAsync(
        string filePath, string defaultSourceType = "manual", CancellationToken ct = default)
    {
        using var reader = new StreamReader(filePath);
        using var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            HeaderValidated = null,
            MissingFieldFound = null,
            PrepareHeaderForMatch = args => args.Header.ToLowerInvariant().Trim()
        });

        await csv.ReadAsync();
        csv.ReadHeader();
        var headers = csv.HeaderRecord?.Select(h => h.ToLowerInvariant().Trim()).ToArray() ?? [];

        var textCol = FindColumn(headers, TextColumns)
            ?? throw new InvalidOperationException(
                $"CSV file must contain a text column. Expected one of: {string.Join(", ", TextColumns)}. " +
                $"Found columns: {string.Join(", ", headers)}");

        var sourceCol = FindColumn(headers, SourceColumns);
        var sourceTypeCol = FindColumn(headers, SourceTypeColumns);
        var componentCol = FindColumn(headers, ComponentColumns);
        var priorityCol = FindColumn(headers, PriorityColumns);
        var tagsCol = FindColumn(headers, TagsColumns);

        var criteria = new List<AcceptanceCriterion>();

        while (await csv.ReadAsync())
        {
            var text = csv.GetField(textCol)?.Trim();
            if (string.IsNullOrWhiteSpace(text))
                continue;

            var criterion = new AcceptanceCriterion
            {
                Text = text,
                Source = sourceCol is not null ? csv.GetField(sourceCol)?.Trim() : null,
                SourceType = sourceTypeCol is not null
                    ? csv.GetField(sourceTypeCol)?.Trim() ?? defaultSourceType
                    : defaultSourceType,
                Component = componentCol is not null ? csv.GetField(componentCol)?.Trim() : null,
                Priority = priorityCol is not null
                    ? NormalizePriority(csv.GetField(priorityCol))
                    : "medium",
                Tags = tagsCol is not null
                    ? ParseTags(csv.GetField(tagsCol))
                    : []
            };

            criteria.Add(criterion);
        }

        return criteria;
    }

    private static string? FindColumn(string[] headers, string[] candidates)
    {
        foreach (var candidate in candidates)
        {
            var match = headers.FirstOrDefault(h => h == candidate);
            if (match is not null) return match;
        }
        return null;
    }

    private static string NormalizePriority(string? p) =>
        p?.Trim().ToLowerInvariant() switch
        {
            "high" or "critical" or "blocker" => "high",
            "low" or "trivial" or "minor" => "low",
            _ => "medium"
        };

    private static List<string> ParseTags(string? tags)
    {
        if (string.IsNullOrWhiteSpace(tags)) return [];
        return tags.Split([',', ';', '|'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(t => t.Length > 0)
            .ToList();
    }
}
