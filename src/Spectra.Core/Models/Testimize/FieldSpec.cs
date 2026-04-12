namespace Spectra.Core.Models.Testimize;

/// <summary>
/// Structured description of a constrained input field extracted from
/// documentation. Produced by the behavior-analysis AI step (preferred) or
/// by the local FieldSpecAnalysisTools regex parser (fallback). Consumed by
/// TestimizeRunner to drive the Testimize engine's parameter builder.
/// </summary>
public sealed class FieldSpec
{
    public string Name { get; init; } = "";

    /// <summary>
    /// Field type key. Case-insensitive. Known values: Text, Integer, Date,
    /// Email, Phone, Password, Url, Username, Boolean, SingleSelect,
    /// MultiSelect. Unknown types are skipped by TestimizeRunner.
    /// </summary>
    public string Type { get; init; } = "Text";

    public bool Required { get; init; }

    public double? Min { get; init; }
    public double? Max { get; init; }

    public string? MinDate { get; init; }
    public string? MaxDate { get; init; }

    public int? MinLength { get; init; }
    public int? MaxLength { get; init; }

    public IReadOnlyList<string>? AllowedValues { get; init; }

    public string? ExpectedInvalidMessage { get; init; }
}
