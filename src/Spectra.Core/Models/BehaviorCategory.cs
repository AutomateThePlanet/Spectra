using System.Text.Json.Serialization;

namespace Spectra.Core.Models;

/// <summary>
/// Categories of testable behaviors identified from documentation analysis.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<BehaviorCategory>))]
public enum BehaviorCategory
{
    HappyPath,
    Negative,
    EdgeCase,
    Security,
    Performance
}
