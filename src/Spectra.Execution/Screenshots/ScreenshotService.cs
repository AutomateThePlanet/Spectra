using System.Diagnostics;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.Processing;

namespace Spectra.MCP.Reports;

/// <summary>
/// Spec 065: transport-neutral screenshot capture/encode shared by the CLI (<c>spectra run
/// screenshot[-clipboard]</c>) and the MCP screenshot tools. Encapsulates the ImageSharp resize/WebP
/// encode (with raw-bytes fallback) and the cross-platform clipboard capture shellout, so failure
/// evidence can be attached from a local CLI process exactly as from the MCP server.
/// </summary>
public sealed class ScreenshotService
{
    private const int MaxWidth = 1920;
    private const int MaxHeight = 1080;
    private const int WebPQuality = 80;

    public sealed record SavedScreenshot(string RelativePath, string AbsolutePath, int SizeBytes, int OriginalSizeBytes, string Format);

    /// <summary>
    /// Encodes image bytes to a compressed WebP (resizing if oversized; raw fallback on decode
    /// failure) and writes it under <c>{reportsPath}/{runId}/attachments/</c>. Returns the saved
    /// metadata including the report-relative path to persist on the test result.
    /// </summary>
    public async Task<SavedScreenshot> EncodeAndSaveAsync(
        string reportsPath, string runId, string testId, int existingCount, byte[] imageBytes, string? filenameOverride = null)
    {
        var testIdSafe = testId.Replace("/", "-").Replace("\\", "-");
        var datePart = DateTime.UtcNow.ToString("yyyyMMdd");
        var filename = string.IsNullOrEmpty(filenameOverride)
            ? $"{testIdSafe}-{datePart}-{existingCount + 1}.webp"
            : Path.GetFileNameWithoutExtension(filenameOverride) + ".webp";

        var attachmentsDir = Path.Combine(reportsPath, runId, "attachments");
        Directory.CreateDirectory(attachmentsDir);

        var originalSize = imageBytes.Length;
        var filePath = Path.Combine(attachmentsDir, filename);

        byte[] compressedBytes;
        var format = "webp";
        try
        {
            using var image = Image.Load(imageBytes);
            if (image.Width > MaxWidth || image.Height > MaxHeight)
            {
                image.Mutate(x => x.Resize(new ResizeOptions { Size = new Size(MaxWidth, MaxHeight), Mode = ResizeMode.Max }));
            }
            using var ms = new MemoryStream();
            await image.SaveAsync(ms, new WebpEncoder { Quality = WebPQuality });
            compressedBytes = ms.ToArray();
        }
        catch
        {
            // Corrupt/unsupported → store raw bytes with the detected extension.
            compressedBytes = imageBytes;
            var ext = DetectImageExtension(imageBytes);
            format = ext.TrimStart('.');
            filename = Path.GetFileNameWithoutExtension(filename) + ext;
            filePath = Path.Combine(attachmentsDir, filename);
        }

        await File.WriteAllBytesAsync(filePath, compressedBytes);
        var relativePath = $"{runId}/attachments/{filename}";
        return new SavedScreenshot(relativePath, filePath, compressedBytes.Length, originalSize, format);
    }

    /// <summary>
    /// Captures the system clipboard image to <paramref name="outputPath"/>. Returns true if an image
    /// was found and saved. Cross-platform: Windows (PowerShell), macOS (pngpaste/osascript), Linux
    /// (xclip/wl-paste). A local CLI process is the host — strictly better than a remote server.
    /// </summary>
    public static async Task<bool> TryCaptureClipboardAsync(string outputPath)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return await CaptureWindows(outputPath);
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) return await CaptureMacOS(outputPath);
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux)) return await CaptureLinux(outputPath);
        return false;
    }

    private static async Task<bool> CaptureWindows(string outputPath)
    {
        var script = $@"
$img = Get-Clipboard -Format Image
if ($img -eq $null) {{ exit 1 }}
$img.Save('{outputPath.Replace("'", "''")}', [System.Drawing.Imaging.ImageFormat]::Png)
exit 0";
        return await RunAsync("powershell", $"-NoProfile -NonInteractive -Command \"{script.Replace("\"", "\\\"")}\"");
    }

    private static async Task<bool> CaptureMacOS(string outputPath)
    {
        if (await RunAsync("pngpaste", outputPath)) return true;
        var script = $"set theFile to POSIX file \"{outputPath}\" as text\n" +
                     "try\n  set imgData to the clipboard as «class PNGf»\n" +
                     "  set fp to open for access file theFile with write permission\n" +
                     "  write imgData to fp\n  close access fp\non error\n  return \"no image\"\nend try";
        return await RunAsync("osascript", $"-e '{script}'") && File.Exists(outputPath) && new FileInfo(outputPath).Length > 0;
    }

    private static async Task<bool> CaptureLinux(string outputPath)
    {
        if (await RunWithRedirectAsync("xclip", "-selection clipboard -t image/png -o", outputPath)) return true;
        if (await RunWithRedirectAsync("wl-paste", "--type image/png", outputPath)) return true;
        return false;
    }

    private static async Task<bool> RunAsync(string command, string arguments)
    {
        try
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = command, Arguments = arguments,
                    UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true
                }
            };
            process.Start();
            await process.WaitForExitAsync();
            return process.ExitCode == 0;
        }
        catch { return false; }
    }

    private static async Task<bool> RunWithRedirectAsync(string command, string arguments, string outputPath)
    {
        try
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = command, Arguments = arguments,
                    UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true
                }
            };
            process.Start();
            await using var fs = File.Create(outputPath);
            await process.StandardOutput.BaseStream.CopyToAsync(fs);
            await process.WaitForExitAsync();
            return process.ExitCode == 0 && new FileInfo(outputPath).Length > 0;
        }
        catch { return false; }
    }

    public static string DetectImageExtension(byte[] bytes)
    {
        if (bytes.Length < 4) return ".png";
        if (bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4E && bytes[3] == 0x47) return ".png";
        if (bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF) return ".jpg";
        if (bytes.Length >= 12 && bytes[0] == 0x52 && bytes[1] == 0x49 && bytes[2] == 0x46 && bytes[3] == 0x46 &&
            bytes[8] == 0x57 && bytes[9] == 0x45 && bytes[10] == 0x42 && bytes[11] == 0x50) return ".webp";
        if (bytes[0] == 0x47 && bytes[1] == 0x49 && bytes[2] == 0x46 && bytes[3] == 0x38) return ".gif";
        return ".png";
    }
}
