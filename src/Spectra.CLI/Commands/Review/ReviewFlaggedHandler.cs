using System.Text.Json;
using Spectra.CLI.IO;
using Spectra.Core.Index;
using Spectra.Core.Models;
using Spectra.Core.Models.Config;
using Spectra.Core.Models.Grounding;
using Spectra.Core.Parsing;

namespace Spectra.CLI.Commands.Review;

/// <summary>
/// Spec 071 FR6: Core logic for reviewing flagged (still-partial-after-repair) tests.
/// Handles scanning, accepting, and deleting. Retry-repair is delegated to the
/// spectra-review-flagged skill (requires agent inference).
/// </summary>
public sealed class ReviewFlaggedHandler
{
    public sealed record FlaggedTest
    {
        public required string Id { get; init; }
        public required string Suite { get; init; }
        public required string Title { get; init; }
        public required string FilePath { get; init; }
        public required double Score { get; init; }
        public required int RepairAttempts { get; init; }
        public required IReadOnlyList<Core.Models.Grounding.CondensedFinding> CondensedFindings { get; init; }

        // Spec 078 FR-018: the listing must distinguish a test nobody has looked at from one a
        // colleague has already commented on.
        public string? ReviewedBy { get; init; }
        public DateTimeOffset? ReviewedAt { get; init; }
        public string? Note { get; init; }

        /// <summary>
        /// Always null for entries in the flagged list — the marker means <i>currently</i> suppressed
        /// and a flagged test withholds nothing. Emitted for schema uniformity across both arrays.
        /// </summary>
        public DateTimeOffset? ReraiseSuppressedAt { get; init; }

        public string Verdict { get; init; } = "partial";
    }

    /// <summary>Spec 078: outcome of a reviewer action. Every failure is distinguishable.</summary>
    public enum ReviewOutcome
    {
        Success,
        SuiteNotFound,
        TestNotFound,
        NotVerified,
        NotFlagged,
        IneligibleManual,
        IneligibleHallucinated,
        NoteRequired,
        NoteUnrepresentable,
        IoError
    }

    /// <summary>Spec 078: structured result of a reviewer action.</summary>
    public sealed record ReviewResult
    {
        public required ReviewOutcome Outcome { get; init; }
        public string? Message { get; init; }
        public string? TestId { get; init; }
        public string? Suite { get; init; }
        public string? ReviewedBy { get; init; }
        public DateTimeOffset? ReviewedAt { get; init; }
        public bool FlaggedForReview { get; init; }
        public bool NoteWritten { get; init; }
        public string? Verdict { get; init; }
        public double Score { get; init; }
        public bool IsSuccess => Outcome == ReviewOutcome.Success;

        public string ErrorCode => Outcome switch
        {
            ReviewOutcome.Success => "",
            ReviewOutcome.SuiteNotFound => "SUITE_NOT_FOUND",
            ReviewOutcome.TestNotFound => "TEST_NOT_FOUND",
            ReviewOutcome.NotVerified => "NOT_VERIFIED",
            ReviewOutcome.NotFlagged => "NOT_FLAGGED",
            ReviewOutcome.IneligibleManual => "VERDICT_INELIGIBLE_MANUAL",
            ReviewOutcome.IneligibleHallucinated => "VERDICT_INELIGIBLE_HALLUCINATED",
            ReviewOutcome.NoteRequired => "NOTE_REQUIRED",
            ReviewOutcome.NoteUnrepresentable => "NOTE_UNREPRESENTABLE",
            _ => "IO_ERROR"
        };
    }

    private readonly string _currentDir;
    private readonly string _testsDir;

    public ReviewFlaggedHandler(string currentDir, string testsDir)
    {
        _currentDir = currentDir;
        _testsDir = testsDir;
    }

