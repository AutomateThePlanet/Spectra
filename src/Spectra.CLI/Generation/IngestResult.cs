using Spectra.Core.Models;

namespace Spectra.CLI.Generation;

/// <summary>
/// Machine-readable failure classes for <see cref="GeneratedTestIngestor"/>. These are
/// the contract a retry skill (Spec 055) keys on — each code, plus the specific
/// <see cref="IngestResult.Errors"/>, is what the skill feeds back to the agent to
/// regenerate against (Spec 053 FR-006/FR-007).
/// </summary>
public static class IngestErrorCode
{
    /// <summary>Content was null/whitespace, or contained no JSON array at all.</summary>
    public const string EmptyContent = "EMPTY_CONTENT";

    /// <summary>Extracted text did not parse as a JSON array.</summary>
    public const string MalformedJson = "MALFORMED_JSON";

    /// <summary>A JSON array opened but never closed (token-limit cut-off). No salvage.</summary>
    public const string Truncated = "TRUNCATED";

    /// <summary>A well-formed array was parsed but contained zero valid test objects.</summary>
    public const string NoTests = "NO_TESTS";

    /// <summary>One or more parsed tests failed schema validation.</summary>
    public const string SchemaInvalid = "SCHEMA_INVALID";
}

/// <summary>
/// Outcome of ingesting agent-generated content at the fail-loud boundary
/// (Spec 053 FR-005/FR-006). On failure, nothing is persisted and
/// <see cref="ErrorCode"/> + <see cref="Errors"/> describe the problem specifically
/// enough for a skill to instruct a regeneration.
/// </summary>
public sealed record IngestResult
{
    /// <summary>True when content parsed, validated, and persisted.</summary>
    public bool IsSuccess { get; private init; }

    /// <summary>Tests written to disk. Populated only on success; empty on failure.</summary>
    public IReadOnlyList<TestCase> PersistedTests { get; private init; } = [];

    /// <summary>One of <see cref="IngestErrorCode"/>. Non-null only on failure.</summary>
    public string? ErrorCode { get; private init; }

    /// <summary>Specific, actionable messages. The retry payload for a skill.</summary>
    public IReadOnlyList<string> Errors { get; private init; } = [];

    private IngestResult() { }

    /// <summary>Creates a successful result listing the persisted tests.</summary>
    public static IngestResult Success(IReadOnlyList<TestCase> persisted) => new()
    {
        IsSuccess = true,
        PersistedTests = persisted ?? []
    };

    /// <summary>Creates a fail-loud result. Persists nothing.</summary>
    public static IngestResult Failure(string errorCode, params string[] errors) => new()
    {
        IsSuccess = false,
        ErrorCode = errorCode,
        Errors = errors ?? []
    };

    /// <summary>Creates a fail-loud result from a message list.</summary>
    public static IngestResult Failure(string errorCode, IReadOnlyList<string> errors) => new()
    {
        IsSuccess = false,
        ErrorCode = errorCode,
        Errors = errors ?? []
    };
}
