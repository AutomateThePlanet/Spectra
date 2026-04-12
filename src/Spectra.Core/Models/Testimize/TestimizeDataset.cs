namespace Spectra.Core.Models.Testimize;

/// <summary>
/// Result of a single per-suite TestimizeRunner invocation. Projected into
/// the test-generation prompt as a literal, authoritative data block — the
/// AI no longer calls Testimize as a tool; it just uses the values it gets.
/// </summary>
public sealed class TestimizeDataset
{
    /// <summary>Human-readable strategy name, e.g. "HybridArtificialBeeColony".</summary>
    public string Strategy { get; init; } = "";

    public int FieldCount { get; init; }

    public IReadOnlyList<FieldSpec> Fields { get; init; } = [];

    public IReadOnlyList<TestimizeRow> TestCases { get; init; } = [];
}

public sealed class TestimizeRow
{
    public IReadOnlyList<TestimizeCell> Values { get; init; } = [];

    /// <summary>
    /// Fitness score from the ABC algorithm (0.0–1.0). Zero for
    /// Pairwise/Combinatorial strategies which don't emit scores.
    /// </summary>
    public double Score { get; init; }
}

public sealed class TestimizeCell
{
    public string FieldName { get; init; } = "";

    public object? Value { get; init; }

    /// <summary>One of: Valid, BoundaryValid, BoundaryInvalid, Invalid.</summary>
    public string Category { get; init; } = "Valid";

    public string? ExpectedInvalidMessage { get; init; }
}
