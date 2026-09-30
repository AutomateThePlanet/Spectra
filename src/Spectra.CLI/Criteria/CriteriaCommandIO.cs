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
    public static async Task<(SpectraConfig? Config, string? Error)> LoadConfigAsync(string repoRoot, CancellationToken ct)
    {
        var loaded = await ConfigLoader.LoadForWriteAsync(repoRoot, ct);
        return (loaded.Config, loaded.Error);
    }

    /// <summary>Failure output: JSON on stderr in JSON mode, a plain message otherwise. Never a human summary.</summary>
    public static void WriteFailure(bool json, string command, string? errorCode, string message)
    {
        if (json)
        {
            Console.Error.WriteLine(JsonSerializer.Serialize(new
            {
                command,
                success = false,
                error_code = errorCode,
                errors = new[] { message }
            }, Json));
        }
        else
        {
            Console.Error.WriteLine(errorCode is null ? message : $"{message} [{errorCode}]");
        }
    }
}
