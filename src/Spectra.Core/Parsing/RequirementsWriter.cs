using System.Text.RegularExpressions;
using Spectra.Core.Models.Coverage;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Spectra.Core.Parsing;

/// <summary>
/// Writes and merges requirements into _requirements.yaml files.
/// Handles duplicate detection, sequential ID allocation, and atomic writes.
/// </summary>
public sealed class RequirementsWriter
{
    private static readonly ISerializer Serializer = new SerializerBuilder()
        .WithNamingConvention(UnderscoredNamingConvention.Instance)
        .ConfigureDefaultValuesHandling(DefaultValuesHandling.OmitNull)
        .Build();

    private static readonly Regex IdPattern = new(@"^REQ-(\d+)$", RegexOptions.Compiled);

    private const int MinSubstringLength = 15;

    /// <summary>
    /// Merges new requirements into an existing file. Creates the file if it doesn't exist.
    /// Returns an ExtractionResult describing what was done.
    /// </summary>
    public async Task<ExtractionResult> MergeAndWriteAsync(
        string filePath,
        IReadOnlyList<RequirementDefinition> newRequirements,
        CancellationToken ct = default)
    {
        // Load existing requirements
        var parser = new RequirementsParser();
        var existing = await parser.ParseAsync(filePath, ct);

        // Detect duplicates and allocate IDs
        var result = DetectDuplicates(existing, newRequirements);

        if (result.Merged.Count == 0)
        {
            return result with { TotalInFile = existing.Count };
        }

        // Allocate IDs for new requirements
        var withIds = AllocateIds(existing, result.Merged);

        // Combine existing + new
        var all = existing.Concat(withIds).ToList();

        // Write atomically
        await WriteAtomicAsync(filePath, all, ct);

        return result with { Merged = withIds, TotalInFile = all.Count };
    }

    /// <summary>
    /// Detects duplicates between existing requirements and candidates.
    /// </summary>
    public ExtractionResult DetectDuplicates(
        IReadOnlyList<RequirementDefinition> existing,
        IReadOnlyList<RequirementDefinition> candidates)
    {
        var merged = new List<RequirementDefinition>();
        var duplicates = new List<DuplicateMatch>();

        foreach (var candidate in candidates)
        {
            if (string.IsNullOrWhiteSpace(candidate.Title))
                continue;

            var normalizedNew = Normalize(candidate.Title);
            var match = FindDuplicate(existing, normalizedNew);

            if (match is not null)
            {
                duplicates.Add(new DuplicateMatch
                {
                    NewTitle = candidate.Title,
                    ExistingId = match.Id,
                    ExistingTitle = match.Title,
                    Source = candidate.Source ?? ""
                });
            }
            else
            {
                // Also check against already-merged items in this batch
                var batchMatch = FindDuplicate(merged, normalizedNew);
                if (batchMatch is not null)
                {
                    duplicates.Add(new DuplicateMatch
                    {
                        NewTitle = candidate.Title,
                        ExistingId = batchMatch.Id,
                        ExistingTitle = batchMatch.Title,
                        Source = candidate.Source ?? ""
                    });
                }
                else
                {
                    merged.Add(candidate);
                }
            }
        }

        return new ExtractionResult
        {
            Extracted = candidates,
            Merged = merged,
            Duplicates = duplicates,
            SkippedCount = duplicates.Count
        };
    }

    /// <summary>
    /// Allocates sequential IDs (REQ-NNN) to new requirements, continuing from the highest existing ID.
    /// </summary>
    public IReadOnlyList<RequirementDefinition> AllocateIds(
        IReadOnlyList<RequirementDefinition> existing,
        IReadOnlyList<RequirementDefinition> newItems)
    {
        var maxId = GetMaxId(existing);

        return newItems.Select((item, i) =>
        {
            var newId = maxId + 1 + i;
            return new RequirementDefinition
            {
                Id = $"REQ-{newId:D3}",
                Title = item.Title,
                Source = item.Source,
                Priority = item.Priority ?? "medium"
            };
        }).ToList();
    }

    private static int GetMaxId(IReadOnlyList<RequirementDefinition> existing)
    {
        var max = 0;
        foreach (var req in existing)
        {
            var match = IdPattern.Match(req.Id);
            if (match.Success && int.TryParse(match.Groups[1].Value, out var num) && num > max)
            {
                max = num;
            }
        }
        return max;
    }

    private static RequirementDefinition? FindDuplicate(
        IReadOnlyList<RequirementDefinition> existing,
        string normalizedCandidate)
    {
        foreach (var req in existing)
        {
            var normalizedExisting = Normalize(req.Title);

            // Exact match (case-insensitive, normalized)
            if (normalizedExisting == normalizedCandidate)
                return req;

            // Substring match (both directions, if long enough)
            if (normalizedCandidate.Length >= MinSubstringLength &&
                normalizedExisting.Length >= MinSubstringLength)
            {
                if (normalizedExisting.Contains(normalizedCandidate) ||
                    normalizedCandidate.Contains(normalizedExisting))
                    return req;
            }
        }

        return null;
    }

    private static string Normalize(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return "";

        // Lowercase, trim, remove punctuation
        var normalized = text.ToLowerInvariant().Trim();
        normalized = Regex.Replace(normalized, @"[^\w\s]", "");
        normalized = Regex.Replace(normalized, @"\s+", " ");
        return normalized;
    }

    private static async Task WriteAtomicAsync(
        string filePath,
        IReadOnlyList<RequirementDefinition> requirements,
        CancellationToken ct)
    {
        // Ensure directory exists
        var dir = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }

        var doc = new RequirementsDocument
        {
            Requirements = requirements.ToList()
        };

        var yaml = Serializer.Serialize(doc);

        // Atomic write: temp file then rename
        var tempPath = filePath + ".tmp";
        await File.WriteAllTextAsync(tempPath, yaml, ct);

        // Rename (atomic on most file systems)
        File.Move(tempPath, filePath, overwrite: true);
    }
}
