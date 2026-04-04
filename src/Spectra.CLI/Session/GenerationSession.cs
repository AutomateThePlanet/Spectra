using System.Text.Json.Serialization;

namespace Spectra.CLI.Session;

/// <summary>
/// Persisted generation session state.
/// </summary>
public sealed class GenerationSessionState
{
    [JsonPropertyName("session_id")]
    public required string SessionId { get; init; }

    [JsonPropertyName("suite")]
    public required string Suite { get; init; }

    [JsonPropertyName("started_at")]
    public DateTimeOffset StartedAt { get; init; }

    [JsonPropertyName("expires_at")]
    public DateTimeOffset ExpiresAt { get; init; }

    [JsonPropertyName("analysis")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public AnalysisSnapshot? Analysis { get; set; }

    [JsonPropertyName("generated")]
    public List<string> Generated { get; set; } = [];

    [JsonPropertyName("suggestions")]
    public List<SessionSuggestion> Suggestions { get; set; } = [];

    [JsonPropertyName("user_described")]
    public List<string> UserDescribed { get; set; } = [];

    public bool IsExpired => DateTimeOffset.UtcNow > ExpiresAt;
}

/// <summary>
/// Captured behavior analysis snapshot.
/// </summary>
public sealed class AnalysisSnapshot
{
    [JsonPropertyName("total_behaviors")]
    public int TotalBehaviors { get; init; }

    [JsonPropertyName("already_covered")]
    public int AlreadyCovered { get; init; }

    [JsonPropertyName("breakdown")]
    public Dictionary<string, int> Breakdown { get; init; } = new();
}

/// <summary>
/// A suggested test case from gap analysis.
/// </summary>
public sealed class SessionSuggestion
{
    [JsonPropertyName("index")]
    public int Index { get; init; }

    [JsonPropertyName("title")]
    public required string Title { get; init; }

    [JsonPropertyName("category")]
    public required string Category { get; init; }

    [JsonPropertyName("status")]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public SuggestionStatus Status { get; set; } = SuggestionStatus.Pending;
}

/// <summary>
/// Status of a session suggestion.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum SuggestionStatus
{
    Pending,
    Generated,
    Skipped
}
