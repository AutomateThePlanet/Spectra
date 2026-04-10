using System.Diagnostics;

namespace Spectra.CLI.Agent.Testimize;

/// <summary>
/// Spec 038: detects whether the Testimize.MCP.Server global .NET tool is
/// installed by shelling out to <c>dotnet tool list -g</c>. Used by the
/// `spectra init` flow to offer enabling Testimize when the tool is present.
/// Failures (no dotnet on PATH, timeout, parse error) silently return false.
/// </summary>
public static class TestimizeDetector
{
    private static readonly TimeSpan ProbeTimeout = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Returns true if the Testimize MCP server is installed as a global
    /// .NET tool. Never throws.
    /// </summary>
    public static async Task<bool> IsInstalledAsync(CancellationToken ct = default)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "dotnet",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            psi.ArgumentList.Add("tool");
            psi.ArgumentList.Add("list");
            psi.ArgumentList.Add("-g");

            using var proc = Process.Start(psi);
            if (proc is null) return false;

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(ProbeTimeout);

            var output = await proc.StandardOutput.ReadToEndAsync(cts.Token);
            await proc.WaitForExitAsync(cts.Token);

            return output.Contains("testimize.mcp.server", StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }
}
