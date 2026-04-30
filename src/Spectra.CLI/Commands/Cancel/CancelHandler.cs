using System.Diagnostics;
using System.Text.Json;
using Spectra.CLI.Cancellation;
using Spectra.CLI.Infrastructure;
using Spectra.CLI.Options;
using Spectra.CLI.Output;
using Spectra.CLI.Results;

namespace Spectra.CLI.Commands.Cancel;

/// <summary>
/// Spec 040: implements <c>spectra cancel</c>. State machine per
/// <c>data-model.md §9b</c>:
/// <list type="number">
///   <item>Read <c>.spectra/.pid</c>; absent → <c>no_active_run</c>.</item>
///   <item>Validate liveness + process-name; stale → clean up + <c>no_active_run</c>.</item>
///   <item>Write <c>.spectra/.cancel</c> sentinel.</item>
///   <item>Force? immediate kill. Otherwise poll PID for ≤ 5 s.</item>
///   <item>Still alive after grace? <c>Process.Kill(entireProcessTree: true)</c>, wait 2 s.</item>
///   <item>Clean up <c>.spectra/.pid</c> + <c>.spectra/.cancel</c>; emit <see cref="CancelResult"/>.</item>
/// </list>
/// </summary>
public sealed class CancelHandler
{
    private static readonly TimeSpan CooperativeGrace = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan KillFollowup = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(200);

    private readonly VerbosityLevel _verbosity;
    private readonly OutputFormat _outputFormat;

    public CancelHandler(VerbosityLevel verbosity = VerbosityLevel.Normal, OutputFormat outputFormat = OutputFormat.Human)
    {
        _verbosity = verbosity;
        _outputFormat = outputFormat;
    }

    public async Task<int> ExecuteAsync(bool force, CancellationToken ct = default)
    {
        var workspace = Directory.GetCurrentDirectory();
        var pidPath = Path.Combine(workspace, ".spectra", ".pid");
        var sentinelPath = Path.Combine(workspace, ".spectra", ".cancel");
        var pidManager = new PidFileManager(pidPath);

        var sw = Stopwatch.StartNew();

        var record = await pidManager.ReadAsync(ct).ConfigureAwait(false);
        if (record is null)
        {
            sw.Stop();
            return EmitNoActiveRun(sw.Elapsed.TotalSeconds, force, "No SPECTRA operation is currently running in this workspace.");
        }

        if (PidFileManager.IsStale(record))
        {
            pidManager.Delete();
            try { if (File.Exists(sentinelPath)) File.Delete(sentinelPath); } catch { /* ignore */ }
            sw.Stop();
            return EmitNoActiveRun(sw.Elapsed.TotalSeconds, force, "Stale PID file detected and cleaned up.");
        }

        // Write the sentinel file. The running command's SentinelWatcher
        // polls every 200 ms and triggers cooperative cancellation.
        try
        {
            File.WriteAllText(sentinelPath, "");
        }
        catch (IOException ex)
        {
            sw.Stop();
            return EmitFailure(record, sw.Elapsed.TotalSeconds, force, $"Failed to write sentinel: {ex.Message}");
        }

        Process? targetProcess = null;
        try
        {
            targetProcess = Process.GetProcessById(record.Pid);
        }
        catch (ArgumentException)
        {
            // Process exited between read and now — count as cooperative success.
            pidManager.Delete();
            try { if (File.Exists(sentinelPath)) File.Delete(sentinelPath); } catch { /* ignore */ }
            sw.Stop();
            return EmitSuccess(record, sw.Elapsed.TotalSeconds, force, "cooperative");
        }

        if (force)
        {
            return await ForceKillAsync(record, targetProcess, pidManager, sentinelPath, sw, force: true, ct).ConfigureAwait(false);
        }

        // Poll for cooperative shutdown
        var deadline = DateTimeOffset.UtcNow + CooperativeGrace;
        while (DateTimeOffset.UtcNow < deadline)
        {
            await Task.Delay(PollInterval, ct).ConfigureAwait(false);

            // Two signals of cooperative completion:
            //   - The PID file was removed (handler unregistered cleanly).
            //   - The process exited (best-effort check).
            if (!File.Exists(pidPath))
            {
                try { if (File.Exists(sentinelPath)) File.Delete(sentinelPath); } catch { /* ignore */ }
                sw.Stop();
                return EmitSuccess(record, sw.Elapsed.TotalSeconds, force: false, "cooperative");
            }

            if (HasExited(targetProcess))
            {
                pidManager.Delete();
                try { if (File.Exists(sentinelPath)) File.Delete(sentinelPath); } catch { /* ignore */ }
                sw.Stop();
                return EmitSuccess(record, sw.Elapsed.TotalSeconds, force: false, "cooperative");
            }
        }

        // Cooperative grace expired — escalate to force kill
        return await ForceKillAsync(record, targetProcess, pidManager, sentinelPath, sw, force: false, ct).ConfigureAwait(false);
    }

