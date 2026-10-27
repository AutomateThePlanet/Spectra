using System.CommandLine;
using System.Text.Json;
using Spectra.CLI.Infrastructure;
using Spectra.CLI.Options;

namespace Spectra.CLI.Commands.Review;

/// <summary>
/// Spec 071 FR6: <c>spectra ai review-flagged</c>. Lists tests with flagged_for_review: true
/// and allows per-test accept or delete. Retry-repair is delegated to the spectra-review-flagged
/// skill (requires agent inference).
/// Exit 0 = success (no flagged tests, or all dispositioned).
/// Exit 2 = flagged tests remain undisposed (--no-interaction or user quit early).
/// </summary>
public sealed class ReviewFlaggedCommand : Command
{
    private const int ExitSuccess = 0;
    private const int ExitError = 1;
    private const int ExitUndisposed = 2;

    // Spec 103: invalid option combinations and unrepresentable notes share the validation code;
    // not-found and ineligible-verdict refusals share the not-found code, disambiguated by `error`.
    private const int ExitValidation = 2;
    private const int ExitNotFound = 4;

    public ReviewFlaggedCommand()
        : base("review-flagged", "Review and disposition partial tests flagged for human review (Spec 071)")
    {
        var suiteOption = new Option<string?>(["--suite", "-s"], "Scope to one suite (default: all suites)");
        var noInteractionFlag = new Option<bool>("--no-interaction", "List flagged tests and exit without disposition");

        // Spec 103 FR-016: every action must be invocable non-interactively. Before this, accept and
        // delete were reachable only as keystrokes inside the interactive loop, which no automated or
        // chat-driven flow could reach.
        var actionOption = new Option<string?>("--action",
            "Action to perform non-interactively: accept | delete (feedback and retry-repair per Spec 103)");
        var testOption = new Option<string?>(["--test", "-t"], "Target test id (required with --action unless --all)");
        var allFlag = new Option<bool>("--all", "Apply the action to every eligible test in scope");
        var noteOption = new Option<string?>("--note", "Reviewer's note recorded on the grounding block");
        var reviewerOption = new Option<string?>("--reviewer", "Explicit reviewer identity (default: git user.name)");
        // Spec 103 FR-015: --fix records the review and returns the repair chain. It does NOT run the
        // repair: that needs inference, and Principle III forbids an in-process model.
        var fixFlag = new Option<bool>("--fix",
            "feedback only: also request a repair. Records the review and emits the repair chain for the skill to drive");

        AddOption(suiteOption);
        AddOption(noInteractionFlag);
        AddOption(actionOption);
        AddOption(testOption);
        AddOption(allFlag);
        AddOption(noteOption);
        AddOption(reviewerOption);
        AddOption(fixFlag);

        this.SetHandler(async (context) =>
        {
            var suite = context.ParseResult.GetValueForOption(suiteOption);
            var noInteraction = context.ParseResult.GetValueForOption(noInteractionFlag);
            var action = context.ParseResult.GetValueForOption(actionOption);
            var testId = context.ParseResult.GetValueForOption(testOption);
            var all = context.ParseResult.GetValueForOption(allFlag);
            var note = context.ParseResult.GetValueForOption(noteOption);
            var reviewer = context.ParseResult.GetValueForOption(reviewerOption);
            var fix = context.ParseResult.GetValueForOption(fixFlag);
            var outputFormat = context.ParseResult.GetValueForOption(GlobalOptions.OutputFormatOption);
            var json = outputFormat == OutputFormat.Json;
            var ct = context.GetCancellationToken();

            context.ExitCode = string.IsNullOrWhiteSpace(action)
                ? await RunAsync(suite, noInteraction, json, ct)
                : await RunActionAsync(suite, action!, testId, all, note, reviewer, fix, json, ct);
        });
    }

    /// <summary>Spec 103 FR-011: JSON projection for a currently-suppressed test.</summary>
    private static object SuppressedProjection(ReviewFlaggedHandler.FlaggedTest t) => new
    {
        id = t.Id,
        suite = t.Suite,
        title = t.Title,
        verdict = t.Verdict,
        score = t.Score,
        condensed_findings = t.CondensedFindings.Select(f => new { element = f.Element, reason = f.Reason }).ToArray(),
        reviewed_by = t.ReviewedBy,
        reviewed_at = t.ReviewedAt?.UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ssZ"),
        note = t.Note,
        reraise_suppressed_at = t.ReraiseSuppressedAt?.UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ssZ")
    };

