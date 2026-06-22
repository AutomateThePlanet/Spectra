using System.CommandLine;
using System.Text.Json;
using Spectra.CLI.Infrastructure;
using Spectra.CLI.Options;
using Spectra.CLI.Verification;
using Spectra.Core.Index;
using Spectra.Core.Models;
using Spectra.Core.Models.Config;
using Spectra.Core.Models.Grounding;

namespace Spectra.CLI.Commands.Generate;

/// <summary>
/// Spec 055: <c>spectra ai ingest-verdict</c>. The fail-loud verdict-ingest boundary. Reads an
/// agent-produced critic JSON (from <c>--from</c> or stdin), classifies it, and reports the verdict
/// + gate decision. Damage (empty / missing-field / unparseable) fails loud with a specific error —
/// never a silent <c>Partial</c>/<c>0.5</c> soft pass (FR-006). The verdict stays advisory-gating:
/// only <c>hallucinated</c> drops (FR-005).
///
/// Spec 077: adds <c>--suite --all</c> batch mode to classify all verdict files for a suite in one
/// call, eliminating per-test shell loops. Per-test <c>--from</c> mode is unchanged.
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
        var suiteOption = new Option<string?>(["--suite", "-s"], "Suite name (required with --all)");
        var allFlag = new Option<bool>("--all", "Batch mode: classify all verdict files for the suite (Spec 077)");

        AddOption(fromOption);
        AddOption(suiteOption);
        AddOption(allFlag);

        this.SetHandler(async (context) =>
        {
            var from = context.ParseResult.GetValueForOption(fromOption);
            var suite = context.ParseResult.GetValueForOption(suiteOption);
            var all = context.ParseResult.GetValueForOption(allFlag);
            var outputFormat = context.ParseResult.GetValueForOption(GlobalOptions.OutputFormatOption);
            var json = outputFormat == OutputFormat.Json;

            if (all)
            {
                if (!string.IsNullOrEmpty(from))
                {
                    Console.Error.WriteLine("--from and --all are mutually exclusive.");
                    context.ExitCode = ExitError;
                    return;
                }
                if (string.IsNullOrEmpty(suite))
                {
                    Console.Error.WriteLine("--suite is required when using --all.");
                    context.ExitCode = ExitError;
                    return;
                }
                context.ExitCode = await RunBatchAsync(suite, json, context.GetCancellationToken());
            }
            else
            {
                context.ExitCode = await RunAsync(from, json, context.GetCancellationToken());
            }
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

    private static async Task<int> RunBatchAsync(string suite, bool json, CancellationToken ct)
    {
        var currentDir = Directory.GetCurrentDirectory();
        var testsDir = await ResolveTestsDirAsync(currentDir, ct);
        var suitePath = Path.Combine(currentDir, testsDir, suite);
        var indexPath = IndexWriter.GetIndexPath(suitePath);

        var index = await new IndexWriter().ReadAsync(indexPath, ct);
        if (index is null)
        {
            Console.Error.WriteLine($"Suite index not found: {indexPath}");
            return ExitError;
        }

        var verdictDir = Path.Combine(currentDir, ".spectra", "verdicts");
        if (!Directory.Exists(verdictDir))
        {
            EmitBatchResult(json, suite, 0, 0, 0, 0);
            return ExitSuccess;
        }

        var indexLookup = index.Tests
            .ToDictionary(t => t.Id, t => t, StringComparer.OrdinalIgnoreCase);

        int grounded = 0, partial = 0, hallucinated = 0, errors = 0;

        var verdictFiles = Directory.GetFiles(verdictDir, "critic-verdict-*.json").OrderBy(f => f);
        foreach (var vf in verdictFiles)
        {
            ct.ThrowIfCancellationRequested();

            var fileName = Path.GetFileNameWithoutExtension(vf);
            var testId = fileName.StartsWith("critic-verdict-", StringComparison.OrdinalIgnoreCase)
                ? fileName["critic-verdict-".Length..] : null;
            if (string.IsNullOrEmpty(testId)) continue;

            // Filter to this suite via index lookup
            if (!indexLookup.ContainsKey(testId)) continue;

            string content;
            try { content = await File.ReadAllTextAsync(vf, ct); }
            catch { errors++; continue; }

            var result = VerdictIngestor.Classify(content);
            if (!result.IsSuccess) { errors++; continue; }

            switch (result.Result!.Verdict)
            {
                case VerificationVerdict.Grounded:
                case VerificationVerdict.Manual:
                    grounded++; break;
                case VerificationVerdict.Partial:
                    partial++; break;
                case VerificationVerdict.Hallucinated:
                    hallucinated++; break;
                default:
                    errors++; break;
            }
        }

        EmitBatchResult(json, suite, grounded, partial, hallucinated, errors);
        return ExitSuccess;
    }

    private static void EmitBatchResult(bool json, string suite,
        int grounded, int partial, int hallucinated, int errors)
    {
        if (json)
        {
            Console.Out.WriteLine(JsonSerializer.Serialize(new
            {
                command = "ingest-verdict",
                mode = "batch",
                suite,
                grounded,
                partial,
                hallucinated,
                errors
            }));
        }
        else
        {
            Console.Out.WriteLine(
                $"Batch verdict-ingest for suite '{suite}': " +
                $"grounded={grounded} partial={partial} hallucinated={hallucinated} errors={errors}");
        }
    }

    private static async Task<string> ResolveTestsDirAsync(string currentDir, CancellationToken ct)
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