    private async Task<int> ForceKillAsync(
        PidFileManager.PidRecord record,
        Process targetProcess,
        PidFileManager pidManager,
        string sentinelPath,
        Stopwatch sw,
        bool force,
        CancellationToken ct)
    {
        try
        {
            targetProcess.Kill(entireProcessTree: true);
        }
        catch (Exception ex)
        {
            sw.Stop();
            return EmitFailure(record, sw.Elapsed.TotalSeconds, force, $"Failed to kill PID {record.Pid}: {ex.Message}");
        }

        // Wait briefly for the kill to take effect
        var deadline = DateTimeOffset.UtcNow + KillFollowup;
        while (DateTimeOffset.UtcNow < deadline && !HasExited(targetProcess))
        {
            await Task.Delay(PollInterval, ct).ConfigureAwait(false);
        }

        pidManager.Delete();
        try { if (File.Exists(sentinelPath)) File.Delete(sentinelPath); } catch { /* ignore */ }

        sw.Stop();
        if (HasExited(targetProcess))
        {
            return EmitSuccess(record, sw.Elapsed.TotalSeconds, force, "forced");
        }

        return EmitFailure(record, sw.Elapsed.TotalSeconds, force, "Kill issued but process did not exit within 2s");
    }

    private static bool HasExited(Process p)
    {
        try
        {
            p.Refresh();
            return p.HasExited;
        }
        catch
        {
            return true;
        }
    }

    private int EmitNoActiveRun(double elapsedSeconds, bool force, string message)
    {
        var result = new CancelResult
        {
            Command = "cancel",
            Status = "no_active_run",
            ShutdownPath = "none",
            ElapsedSeconds = Math.Round(elapsedSeconds, 2),
            Force = force,
            Message = message
        };
        EmitResult(result);
        if (_verbosity != VerbosityLevel.Quiet && _outputFormat == OutputFormat.Human)
        {
            Console.WriteLine(message);
        }
        return ExitCodes.Success;
    }

    private int EmitSuccess(PidFileManager.PidRecord record, double elapsedSeconds, bool force, string shutdownPath)
    {
        var result = new CancelResult
        {
            Command = "cancel",
            Status = "completed",
            TargetPid = record.Pid,
            TargetCommand = record.Command,
            ShutdownPath = shutdownPath,
            ElapsedSeconds = Math.Round(elapsedSeconds, 2),
            Force = force,
            Message = $"Cancelled {record.Command} (PID {record.Pid}) via {shutdownPath} shutdown in {elapsedSeconds:F2}s"
        };
        EmitResult(result);
        if (_verbosity != VerbosityLevel.Quiet && _outputFormat == OutputFormat.Human)
        {
            Console.WriteLine(result.Message);
        }
        return ExitCodes.Success;
    }

    private int EmitFailure(PidFileManager.PidRecord record, double elapsedSeconds, bool force, string message)
    {
        var result = new CancelResult
        {
            Command = "cancel",
            Status = "failed",
            TargetPid = record.Pid,
            TargetCommand = record.Command,
            ShutdownPath = "forced",
            ElapsedSeconds = Math.Round(elapsedSeconds, 2),
            Force = force,
            Message = message
        };
        EmitResult(result);
        if (_outputFormat == OutputFormat.Human)
        {
            Console.Error.WriteLine(message);
        }
        return ExitCodes.Error;
    }

    private void EmitResult(CommandResult result)
    {
        if (_outputFormat == OutputFormat.Json)
        {
            JsonResultWriter.Write(result);
        }
        // Always write to .spectra-result.json
        var resultPath = Path.Combine(Directory.GetCurrentDirectory(), ".spectra-result.json");
        try
        {
            using var fs = new FileStream(resultPath, FileMode.Create, FileAccess.Write, FileShare.None);
            using var writer = new StreamWriter(fs);
            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
            };
            writer.Write(JsonSerializer.Serialize(result, result.GetType(), options));
            writer.Flush();
            fs.Flush(true);
        }
        catch
        {
            // non-critical
        }
    }
}
