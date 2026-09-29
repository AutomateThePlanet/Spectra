using System.Text.Json;
using System.Text.Json.Serialization;
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

    /// <summary>Loads <c>spectra.config.json</c>; returns an error message instead of throwing.</summary>
    public static async Task<(SpectraConfig? Config, string? Error)> LoadConfigAsync(string repoRoot, CancellationToken ct)
    {
        var configPath = Path.Combine(repoRoot, "spectra.config.json");
        if (!File.Exists(configPath))
        {
            return (null, "No spectra.config.json found. Run 'spectra init' first.");
        }

        try
        {
            var config = JsonSerializer.Deserialize<SpectraConfig>(await File.ReadAllTextAsync(configPath, ct),
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            return config is null ? (null, "Could not parse spectra.config.json.") : (config, null);
        }
        catch (JsonException ex)
        {
            return (null, $"Error reading config: {ex.Message}");
        }
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
