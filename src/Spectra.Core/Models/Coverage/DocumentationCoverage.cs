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

    /// <summary>
    /// Number of documents dropped from the coverage denominator by
    /// <c>coverage.coverage_exclude_patterns</c> (Spec 060). Zero when no
    /// coverage-scoped exclusions are configured; omitted from JSON in that case
    /// so unconfigured output is byte-for-byte unchanged.
    /// </summary>
    [JsonPropertyName("excluded_docs")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public int ExcludedDocs { get; init; }

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

    /// <summary>
    /// True when this document matched a <c>coverage.coverage_exclude_patterns</c>
    /// glob and was dropped from the coverage denominator (Spec 060). Takes
    /// precedence over <see cref="Covered"/> for status display. Omitted from
    /// JSON when false so unconfigured output is unchanged.
    /// </summary>
    [JsonPropertyName("excluded")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool Excluded { get; init; }

    /// <summary>
    /// The first coverage-exclude glob that matched this document (for
    /// auditability). Null when not excluded; omitted from JSON in that case.
    /// </summary>
    [JsonPropertyName("excluded_by_pattern")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public string? ExcludedByPattern { get; init; }
}
