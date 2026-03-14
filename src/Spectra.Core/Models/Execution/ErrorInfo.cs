using System.Text.Json.Serialization;

namespace Spectra.Core.Models.Execution;

/// <summary>
/// Error details for MCP tool responses.
/// </summary>
public sealed record ErrorInfo
{
    /// <summary>Error code identifier (e.g., INVALID_SUITE, RUN_NOT_FOUND).</summary>
    [JsonPropertyName("code")]
    public required string Code { get; init; }

    /// <summary>Human-readable error message.</summary>
    [JsonPropertyName("message")]
    public required string Message { get; init; }
}
