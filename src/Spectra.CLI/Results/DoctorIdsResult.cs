using System.Text.Json.Serialization;

namespace Spectra.CLI.Results;

public sealed class DoctorIdsResult : CommandResult
{
    [JsonPropertyName("fix_applied")]
    public bool FixApplied { get; init; }

    [JsonPropertyName("total_tests")]
    public int TotalTests { get; init; }

    [JsonPropertyName("unique_ids")]
    public int UniqueIds { get; init; }

    [JsonPropertyName("duplicates")]
    public IReadOnlyList<DuplicateIdGroup> Duplicates { get; init; } = Array.Empty<DuplicateIdGroup>();

    [JsonPropertyName("index_mismatches")]
    public IReadOnlyList<IndexMismatch> IndexMismatches { get; init; } = Array.Empty<IndexMismatch>();

    [JsonPropertyName("high_water_mark")]
    public int HighWaterMark { get; init; }

    [JsonPropertyName("next_id")]
    public required string NextId { get; init; }

    [JsonPropertyName("renumbered")]
    public IReadOnlyList<RenumberedTest> Renumbered { get; init; } = Array.Empty<RenumberedTest>();

    [JsonPropertyName("unfixable_references")]
    public IReadOnlyList<UnfixableReference> UnfixableReferences { get; init; } = Array.Empty<UnfixableReference>();
}

public sealed class DuplicateIdGroup
{
    [JsonPropertyName("id")]
    public required string Id { get; init; }

    [JsonPropertyName("occurrences")]
    public IReadOnlyList<DuplicateOccurrence> Occurrences { get; init; } = Array.Empty<DuplicateOccurrence>();
}

public sealed class DuplicateOccurrence
{
    [JsonPropertyName("file")]
    public required string File { get; init; }

    [JsonPropertyName("title")]
    public string? Title { get; init; }

    [JsonPropertyName("mtime")]
    public required string Mtime { get; init; }
}

public sealed class IndexMismatch
{
    [JsonPropertyName("suite")]
    public required string Suite { get; init; }

    [JsonPropertyName("id")]
    public required string Id { get; init; }

    [JsonPropertyName("in_index")]
    public bool InIndex { get; init; }

    [JsonPropertyName("on_disk")]
    public bool OnDisk { get; init; }
}

public sealed class RenumberedTest
{
    [JsonPropertyName("from")]
    public required string From { get; init; }

    [JsonPropertyName("to")]
    public required string To { get; init; }

    [JsonPropertyName("file")]
    public required string File { get; init; }

    [JsonPropertyName("now_at")]
    public required string NowAt { get; init; }
}

public sealed class UnfixableReference
{
    [JsonPropertyName("file")]
    public required string File { get; init; }

    [JsonPropertyName("reference")]
    public required string Reference { get; init; }

    [JsonPropertyName("reason")]
    public required string Reason { get; init; }
}
