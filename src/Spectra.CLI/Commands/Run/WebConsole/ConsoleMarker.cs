using System.Diagnostics;
using System.Text.Json;

namespace Spectra.CLI.Commands.Run.WebConsole;

/// <summary>
/// Spec 066: the ephemeral, local-only marker (<c>.execution/console.json</c>) written at console launch
/// so a running console can be discovered and explicitly stopped (FR-010), and so a stale launch (a pid
/// reused by an unrelated process) is detectable (FR-012). NOT persisted to SQLite — it is
/// process-coordination metadata, not run state, which keeps the "SQLite = run source of truth" invariant.
/// </summary>
public sealed record ConsoleMarkerData(int Pid, int Port, string Url, string StartedUtc, string? RunId);

public static class ConsoleMarker
{
    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = true };

    public static string MarkerPath(string basePath) => Path.Combine(basePath, ".execution", "console.json");

    public static async Task WriteAsync(string basePath, ConsoleMarkerData data)
    {
        var path = MarkerPath(basePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllTextAsync(path, JsonSerializer.Serialize(data, JsonOpts));
    }

    public static async Task<ConsoleMarkerData?> ReadAsync(string basePath)
    {
        var path = MarkerPath(basePath);
        if (!File.Exists(path)) return null;
        try { return JsonSerializer.Deserialize<ConsoleMarkerData>(await File.ReadAllTextAsync(path), JsonOpts); }
        catch { return null; }
    }

    public static void Delete(string basePath)
    {
        try { var path = MarkerPath(basePath); if (File.Exists(path)) File.Delete(path); }
        catch { /* best-effort cleanup */ }
    }

    /// <summary>
    /// A marker is "live" only if its pid is a running process whose start time is consistent with the
    /// recorded <see cref="ConsoleMarkerData.StartedUtc"/>. Pid-reuse (a dead worker whose pid was handed
    /// to an unrelated process) is rejected by the start-time check, so a stale marker reads as absent.
    /// </summary>
    public static bool IsLive(ConsoleMarkerData? marker)
    {
        if (marker is null) return false;
        try
        {
            using var proc = Process.GetProcessById(marker.Pid);
            if (proc.HasExited) return false;
            // Start-time consistency guards against pid reuse. If the start time is unreadable
            // (access denied on some processes), fall back to pid-existence alone.
            try
            {
                if (DateTime.TryParse(marker.StartedUtc, out var recorded))
                {
                    var actual = proc.StartTime.ToUniversalTime();
                    if (Math.Abs((actual - recorded.ToUniversalTime()).TotalSeconds) > 5) return false;
                }
            }
            catch { /* start time unreadable — accept pid existence */ }
            return true;
        }
        catch
        {
            // No such process → stale/absent.
            return false;
        }
    }
}
