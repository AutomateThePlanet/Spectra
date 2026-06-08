using System.Diagnostics;

namespace Spectra.CLI.Commands.Run.WebConsole;

/// <summary>
/// Spec 066: launch/serve/stop for <c>spectra run console</c>. The launcher spawns a DETACHED worker
/// process (<c>--serve</c>) so the console survives the launching terminal/agent session ending (FR-009)
/// and stops only on <c>--stop</c> (FR-010). A marker file (<see cref="ConsoleMarker"/>) records the
/// worker's pid/port/url for discovery and stop.
///
/// Detachment mechanism (research R2): the worker is started via the shell (<c>UseShellExecute=true</c>,
/// hidden window) so it does not inherit the launcher's stdio and runs independently. The launcher gets
/// the real worker pid for the marker. Survival across an agent-tracked job-object close is the
/// prototype-gated case (tasks T020); the breakaway-from-job fallback is documented in research.md.
/// </summary>
public sealed class ConsoleCommand
{
    public const int AlreadyRunningExitCode = 1;

    private readonly string _basePath;

    public ConsoleCommand(string? basePath = null) => _basePath = basePath ?? Directory.GetCurrentDirectory();

    /// <summary>Dispatch entry point for the <c>console</c> subcommand.</summary>
    public async Task<int> RunAsync(int port, bool stop, bool serve, CancellationToken ct)
    {
        if (stop) return await StopAsync();
        if (serve) return await ServeAsync(port, ct);
        return await LaunchAsync(port);
    }

    // ---- launch (detached) --------------------------------------------------

    public async Task<int> LaunchAsync(int requestedPort)
    {
        var existing = await ConsoleMarker.ReadAsync(_basePath);
        if (ConsoleMarker.IsLive(existing))
        {
            System.Console.WriteLine($"Console already running at {existing!.Url} (pid {existing.Pid}). Use `spectra run console --stop` first.");
            return AlreadyRunningExitCode;
        }
        // A stale marker (dead pid) is simply overwritten below.
        ConsoleMarker.Delete(_basePath);

        var port = requestedPort > 0 ? requestedPort : FreeTcpPort();
        var (fileName, argPrefix) = ResolveWorkerLauncher();
        var psi = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = $"{argPrefix}run console --serve --port {port}",
            UseShellExecute = true,         // detach from this process's stdio / console
            WindowStyle = ProcessWindowStyle.Hidden,
            WorkingDirectory = _basePath
        };

        Process? worker;
        try { worker = Process.Start(psi); }
        catch (Exception ex)
        {
            System.Console.Error.WriteLine($"Failed to launch console worker: {ex.Message}");
            return 2;
        }
        if (worker is null)
        {
            System.Console.Error.WriteLine("Failed to launch console worker.");
            return 2;
        }

        string startedUtc;
        try { startedUtc = worker.StartTime.ToUniversalTime().ToString("O"); }
        catch { startedUtc = DateTime.UtcNow.ToString("O"); }

        var url = $"http://127.0.0.1:{port}/";
        await ConsoleMarker.WriteAsync(_basePath, new ConsoleMarkerData(worker.Id, port, url, startedUtc, null));

        System.Console.WriteLine($"Console running at {url} (pid {worker.Id}).");
        System.Console.WriteLine("Open it in your browser. Stop with `spectra run console --stop`.");
        return 0;
    }

    // ---- stop ---------------------------------------------------------------

    public async Task<int> StopAsync()
    {
        var marker = await ConsoleMarker.ReadAsync(_basePath);
        if (!ConsoleMarker.IsLive(marker))
        {
            ConsoleMarker.Delete(_basePath); // clear any stale marker
            System.Console.WriteLine("No running console.");
            return 0;
        }
        try
        {
            using var proc = Process.GetProcessById(marker!.Pid);
            proc.Kill(entireProcessTree: true);
        }
        catch { /* already gone */ }
        ConsoleMarker.Delete(_basePath);
        System.Console.WriteLine("Console stopped.");
        return 0;
    }

    // ---- serve (foreground worker) -----------------------------------------

    public async Task<int> ServeAsync(int port, CancellationToken ct)
    {
        await using var server = new ConsoleServer(_basePath, port);
        await server.RunAsync(ct);
        return 0;
    }

    /// <summary>
    /// Resolves how to re-invoke this CLI for the detached worker. For the shipped dotnet-tool apphost
    /// (<c>spectra</c>), <see cref="Environment.ProcessPath"/> is the apphost and no prefix is needed.
    /// When hosted as <c>dotnet Spectra.CLI.dll</c> (dev), the worker must be launched as
    /// <c>dotnet "&lt;entry.dll&gt;" …</c>, so we prefix the managed entry assembly path.
    /// </summary>
    private static (string FileName, string ArgPrefix) ResolveWorkerLauncher()
    {
        var exe = Environment.ProcessPath ?? "spectra";
        var name = Path.GetFileNameWithoutExtension(exe);
        if (string.Equals(name, "dotnet", StringComparison.OrdinalIgnoreCase))
        {
            var entry = System.Reflection.Assembly.GetEntryAssembly()?.Location;
            if (!string.IsNullOrEmpty(entry))
                return (exe, $"\"{entry}\" ");
        }
        return (exe, "");
    }

    private static int FreeTcpPort()
    {
        var l = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, 0);
        l.Start();
        var p = ((System.Net.IPEndPoint)l.LocalEndpoint).Port;
        l.Stop();
        return p;
    }
}
