using System.Text.Json.Serialization;

namespace Spectra.Core.Models.Coverage;

/// <summary>
/// Documentation coverage: how many docs have linked tests.
/// </summary>
public sealed class DocumentationCoverage
{
    [JsonPropertyName("total_docs")]
    public required int TotalDocs { get; init; }

    [JsonPropertyName("covered_docs")]
    public required int CoveredDocs { get; init; }

    [JsonPropertyName("percentage")]
    public required decimal Percentage { get; init; }

    [JsonPropertyName("undocumented_test_count")]
    public int UndocumentedTestCount { get; init; }

    [JsonPropertyName("undocumented_test_ids")]
    public IReadOnlyList<string> UndocumentedTestIds { get; init; } = [];

    [JsonPropertyName("details")]
    public IReadOnlyList<DocumentCoverageDetail> Details { get; init; } = [];
}

/// <summary>
/// Coverage detail for a single document.
/// </summary>
public sealed class DocumentCoverageDetail
{
    [JsonPropertyName("doc")]
    public required string Doc { get; init; }

    [JsonPropertyName("test_count")]
    public required int TestCount { get; init; }

    [JsonPropertyName("covered")]
    public required bool Covered { get; init; }

    [JsonPropertyName("test_ids")]
    public IReadOnlyList<string> TestIds { get; init; } = [];
}