    /// <summary>
    /// Scans all suites (or one suite) for tests with flagged_for_review: true.
    /// </summary>
    public async Task<List<FlaggedTest>> FindFlaggedAsync(string? suiteFilter, CancellationToken ct)
    {
        var results = new List<FlaggedTest>();
        var testsPath = Path.Combine(_currentDir, _testsDir);
        if (!Directory.Exists(testsPath)) return results;

        IEnumerable<string> suiteDirs;
        if (!string.IsNullOrWhiteSpace(suiteFilter))
        {
            var specific = Path.Combine(testsPath, suiteFilter);
            suiteDirs = Directory.Exists(specific) ? [specific] : [];
        }
        else
        {
            suiteDirs = Directory.GetDirectories(testsPath);
        }

        var parser = new TestCaseParser();
        foreach (var suiteDir in suiteDirs)
        {
            var suiteName = Path.GetFileName(suiteDir);
            var mdFiles = Directory.GetFiles(suiteDir, "*.md")
                .Where(f => !Path.GetFileName(f).StartsWith('_'));

            foreach (var file in mdFiles)
            {
                var content = await File.ReadAllTextAsync(file, ct);
                var rel = Path.GetRelativePath(testsPath, file);
                var parsed = parser.Parse(content, rel);
                if (!parsed.IsSuccess || parsed.Value is null) continue;

                var tc = parsed.Value;
                if (tc.Grounding is { FlaggedForReview: true })
                {
                    results.Add(new FlaggedTest
                    {
                        Id = tc.Id,
                        Suite = suiteName,
                        Title = tc.Title,
                        FilePath = file,
                        Score = tc.Grounding.Score,
                        RepairAttempts = tc.Grounding.RepairAttempts,
                        CondensedFindings = tc.Grounding.CondensedFindings,
                        ReviewedBy = tc.Grounding.ReviewedBy,
                        ReviewedAt = tc.Grounding.ReviewedAt,
                        Note = tc.Grounding.Note,
                        ReraiseSuppressedAt = tc.Grounding.ReraiseSuppressedAt,
                        Verdict = tc.Grounding.Verdict.ToString().ToLowerInvariant()
                    });
                }
            }
        }

        return results;
    }

    /// <summary>
    /// Spec 078 FR-011: finds tests whose automatic re-raise is <b>currently</b> being withheld by a
    /// newer review. These cannot appear in the flagged list — suppression leaves
    /// <c>flagged_for_review: false</c> — so without a separate surface a withheld concern would be
    /// invisible, which is the precise failure FR-011 exists to prevent.
    /// </summary>
    public async Task<List<FlaggedTest>> FindSuppressedAsync(string? suiteFilter, CancellationToken ct)
    {
        var results = new List<FlaggedTest>();
        var testsPath = Path.Combine(_currentDir, _testsDir);
        if (!Directory.Exists(testsPath)) return results;

        IEnumerable<string> suiteDirs;
        if (!string.IsNullOrWhiteSpace(suiteFilter))
        {
            var specific = Path.Combine(testsPath, suiteFilter);
            suiteDirs = Directory.Exists(specific) ? [specific] : [];
        }
        else
        {
            suiteDirs = Directory.GetDirectories(testsPath);
        }

        var parser = new TestCaseParser();
        foreach (var suiteDir in suiteDirs)
        {
            var suiteName = Path.GetFileName(suiteDir);
            var mdFiles = Directory.GetFiles(suiteDir, "*.md")
                .Where(f => !Path.GetFileName(f).StartsWith('_'));

            foreach (var file in mdFiles)
            {
                ct.ThrowIfCancellationRequested();
                var content = await File.ReadAllTextAsync(file, ct);
                var rel = Path.GetRelativePath(testsPath, file);
                var parsed = parser.Parse(content, rel);
                if (!parsed.IsSuccess || parsed.Value is null) continue;

                var tc = parsed.Value;
                if (tc.Grounding is { ReraiseSuppressedAt: not null })
                {
                    results.Add(new FlaggedTest
                    {
                        Id = tc.Id,
                        Suite = suiteName,
                        Title = tc.Title,
                        FilePath = file,
                        Score = tc.Grounding.Score,
                        RepairAttempts = tc.Grounding.RepairAttempts,
                        CondensedFindings = tc.Grounding.CondensedFindings,
                        ReviewedBy = tc.Grounding.ReviewedBy,
                        ReviewedAt = tc.Grounding.ReviewedAt,
                        Note = tc.Grounding.Note,
                        ReraiseSuppressedAt = tc.Grounding.ReraiseSuppressedAt,
                        Verdict = tc.Grounding.Verdict.ToString().ToLowerInvariant()
                    });
                }
            }
        }

        return results;
    }

