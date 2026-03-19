using Spectre.Console;
using Spectra.Core.Models;
using Spectra.Core.Models.Grounding;

namespace Spectra.CLI.Output;

/// <summary>
/// Presents verification results in the CLI.
/// </summary>
public sealed class VerificationPresenter
{
    /// <summary>
    /// Displays a summary of verification results.
    /// </summary>
    public void ShowSummary(IReadOnlyList<(TestCase Test, VerificationResult Result)> results)
    {
        var grounded = results.Count(r => r.Result.Verdict == VerificationVerdict.Grounded);
        var partial = results.Count(r => r.Result.Verdict == VerificationVerdict.Partial);
        var hallucinated = results.Count(r => r.Result.Verdict == VerificationVerdict.Hallucinated);

        AnsiConsole.WriteLine();

        if (grounded > 0)
        {
            AnsiConsole.MarkupLine($"  {OutputSymbols.SuccessMarkup} [green]{grounded}[/] grounded");
        }

        if (partial > 0)
        {
            AnsiConsole.MarkupLine($"  {OutputSymbols.WarningMarkup} [yellow]{partial}[/] partial — written with grounding warnings");
        }

        if (hallucinated > 0)
        {
            AnsiConsole.MarkupLine($"  {OutputSymbols.ErrorMarkup} [red]{hallucinated}[/] hallucinated — rejected");
        }
    }

    /// <summary>
    /// Displays detailed information about partial verdicts.
    /// </summary>
    public void ShowPartialDetails(IReadOnlyList<(TestCase Test, VerificationResult Result)> results)
    {
        var partials = results.Where(r => r.Result.Verdict == VerificationVerdict.Partial).ToList();

        if (partials.Count == 0)
            return;

        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine($"  {OutputSymbols.InfoMarkup} [cyan]Partial tests (review recommended):[/]");

        foreach (var (test, result) in partials)
        {
            var unverifiedClaims = result.Findings
                .Where(f => f.Status != FindingStatus.Grounded)
                .Take(2) // Show at most 2 unverified claims
                .ToList();

            if (unverifiedClaims.Count > 0)
            {
                var firstClaim = unverifiedClaims[0];
                var reason = firstClaim.Reason ?? firstClaim.Claim;
                AnsiConsole.MarkupLine($"    [dim]{test.Id}[/]  {Markup.Escape(reason)}");
            }
        }
    }

    /// <summary>
    /// Displays detailed information about rejected (hallucinated) tests.
    /// </summary>
    public void ShowRejectedDetails(IReadOnlyList<(TestCase Test, VerificationResult Result)> results)
    {
        var rejected = results.Where(r => r.Result.Verdict == VerificationVerdict.Hallucinated).ToList();

        if (rejected.Count == 0)
            return;

        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine($"  {OutputSymbols.InfoMarkup} [cyan]Rejected tests:[/]");

        foreach (var (test, result) in rejected)
        {
            var hallucinated = result.Findings
                .Where(f => f.Status == FindingStatus.Hallucinated)
                .FirstOrDefault();

            var reason = hallucinated?.Reason ??
                result.Findings.FirstOrDefault(f => f.Reason is not null)?.Reason ??
                "Contains invented content not found in documentation";

            AnsiConsole.MarkupLine($"    [dim]{test.Id}[/]  {Markup.Escape(reason)}");
        }
    }

    /// <summary>
    /// Displays a notice that verification was skipped.
    /// </summary>
    public void ShowSkippedNotice()
    {
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine($"  {OutputSymbols.InfoMarkup} [cyan]Verification skipped (--skip-critic)[/]");
    }

    /// <summary>
    /// Displays a notice that verification is disabled.
    /// </summary>
    public void ShowDisabledNotice()
    {
        // Silent when disabled - no output needed
    }

    /// <summary>
    /// Displays a warning that critic is unavailable.
    /// </summary>
    public void ShowCriticUnavailable(string reason)
    {
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine($"  {OutputSymbols.WarningMarkup} [yellow]Critic unavailable: {Markup.Escape(reason)}[/]");
    }

    /// <summary>
    /// Displays the completion message with grounded test count.
    /// </summary>
    public void ShowVerificationComplete(int total, int grounded, int partial, int rejected)
    {
        var written = grounded + partial;
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine($"  {OutputSymbols.SuccessMarkup} [green]{written} tests written[/]");

        if (rejected > 0)
        {
            AnsiConsole.MarkupLine($"  {OutputSymbols.InfoMarkup} [dim]{rejected} tests rejected (hallucinated)[/]");
        }
    }

    /// <summary>
    /// Gets the verdict symbol for display.
    /// </summary>
    public static string GetVerdictSymbol(VerificationVerdict verdict) => verdict switch
    {
        VerificationVerdict.Grounded => OutputSymbols.Success,
        VerificationVerdict.Partial => OutputSymbols.Warning,
        VerificationVerdict.Hallucinated => OutputSymbols.Error,
        _ => "?"
    };

    /// <summary>
    /// Gets the verdict markup for display.
    /// </summary>
    public static string GetVerdictMarkup(VerificationVerdict verdict) => verdict switch
    {
        VerificationVerdict.Grounded => OutputSymbols.SuccessMarkup,
        VerificationVerdict.Partial => OutputSymbols.WarningMarkup,
        VerificationVerdict.Hallucinated => OutputSymbols.ErrorMarkup,
        _ => "[grey]?[/]"
    };
}