    private static void EmitSuppressedHuman(List<ReviewFlaggedHandler.FlaggedTest> suppressed)
    {
        if (suppressed.Count == 0) return;

        Console.Out.WriteLine();
        Console.Out.WriteLine($"{suppressed.Count} concern(s) currently withheld by a newer review:");
        foreach (var t in suppressed)
        {
            Console.Out.WriteLine(
                $"  {t.Id} ({t.Suite}) — suppressed {t.ReraiseSuppressedAt?.UtcDateTime:yyyy-MM-dd}, " +
                $"reviewed by {t.ReviewedBy} on {t.ReviewedAt?.UtcDateTime:yyyy-MM-dd}");
        }
    }

    /// <summary>
    /// Spec 103 FR-016: the non-interactive action path. Validates the option combination, then
    /// performs the action through the same handler methods the interactive loop uses.
    /// </summary>
    private static async Task<int> RunActionAsync(
        string? suite, string action, string? testId, bool all, string? note, string? reviewer,
        bool fix, bool json, CancellationToken ct)
    {
        var normalized = action.Trim().ToLowerInvariant();

        int Reject(string code, string message)
        {
            if (json)
                Console.Out.WriteLine(JsonSerializer.Serialize(new { command = "review-flagged", status = "failed", error = code, message }));
            else
                Console.Error.WriteLine($"{code}: {message}");
            return ExitValidation;
        }

        if (normalized is not ("accept" or "delete" or "feedback"))
            return Reject("UNKNOWN_ACTION", $"Unknown action '{action}'. Valid actions: accept, delete, feedback.");

        // Spec 103 FR-015: feedback without content records nothing a later reader could act on.
        if (normalized == "feedback" && string.IsNullOrWhiteSpace(note))
            return Reject("NOTE_REQUIRED", "The feedback action requires --note \"<text>\".");

        if (string.IsNullOrWhiteSpace(testId) && !all)
            return Reject("TARGET_REQUIRED", "Specify --test <id> or --all.");

        if (!string.IsNullOrWhiteSpace(testId) && all)
            return Reject("TARGET_AMBIGUOUS", "--test and --all are mutually exclusive.");

        if (normalized == "delete" && all)
            return Reject("BULK_DELETE_REFUSED",
                "--all is not offered for delete: it is the only irreversible action and --all hides the blast radius. Delete one test at a time with --test.");

        if (fix && normalized != "feedback")
            return Reject("FIX_NOT_APPLICABLE", "--fix applies only to the feedback action.");

        // Spec 103: a repair is one test at a time — --all would flag every test and hand back no chain.
        if (fix && all)
            return Reject("BULK_FIX_REFUSED", "--fix works on one test: use --test <id>.");

        var currentDir = Directory.GetCurrentDirectory();
        var testsDir = await ReviewFlaggedHandler.ResolveTestsDirAsync(currentDir, ct);
        var handler = new ReviewFlaggedHandler(currentDir, testsDir);

        // Resolve the target set. Spec 103 FR-017: feedback may target ANY eligible test, while
        // accept and delete stay restricted to the currently-flagged set — acting on an unmarked test
        // through those two has no defined meaning.
        List<(string Suite, string Id)> targets;
        if (all)
        {
            targets = normalized == "feedback"
                ? await handler.FindFeedbackEligibleAsync(suite, ct)
                : (await handler.FindFlaggedAsync(suite, ct)).Select(t => (t.Suite, t.Id)).ToList();
        }
        else if (!string.IsNullOrWhiteSpace(suite))
        {
            targets = [(suite!, testId!)];
        }
        else
        {
            // Without --suite, locate the test so a caller need not know which suite it lives in.
            var candidates = normalized == "feedback"
                ? await handler.FindFeedbackEligibleAsync(null, ct)
                : (await handler.FindFlaggedAsync(null, ct)).Select(t => (t.Suite, t.Id)).ToList();

            var match = candidates.FirstOrDefault(t => string.Equals(t.Id, testId, StringComparison.OrdinalIgnoreCase));
            if (match.Id is null)
                return Reject("TARGET_REQUIRED", $"Could not locate test '{testId}'. Pass --suite to disambiguate.");
            targets = [match];
        }

        var results = new List<ReviewFlaggedHandler.ReviewResult>();
        foreach (var (targetSuite, targetId) in targets)
        {
            ct.ThrowIfCancellationRequested();
            results.Add(normalized switch
            {
                "accept" => await handler.AcceptAsync(targetSuite, targetId, note, reviewer, ct),
                "feedback" => await handler.FeedbackAsync(targetSuite, targetId, note, reviewer, ct, fix),
                _ => await handler.DeleteByIdAsync(targetSuite, targetId, ct)
            });
        }

        if (results.Count == 0)
        {
            if (json)
                Console.Out.WriteLine(JsonSerializer.Serialize(new
                {
                    command = "review-flagged", status = "completed", action = normalized,
                    suite, mode = "batch", processed = 0, skipped_ineligible = 0, errors = 0,
                    test_ids = Array.Empty<string>(), skipped = Array.Empty<object>()
                }));
            else
                Console.Out.WriteLine($"{normalized}: no eligible tests in scope.");
            return ExitSuccess;
        }

        return EmitActionResults(normalized, suite, all, results, json, fix);
    }

