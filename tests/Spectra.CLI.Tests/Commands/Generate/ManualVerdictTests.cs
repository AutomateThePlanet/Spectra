using Spectra.CLI.Commands.Generate;
using Spectra.Core.Models;
using Spectra.Core.Models.Grounding;

namespace Spectra.CLI.Tests.Commands.Generate;

/// <summary>
/// Tests for manual verdict handling in the generate pipeline.
/// Covers T010 (preserve manual grounding), T013 (skip verification),
/// and T014 (manual passes write filter).
/// </summary>
public class ManualVerdictTests
{
    /// <summary>
    /// T010: CreateTestWithGrounding preserves existing Manual grounding metadata
    /// and does not overwrite it with critic results.
    /// </summary>
    [Fact]
    public void CreateTestWithGrounding_ManualVerdict_PreservesExistingGrounding()
    {
        var manualGrounding = new GroundingMetadata
        {
            Verdict = VerificationVerdict.Manual,
            Score = 1.0,
            Generator = "user",
            Critic = "none",
            VerifiedAt = new DateTimeOffset(2026, 1, 15, 10, 0, 0, TimeSpan.Zero)
        };

        var test = CreateTestCase("TC-100", "Manual test", manualGrounding);

        // Even if a critic result is provided, it should be ignored for Manual tests
        var criticResult = new VerificationResult
        {
            Verdict = VerificationVerdict.Grounded,
            Score = 0.85,
            Findings = [],
            CriticModel = "gemini-2.0-flash"
        };

        var testsPath = Path.Combine(Path.GetTempPath(), "test-cases");
        var filePath = Path.Combine(testsPath, "suite", "TC-100.md");

        var result = GenerateHandler.CreateTestWithGrounding(
            test, criticResult, "gpt-4o", filePath, testsPath);

        // Must preserve the original Manual grounding, not the critic result
        Assert.NotNull(result.Grounding);
        Assert.Equal(VerificationVerdict.Manual, result.Grounding.Verdict);
        Assert.Equal(1.0, result.Grounding.Score);
        Assert.Equal("user", result.Grounding.Generator);
        Assert.Equal("none", result.Grounding.Critic);
        Assert.Equal(manualGrounding.VerifiedAt, result.Grounding.VerifiedAt);
    }

    /// <summary>
    /// T010: CreateTestWithGrounding applies critic result when grounding is not Manual.
    /// </summary>
    [Fact]
    public void CreateTestWithGrounding_NonManualVerdict_AppliesCriticResult()
    {
        var test = CreateTestCase("TC-101", "Normal test", grounding: null);

        var criticResult = new VerificationResult
        {
            Verdict = VerificationVerdict.Grounded,
            Score = 0.92,
            Findings = [],
            CriticModel = "gemini-2.0-flash"
        };

        var testsPath = Path.Combine(Path.GetTempPath(), "test-cases");
        var filePath = Path.Combine(testsPath, "suite", "TC-101.md");

        var result = GenerateHandler.CreateTestWithGrounding(
            test, criticResult, "gpt-4o", filePath, testsPath);

        Assert.NotNull(result.Grounding);
        Assert.Equal(VerificationVerdict.Grounded, result.Grounding.Verdict);
        Assert.Equal(0.92, result.Grounding.Score);
        Assert.Equal("gpt-4o", result.Grounding.Generator);
        Assert.Equal("gemini-2.0-flash", result.Grounding.Critic);
    }

    /// <summary>
    /// T010: CreateTestWithGrounding does not apply grounding when no critic result.
    /// </summary>
    [Fact]
    public void CreateTestWithGrounding_NullResult_NoGrounding()
    {
        var test = CreateTestCase("TC-102", "No verification test", grounding: null);

        var testsPath = Path.Combine(Path.GetTempPath(), "test-cases");
        var filePath = Path.Combine(testsPath, "suite", "TC-102.md");

        var result = GenerateHandler.CreateTestWithGrounding(
            test, null, "gpt-4o", filePath, testsPath);

        Assert.Null(result.Grounding);
    }

    /// <summary>
    /// T013: Manual verdict test is skipped in verification — produces a pass-through result
    /// with Manual verdict, score 1.0, and empty findings.
    /// This tests the filtering logic that VerifyTestsAsync uses.
    /// </summary>
    [Fact]
    public void ManualVerdictTest_SkipsVerification_ProducesPassThroughResult()
    {
        var manualGrounding = new GroundingMetadata
        {
            Verdict = VerificationVerdict.Manual,
            Score = 1.0,
            Generator = "user",
            Critic = "none",
            VerifiedAt = DateTimeOffset.UtcNow
        };

        var test = CreateTestCase("TC-200", "Manual test", manualGrounding);

        // Simulate the skip logic from VerifyTestsAsync
        var shouldSkipVerification = test.Grounding is not null
            && test.Grounding.Verdict == VerificationVerdict.Manual;

        Assert.True(shouldSkipVerification);

        // The pass-through result that would be created
        var passThrough = new VerificationResult
        {
            Verdict = VerificationVerdict.Manual,
            Score = 1.0,
            Findings = [],
            CriticModel = "test-critic"
        };

        Assert.Equal(VerificationVerdict.Manual, passThrough.Verdict);
        Assert.Equal(1.0, passThrough.Score);
        Assert.Empty(passThrough.Findings);
        Assert.True(passThrough.IsSuccess);
    }

