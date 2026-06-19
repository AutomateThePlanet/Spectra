using Spectra.CLI.IO;
using Spectra.Core.Models;
using Spectra.Core.Models.Grounding;

namespace Spectra.CLI.Tests.IO;

public class GroundingWriteBackServiceTests : IDisposable
{
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
    private readonly GroundingWriteBackService _service = new(new TestFileWriter());

    public GroundingWriteBackServiceTests() => Directory.CreateDirectory(_tempDir);
    public void Dispose() => Directory.Delete(_tempDir, recursive: true);

    private async Task<string> WriteTestFileAsync(string id = "TC-113")
    {
        var path = Path.Combine(_tempDir, $"{id}.md");
        await File.WriteAllTextAsync(path, $"""
            ---
            id: {id}
            priority: medium
            criteria: []
            ---

            # Test {id}

            ## Steps

            1. Step one

            ## Expected Result

            Result here
            """);
        return path;
    }

    private static string GroundedVerdictJson(string model = "claude-sonnet-4-6") => $$$"""
        {
          "verdict": "grounded",
          "score": 0.95,
          "critic_model": "{{{model}}}",
          "findings": [
            { "element": "Step 1", "claim": "step action", "status": "grounded", "evidence": "doc says so" }
          ]
        }
        """;

    private static string PartialVerdictJson() => """
        {
          "verdict": "partial",
          "score": 0.72,
          "critic_model": "claude-sonnet-4-6",
          "findings": [
            { "element": "Step 1", "claim": "step action", "status": "grounded", "evidence": "doc says so" },
            { "element": "Expected Result", "claim": "exact value", "status": "unverified", "reason": "not in docs" }
          ]
        }
        """;

    private static string HallucinatedVerdictJson() => """
        {
          "verdict": "hallucinated",
          "score": 0.30,
          "critic_model": "claude-sonnet-4-6",
          "findings": [
            { "element": "Step 2", "claim": "1 KB = 1000 bytes", "status": "hallucinated", "reason": "contradicts docs" }
          ]
        }
        """;

    [Fact]
    public async Task WriteAsync_GroundedVerdict_WritesGroundingBlock()
    {
        var path = await WriteTestFileAsync();
        var result = await _service.WriteAsync(path, GroundedVerdictJson());

        Assert.True(result.IsSuccess);
        Assert.Equal(GroundingWriteBackService.WriteBackOutcome.Success, result.Outcome);
        Assert.NotNull(result.Grounding);
        Assert.Equal(VerificationVerdict.Grounded, result.Grounding!.Verdict);
        Assert.False(result.Grounding.FlaggedForReview);

        var content = await File.ReadAllTextAsync(path);
        Assert.Contains("verdict: grounded", content);
        Assert.Contains("score: 0.95", content);
    }

    [Fact]
    public async Task WriteAsync_PartialVerdict_SetsFlaggedForReview()
    {
        var path = await WriteTestFileAsync();
        var result = await _service.WriteAsync(path, PartialVerdictJson());

        Assert.True(result.IsSuccess);
        Assert.True(result.Grounding!.FlaggedForReview);
        Assert.Equal(VerificationVerdict.Partial, result.Grounding.Verdict);
        Assert.NotEmpty(result.Grounding.CondensedFindings);
        Assert.Equal("Expected Result", result.Grounding.CondensedFindings[0].Element);

        var content = await File.ReadAllTextAsync(path);
        Assert.Contains("flagged_for_review: true", content);
        Assert.Contains("condensed_findings:", content);
    }

    [Fact]
    public async Task WriteAsync_HallucinatedVerdict_ReturnsHallucinatedRefused()
    {
        var path = await WriteTestFileAsync();
        var result = await _service.WriteAsync(path, HallucinatedVerdictJson());

        Assert.False(result.IsSuccess);
        Assert.Equal(GroundingWriteBackService.WriteBackOutcome.HallucinatedRefused, result.Outcome);
    }

    [Fact]
    public async Task WriteAsync_MissingFile_ReturnsTestNotFound()
    {
        var result = await _service.WriteAsync(
            Path.Combine(_tempDir, "nonexistent.md"), GroundedVerdictJson());

        Assert.False(result.IsSuccess);
        Assert.Equal(GroundingWriteBackService.WriteBackOutcome.TestNotFound, result.Outcome);
    }

    [Fact]
    public async Task WriteAsync_Repaired_True_SetsRepairedAndClearsFlag()
    {
        var path = await WriteTestFileAsync();
        var result = await _service.WriteAsync(path, GroundedVerdictJson(), repairAttempts: 1, repaired: true);

        Assert.True(result.IsSuccess);
        Assert.True(result.Grounding!.Repaired);
        Assert.Equal(1, result.Grounding.RepairAttempts);
        Assert.False(result.Grounding.FlaggedForReview);

        var content = await File.ReadAllTextAsync(path);
        Assert.Contains("repaired: true", content);
        Assert.Contains("repair_attempts: 1", content);
    }

    [Fact]
    public async Task WriteAsync_GeneratorFallback_UsesClaudeCodeSession()
    {
        var path = await WriteTestFileAsync();
        var result = await _service.WriteAsync(path, GroundedVerdictJson());

        Assert.True(result.IsSuccess);
        Assert.Equal("claude-code-session", result.Grounding!.Generator);
    }
}
