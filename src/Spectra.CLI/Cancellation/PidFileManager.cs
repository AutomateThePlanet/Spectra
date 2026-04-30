using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using Spectra.Core.Index;

namespace Spectra.CLI.Cancellation;

/// <summary>
/// Spec 040: writes, validates, and cleans <c>.spectra/.pid</c>. The PID
/// file announces that a long-running command is in flight in this
/// workspace so a peer <c>spectra cancel</c> invocation knows whom to signal.
/// </summary>
/// <remarks>
/// Stale-PID detection (Decision 11): a PID record is considered stale if
/// <see cref="Process.GetProcessById(int)"/> throws, or the live process's
/// name is not in the allow-list (<c>spectra</c>, <c>dotnet</c>,
/// <c>Spectra.CLI</c>). The dev-mode <c>dotnet</c> exception lets developers
/// run <c>dotnet run --project src/Spectra.CLI -- ai generate</c> without
/// false stale-PID hits.
/// </remarks>
public sealed class PidFileManager
{
    private static readonly HashSet<string> AllowedProcessNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "spectra",
        "spectra.cli",
        "dotnet"
    };

    private readonly string _pidPath;

    public PidFileManager(string pidPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pidPath);
        _pidPath = pidPath;
    }

    public string Path => _pidPath;

    public async Task WriteAsync(int pid, string command, string processName, CancellationToken ct = default)
    {
        var record = new PidRecord
        {
            Pid = pid,
            Command = command,
            StartedAt = DateTimeOffset.UtcNow.ToString("o"),
            ProcessName = processName
        };
        var json = JsonSerializer.Serialize(record, new JsonSerializerOptions
        {
            WriteIndented = true
        });
        await AtomicFileWriter.WriteAllTextAsync(_pidPath, json, ct).ConfigureAwait(false);
    }

    public async Task<PidRecord?> ReadAsync(CancellationToken ct = default)
    {
        if (!File.Exists(_pidPath))
        {
            return null;
        }
        try
        {
            var json = await File.ReadAllTextAsync(_pidPath, ct).ConfigureAwait(false);
            return JsonSerializer.Deserialize<PidRecord>(json);
        }
        catch (Exception ex) when (ex is JsonException or IOException)
        {
            return null;
        }
    }

    public void Delete()
    {
        try
        {
            if (File.Exists(_pidPath))
            {
                File.Delete(_pidPath);
            }
        }
        catch
        {
            // best-effort
        }
    }

    /// <summary>
    /// Returns true if the recorded PID is no longer a live SPECTRA process.
    /// </summary>
    public static bool IsStale(PidRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);

        try
        {
            var process = Process.GetProcessById(record.Pid);
            // Process exists — verify name allow-list to defend against PID reuse
            return !AllowedProcessNames.Contains(process.ProcessName);
        }
        catch (ArgumentException)
        {
            // No such PID
            return true;
        }
        catch (InvalidOperationException)
        {
            return true;
        }
    }

    public sealed class PidRecord
    {
        [JsonPropertyName("pid")]
        public int Pid { get; init; }

        [JsonPropertyName("command")]
        public string Command { get; init; } = string.Empty;

        [JsonPropertyName("started_at")]
        public string StartedAt { get; init; } = string.Empty;

        [JsonPropertyName("process_name")]
        public string ProcessName { get; init; } = string.Empty;
    }
}
