using System.Text.Json;
using System.Text.Json.Serialization;
using Spectra.Core.Models.Execution;
using Spectra.MCP.Execution;
using Spectra.MCP.Server;

namespace Spectra.MCP.Tools.TestExecution;

/// <summary>
/// MCP tool: save_screenshot
/// Saves a base64-encoded screenshot as an attachment.
/// </summary>
public sealed class SaveScreenshotTool : IMcpTool
{
    private readonly ExecutionEngine _engine;
    private readonly string _reportsPath;

    public string Description => "Saves a screenshot as an attachment for the current test";

    public object? ParameterSchema => new
    {
        type = "object",
        properties = new
        {
            test_handle = new { type = "string", description = "Test handle to attach screenshot to" },
            image_data = new { type = "string", description = "Base64-encoded image data (PNG, JPG, or WebP)" },
            filename = new { type = "string", description = "Optional filename (default: screenshot_<timestamp>.png)" }
        },
        required = new[] { "test_handle", "image_data" }
    };

    public SaveScreenshotTool(ExecutionEngine engine, string reportsPath)
    {
        _engine = engine;
        _reportsPath = reportsPath;
    }

    public async Task<string> ExecuteAsync(JsonElement? parameters)
    {
        var request = McpProtocol.DeserializeParams<SaveScreenshotRequest>(parameters);
        if (request is null || string.IsNullOrEmpty(request.TestHandle))
        {
            return JsonSerializer.Serialize(McpToolResponse<object>.Failure(
                "INVALID_PARAMS",
                "test_handle is required"));
        }

        if (string.IsNullOrEmpty(request.ImageData))
        {
            return JsonSerializer.Serialize(McpToolResponse<object>.Failure(
                "INVALID_PARAMS",
                "image_data is required (base64-encoded)"));
        }

        var result = await _engine.GetTestResultAsync(request.TestHandle);
        if (result is null)
        {
            return JsonSerializer.Serialize(McpToolResponse<object>.Failure(
                "INVALID_HANDLE",
                $"Test handle '{request.TestHandle}' not found"));
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

            // Detect image format from magic bytes
            var extension = DetectImageExtension(imageBytes);

            // Generate filename if not provided
            var timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
            var filename = string.IsNullOrEmpty(request.Filename)
                ? $"screenshot_{timestamp}{extension}"
                : request.Filename;

            // Ensure filename has correct extension
            if (!filename.EndsWith(extension, StringComparison.OrdinalIgnoreCase))
            {
                filename = Path.GetFileNameWithoutExtension(filename) + extension;
            }

            // Create attachments directory: reports/{run_id}/attachments/
            var attachmentsDir = Path.Combine(_reportsPath, result.RunId, "attachments");
            Directory.CreateDirectory(attachmentsDir);

            // Save the image
            var filePath = Path.Combine(attachmentsDir, filename);
            await File.WriteAllBytesAsync(filePath, imageBytes);

            // Add a note referencing the screenshot
            var relativePath = $"attachments/{filename}";
            await _engine.AddNoteAsync(request.TestHandle, $"[Screenshot: {relativePath}]");

            var queue = await _engine.GetQueueAsync(result.RunId);
            var progress = queue?.GetProgress() ?? "?/?";

            var data = new
            {
                test_id = result.TestId,
                screenshot_saved = true,
                path = filePath,
                relative_path = relativePath,
                size_bytes = imageBytes.Length
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

    [JsonPropertyName("filename")]
    public string? Filename { get; set; }
}
