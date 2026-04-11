using Spectra.CLI.Progress;
using Spectra.CLI.Results;
using System.Text.Json;

namespace Spectra.CLI.Tests.Progress;

/// <summary>
/// Spec 041: ProgressPageWriter must render the new generation/verification
/// progress bar sections from the in-flight progress object inside
/// .spectra-result.json.
/// </summary>
public class ProgressPageProgressBarTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _htmlPath;

    public ProgressPageProgressBarTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"spectra-pp-prog-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
        _htmlPath = Path.Combine(_tempDir, ".spectra-progress.html");
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
    };

    [Fact]
    public void ProgressSection_RendersBarDuringGeneration()
    {
        var result = new GenerateResult
        {
            Command = "generate",
            Status = "generating",
            Suite = "checkout",
            Generation = new GenerateGeneration
            {
                TestsGenerated = 24,
                TestsWritten = 24,
                TestsRejectedByCritic = 0
            },
            FilesCreated = [],
            Progress = new ProgressSnapshot
            {
                Phase = ProgressPhase.Generating,
                TestsTarget = 40,
                TestsGenerated = 24,
                CurrentBatch = 3,
                TotalBatches = 5
            }
        };
        var json = JsonSerializer.Serialize(result, result.GetType(), JsonOpts);

        ProgressPageWriter.WriteProgressPage(_htmlPath, json, isTerminal: false);
        var html = File.ReadAllText(_htmlPath);

        Assert.Contains("progress-section", html);
        Assert.Contains("Generating tests", html);
        Assert.Contains("24 / 40", html);
        Assert.Contains("60.0%", html); // 24/40 = 60%
        Assert.Contains("Batch 3 of 5", html);
        // Verification bar should be present and dimmed (generation phase)
        Assert.Contains("progress-section dimmed", html);
        Assert.Contains("Verifying tests", html);
    }

    [Fact]
    public void ProgressSection_ShowsVerifyingPhase()
    {
        var result = new GenerateResult
        {
            Command = "generate",
            Status = "verifying",
            Suite = "checkout",
            Generation = new GenerateGeneration
            {
                TestsGenerated = 40,
                TestsWritten = 40,
                TestsRejectedByCritic = 0
            },
            FilesCreated = [],
            Progress = new ProgressSnapshot
            {
                Phase = ProgressPhase.Verifying,
                TestsTarget = 40,
                TestsGenerated = 40,
                TestsVerified = 18,
                CurrentBatch = 5,
                TotalBatches = 5,
                LastTestId = "TC-118",
                LastVerdict = "grounded"
            }
        };
        var json = JsonSerializer.Serialize(result, result.GetType(), JsonOpts);

        ProgressPageWriter.WriteProgressPage(_htmlPath, json, isTerminal: false);
        var html = File.ReadAllText(_htmlPath);

        Assert.Contains("Verifying tests", html);
        Assert.Contains("18 / 40", html);
        Assert.Contains("45.0%", html); // 18/40
        Assert.Contains("TC-118", html);
        Assert.Contains("grounded", html);
        // Generation bar should be 100% (40/40)
        Assert.Contains("40 / 40", html);
        Assert.Contains("100.0%", html);
        // Verification section should NOT be dimmed in verifying phase
        var dimmedAfterVerifying = html.IndexOf("progress-section dimmed");
        var verifyingIndex = html.IndexOf("Verifying tests");
        // either "dimmed" not present at all, or only before the verification block
        Assert.True(dimmedAfterVerifying == -1 || dimmedAfterVerifying < verifyingIndex);
    }

    [Fact]
    public void ProgressSection_UpdatePhase_RendersSingleBar()
    {
        var result = new UpdateResult
        {
            Command = "update",
            Status = "updating",
            Success = false,
            Suite = "checkout",
            TotalTests = 30,
            TestsUpdated = 12,
            Progress = new ProgressSnapshot
            {
                Phase = ProgressPhase.Updating,
                TestsTarget = 30,
                TotalBatches = 30,
                CurrentBatch = 12,
                LastTestId = "TC-012"
            }
        };
        var json = JsonSerializer.Serialize(result, result.GetType(), JsonOpts);

        ProgressPageWriter.WriteProgressPage(_htmlPath, json, isTerminal: false);
        var html = File.ReadAllText(_htmlPath);

        Assert.Contains("Updating tests", html);
        Assert.Contains("12 / 30", html);
        Assert.Contains("Proposal 12 of 30", html);
        // Updating phase shows ONE bar — no verification bar
        Assert.DoesNotContain("Verifying tests", html);
        // And no second "Generating tests" bar either
        Assert.DoesNotContain("Generating tests", html);
    }

    [Fact]
    public void ProgressSection_NotPresent_WhenNoProgressField()
    {
        var result = new GenerateResult
        {
            Command = "generate",
            Status = "completed",
            Suite = "checkout",
            Generation = new GenerateGeneration
            {
                TestsGenerated = 40,
                TestsWritten = 40,
                TestsRejectedByCritic = 0
            },
            FilesCreated = [],
            Progress = null
        };
        var json = JsonSerializer.Serialize(result, result.GetType(), JsonOpts);

        ProgressPageWriter.WriteProgressPage(_htmlPath, json, isTerminal: true);
        var html = File.ReadAllText(_htmlPath);

        // The new progress-section divs should not appear (no in-flight progress)
        // Note: ".progress-section" CSS class definition is still in the <style>
        // block; we need to check that no live <div class="progress-section">
        // was rendered in BuildBody.
        var bodyStart = html.IndexOf("<div class=\"container\"");
        Assert.True(bodyStart >= 0);
        var body = html.Substring(bodyStart);
        Assert.DoesNotContain("class=\"progress-section\"", body);
        Assert.DoesNotContain("class=\"progress-section dimmed\"", body);
    }
}
