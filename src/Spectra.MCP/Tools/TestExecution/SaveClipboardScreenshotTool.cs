using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using Spectra.Core.Models.Execution;
using Spectra.MCP.Execution;
using Spectra.MCP.Server;
using Spectra.MCP.Storage;

namespace Spectra.MCP.Tools.TestExecution;

/// <summary>
/// MCP tool: save_clipboard_screenshot
/// Reads an image from the system clipboard and saves it as a test attachment.
/// Cross-platform: Windows (PowerShell), macOS (osascript/pngpaste), Linux (xclip).
/// </summary>
public sealed class SaveClipboardScreenshotTool : IMcpTool
{
    private readonly ExecutionEngine _engine;
    private readonly RunRepository _runRepo;
    private readonly ResultRepository _resultRepo;
    private readonly string _reportsPath;
    private readonly SaveScreenshotTool _screenshotTool;

    public string Description => "Reads an image from the system clipboard and saves it as a screenshot attachment. Use this when the user pastes a screenshot in chat but base64 extraction is not available.";

    public object? ParameterSchema => new
    {
        type = "object",
        properties = new
        {
            test_handle = new { type = "string", description = "Test handle to attach screenshot to. If omitted, auto-detects from the active or most recently completed test." },
            caption = new { type = "string", description = "Optional caption describing what the screenshot shows" }
        }
    };

    public SaveClipboardScreenshotTool(
        ExecutionEngine engine,
        string reportsPath,
        RunRepository runRepo,
        ResultRepository resultRepo,
        SaveScreenshotTool screenshotTool)
    {
        _engine = engine;
        _reportsPath = reportsPath;
        _runRepo = runRepo;
        _resultRepo = resultRepo;
        _screenshotTool = screenshotTool;
    }

    public async Task<string> ExecuteAsync(JsonElement? parameters)
    {
        var request = McpProtocol.DeserializeParams<SaveClipboardScreenshotRequest>(parameters);

        // Save clipboard image to a temp file
        var tempPath = Path.Combine(Path.GetTempPath(), $"spectra-clipboard-{Guid.NewGuid():N}.png");

        try
        {
            var saved = await SaveClipboardImageAsync(tempPath);
            if (!saved)
            {
                return JsonSerializer.Serialize(McpToolResponse<object>.Failure(
                    "NO_CLIPBOARD_IMAGE",
                    "No image found on the system clipboard. The user may need to copy/paste the screenshot again, or save it to a file and use save_screenshot with file_path instead."));
            }

            // Delegate to save_screenshot with file_path
            var delegateParams = new Dictionary<string, object?>();
            delegateParams["file_path"] = tempPath;
            if (!string.IsNullOrEmpty(request?.TestHandle))
                delegateParams["test_handle"] = request.TestHandle;
            if (!string.IsNullOrEmpty(request?.Caption))
                delegateParams["caption"] = request.Caption;

            var jsonParams = JsonSerializer.Serialize(delegateParams);
            var element = JsonDocument.Parse(jsonParams).RootElement;

            return await _screenshotTool.ExecuteAsync(element);
        }
        finally
        {
            // Clean up temp file
            try { if (File.Exists(tempPath)) File.Delete(tempPath); } catch { }
        }
    }

    /// <summary>
    /// Saves the clipboard image to a file. Returns true if an image was found and saved.
    /// </summary>
    private static async Task<bool> SaveClipboardImageAsync(string outputPath)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return await SaveClipboardWindows(outputPath);
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            return await SaveClipboardMacOS(outputPath);
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            return await SaveClipboardLinux(outputPath);

        return false;
    }

    private static async Task<bool> SaveClipboardWindows(string outputPath)
    {
        // Use PowerShell to read clipboard image and save as PNG
        var script = $@"
$img = Get-Clipboard -Format Image
if ($img -eq $null) {{ exit 1 }}
$img.Save('{outputPath.Replace("'", "''")}', [System.Drawing.Imaging.ImageFormat]::Png)
exit 0";

        return await RunProcessAsync("powershell", $"-NoProfile -NonInteractive -Command \"{script.Replace("\"", "\\\"")}\"");
    }

    private static async Task<bool> SaveClipboardMacOS(string outputPath)
    {
        // Try pngpaste first (brew install pngpaste), fall back to osascript
        if (await RunProcessAsync("pngpaste", outputPath))
            return true;

        // Fallback: osascript to save clipboard image
        var script = $"set theFile to POSIX file \"{outputPath}\" as text\n" +
                     "try\n" +
                     "  set imgData to the clipboard as «class PNGf»\n" +
                     "  set fp to open for access file theFile with write permission\n" +
                     "  write imgData to fp\n" +
                     "  close access fp\n" +
                     "on error\n" +
                     "  return \"no image\"\n" +
                     "end try";

        return await RunProcessAsync("osascript", $"-e '{script}'") && File.Exists(outputPath) && new FileInfo(outputPath).Length > 0;
    }

    private static async Task<bool> SaveClipboardLinux(string outputPath)
    {
        // Try xclip first, then xsel, then wl-paste (Wayland)
        if (await RunProcessWithRedirectAsync("xclip", "-selection clipboard -t image/png -o", outputPath))
            return true;

        if (await RunProcessWithRedirectAsync("wl-paste", "--type image/png", outputPath))
            return true;

        return false;
    }

    private static async Task<bool> RunProcessAsync(string command, string arguments)
    {
        try
        {
            using var process = new Process();
            process.StartInfo = new ProcessStartInfo
            {
                FileName = command,
                Arguments = arguments,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            process.Start();
            await process.WaitForExitAsync();
            return process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    private static async Task<bool> RunProcessWithRedirectAsync(string command, string arguments, string outputPath)
    {
        try
        {
            using var process = new Process();
            process.StartInfo = new ProcessStartInfo
            {
                FileName = command,
                Arguments = arguments,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            process.Start();

            // Write stdout to file
            await using var fs = File.Create(outputPath);
            await process.StandardOutput.BaseStream.CopyToAsync(fs);
            await process.WaitForExitAsync();

            return process.ExitCode == 0 && new FileInfo(outputPath).Length > 0;
        }
        catch
        {
            return false;
        }
    }
}

internal sealed class SaveClipboardScreenshotRequest
{
    [JsonPropertyName("test_handle")]
    public string? TestHandle { get; set; }

    [JsonPropertyName("caption")]
    public string? Caption { get; set; }
}
