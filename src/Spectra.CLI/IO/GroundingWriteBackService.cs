using Spectra.CLI.Verification;
using Spectra.Core.Models;
using Spectra.Core.Models.Grounding;
using Spectra.Core.Parsing;

namespace Spectra.CLI.IO;

/// <summary>
/// Reads an existing test from disk, builds GroundingMetadata from a verdict JSON classification,
/// and rewrites the test file with the new grounding block via TestFileWriter.
/// Refuses hallucinated verdicts (exit 4 — those must go through record-drop + delete instead).
/// </summary>
public sealed class GroundingWriteBackService
{
    private readonly TestFileWriter _writer;

    public GroundingWriteBackService(TestFileWriter writer)
    {
        _writer = writer;
    }

    public enum WriteBackOutcome { Success, HallucinatedRefused, TestNotFound, VerdictFailure }

    public sealed record WriteBackResult
    {
        public WriteBackOutcome Outcome { get; init; }
        public string? ErrorMessage { get; init; }
        public GroundingMetadata? Grounding { get; init; }
        public bool IsSuccess => Outcome == WriteBackOutcome.Success;
    }

    /// <summary>
    /// Classifies the verdict JSON, builds GroundingMetadata, and rewrites the test file.
    /// </summary>
    public async Task<WriteBackResult> WriteAsync(
        string testFilePath,
        string verdictJson,
        int repairAttempts = 0,
        bool repaired = false,
        CancellationToken ct = default)
    {
        if (!File.Exists(testFilePath))
            return new WriteBackResult { Outcome = WriteBackOutcome.TestNotFound, ErrorMessage = $"Test file not found: {testFilePath}" };

        var classification = VerdictIngestor.Classify(verdictJson);
        if (!classification.IsSuccess)
            return new WriteBackResult
            {
                Outcome = WriteBackOutcome.VerdictFailure,
                ErrorMessage = string.Join("; ", classification.Errors)
            };

        var result = classification.Result!;

        if (result.Verdict == VerificationVerdict.Hallucinated)
            return new WriteBackResult
            {
                Outcome = WriteBackOutcome.HallucinatedRefused,
                ErrorMessage = "Hallucinated verdict cannot have a grounding block written. Use record-drop + delete instead."
            };

        var content = await File.ReadAllTextAsync(testFilePath, ct);
        var relativePath = Path.GetFileName(testFilePath);
        var parsed = new TestCaseParser().Parse(content, relativePath);
        if (!parsed.IsSuccess || parsed.Value is null)
            return new WriteBackResult { Outcome = WriteBackOutcome.TestNotFound, ErrorMessage = $"Could not parse test file: {testFilePath}" };

        var original = parsed.Value;
        var generator = !string.IsNullOrWhiteSpace(original.Grounding?.Generator)
            ? original.Grounding.Generator
            : "claude-code-session";

        var nonGrounded = result.Findings
            .Where(f => f.Status != FindingStatus.Grounded)
            .Select(f => new CondensedFinding
            {
                Element = f.Element,
                Reason = f.Reason ?? f.Claim
            })
            .ToList();

        var grounding = new GroundingMetadata
        {
            Verdict = result.Verdict,
            Score = result.Score,
            Generator = generator,
            Critic = result.CriticModel,
            VerifiedAt = DateTimeOffset.UtcNow,
            UnverifiedClaims = result.Findings
                .Where(f => f.Status != FindingStatus.Grounded)
                .Select(f => $"{f.Element}: {f.Reason ?? f.Claim}")
                .ToList(),
            FlaggedForReview = result.Verdict == VerificationVerdict.Partial && !repaired,
            RepairAttempts = repairAttempts,
            Repaired = repaired,
            CondensedFindings = nonGrounded
        };

        var updated = CopyWithGrounding(original, grounding);
        await _writer.WriteAsync(testFilePath, updated, ct);

        return new WriteBackResult { Outcome = WriteBackOutcome.Success, Grounding = grounding };
    }

    private static TestCase CopyWithGrounding(TestCase original, GroundingMetadata grounding) => new()
    {
        Id = original.Id,
        FilePath = original.FilePath,
        Priority = original.Priority,
        Tags = original.Tags,
        Component = original.Component,
        Description = original.Description,
        Preconditions = original.Preconditions,
        Environment = original.Environment,
        EstimatedDuration = original.EstimatedDuration,
        DependsOn = original.DependsOn,
        SourceRefs = original.SourceRefs,
        ScenarioFromDoc = original.ScenarioFromDoc,
        RelatedWorkItems = original.RelatedWorkItems,
        Custom = original.Custom,
        AutomatedBy = original.AutomatedBy,
        Requirements = original.Requirements,
        Criteria = original.Criteria,
        Bugs = original.Bugs,
        Status = original.Status,
        OrphanedReason = original.OrphanedReason,
        OrphanedDate = original.OrphanedDate,
        Title = original.Title,
        Steps = original.Steps,
        ExpectedResult = original.ExpectedResult,
        TestData = original.TestData,
        Grounding = grounding
    };
}
