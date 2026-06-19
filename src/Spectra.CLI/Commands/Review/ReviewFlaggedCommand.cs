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

    public ReviewFlaggedCommand()
        : base("review-flagged", "Review and disposition partial tests flagged for human review (Spec 071)")
    {
        var suiteOption = new Option<string?>(["--suite", "-s"], "Scope to one suite (default: all suites)");
        var noInteractionFlag = new Option<bool>("--no-interaction", "List flagged tests and exit without disposition");

        AddOption(suiteOption);
        AddOption(noInteractionFlag);

        this.SetHandler(async (context) =>
        {
            var suite = context.ParseResult.GetValueForOption(suiteOption);
            var noInteraction = context.ParseResult.GetValueForOption(noInteractionFlag);
            var outputFormat = context.ParseResult.GetValueForOption(GlobalOptions.OutputFormatOption);
            context.ExitCode = await RunAsync(suite, noInteraction, outputFormat == OutputFormat.Json,
                context.GetCancellationToken());
        });
    }

    private static async Task<int> RunAsync(string? suite, bool noInteraction, bool json, CancellationToken ct)
    {
        var currentDir = Directory.GetCurrentDirectory();
        var testsDir = await ReviewFlaggedHandler.ResolveTestsDirAsync(currentDir, ct);
        var handler = new ReviewFlaggedHandler(currentDir, testsDir);

        var flagged = await handler.FindFlaggedAsync(suite, ct);

        if (flagged.Count == 0)
        {
            if (json)
                Console.Out.WriteLine(JsonSerializer.Serialize(new { flagged_count = 0, tests = Array.Empty<object>() }));
            else
                Console.Out.WriteLine("No flagged tests found.");
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
                    score = t.Score,
                    repair_attempts = t.RepairAttempts,
                    condensed_findings = t.CondensedFindings.Select(f => new { element = f.Element, reason = f.Reason }).ToArray()
                }).ToArray()
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