    /// <summary>
    /// Spec 103 FR-015 (research R8): the repair chain <c>--fix</c> hands back. The CLI must NOT run
    /// it — repair requires inference and Principle III forbids an in-process model — so the skill
    /// reads this and drives it. The skill must not reconstruct the chain from memory.
    /// </summary>
    private sealed record NextStep(string reason, string[] chain);

    private static NextStep BuildNextStep(string suite, string testId) => new(
        "fix_requested",
        [
            $"spectra ai compile-repair-prompt --suite {suite} --test {testId} --feedback-from-review",
            "patch in session, write the JSON array to .spectra/repaired.json",
            $"spectra ai ingest-update {suite} --test-id {testId} --from .spectra/repaired.json --keep-baseline",
            $"spectra ai compile-critic-prompt --suite {suite} --test {testId}, then the spectra-critic subagent",
            // Spec 103 (M1): this step also writes the staged review back (ingest-update dropped the block).
            $"spectra ai ingest-grounding --suite {suite} --test {testId} [--repaired when grounded] --repair-attempts 1"
        ]);

    private static int EmitActionResults(
        string action, string? suite, bool batch,
        List<ReviewFlaggedHandler.ReviewResult> results, bool json, bool fix = false)
    {
        var succeeded = results.Where(r => r.IsSuccess).ToList();
        var skipped = results.Where(r => !r.IsSuccess).ToList();

        if (batch)
        {
            if (json)
            {
                Console.Out.WriteLine(JsonSerializer.Serialize(new
                {
                    command = "review-flagged",
                    status = "completed",
                    action,
                    suite,
                    mode = "batch",
                    reviewed_by = succeeded.FirstOrDefault()?.ReviewedBy,
                    processed = succeeded.Count,
                    skipped_ineligible = skipped.Count,
                    errors = 0,
                    test_ids = succeeded.Select(r => r.TestId).ToArray(),
                    skipped = skipped.Select(r => new { test_id = r.TestId, reason = r.ErrorCode }).ToArray()
                }));
            }
            else
            {
                Console.Out.WriteLine($"{action}: {succeeded.Count} processed, {skipped.Count} skipped.");
                foreach (var s in skipped)
                    Console.Out.WriteLine($"  skipped {s.TestId}: {s.ErrorCode} — {s.Message}");
            }

            // A skip inside a batch is reported, never fatal (SC-006).
            return ExitSuccess;
        }

        var single = results[0];
        if (!single.IsSuccess)
        {
            if (json)
                Console.Out.WriteLine(JsonSerializer.Serialize(new
                {
                    command = "review-flagged",
                    status = "failed",
                    action,
                    test_id = single.TestId,
                    suite = single.Suite,
                    error = single.ErrorCode,
                    message = single.Message
                }));
            else
                Console.Error.WriteLine($"{single.ErrorCode}: {single.Message}");

            return single.Outcome switch
            {
                ReviewFlaggedHandler.ReviewOutcome.NoteRequired => ExitValidation,
                ReviewFlaggedHandler.ReviewOutcome.NoteUnrepresentable => ExitValidation,
                ReviewFlaggedHandler.ReviewOutcome.IoError => ExitError,
                _ => ExitNotFound
            };
        }

        if (json)
            Console.Out.WriteLine(JsonSerializer.Serialize(new
            {
                command = "review-flagged",
                status = "completed",
                action,
                suite = single.Suite,
                test_id = single.TestId,
                reviewed_by = single.ReviewedBy,
                reviewed_at = single.ReviewedAt?.UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                flagged_for_review = single.FlaggedForReview,
                note_written = single.NoteWritten,
                verdict = single.Verdict,
                score = single.Score,
                message = single.Message,
                next_step = fix ? BuildNextStep(single.Suite!, single.TestId!) : null
            }));
        else
        {
            Console.Out.WriteLine(single.Message);
            if (fix)
            {
                Console.Out.WriteLine();
                Console.Out.WriteLine("Repair requested. This command does not run the repair — drive the chain:");
                foreach (var step in BuildNextStep(single.Suite!, single.TestId!).chain)
                    Console.Out.WriteLine($"  → {step}");
            }
        }

        return ExitSuccess;
    }

