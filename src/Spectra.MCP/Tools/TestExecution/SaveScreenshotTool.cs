using System.Text.Json;
using System.Text.Json.Serialization;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.Processing;
using Spectra.Core.Models.Execution;
using Spectra.MCP.Execution;
using Spectra.MCP.Server;
using Spectra.MCP.Storage;
using Spectra.MCP.Tools;

namespace Spectra.MCP.Tools.TestExecution;

/// <summary>
/// MCP tool: save_screenshot
/// Saves a base64-encoded screenshot as a compressed WebP attachment.
/// </summary>
public sealed class SaveScreenshotTool : IMcpTool
{
    private readonly ExecutionEngine _engine;
    private readonly RunRepository _runRepo;
    private readonly ResultRepository _resultRepo;
    private readonly string _reportsPath;

    private const int MaxWidth = 1920;
    private const int MaxHeight = 1080;
    private const int WebPQuality = 80;

    public string Description => "Saves a screenshot as an attachment for the current test. Auto-detects the active test if test_handle is omitted.";

    public object? ParameterSchema => new
    {
        type = "object",
        properties = new
        {
            test_handle = new { type = "string", description = "Test handle to attach screenshot to (auto-detected from active run if omitted)" },
            image_data = new { type = "string", description = "Base64-encoded image data (PNG, JPG, or WebP)" },
            caption = new { type = "string", description = "Optional caption describing what the screenshot shows" },
            filename = new { type = "string", description = "Optional filename override" }
        },
        required = new[] { "image_data" }
    };

    public SaveScreenshotTool(ExecutionEngine engine, string reportsPath, RunRepository runRepo, ResultRepository? resultRepo = null)
    {
        _engine = engine;
        _reportsPath = reportsPath;
        _runRepo = runRepo;
        _resultRepo = resultRepo!;
    }