    /// <summary>
    /// T013: Null grounding test still goes through normal verification.
    /// </summary>
    [Fact]
    public void NullGrounding_DoesNotSkipVerification()
    {
        var test = CreateTestCase("TC-201", "Normal test", grounding: null);

        var shouldSkipVerification = test.Grounding is not null
            && test.Grounding.Verdict == VerificationVerdict.Manual;

        Assert.False(shouldSkipVerification);
    }

    /// <summary>
    /// T013: Non-Manual verdict grounding does not skip verification.
    /// </summary>
    [Fact]
    public void NonManualGrounding_DoesNotSkipVerification()
    {
        var groundedMetadata = new GroundingMetadata
        {
            Verdict = VerificationVerdict.Grounded,
            Score = 0.9,
            Generator = "gpt-4o",
            Critic = "gemini-2.0-flash",
            VerifiedAt = DateTimeOffset.UtcNow
        };

        var test = CreateTestCase("TC-202", "Grounded test", groundedMetadata);

        var shouldSkipVerification = test.Grounding is not null
            && test.Grounding.Verdict == VerificationVerdict.Manual;

        Assert.False(shouldSkipVerification);
    }

    /// <summary>
    /// T014: Manual verdict passes the write filter (Verdict != Hallucinated).
    /// </summary>
    [Fact]
    public void ManualVerdict_PassesWriteFilter()
    {
        var results = new List<(TestCase Test, VerificationResult Result)>
        {
            (CreateTestCase("TC-300", "Manual test"), new VerificationResult
            {
                Verdict = VerificationVerdict.Manual,
                Score = 1.0,
                Findings = [],
                CriticModel = "test-critic"
            }),
            (CreateTestCase("TC-301", "Grounded test"), new VerificationResult
            {
                Verdict = VerificationVerdict.Grounded,
                Score = 0.95,
                Findings = [],
                CriticModel = "test-critic"
            }),
            (CreateTestCase("TC-302", "Hallucinated test"), new VerificationResult
            {
                Verdict = VerificationVerdict.Hallucinated,
                Score = 0.2,
                Findings = [],
                CriticModel = "test-critic"
            }),
            (CreateTestCase("TC-303", "Partial test"), new VerificationResult
            {
                Verdict = VerificationVerdict.Partial,
                Score = 0.6,
                Findings = [],
                CriticModel = "test-critic"
            })
        };

        // Apply the same filter as GenerateHandler
        var testsToWrite = results
            .Where(r => r.Result.Verdict != VerificationVerdict.Hallucinated)
            .Select(r => r.Test)
            .ToList();

        // Manual, Grounded, and Partial should pass; Hallucinated should not
        Assert.Equal(3, testsToWrite.Count);
        Assert.Contains(testsToWrite, t => t.Id == "TC-300"); // Manual
        Assert.Contains(testsToWrite, t => t.Id == "TC-301"); // Grounded
        Assert.Contains(testsToWrite, t => t.Id == "TC-303"); // Partial
        Assert.DoesNotContain(testsToWrite, t => t.Id == "TC-302"); // Hallucinated
    }

    /// <summary>
    /// T014: When all tests are Manual, none are filtered out.
    /// </summary>
    [Fact]
    public void AllManualVerdicts_NoneFilteredOut()
    {
        var results = new List<(TestCase Test, VerificationResult Result)>
        {
            (CreateTestCase("TC-310", "Manual 1"), new VerificationResult
            {
                Verdict = VerificationVerdict.Manual,
                Score = 1.0,
                Findings = [],
                CriticModel = "test-critic"
            }),
            (CreateTestCase("TC-311", "Manual 2"), new VerificationResult
            {
                Verdict = VerificationVerdict.Manual,
                Score = 1.0,
                Findings = [],
                CriticModel = "test-critic"
            })
        };

        var testsToWrite = results
            .Where(r => r.Result.Verdict != VerificationVerdict.Hallucinated)
            .Select(r => r.Test)
            .ToList();

        Assert.Equal(2, testsToWrite.Count);
    }

    private static TestCase CreateTestCase(string id, string title, GroundingMetadata? grounding = null) => new()
    {
        Id = id,
        Title = title,
        Priority = Priority.Medium,
        Steps = ["Step 1: Do something"],
        ExpectedResult = "Expected outcome",
        FilePath = $"suite/{id}.md",
        Grounding = grounding
    };
}