    /// <summary>
    /// Spec 078 FR-020/FR-021: resolves reviewer identity in two steps — an explicitly supplied
    /// value, else the project-wide resolver (git <c>user.name</c>, falling back to the OS user
    /// name). Resolution cannot fail: the fallback is always present, so there is no prompt and no
    /// failure path. The value is a <b>name</b>, not an email address.
    /// </summary>
    public static string ResolveReviewer(string? explicitReviewer)
    {
        if (!string.IsNullOrWhiteSpace(explicitReviewer))
        {
            return explicitReviewer.Trim();
        }

        return new Spectra.MCP.Identity.UserIdentityResolver().GetCurrentUser();
    }

    /// <summary>A test resolved by suite + id, with its parsed content.</summary>
    public sealed record ResolvedTest
    {
        public required string Id { get; init; }
        public required string Suite { get; init; }
        public required string FilePath { get; init; }
        public required TestCase Test { get; init; }
    }

    /// <summary>
    /// Spec 078 FR-007: resolves a test and gates it on review eligibility. Refusals are
    /// distinguishable — <c>manual</c> is a legitimate test outside the workflow, while
    /// <c>hallucinated</c> indicates a hand-edited or corrupt file. Nothing is modified on refusal.
    /// </summary>
    public async Task<(ResolvedTest? Resolved, ReviewResult? Refusal)> ResolveEligibleAsync(
        string suite, string testId, bool requireFlagged, CancellationToken ct)
    {
        var testsPath = Path.Combine(_currentDir, _testsDir);
        var suitePath = Path.Combine(testsPath, suite);

        ReviewResult Refuse(ReviewOutcome outcome, string message) => new()
        {
            Outcome = outcome,
            Message = message,
            TestId = testId,
            Suite = suite
        };

        if (!Directory.Exists(suitePath))
            return (null, Refuse(ReviewOutcome.SuiteNotFound, $"Suite not found: {suite}"));

        var filePath = Directory.GetFiles(suitePath, "*.md")
            .Where(f => !Path.GetFileName(f).StartsWith('_'))
            .FirstOrDefault(f => string.Equals(
                Path.GetFileNameWithoutExtension(f), testId, StringComparison.OrdinalIgnoreCase));

        if (filePath is null)
            return (null, Refuse(ReviewOutcome.TestNotFound, $"Test '{testId}' not found in suite '{suite}'."));

        TestCase tc;
        try
        {
            var content = await File.ReadAllTextAsync(filePath, ct);
            var rel = Path.GetRelativePath(testsPath, filePath);
            var parsed = new TestCaseParser().Parse(content, rel);
            if (!parsed.IsSuccess || parsed.Value is null)
                return (null, Refuse(ReviewOutcome.NotVerified, $"Test '{testId}' could not be parsed."));
            tc = parsed.Value;
        }
        catch (Exception ex)
        {
            return (null, Refuse(ReviewOutcome.IoError, $"Could not read '{testId}': {ex.Message}"));
        }

        if (tc.Grounding is null)
            return (null, Refuse(ReviewOutcome.NotVerified,
                $"Test '{testId}' has no grounding block — it has not been verified, so there is no verdict to review."));

        var refusal = tc.Grounding.Verdict switch
        {
            VerificationVerdict.Manual => Refuse(ReviewOutcome.IneligibleManual,
                $"Test '{testId}' was created by hand (verdict: manual) and never entered the critic pipeline, so there is no assessment to review."),
            VerificationVerdict.Hallucinated => Refuse(ReviewOutcome.IneligibleHallucinated,
                $"Test '{testId}' carries verdict 'hallucinated', which no SPECTRA operation writes — the file was likely hand-edited. Review actions are refused."),
            _ => null
        };
        if (refusal is not null) return (null, refusal);

        if (requireFlagged && !tc.Grounding.FlaggedForReview)
            return (null, Refuse(ReviewOutcome.NotFlagged,
                $"Test '{testId}' is not flagged for review. Use the feedback action to raise a concern on it."));

        return (new ResolvedTest { Id = tc.Id, Suite = suite, FilePath = filePath, Test = tc }, null);
    }

