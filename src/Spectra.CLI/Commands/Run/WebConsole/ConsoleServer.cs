using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace Spectra.CLI.Commands.Run.WebConsole;

/// <summary>
/// Spec 066: the console's HTTP transport — a minimal <see cref="HttpListener"/> host bound to
/// localhost that constructs ONE long-lived <see cref="RunServices"/> (FR-001) and dispatches each
/// request to <see cref="ConsoleEndpoints"/>. It owns no run logic and holds no run state; it only
/// parses HTTP into endpoint calls and serializes <see cref="ConsoleResponse"/> back out. Not a route on
/// the stdio MCP host (FR-002) — the console brings its own transport (research R1).
/// </summary>
public sealed class ConsoleServer : IAsyncDisposable
{
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    private readonly RunServices _services;
    private readonly ConsoleEndpoints _endpoints;
    private readonly HttpListener _listener = new();

    public int Port { get; private set; }
    public string Url => $"http://127.0.0.1:{Port}/";

    public ConsoleServer(string? basePath = null, int port = 0)
    {
        _services = new RunServices(basePath);
        _endpoints = new ConsoleEndpoints(_services);
        Port = port > 0 ? port : FreeTcpPort();
        _listener.Prefixes.Add($"http://127.0.0.1:{Port}/");
    }

    /// <summary>Binds the port (falling back to a free one if the requested port is taken) and serves until cancelled.</summary>
    public async Task RunAsync(CancellationToken ct)
    {
        try { _listener.Start(); }
        catch (HttpListenerException)
        {
            // Requested port unavailable — rebind to a free ephemeral port (FR: port fallback).
            _listener.Prefixes.Clear();
            Port = FreeTcpPort();
            _listener.Prefixes.Add($"http://127.0.0.1:{Port}/");
            _listener.Start();
        }

        using var reg = ct.Register(() => { try { _listener.Stop(); } catch { } });
        while (!ct.IsCancellationRequested)
        {
            HttpListenerContext ctx;
            try { ctx = await _listener.GetContextAsync(); }
            catch { break; } // listener stopped
            _ = HandleAsync(ctx); // fire-and-forget per request; single local user
        }
    }

    private async Task HandleAsync(HttpListenerContext ctx)
    {
        try
        {
            var path = ctx.Request.Url?.AbsolutePath ?? "/";
            var method = ctx.Request.HttpMethod;

            if (method == "GET" && path == "/") { await WriteHtmlAsync(ctx, ConsolePage.Render()); return; }
            if (method == "GET" && path.StartsWith("/reports/")) { await ServeReportFileAsync(ctx, path["/reports/".Length..]); return; }

            ConsoleResponse resp;
            if (method == "GET" && path == "/current") resp = await _endpoints.GetCurrentAsync();
            else if (method == "POST" && path == "/advance") { var b = await ReadJsonAsync(ctx); resp = await _endpoints.AdvanceAsync(Str(b, "status"), Str(b, "notes")); }
            else if (method == "POST" && path == "/note") { var b = await ReadJsonAsync(ctx); resp = await _endpoints.NoteAsync(Str(b, "note")); }
            else if (method == "POST" && path == "/finalize") { var b = await ReadJsonAsync(ctx); resp = await _endpoints.FinalizeAsync(Bool(b, "force")); }
            else if (method == "POST" && path == "/screenshot") resp = await _endpoints.ScreenshotAsync(await ReadImageBytesAsync(ctx));
            else if (method == "POST" && path == "/retest") { var b = await ReadJsonAsync(ctx); resp = await _endpoints.RetestAsync(Str(b, "testId")); }
            else resp = new ConsoleResponse(404, new ConsoleError("NOT_FOUND", $"No route for {method} {path}."));

            await WriteJsonAsync(ctx, resp.StatusCode, resp.Body);
        }
        catch (Exception ex)
        {
            try { await WriteJsonAsync(ctx, 500, new ConsoleError("INTERNAL_ERROR", ex.Message)); } catch { }
        }
    }

    // ---- request parsing ----------------------------------------------------

    private static async Task<JsonElement> ReadJsonAsync(HttpListenerContext ctx)
    {
        using var reader = new StreamReader(ctx.Request.InputStream, ctx.Request.ContentEncoding ?? Encoding.UTF8);
        var body = await reader.ReadToEndAsync();
        if (string.IsNullOrWhiteSpace(body)) return default;
        try { return JsonDocument.Parse(body).RootElement.Clone(); } catch { return default; }
    }

