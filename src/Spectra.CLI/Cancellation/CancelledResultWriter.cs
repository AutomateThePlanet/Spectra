using System.Text.Json;
using System.Text.Json.Serialization;
using Spectra.CLI.Results;

namespace Spectra.CLI.Cancellation;

/// <summary>
/// Spec 040: shared helper for long-running handlers to write a cancelled
/// status to <c>.spectra-result.json</c> when an
/// <see cref="OperationCanceledException"/> propagates out of their main
/// pipeline. Keeps the handler-side cancellation glue minimal.
/// </summary>
public static class CancelledResultWriter
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    /// <summary>
    /// Writes a minimal cancelled-status result to <c>.spectra-result.json</c>
    /// in the current working directory. Used by handlers that don't carry
    /// rich progress state into their cancel path; richer handlers can
    /// build a typed cancelled result themselves.
    /// </summary>
    public static void WriteMinimal(string command, string phase, int? testsWritten = null)
    {
        var result = new CancelledMinimalResult
        {
            Command = command,
            Status = "cancelled",
            CancelledAt = DateTimeOffset.UtcNow.ToString("o"),
            Phase = phase,
            TestsWritten = testsWritten,
            Message = testsWritten.HasValue
                ? $"{command} cancelled by user. {testsWritten} artifact(s) written before stopping."
                : $"{command} cancelled by user during {phase} phase."
        };

        var path = Path.Combine(Directory.GetCurrentDirectory(), ".spectra-result.json");
        try
        {
            using var fs = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None);
            using var writer = new StreamWriter(fs);
            writer.Write(JsonSerializer.Serialize(result, result.GetType(), Options));
            writer.Flush();
            fs.Flush(true);
        }
        catch
        {
            // non-critical
        }

        // Also try to update the progress page to terminal Cancelled state
        try
        {
            var progressPath = Path.Combine(Directory.GetCurrentDirectory(), ".spectra-progress.html");
            if (File.Exists(progressPath))
            {
                var json = JsonSerializer.Serialize(result, result.GetType(), Options);
                Spectra.CLI.Progress.ProgressPageWriter.WriteProgressPage(progressPath, json, isTerminal: true);
            }
        }
        catch
        {
            // non-critical
        }
    }

    private sealed class CancelledMinimalResult : CommandResult
    {
        [JsonPropertyName("cancelled_at")]
        public string CancelledAt { get; init; } = string.Empty;

        [JsonPropertyName("phase")]
        public string Phase { get; init; } = string.Empty;

        [JsonPropertyName("tests_written")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public int? TestsWritten { get; init; }
    }
}
