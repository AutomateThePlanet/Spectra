using System.CommandLine;
using System.Text.Json;
using Spectra.CLI.Infrastructure;
using Spectra.CLI.Options;
using Spectra.CLI.Verification;

namespace Spectra.CLI.Commands.Generate;

/// <summary>
/// Spec 055: <c>spectra ai ingest-verdict</c>. The fail-loud verdict-ingest boundary. Reads an
/// agent-produced critic JSON (from <c>--from</c> or stdin), classifies it, and reports the verdict
/// + gate decision. Damage (empty / missing-field / unparseable) fails loud with a specific error —
/// never a silent <c>Partial</c>/<c>0.5</c> soft pass (FR-006). The verdict stays advisory-gating:
/// only <c>hallucinated</c> drops (FR-005).
///
/// Mirrors <see cref="IngestTestsCommand"/> / <see cref="IngestCriteriaCommand"/>. Unlike those, it
/// persists nothing — grounding write-back is handled by <see cref="IngestGroundingCommand"/> (Spec 071)
/// which activates the TestFileWriter grounding path. This command is advisory-gate only.
/// </summary>
public sealed class IngestVerdictCommand : Command
{
    private const int ExitSuccess = 0;
    private const int ExitError = 1;
    private const int ExitEmpty = 5;        // EmptyResponse
    private const int ExitParseInvalid = 6; // ParseFailure

    public IngestVerdictCommand()
        : base("ingest-verdict", "Classify an agent-produced critic verdict (fail-loud, no model call)")
    {
        var fromOption = new Option<string?>("--from", "File containing the critic's JSON response (omit to read stdin)");
        AddOption(fromOption);

        this.SetHandler(async (context) =>
        {
            var from = context.ParseResult.GetValueForOption(fromOption);
            var outputFormat = context.ParseResult.GetValueForOption(GlobalOptions.OutputFormatOption);
            context.ExitCode = await RunAsync(from, outputFormat == OutputFormat.Json, context.GetCancellationToken());
        });
    }

    private static async Task<int> RunAsync(string? from, bool json, CancellationToken ct)
    {
        string content;
        if (!string.IsNullOrWhiteSpace(from))
        {
            if (!File.Exists(from))
            {
                Console.Error.WriteLine($"Content file not found: {from}");
                return ExitError;
            }
            content = await File.ReadAllTextAsync(from, ct);
        }
        else
        {
            content = await Console.In.ReadToEndAsync(ct);
        }

        var result = VerdictIngestor.Classify(content);

        if (result.IsSuccess)
        {
            var verdict = result.Result!.Verdict.ToString().ToLowerInvariant();
            if (json)
            {
                Console.Out.WriteLine(JsonSerializer.Serialize(new
                {
                    outcome = result.Outcome.ToString(),
                    verdict,
                    score = result.Result.Score,
                    drop = result.Drops
                }));
            }
            else
            {
                Console.Out.WriteLine(
                    $"verdict={verdict} score={result.Result.Score:F2} gate={(result.Drops ? "drop" : "pass")}");
            }
            return ExitSuccess;
        }

        // Fail loud — damage. Nothing is treated as a verdict.
        if (json)
        {
            Console.Error.WriteLine(JsonSerializer.Serialize(new
            {
                outcome = result.Outcome.ToString(),
                errors = result.Errors
            }));
        }
        else
        {
            Console.Error.WriteLine($"Verdict ingest failed [{result.Outcome}] — no verdict applied:");
            foreach (var e in result.Errors)
                Console.Error.WriteLine($"  - {e}");
        }

        return result.Outcome == VerdictIngestOutcome.EmptyResponse ? ExitEmpty : ExitParseInvalid;
    }
}