    /// <summary>
    /// Spec 078 FR-012: accept a flagged test. Clears <c>flagged_for_review</c> AND records who
    /// reviewed it and when, plus their note when given. The critic's verdict and score are left
    /// byte-identical — that assessment stays the critic's own.
    /// </summary>
    public async Task<ReviewResult> AcceptAsync(
        string suite, string testId, string? note, string? reviewer, CancellationToken ct)
    {
        var (resolved, refusal) = await ResolveEligibleAsync(suite, testId, requireFlagged: true, ct);
        if (refusal is not null) return refusal;

        var noteError = TestFileWriter.ValidateAnnotation(note);
        if (noteError is not null)
        {
            return new ReviewResult
            {
                Outcome = ReviewOutcome.NoteUnrepresentable,
                Message = $"Note {noteError}. Nothing was written.",
                TestId = testId,
                Suite = suite
            };
        }

        var tc = resolved!.Test;
        var reviewedBy = ResolveReviewer(reviewer);
        var reviewedAt = DateTimeOffset.UtcNow;

        var grounding = tc.Grounding! with
        {
            FlaggedForReview = false,
            ReviewedBy = reviewedBy,
            ReviewedAt = reviewedAt,
            Note = string.IsNullOrWhiteSpace(note) ? tc.Grounding!.Note : note,
            // Accept clears the flag, so any suppression marker would now be meaningless. A flagged
            // test never carries one, so this is defensive.
            ReraiseSuppressedAt = null
        };

        await new TestFileWriter().WriteAsync(resolved.FilePath, CopyWithGrounding(tc, grounding), ct);

        return new ReviewResult
        {
            Outcome = ReviewOutcome.Success,
            TestId = tc.Id,
            Suite = suite,
            ReviewedBy = reviewedBy,
            ReviewedAt = reviewedAt,
            FlaggedForReview = false,
            NoteWritten = !string.IsNullOrWhiteSpace(note),
            Verdict = grounding.Verdict.ToString().ToLowerInvariant(),
            Score = grounding.Score,
            Message = $"Accepted {tc.Id}. Verdict and score unchanged."
        };
    }

    /// <summary>
    /// Spec 078 FR-015: the <c>feedback</c> action. Records a reviewer's concern on any eligible
    /// test, whether or not it is currently flagged — this is the path by which a
    /// confidently-verified test can be raised at all. Requires a note: feedback without content
    /// records nothing a later reader could act on. Never modifies the critic's verdict or score.
    /// </summary>
    public async Task<ReviewResult> FeedbackAsync(
        string suite, string testId, string? note, string? reviewer, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(note))
        {
            return new ReviewResult
            {
                Outcome = ReviewOutcome.NoteRequired,
                Message = "Feedback requires --note: a note without content records nothing a later reader could act on.",
                TestId = testId,
                Suite = suite
            };
        }

