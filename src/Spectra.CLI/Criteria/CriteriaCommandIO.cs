using System.Text.Json;
using System.Text.Json.Serialization;
using Spectra.Core.Config;
using Spectra.Core.Models.Config;

namespace Spectra.CLI.Criteria;

/// <summary>
/// Spec 079: shared plumbing for the criteria lifecycle commands (<c>docs retire</c>, <c>docs apply-move</c>,
/// <c>docs migrate-criteria</c>): config loading and the machine-readable failure shape.
/// </summary>
public static class CriteriaCommandIO
{
    public static readonly JsonSerializerOptions Json = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    /// <summary>
    /// Loads <c>spectra.config.json</c> for a criteria write command (Spec 080 A5: through
    /// <see cref="ConfigLoader.LoadForWriteAsync"/> — missing or invalid is an error); never throws.
    /// </summary>
    /// <param name="reads">Sections it only reads; a missing one gives defaults plus a warning.</param>
    public static Task<ConfigLoadResult> LoadConfigAsync(
        string repoRoot, ConfigSections reads, CancellationToken ct) =>
        ConfigLoader.LoadForWriteAsync(repoRoot, reads, ct);

    /// <summary>Failure output: JSON on stderr in JSON mode, a plain message otherwise. Never a human summary.</summary>
    public static void WriteFailure(
        bool json, string command, string? errorCode, string message,
        IReadOnlyList<string>? warnings = null, IReadOnlyDictionary<string, object?>? extras = null)
    {
        if (json)
        {
            WriteFailureJson(command, errorCode, [message], warnings, extras);
            return;
        }

        foreach (var warning in warnings ?? [])
        {
            Console.Error.WriteLine($"Warning: {warning}");
        }
        Console.Error.WriteLine(errorCode is null ? message : $"{message} [{errorCode}]");
    }

    /// <summary>
    /// Spec 081 C4: the one JSON failure shape for every criteria command — <c>command</c>, <c>success</c>,
    /// <c>error_code</c>, <c>errors</c> and <c>warnings</c> (always an array), then any command-specific
    /// <paramref name="extras"/> (<c>outcome</c> on ingest, <c>missing_input</c> on compile refusals). One document on
    /// stderr, in a fixed field order.
    /// </summary>
    public static void WriteFailureJson(
        string command, string? errorCode, IReadOnlyList<string> errors,
        IReadOnlyList<string>? warnings = null, IReadOnlyDictionary<string, object?>? extras = null)
    {
        var document = new Dictionary<string, object?>
        {
            ["command"] = command,
            ["success"] = false,
            ["error_code"] = errorCode,
            ["errors"] = errors,
            ["warnings"] = warnings ?? []
        };
        foreach (var (key, value) in extras ?? new Dictionary<string, object?>())
        {
            document[key] = value;
        }

        Console.Error.WriteLine(JsonSerializer.Serialize(document, Json));
    }
}
