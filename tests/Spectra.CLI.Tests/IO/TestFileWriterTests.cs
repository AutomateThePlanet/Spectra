using Spectra.CLI.IO;
using Spectra.Core.Models;
using Spectra.Core.Models.Grounding;

namespace Spectra.CLI.Tests.IO;

public class TestFileWriterTests
{
    private readonly TestFileWriter _writer = new();

    [Fact]
    public void FormatTestCase_WithGrounding_IncludesGroundingSection()
    {
        var testCase = CreateTestCase(
            grounding: new GroundingMetadata
            {
                Verdict = VerificationVerdict.Grounded,
                Score = 0.95,
                Generator = "claude-sonnet-4",
                Critic = "gemini-2.0-flash",
                VerifiedAt = new DateTimeOffset(2026, 3, 19, 10, 30, 0, TimeSpan.Zero),
                UnverifiedClaims = []
            }
        );

        var content = _writer.FormatTestCase(testCase);

        Assert.Contains("grounding:", content);
        Assert.Contains("verdict: grounded", content);
        Assert.Contains("score: 0.95", content);
        Assert.Contains("generator: claude-sonnet-4", content);
        Assert.Contains("critic: gemini-2.0-flash", content);
        Assert.Contains("verified_at: 2026-03-19T10:30:00Z", content);
    }

    [Fact]
    public void FormatTestCase_WithPartialVerdict_IncludesUnverifiedClaims()
    {
        var testCase = CreateTestCase(
            grounding: new GroundingMetadata
            {
                Verdict = VerificationVerdict.Partial,
                Score = 0.72,
                Generator = "gpt-4o",
                Critic = "gemini-2.0-flash",
                VerifiedAt = DateTimeOffset.UtcNow,
                UnverifiedClaims = ["Email sent within 5 minutes", "Mobile notification appears"]
            }
        );

        var content = _writer.FormatTestCase(testCase);

        Assert.Contains("verdict: partial", content);
        Assert.Contains("unverified_claims:", content);
        Assert.Contains("Email sent within 5 minutes", content);
        Assert.Contains("Mobile notification appears", content);
    }

    [Fact]
    public void FormatTestCase_WithHallucinatedVerdict_FormatsCorrectly()
    {
        var testCase = CreateTestCase(
            grounding: new GroundingMetadata
            {
                Verdict = VerificationVerdict.Hallucinated,
                Score = 0.25,
                Generator = "claude-sonnet-4",
                Critic = "gpt-4o-mini",
                VerifiedAt = DateTimeOffset.UtcNow,
                UnverifiedClaims = []
            }
        );

        var content = _writer.FormatTestCase(testCase);

        Assert.Contains("verdict: hallucinated", content);
        Assert.Contains("score: 0.25", content);
    }

    [Fact]
    public void FormatTestCase_WithoutGrounding_NoGroundingSection()
    {
        var testCase = CreateTestCase();

        var content = _writer.FormatTestCase(testCase);

        Assert.DoesNotContain("grounding:", content);
        Assert.DoesNotContain("verdict:", content);
        Assert.DoesNotContain("critic:", content);
    }

    [Fact]
    public void FormatTestCase_GroundingAndOtherFields_AllPresent()
    {
        var testCase = new TestCase
        {
            Id = "TC-001",
            Title = "Test Title",
            Priority = Priority.High,
            Steps = ["Step 1", "Step 2"],
            ExpectedResult = "Expected result",
            FilePath = "suite/TC-001.md",
            Tags = ["smoke", "api"],
            Component = "auth",
            SourceRefs = ["docs/auth.md"],
            Grounding = new GroundingMetadata
            {
                Verdict = VerificationVerdict.Grounded,
                Score = 0.98,
                Generator = "claude-sonnet-4",
                Critic = "gemini-2.0-flash",
                VerifiedAt = DateTimeOffset.UtcNow,
                UnverifiedClaims = []
            }
        };

        var content = _writer.FormatTestCase(testCase);

        // Verify other fields
        Assert.Contains("id: TC-001", content);
        Assert.Contains("priority: high", content);
        Assert.Contains("tags:", content);
        Assert.Contains("- smoke", content);
        Assert.Contains("component: auth", content);
        Assert.Contains("source_refs:", content);

        // Verify grounding
        Assert.Contains("grounding:", content);
        Assert.Contains("verdict: grounded", content);
    }

    [Fact]
    public void FormatTestCase_UnverifiedClaimsWithQuotes_EscapesCorrectly()
    {
        var testCase = CreateTestCase(
            grounding: new GroundingMetadata
            {
                Verdict = VerificationVerdict.Partial,
                Score = 0.65,
                Generator = "test-gen",
                Critic = "test-critic",
                VerifiedAt = DateTimeOffset.UtcNow,
                UnverifiedClaims = ["User sees \"Welcome\" message"]
            }
        );

        var content = _writer.FormatTestCase(testCase);

        // Should escape the internal quotes
        Assert.Contains("unverified_claims:", content);
        Assert.Contains("\\\"Welcome\\\"", content);
    }

    [Fact]
    public void FormatTestCase_ScoreFormatting_TwoDecimalPlaces()
    {
        var testCase = CreateTestCase(
            grounding: new GroundingMetadata
            {
                Verdict = VerificationVerdict.Grounded,
                Score = 0.9,
                Generator = "test-gen",
                Critic = "test-critic",
                VerifiedAt = DateTimeOffset.UtcNow,
                UnverifiedClaims = []
            }
        );

        var content = _writer.FormatTestCase(testCase);

        // Score should be formatted with 2 decimal places
        Assert.Contains("score: 0.90", content);
    }

    [Fact]
    public async Task WriteAsync_WithGrounding_CreatesValidFile()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var filePath = Path.Combine(tempDir, "TC-001.md");

        try
        {
            var testCase = CreateTestCase(
                grounding: new GroundingMetadata
                {
                    Verdict = VerificationVerdict.Grounded,
                    Score = 0.95,
                    Generator = "claude-sonnet-4",
                    Critic = "gemini-2.0-flash",
                    VerifiedAt = new DateTimeOffset(2026, 3, 19, 10, 30, 0, TimeSpan.Zero),
                    UnverifiedClaims = []
                }
            );

            await _writer.WriteAsync(filePath, testCase);

            Assert.True(File.Exists(filePath));
            var content = await File.ReadAllTextAsync(filePath);
            Assert.Contains("grounding:", content);
            Assert.Contains("verdict: grounded", content);
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }
    }

    [Fact]
    public void GetFilePath_ReturnsCorrectPath()
    {
        var result = TestFileWriter.GetFilePath("test-cases", "auth", "TC-001");

        Assert.Equal(Path.Combine("test-cases", "auth", "TC-001.md"), result);
    }

    private static TestCase CreateTestCase(GroundingMetadata? grounding = null) => new()
    {
        Id = "TC-001",
        Title = "Test Title",
        Priority = Priority.High,
        Steps = ["Step 1", "Step 2"],
        ExpectedResult = "Expected result",
        FilePath = "suite/TC-001.md",
        Grounding = grounding
    };
}