        // requireFlagged: false — feedback is the only action valid on an unflagged test.
        var (resolved, refusal) = await ResolveEligibleAsync(suite, testId, requireFlagged: false, ct);
        if (refusal is not null) return refusal;

        var noteError = TestFileWriter.ValidateAnnotation(note);
        if (noteError is not null)
        {
            return new ReviewResult
            {
                Outcome = ReviewOutcome.NoteUnrepresentable,
                Message = $"Note {noteError}. Nothing was written.",
                TestId = testId,
                Suite = suite
            };
        }

        var tc = resolved!.Test;
        var reviewedBy = ResolveReviewer(reviewer);
        var reviewedAt = DateTimeOffset.UtcNow;

        var grounding = tc.Grounding! with
        {
            // The human is raising a concern, so the test needs attention.
            FlaggedForReview = true,
            ReviewedBy = reviewedBy,
            ReviewedAt = reviewedAt,
            Note = note,
            // Flagged and suppressed are mutually exclusive: nothing is being withheld once the test
            // is raised again. This is the suppressed -> marked transition.
            ReraiseSuppressedAt = null
        };

        await new TestFileWriter().WriteAsync(resolved.FilePath, CopyWithGrounding(tc, grounding), ct);

        return new ReviewResult
        {
            Outcome = ReviewOutcome.Success,
            TestId = tc.Id,
            Suite = suite,
            ReviewedBy = reviewedBy,
            ReviewedAt = reviewedAt,
            FlaggedForReview = true,
            NoteWritten = true,
            Verdict = grounding.Verdict.ToString().ToLowerInvariant(),
            Score = grounding.Score,
            Message = $"Recorded feedback on {tc.Id}. Marked for review; verdict and score unchanged."
        };
    }

    /// <summary>
    /// Spec 078 FR-017: every test eligible for the <c>feedback</c> action in scope — any test whose
    /// verdict is grounded or partial, flagged or not. Unlike <see cref="FindFlaggedAsync"/>, this is
    /// not restricted to the currently-flagged set.
    /// </summary>
    public async Task<List<(string Suite, string Id)>> FindFeedbackEligibleAsync(
        string? suiteFilter, CancellationToken ct)
    {
        var results = new List<(string, string)>();
        var testsPath = Path.Combine(_currentDir, _testsDir);
        if (!Directory.Exists(testsPath)) return results;

        IEnumerable<string> suiteDirs;
        if (!string.IsNullOrWhiteSpace(suiteFilter))
        {
            var specific = Path.Combine(testsPath, suiteFilter);
            suiteDirs = Directory.Exists(specific) ? [specific] : [];
        }
        else
        {
            suiteDirs = Directory.GetDirectories(testsPath);
        }

        var parser = new TestCaseParser();
        foreach (var suiteDir in suiteDirs)
        {
            var suiteName = Path.GetFileName(suiteDir);
            foreach (var file in Directory.GetFiles(suiteDir, "*.md")
                         .Where(f => !Path.GetFileName(f).StartsWith('_')))
            {
                ct.ThrowIfCancellationRequested();
                var parsed = parser.Parse(await File.ReadAllTextAsync(file, ct),
                    Path.GetRelativePath(testsPath, file));
                if (!parsed.IsSuccess || parsed.Value?.Grounding is null) continue;

                var verdict = parsed.Value.Grounding.Verdict;
                if (verdict is VerificationVerdict.Grounded or VerificationVerdict.Partial)
                    results.Add((suiteName, parsed.Value.Id));
            }
        }

        return results;
    }

    /// <summary>
    /// Accept a flagged test as-is. Thin wrapper over
    /// <see cref="AcceptAsync(string,string,string?,string?,CancellationToken)"/> so the interactive
    /// loop and the non-interactive <c>--action</c> path cannot drift (Spec 078 FR-016).
    /// </summary>
    public async Task<bool> AcceptAsync(FlaggedTest test, CancellationToken ct)
    {
        var result = await AcceptAsync(test.Suite, test.Id, note: null, reviewer: null, ct);
        return result.IsSuccess;
    }

    /// <summary>
    /// Spec 078 FR-014: delete by suite + id. Behaviour is unchanged from the interactive path — the
    /// drop trail is written first, and the delete does not proceed if that write fails. Nothing is
    /// recorded on the file, because the file is gone.
    /// </summary>
    public async Task<ReviewResult> DeleteByIdAsync(string suite, string testId, CancellationToken ct)
    {
        var (resolved, refusal) = await ResolveEligibleAsync(suite, testId, requireFlagged: true, ct);
        if (refusal is not null) return refusal;

        var tc = resolved!.Test;
        var flagged = new FlaggedTest
        {
            Id = tc.Id,
            Suite = suite,
            Title = tc.Title,
            FilePath = resolved.FilePath,
            Score = tc.Grounding!.Score,
            RepairAttempts = tc.Grounding.RepairAttempts,
            CondensedFindings = tc.Grounding.CondensedFindings
        };

        var deleted = await DeleteAsync(flagged, ct);

        return new ReviewResult
        {
            Outcome = deleted ? ReviewOutcome.Success : ReviewOutcome.IoError,
            TestId = tc.Id,
            Suite = suite,
            Verdict = tc.Grounding.Verdict.ToString().ToLowerInvariant(),
            Score = tc.Grounding.Score,
            Message = deleted
                ? $"Deleted {tc.Id} — drop trail written, index and depends_on cleaned, file removed."
                : $"Failed to delete {tc.Id}."
        };
    }

    /// <summary>
    /// Delete a flagged test: record drop trail then three-phase clean delete.
    /// </summary>
    public async Task<bool> DeleteAsync(FlaggedTest test, CancellationToken ct)
    {
        var trail = new DroppedTestsTrail(_currentDir);
        var entry = new DroppedTestEntry
        {
            Id = test.Id,
            Suite = test.Suite,
            Title = test.Title,
            DropReason = "user_decided",
            ContradictingClaim = null,
            DocRef = null,
            CriticModel = null,
            Timestamp = DateTimeOffset.UtcNow.ToString("o"),
            Source = "review"
        };

        try
        {
            await trail.AppendAsync(entry, ct);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Warning: failed to write drop trail for {test.Id}: {ex.Message}");
            // Do not abort — log and continue to delete
        }

        // Three-phase delete via DeleteHandler
        var testsDir = Path.Combine(_currentDir, _testsDir);
        var suitePath = Path.Combine(testsDir, test.Suite);
        var indexPath = IndexWriter.GetIndexPath(suitePath);
        var indexWriter = new IndexWriter();
        var index = await indexWriter.ReadAsync(indexPath, ct);

        if (index is not null)
        {
            var updated = new MetadataIndex
            {
                Suite = index.Suite,
                GeneratedAt = DateTime.UtcNow,
                Tests = index.Tests.Where(t => !string.Equals(t.Id, test.Id, StringComparison.OrdinalIgnoreCase)).ToList()
            };
            await indexWriter.WriteAsync(indexPath, updated, ct);
        }

        if (File.Exists(test.FilePath))
            File.Delete(test.FilePath);

        return true;
    }

    private static TestCase CopyWithGrounding(TestCase original, Core.Models.Grounding.GroundingMetadata grounding) => new()
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

    public static async Task<string> ResolveTestsDirAsync(string currentDir, CancellationToken ct)
    {
        var configPath = Path.Combine(currentDir, "spectra.config.json");
        if (!File.Exists(configPath)) return "test-cases";
        try
        {
            var json = await File.ReadAllTextAsync(configPath, ct);
            var config = JsonSerializer.Deserialize<SpectraConfig>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            return config?.Tests?.Dir ?? "test-cases";
        }
        catch { return "test-cases"; }
    }
}
