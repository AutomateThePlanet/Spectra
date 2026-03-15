using System.Text.Json.Serialization;

namespace Spectra.Core.Models.Coverage;

/// <summary>
/// Health status of a coverage link.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum LinkStatus
{
    /// <summary>Both endpoints exist and are consistent.</summary>
    Valid,

    /// <summary>Target doesn't exist.</summary>
    Broken,

    /// <summary>Link only exists in one direction.</summary>
    Mismatch,

    /// <summary>Source has no corresponding target.</summary>
    Orphaned
}