    private static async Task<int> RunAsync(string? suite, bool noInteraction, bool json, CancellationToken ct)
    {
        var currentDir = Directory.GetCurrentDirectory();
        var testsDir = await ReviewFlaggedHandler.ResolveTestsDirAsync(currentDir, ct);
        var handler = new ReviewFlaggedHandler(currentDir, testsDir);

        var flagged = await handler.FindFlaggedAsync(suite, ct);
        // Spec 103 FR-011: suppressed tests are unflagged, so they need their own array or the
        // withheld concerns are invisible here.
        var suppressed = await handler.FindSuppressedAsync(suite, ct);

        if (flagged.Count == 0)
        {
            if (json)
                Console.Out.WriteLine(JsonSerializer.Serialize(new
                {
                    flagged_count = 0,
                    tests = Array.Empty<object>(),
                    suppressed_count = suppressed.Count,
                    suppressed = suppressed.Select(SuppressedProjection).ToArray()
                }));
            else
            {
                Console.Out.WriteLine("No flagged tests found.");
                EmitSuppressedHuman(suppressed);
            }
            return ExitSuccess;
        }

        if (json || noInteraction)
        {
            var output = new
            {
                flagged_count = flagged.Count,
                tests = flagged.Select(t => new
                {
                    id = t.Id,
                    suite = t.Suite,
                    title = t.Title,
                    verdict = t.Verdict,
                    score = t.Score,
                    repair_attempts = t.RepairAttempts,
                    condensed_findings = t.CondensedFindings.Select(f => new { element = f.Element, reason = f.Reason }).ToArray(),
                    // Spec 103 FR-018: distinguishes a test nobody has looked at from one a
                    // colleague has already commented on. reraise_suppressed_at is always null in
                    // this array — a flagged test withholds nothing — and is emitted for schema
                    // uniformity with the `suppressed` array.
                    reviewed_by = t.ReviewedBy,
                    reviewed_at = t.ReviewedAt?.UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                    note = t.Note,
                    reraise_suppressed_at = (string?)null
                }).ToArray(),
                suppressed_count = suppressed.Count,
                suppressed = suppressed.Select(SuppressedProjection).ToArray()
            };
            Console.Out.WriteLine(JsonSerializer.Serialize(output));
            return ExitUndisposed;
        }

        // Interactive mode
        Console.Out.WriteLine($"Found {flagged.Count} flagged test(s) awaiting review.");
        Console.Out.WriteLine();

        var allDispositioned = true;
        foreach (var test in flagged)
        {
            if (ct.IsCancellationRequested) break;

            Console.Out.WriteLine($"── {test.Id}  [partial, score: {test.Score:F2}, attempts: {test.RepairAttempts}]");
            Console.Out.WriteLine($"   Suite: {test.Suite}");
            Console.Out.WriteLine($"   Title: {test.Title}");

            if (test.CondensedFindings.Count > 0)
            {
                Console.Out.WriteLine("   Flagged findings:");
                foreach (var f in test.CondensedFindings)
                    Console.Out.WriteLine($"     • {f.Element} — {f.Reason}");
            }

            Console.Out.WriteLine();
            Console.Out.Write("[A]ccept as-is  [D]elete  [S]kip  [Q]uit: ");
            var key = Console.ReadKey(intercept: true);
            Console.Out.WriteLine();

            switch (char.ToUpperInvariant(key.KeyChar))
            {
                case 'A':
                    var accepted = await handler.AcceptAsync(test, ct);
                    Console.Out.WriteLine(accepted
                        ? $"  ✓ Accepted {test.Id} — flagged_for_review cleared; verdict stays partial."
                        : $"  ✗ Failed to accept {test.Id}.");
                    break;

                case 'D':
                    var deleted = await handler.DeleteAsync(test, ct);
                    Console.Out.WriteLine(deleted
                        ? $"  ✓ Deleted {test.Id} — drop trail written + clean delete complete."
                        : $"  ✗ Failed to delete {test.Id}.");
                    break;

                case 'S':
                    Console.Out.WriteLine($"  → Skipped {test.Id}.");
                    allDispositioned = false;
                    break;

                case 'Q':
                    Console.Out.WriteLine("  → Quit. Remaining tests left undisposed.");
                    allDispositioned = false;
                    goto done;
            }

            Console.Out.WriteLine();
        }

        done:
        if (!allDispositioned)
        {
            Console.Out.WriteLine();
            Console.Out.WriteLine("To retry repair for any flagged test, run:");
            Console.Out.WriteLine("  spectra ai compile-repair-prompt --suite <suite> --test <id>");
            Console.Out.WriteLine("  (then follow the repair flow in the spectra-review-flagged skill)");
            return ExitUndisposed;
        }

        return ExitSuccess;
    }
}
