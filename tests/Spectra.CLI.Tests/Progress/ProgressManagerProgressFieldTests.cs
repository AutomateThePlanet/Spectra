using Spectra.CLI.Progress;
using Spectra.CLI.Results;

namespace Spectra.CLI.Tests.Progress;

/// <summary>
/// Spec 041: ProgressManager must clear the in-flight ProgressSnapshot from
/// GenerateResult / UpdateResult on Complete() and Fail() so the final result
/// file shows runSummary instead of stale progress data.
/// </summary>
public class ProgressManagerProgressFieldTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _resultPath;
    private readonly string _progressPath;

    public ProgressManagerProgressFieldTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"spectra-pm-prog-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
        _resultPath = Path.Combine(_tempDir, ".spectra-result.json");
        _progressPath = Path.Combine(_tempDir, ".spectra-progress.html");
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    private ProgressManager CreateManager() =>
        new("generate", ProgressPhases.Generate, _resultPath, _progressPath);

    [Fact]
    public void Update_WithProgressSnapshot_WritesProgressField()
    {
        var pm = CreateManager();
        var result = new GenerateResult
        {
            Command = "generate",
            Status = "generating",
            Suite = "checkout",
            Generation = new GenerateGeneration
            {
                TestsGenerated = 10,
                TestsWritten = 0,
                TestsRejectedByCritic = 0
            },
            FilesCreated = [],
            Progress = new ProgressSnapshot
            {
                Phase = ProgressPhase.Generating,
                TestsTarget = 40,
                TestsGenerated = 10,
                CurrentBatch = 1,
                TotalBatches = 4
            }
        };

        pm.Update(result);

        var json = File.ReadAllText(_resultPath);
        Assert.Contains("\"progress\"", json);
        Assert.Contains("\"testsTarget\": 40", json);
        Assert.Contains("\"testsGenerated\": 10", json);
        Assert.Contains("\"Generating\"", json);
    }

    [Fact]
    public void Complete_ClearsProgressField()
    {
        var pm = CreateManager();
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
            Progress = new ProgressSnapshot
            {
                Phase = ProgressPhase.Verifying,
                TestsTarget = 40,
                TestsGenerated = 40,
                TestsVerified = 40,
                CurrentBatch = 4,
                TotalBatches = 4
            }
        };

        pm.Complete(result);

        var json = File.ReadAllText(_resultPath);
        Assert.DoesNotContain("\"progress\"", json);
        Assert.Null(result.Progress);
    }

    [Fact]
    public void Fail_ClearsProgressField()
    {
        var pm = CreateManager();
        var partial = new GenerateResult
        {
            Command = "generate",
            Status = "failed",
            Suite = "checkout",
            Generation = new GenerateGeneration
            {
                TestsGenerated = 5,
                TestsWritten = 0,
                TestsRejectedByCritic = 0
            },
            FilesCreated = [],
            Progress = new ProgressSnapshot
            {
                Phase = ProgressPhase.Generating,
                TestsTarget = 40,
                TestsGenerated = 5,
                CurrentBatch = 1,
                TotalBatches = 4
            }
        };

        pm.Fail("Something broke", partial);

        var json = File.ReadAllText(_resultPath);
        Assert.DoesNotContain("\"progress\"", json);
        Assert.Null(partial.Progress);
    }

    [Fact]
    public void UpdateResult_WithProgressSnapshot_WritesProgressField()
    {
        var pm = new ProgressManager("update", ProgressPhases.Update, _resultPath, _progressPath);
        var result = new UpdateResult
        {
            Command = "update",
            Status = "updating",
            Success = false,
            Suite = "checkout",
            TotalTests = 30,
            TestsUpdated = 5,
            Progress = new ProgressSnapshot
            {
                Phase = ProgressPhase.Updating,
                TestsTarget = 30,
                TotalBatches = 30,
                CurrentBatch = 5
            }
        };

        pm.Update(result);

        var json = File.ReadAllText(_resultPath);
        Assert.Contains("\"progress\"", json);
        Assert.Contains("\"Updating\"", json);
        Assert.Contains("\"currentBatch\": 5", json);
    }

    [Fact]
    public void UpdateResult_Complete_ClearsProgressField()
    {
        var pm = new ProgressManager("update", ProgressPhases.Update, _resultPath, _progressPath);
        var result = new UpdateResult
        {
            Command = "update",
            Status = "completed",
            Success = true,
            Suite = "checkout",
            TotalTests = 30,
            TestsUpdated = 30,
            Progress = new ProgressSnapshot
            {
                Phase = ProgressPhase.Updating,
                TestsTarget = 30,
                TotalBatches = 30,
                CurrentBatch = 30
            }
        };

        pm.Complete(result);

        var json = File.ReadAllText(_resultPath);
        Assert.DoesNotContain("\"progress\"", json);
        Assert.Null(result.Progress);
    }
}
