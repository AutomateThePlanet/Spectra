using System.CommandLine;
using System.Text.Json;
using Spectra.CLI.Infrastructure;
using Spectra.CLI.IO;
using Spectra.CLI.Options;
using Spectra.CLI.Verification;
using Spectra.Core.Index;
using Spectra.Core.Models.Config;
using Spectra.Core.Models.Grounding;

namespace Spectra.CLI.Commands.Generate;

/// <summary>
/// Spec 071: <c>spectra ai record-drop</c>. Appends a drop-trail entry to
/// .spectra/dropped-tests.json before a hallucinated test is deleted.
/// Must be called BEFORE spectra delete — the trail is written first.
/// Deterministic and model-free; never calls a model.
/// </summary>
public sealed class RecordDropCommand : Command
{
    private const int ExitSuccess = 0;
    private const int ExitError = 1;
    private const int ExitVerdictEmpty = 5;
    private const int ExitVerdictParseFailed = 6;

    public RecordDropCommand()
        : base("record-drop", "Append a drop-trail entry before deleting a hallucinated test (Spec 071)")
    {
        var suiteOption = new Option<string>(["--suite", "-s"], "Suite name") { IsRequired = true };
        var testOption = new Option<string>(["--test", "-t"], "Test ID being dropped (e.g., TC-138)") { IsRequired = true };
        var fromOption = new Option<string?>("--from", "Per-test verdict JSON file (default: .spectra/verdicts/critic-verdict-{id}.json)");
        var reasonOption = new Option<string>("--reason", () => "hallucinated",
            "Drop reason: 'hallucinated' (critic drop) or 'user_decided' (review delete)");

        AddOption(suiteOption);
        AddOption(testOption);
        AddOption(fromOption);
        AddOption(reasonOption);

        this.SetHandler(async (context) =>
        {
            var suite = context.ParseResult.GetValueForOption(suiteOption)!;
            var testId = context.ParseResult.GetValueForOption(testOption)!;
            var from = context.ParseResult.GetValueForOption(fromOption);
            var reason = context.ParseResult.GetValueForOption(reasonOption)!;
            var outputFormat = context.ParseResult.GetValueForOption(GlobalOptions.OutputFormatOption);
            context.ExitCode = await RunAsync(suite, testId, from, reason,
                outputFormat == OutputFormat.Json, context.GetCancellationToken());
        });
    }

    private static async Task<int> RunAsync(
        string suite, string testId, string? from, string reason,
        bool json, CancellationToken ct)
    {
        var currentDir = Directory.GetCurrentDirectory();
        var testsDir = await ResolveTestsDirAsync(currentDir, ct);
        var suitePath = Path.Combine(currentDir, testsDir, suite);
        var indexPath = IndexWriter.GetIndexPath(suitePath);

        var index = await new IndexWriter().ReadAsync(indexPath, ct);
        var title = index?.Tests.FirstOrDefault(t =>
            string.Equals(t.Id, testId, StringComparison.OrdinalIgnoreCase))?.Title ?? testId;

        string? contradictingClaim = null;
        string? docRef = null;
        string? criticModel = null;

        var normalizedReason = reason.Trim().ToLowerInvariant();

        if (normalizedReason == "hallucinated")
        {
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

            var classification = VerdictIngestor.Classify(verdictJson);
            if (!classification.IsSuccess)
            {
                Console.Error.WriteLine($"Could not parse verdict file: {string.Join("; ", classification.Errors)}");
                return ExitVerdictParseFailed;
            }

            var verResult = classification.Result!;
            criticModel = verResult.CriticModel;

            var hallucinatedFinding = verResult.Findings.FirstOrDefault(f => f.Status == FindingStatus.Hallucinated);
            if (hallucinatedFinding is not null)
            {
                contradictingClaim = hallucinatedFinding.Claim;
                // Use evidence as doc ref if available, otherwise try to get from first source ref context
                docRef = hallucinatedFinding.Evidence;
            }
        }

        var trail = new DroppedTestsTrail(currentDir);
        var entry = new DroppedTestEntry
        {
            Id = testId,
            Suite = suite,
            Title = title,
            DropReason = normalizedReason,
            ContradictingClaim = contradictingClaim,
            DocRef = docRef,
            CriticModel = criticModel,
            Timestamp = DateTimeOffset.UtcNow.ToString("o"),
            Source = normalizedReason == "hallucinated" ? "critic" : "review"
        };

        try
        {
            var totalEntries = await trail.AppendAsync(entry, ct);
            if (json)
            {
                Console.Out.WriteLine(JsonSerializer.Serialize(new
                {
                    success = true,
                    id = testId,
                    suite,
                    trail_file = trail.TrailPath,
                    entries_total = totalEntries
                }));
            }
            else
            {
                Console.Out.WriteLine($"Drop trail: recorded {testId} ('{title}') as {normalizedReason} in {trail.TrailPath}");
            }
            return ExitSuccess;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Failed to append to drop trail: {ex.Message}");
            return ExitError;
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
