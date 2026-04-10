using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Spectra.Core.Models.Config;

namespace Spectra.CLI.Agent.Testimize;

/// <summary>
/// Wraps the Testimize MCP server child process and exposes a minimal
/// JSON-RPC tool-call interface to the rest of SPECTRA.
///
/// Spec 038: every failure path (process not found, crash, timeout, malformed
/// JSON) maps to a single behavior — log nothing fatally, return false/null,
/// let the caller fall back to AI-only generation. The class never throws.
/// </summary>
public sealed class TestimizeMcpClient : IAsyncDisposable
{
    private static readonly TimeSpan HealthTimeout = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan CallTimeout = TimeSpan.FromSeconds(30);
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private Process? _process;
    private int _nextRequestId = 1;
    private readonly SemaphoreSlim _writeLock = new(1, 1);

    /// <summary>
    /// Starts the Testimize MCP server in stdio mode. Returns false (without
    /// throwing) if the executable is not found, fails to start, or exits
    /// immediately. Callers should treat false as "Testimize unavailable" and
    /// continue with AI-only generation.
    /// </summary>
    public Task<bool> StartAsync(TestimizeMcpConfig config, CancellationToken ct = default)
    {
        if (_process is not null)
            return Task.FromResult(true);

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = config.Command,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            foreach (var arg in config.Args)
                psi.ArgumentList.Add(arg);

            var proc = Process.Start(psi);
            if (proc is null)
                return Task.FromResult(false);

            // Give the process a brief moment; if it dies immediately, treat as
            // "not installed" without throwing.
            if (proc.WaitForExit(150) && proc.ExitCode != 0)
            {
                proc.Dispose();
                return Task.FromResult(false);
            }

            _process = proc;
            return Task.FromResult(true);
        }
        catch
        {
            // Win32Exception (file not found), UnauthorizedAccess, etc. — all
            // collapse to "not available".
            return Task.FromResult(false);
        }
    }

    /// <summary>
    /// Probes the server with a lightweight tool-list call. Returns false on
    /// any failure (process gone, timeout, parse error). 5-second budget.
    /// </summary>
    public async Task<bool> IsHealthyAsync(CancellationToken ct = default)
    {
        if (_process is null || _process.HasExited)
            return false;

        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(HealthTimeout);

            var response = await SendRequestAsync(
                method: "tools/list",
                parameters: null,
                cts.Token);

            return response is not null;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Calls a Testimize MCP tool by name. Returns null on any failure
    /// (process crashed, timeout, malformed JSON, server error).
    /// 30-second per-call budget.
    /// </summary>
    public async Task<JsonElement?> CallToolAsync(
        string toolName,
        JsonElement parameters,
        CancellationToken ct = default)
    {
        if (_process is null || _process.HasExited)
            return null;

        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(CallTimeout);

            var paramObj = new Dictionary<string, object?>
            {
                ["name"] = toolName,
                ["arguments"] = parameters
            };
            var paramJson = JsonSerializer.SerializeToElement(paramObj, JsonOptions);

            var response = await SendRequestAsync(
                method: "tools/call",
                parameters: paramJson,
                cts.Token);

            return response;
        }
        catch
        {
            return null;
        }
    }

    private async Task<JsonElement?> SendRequestAsync(
        string method,
        JsonElement? parameters,
        CancellationToken ct)
    {
        if (_process is null || _process.HasExited)
            return null;

        var id = Interlocked.Increment(ref _nextRequestId);
        var request = new Dictionary<string, object?>
        {
            ["jsonrpc"] = "2.0",
            ["id"] = id,
            ["method"] = method
        };
        if (parameters.HasValue)
            request["params"] = parameters.Value;

        var requestJson = JsonSerializer.Serialize(request, JsonOptions);

        await _writeLock.WaitAsync(ct);
        try
        {
            // MCP stdio framing: Content-Length header + blank line + JSON body.
            var bytes = Encoding.UTF8.GetBytes(requestJson);
            var header = $"Content-Length: {bytes.Length}\r\n\r\n";
            var stdin = _process.StandardInput.BaseStream;
            await stdin.WriteAsync(Encoding.ASCII.GetBytes(header), ct);
            await stdin.WriteAsync(bytes, ct);
            await stdin.FlushAsync(ct);
        }
        finally
        {
            _writeLock.Release();
        }

        // Read response (single-shot — this client is single-threaded by design).
        var stdout = _process.StandardOutput;
        var headerLine = await stdout.ReadLineAsync(ct);
        if (headerLine is null)
            return null;

        if (!headerLine.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase))
            return null;

        if (!int.TryParse(headerLine["Content-Length:".Length..].Trim(), out var contentLength))
            return null;

        // Consume the blank separator line.
        _ = await stdout.ReadLineAsync(ct);

        var buffer = new char[contentLength];
        var read = 0;
        while (read < contentLength)
        {
            var n = await stdout.ReadAsync(buffer.AsMemory(read, contentLength - read), ct);
            if (n == 0) return null;
            read += n;
        }

        try
        {
            using var doc = JsonDocument.Parse(new string(buffer, 0, read));
            // Standard JSON-RPC: {"jsonrpc":"2.0","id":N,"result":{...}}
            if (doc.RootElement.TryGetProperty("result", out var result))
                return result.Clone();
            return null;
        }
        catch
        {
            return null;
        }
    }

    public async ValueTask DisposeAsync()
    {
        var proc = Interlocked.Exchange(ref _process, null);
        if (proc is null)
            return;

        try
        {
            if (!proc.HasExited)
            {
                try { proc.Kill(entireProcessTree: true); } catch { /* best effort */ }
            }
            await Task.Run(() => proc.WaitForExit(2000));
        }
        catch
        {
            // Ignore — disposal must never throw.
        }
        finally
        {
            try { proc.Dispose(); } catch { }
            _writeLock.Dispose();
        }
    }
}