    public async Task<string> ExecuteAsync(JsonElement? parameters)
    {
        var request = McpProtocol.DeserializeParams<SaveScreenshotRequest>(parameters);

        // Resolve test_handle
        var (resolvedRunId, runError) = await ActiveRunResolver.ResolveRunIdAsync(null, _runRepo);
        string? resolvedTestHandle = request?.TestHandle;
        if (string.IsNullOrEmpty(resolvedTestHandle))
        {
            if (runError is not null)
                return runError;
            var (autoHandle, handleError) = await ActiveRunResolver.ResolveTestHandleAsync(null, resolvedRunId!, _resultRepo!);
            if (handleError is not null)
                return handleError;
            resolvedTestHandle = autoHandle;
        }

        if (string.IsNullOrEmpty(request?.ImageData))
        {
            return JsonSerializer.Serialize(McpToolResponse<object>.Failure(
                "INVALID_PARAMS",
                "image_data is required (base64-encoded)"));
        }

        var result = await _engine.GetTestResultAsync(resolvedTestHandle);
        if (result is null)
        {
            return JsonSerializer.Serialize(McpToolResponse<object>.Failure(
                "INVALID_HANDLE",
                $"Test handle '{resolvedTestHandle}' not found"));
        }

        var run = await _engine.GetRunAsync(result.RunId);
        if (run is null)
        {
            return JsonSerializer.Serialize(McpToolResponse<object>.Failure(
                "RUN_NOT_FOUND",
                "Run not found"));
        }

        try
        {
            // Decode base64 image data
            byte[] imageBytes;
            try
            {
                // Handle data URI format (data:image/png;base64,...)
                var base64Data = request.ImageData;
                if (base64Data.Contains(','))
                {
                    base64Data = base64Data.Split(',')[1];
                }
                imageBytes = Convert.FromBase64String(base64Data);
            }
            catch (FormatException)
            {
                return JsonSerializer.Serialize(McpToolResponse<object>.Failure(
                    "INVALID_IMAGE_DATA",
                    "image_data must be valid base64-encoded image data"));
            }

            // Count existing screenshots for this test to generate index
            var existingPaths = result.ScreenshotPaths?.Count ?? 0;
            var index = existingPaths + 1;

            // Generate WebP filename: {testId}-{date}-{index}.webp
            var testIdSafe = result.TestId.Replace("/", "-").Replace("\\", "-");
            var datePart = DateTime.UtcNow.ToString("yyyyMMdd");
            var filename = string.IsNullOrEmpty(request.Filename)
                ? $"{testIdSafe}-{datePart}-{index}.webp"
                : Path.GetFileNameWithoutExtension(request.Filename) + ".webp";

            // Create attachments directory: reports/{run_id}/attachments/
            var attachmentsDir = Path.Combine(_reportsPath, result.RunId, "attachments");
            Directory.CreateDirectory(attachmentsDir);

            var filePath = Path.Combine(attachmentsDir, filename);
            var originalSize = imageBytes.Length;

            // Compress: load with ImageSharp → resize if needed → encode as WebP
            byte[] compressedBytes;
            try
            {
                using var image = Image.Load(imageBytes);

                // Resize if larger than max dimensions
                if (image.Width > MaxWidth || image.Height > MaxHeight)
                {
                    image.Mutate(x => x.Resize(new ResizeOptions
                    {
                        Size = new Size(MaxWidth, MaxHeight),
                        Mode = ResizeMode.Max
                    }));
                }

                using var ms = new MemoryStream();
                await image.SaveAsync(ms, new WebpEncoder { Quality = WebPQuality });
                compressedBytes = ms.ToArray();
            }
            catch
            {
                // If ImageSharp fails (corrupt/unsupported), save raw bytes with original extension
                compressedBytes = imageBytes;
                var ext = DetectImageExtension(imageBytes);
                filename = Path.GetFileNameWithoutExtension(filename) + ext;
                filePath = Path.Combine(attachmentsDir, filename);
            }

            await File.WriteAllBytesAsync(filePath, compressedBytes);

            // Build relative path from reports/ directory (where the HTML report lives)
            var relativePath = $"{result.RunId}/attachments/{filename}";
            var noteText = string.IsNullOrEmpty(request.Caption)
                ? $"[Screenshot: {relativePath}]"
                : $"[Screenshot: {relativePath}] {request.Caption}";
            await _engine.AddNoteAsync(resolvedTestHandle, noteText);

            // Store screenshot path in DB
            if (_resultRepo is not null)
            {
                await _resultRepo.AppendScreenshotPathAsync(resolvedTestHandle, relativePath);
            }

            var queue = await _engine.GetQueueAsync(result.RunId);
            var progress = queue?.GetProgress() ?? "?/?";

            var data = new
            {
                test_id = result.TestId,
                screenshot_saved = true,
                path = filePath,
                relative_path = relativePath,
                size_bytes = compressedBytes.Length,
                original_size_bytes = originalSize,
                format = "webp"
            };

            var nextAction = result.Status == TestStatus.InProgress ? "advance_test_case" : "get_test_case_details";

            return JsonSerializer.Serialize(McpToolResponse<object>.Success(
                data,
                run.Status,
                progress,
                nextAction));
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(McpToolResponse<object>.Failure(
                "SAVE_FAILED",
                $"Failed to save screenshot: {ex.Message}"));
        }
    }

    private static string DetectImageExtension(byte[] bytes)
    {
        if (bytes.Length < 4) return ".png";

        // PNG: 89 50 4E 47
        if (bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4E && bytes[3] == 0x47)
            return ".png";

        // JPEG: FF D8 FF
        if (bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF)
            return ".jpg";

        // WebP: 52 49 46 46 ... 57 45 42 50
        if (bytes.Length >= 12 && bytes[0] == 0x52 && bytes[1] == 0x49 && bytes[2] == 0x46 && bytes[3] == 0x46 &&
            bytes[8] == 0x57 && bytes[9] == 0x45 && bytes[10] == 0x42 && bytes[11] == 0x50)
            return ".webp";

        // GIF: 47 49 46 38
        if (bytes[0] == 0x47 && bytes[1] == 0x49 && bytes[2] == 0x46 && bytes[3] == 0x38)
            return ".gif";

        return ".png"; // Default to PNG
    }
}

internal sealed class SaveScreenshotRequest
{
    [JsonPropertyName("test_handle")]
    public string? TestHandle { get; set; }

    [JsonPropertyName("image_data")]
    public string? ImageData { get; set; }

    [JsonPropertyName("caption")]
    public string? Caption { get; set; }

    [JsonPropertyName("filename")]
    public string? Filename { get; set; }
}