    /// <summary>Decodes a screenshot upload: JSON <c>{ dataUrl }</c> (base64 data-URL) or raw body bytes.</summary>
    private static async Task<byte[]?> ReadImageBytesAsync(HttpListenerContext ctx)
    {
        var contentType = ctx.Request.ContentType ?? "";
        if (contentType.Contains("application/json", StringComparison.OrdinalIgnoreCase))
        {
            var b = await ReadJsonAsync(ctx);
            var dataUrl = Str(b, "dataUrl");
            if (string.IsNullOrEmpty(dataUrl)) return null;
            var comma = dataUrl.IndexOf(',');
            var b64 = comma >= 0 ? dataUrl[(comma + 1)..] : dataUrl;
            try { return Convert.FromBase64String(b64); } catch { return null; }
        }
        using var ms = new MemoryStream();
        await ctx.Request.InputStream.CopyToAsync(ms);
        return ms.ToArray();
    }

    private static string? Str(JsonElement el, string name) =>
        el.ValueKind == JsonValueKind.Object && el.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    private static bool Bool(JsonElement el, string name) =>
        el.ValueKind == JsonValueKind.Object && el.TryGetProperty(name, out var v) && (v.ValueKind == JsonValueKind.True);

    // ---- response writing ---------------------------------------------------

    private static async Task WriteJsonAsync(HttpListenerContext ctx, int status, object? body)
    {
        var json = JsonSerializer.Serialize(body, JsonOpts);
        var bytes = Encoding.UTF8.GetBytes(json);
        ctx.Response.StatusCode = status;
        ctx.Response.ContentType = "application/json";
        ctx.Response.ContentLength64 = bytes.Length;
        await ctx.Response.OutputStream.WriteAsync(bytes);
        ctx.Response.Close();
    }

    private static async Task WriteHtmlAsync(HttpListenerContext ctx, string html)
    {
        var bytes = Encoding.UTF8.GetBytes(html);
        ctx.Response.StatusCode = 200;
        ctx.Response.ContentType = "text/html; charset=utf-8";
        ctx.Response.ContentLength64 = bytes.Length;
        await ctx.Response.OutputStream.WriteAsync(bytes);
        ctx.Response.Close();
    }

    /// <summary>
    /// Serves a file from the reports directory. Validates that the resolved path is inside
    /// <c>.execution/reports/</c> (path-traversal guard) and that the extension is in the allowed set.
    /// Handles both top-level report files (e.g. <c>run-XYZ.html</c>) and screenshot sub-paths
    /// (e.g. <c>{runId}/attachments/screenshot_0.png</c>).
    /// </summary>
    private async Task ServeReportFileAsync(HttpListenerContext ctx, string subPath)
    {
        subPath = Uri.UnescapeDataString(subPath);
        var reportsDir = Path.GetFullPath(_services.Config.ReportsPath);
        var fullPath = Path.GetFullPath(Path.Combine(reportsDir, subPath));

        // Path-traversal guard: resolved path must be inside the reports directory
        if (!fullPath.StartsWith(reportsDir + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
        {
            ctx.Response.StatusCode = 403;
            ctx.Response.Close();
            return;
        }

        var ext = Path.GetExtension(fullPath).ToLowerInvariant();
        var contentType = ext switch
        {
            ".html"          => "text/html; charset=utf-8",
            ".json"          => "application/json",
            ".md"            => "text/markdown",
            ".png"           => "image/png",
            ".jpg" or ".jpeg"=> "image/jpeg",
            ".webp"          => "image/webp",
            ".gif"           => "image/gif",
            _                => null
        };

        if (contentType is null) { ctx.Response.StatusCode = 403; ctx.Response.Close(); return; }
        if (!File.Exists(fullPath)) { ctx.Response.StatusCode = 404; ctx.Response.Close(); return; }

        var bytes = await File.ReadAllBytesAsync(fullPath);
        ctx.Response.StatusCode = 200;
        ctx.Response.ContentType = contentType;
        ctx.Response.ContentLength64 = bytes.Length;
        await ctx.Response.OutputStream.WriteAsync(bytes);
        ctx.Response.Close();
    }

    private static int FreeTcpPort()
    {
        var l = new TcpListener(IPAddress.Loopback, 0);
        l.Start();
        var port = ((IPEndPoint)l.LocalEndpoint).Port;
        l.Stop();
        return port;
    }

    public async ValueTask DisposeAsync()
    {
        try { _listener.Close(); } catch { }
        await _services.DisposeAsync();
    }
}
