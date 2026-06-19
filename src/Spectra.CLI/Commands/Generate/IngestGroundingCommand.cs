using System.CommandLine;
using System.Text.Json;
using Spectra.CLI.Infrastructure;
using Spectra.CLI.IO;
using Spectra.CLI.Options;
using Spectra.Core.Index;
using Spectra.Core.Models.Config;

namespace Spectra.CLI.Commands.Generate;

/// <summary>
/// Spec 071: <c>spectra ai ingest-grounding</c>. Reads a per-test critic verdict JSON and writes
/// the condensed grounding block into the named test's .md file. Activates the previously dead
/// TestFileWriter grounding code path. Deterministic and model-free; never calls a model.
/// Refuses hallucinated verdicts (exit 4) — those must be handled by record-drop + delete.
/// </summary>
public sealed class IngestGroundingCommand : Command
{
    private const int ExitSuccess = 0;
    private const int ExitError = 1;
    private const int ExitHallucinatedRefused = 4;
    private const int ExitVerdictEmpty = 5;
    private const int ExitVerdictParseFailed = 6;

    public IngestGroundingCommand()
        : base("ingest-grounding", "Write critic verdict as grounding block to a test .md file (Spec 071, no model call)")
    {
        var suiteOption = new Option<string>(["--suite", "-s"], "Suite name to resolve the test from") { IsRequired = true };
        var testOption = new Option<string>(["--test", "-t"], "Test ID to write grounding for (e.g., TC-113)") { IsRequired = true };
        var fromOption = new Option<string?>("--from", "Verdict JSON file path (default: .spectra/verdicts/critic-verdict-{id}.json)");
        var repairedFlag = new Option<bool>("--repaired", "Mark test as repaired (sets repaired: true in the grounding block)");
        var repairAttemptsOption = new Option<int>("--repair-attempts", () => 0, "Number of repair attempts performed");

        AddOption(suiteOption);
        AddOption(testOption);
        AddOption(fromOption);
        AddOption(repairedFlag);
        AddOption(repairAttemptsOption);

        this.SetHandler(async (context) =>
        {
            var suite = context.ParseResult.GetValueForOption(suiteOption)!;
            var testId = context.ParseResult.GetValueForOption(testOption)!;
            var from = context.ParseResult.GetValueForOption(fromOption);
            var repaired = context.ParseResult.GetValueForOption(repairedFlag);
            var repairAttempts = context.ParseResult.GetValueForOption(repairAttemptsOption);
            var outputFormat = context.ParseResult.GetValueForOption(GlobalOptions.OutputFormatOption);
            context.ExitCode = await RunAsync(suite, testId, from, repaired, repairAttempts,
                outputFormat == OutputFormat.Json, context.GetCancellationToken());
        });
    }

    private static async Task<int> RunAsync(
        string suite, string testId, string? from, bool repaired, int repairAttempts,
        bool json, CancellationToken ct)
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

        var entry = index.Tests.FirstOrDefault(t => string.Equals(t.Id, testId, StringComparison.OrdinalIgnoreCase));
        if (entry is null)
        {
            Console.Error.WriteLine($"Test id '{testId}' not found in suite '{suite}'.");
            return ExitError;
        }

        var testFilePath = Path.Combine(suitePath, entry.File);
        if (!File.Exists(testFilePath))
        {
            Console.Error.WriteLine($"Test file not found: {testFilePath}");
            return ExitError;
        }

        // Resolve verdict file path
        var verdictPath = from ?? Path.Combine(currentDir, ".spectra", "verdicts", $"critic-verdict-{testId}.json");
        if (!File.Exists(verdictPath))
        {
            Console.Error.WriteLine($"Verdict file not found: {verdictPath}");
            return ExitVerdictEmpty;
        }

        var verdictJson = await File.ReadAllTextAsync(verdictPath, ct);
        if (string.IsNullOrWhiteSpace(verdictJson))
        {
            Console.Error.WriteLine($"Verdict file is empty: {verdictPath}");
            return ExitVerdictEmpty;
        }

        var service = new GroundingWriteBackService(new TestFileWriter());
        var result = await service.WriteAsync(testFilePath, verdictJson, repairAttempts, repaired, ct);

        if (result.IsSuccess)
        {
            var g = result.Grounding!;
            if (json)
            {
                Console.Out.WriteLine(JsonSerializer.Serialize(new
                {
                    success = true,
                    id = testId,
                    suite,
                    verdict = g.Verdict.ToString().ToLowerInvariant(),
                    score = g.Score,
                    flagged_for_review = g.FlaggedForReview,
                    repair_attempts = g.RepairAttempts,
                    repaired = g.Repaired
                }));
            }
            else
            {
                Console.Out.WriteLine($"Grounding written for {testId} in suite '{suite}': verdict={g.Verdict.ToString().ToLowerInvariant()} score={g.Score:F2}");
                if (g.FlaggedForReview)
                    Console.Out.WriteLine($"  Flagged for review ({g.CondensedFindings.Count} condensed finding(s)).");
                if (g.Repaired)
                    Console.Out.WriteLine($"  Repaired after {g.RepairAttempts} attempt(s).");
            }
            return ExitSuccess;
        }

        return result.Outcome switch
        {
            GroundingWriteBackService.WriteBackOutcome.HallucinatedRefused =>
                EmitRefusalAndReturn(json, testId, result.ErrorMessage!, ExitHallucinatedRefused),
            GroundingWriteBackService.WriteBackOutcome.VerdictFailure =>
                EmitRefusalAndReturn(json, testId, result.ErrorMessage!, ExitVerdictParseFailed),
            _ =>
                EmitRefusalAndReturn(json, testId, result.ErrorMessage!, ExitError)
        };
    }

    private static int EmitRefusalAndReturn(bool json, string testId, string message, int exitCode)
    {
        if (json)
            Console.Error.WriteLine(JsonSerializer.Serialize(new { success = false, id = testId, error = message }));
        else
            Console.Error.WriteLine($"ingest-grounding failed for {testId}: {message}");
        return exitCode;
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
