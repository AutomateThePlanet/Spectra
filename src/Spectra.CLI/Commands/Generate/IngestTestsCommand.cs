using System.CommandLine;
using System.Text.Json;
using Spectra.CLI.Baseline;
using Spectra.CLI.Criteria;
using Spectra.CLI.Generation;
using Spectra.CLI.Infrastructure;
using Spectra.CLI.IO;
using Spectra.CLI.Options;
using Spectra.Core.Index;
using Spectra.Core.Models;
using Spectra.Core.Models.Config;
using Spectra.Core.Parsing;

namespace Spectra.CLI.Commands.Generate;

/// <summary>
/// Spec 053: <c>spectra ai ingest-tests</c>. The fail-loud boundary. Reads agent-generated
/// content (from <c>--from</c> or stdin), validates it, and persists only when the whole batch
/// is valid. On any failure it persists nothing and reports a specific, machine-readable error
/// a retry skill can act on.
/// </summary>
public sealed class IngestTestsCommand : Command
{
    private const int ExitSuccess = 0;
    private const int ExitError = 1;
    private const int ExitContentInvalid = 5; // EMPTY/MALFORMED/TRUNCATED/NO_TESTS
    private const int ExitSchemaInvalid = 6;  // SCHEMA_INVALID

    public IngestTestsCommand()
        : base("ingest-tests", "Validate and persist agent-generated test content (fail-loud)")
    {
        var suiteArgument = new Argument<string>("suite", "Target suite to persist into");
        var fromOption = new Option<string?>("--from", "File containing the agent's JSON output (omit to read stdin)");

        AddArgument(suiteArgument);
        AddOption(fromOption);

        this.SetHandler(async (context) =>
        {
            var suite = context.ParseResult.GetValueForArgument(suiteArgument);
            var from = context.ParseResult.GetValueForOption(fromOption);
            var outputFormat = context.ParseResult.GetValueForOption(GlobalOptions.OutputFormatOption);
            context.ExitCode = await RunAsync(suite, from, outputFormat == OutputFormat.Json, context.GetCancellationToken());
        });
    }

    private static async Task<int> RunAsync(string suite, string? from, bool json, CancellationToken ct)
    {
        var currentDir = Directory.GetCurrentDirectory();
        var configPath = Path.Combine(currentDir, "spectra.config.json");
        if (!File.Exists(configPath))
        {
            Console.Error.WriteLine("No spectra.config.json found. Run 'spectra init' first.");
            return ExitError;
        }

        SpectraConfig? config;
        try
        {
            config = JsonSerializer.Deserialize<SpectraConfig>(
                await File.ReadAllTextAsync(configPath, ct),
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch (JsonException ex)
        {
            Console.Error.WriteLine($"Error reading config: {ex.Message}");
            return ExitError;
        }
        if (config is null)
        {
            Console.Error.WriteLine("Could not parse spectra.config.json.");
            return ExitError;
        }

        // Read agent content from file or stdin.
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

        var testsDir = config.Tests?.Dir ?? "test-cases";
        var testsPath = Path.Combine(currentDir, testsDir);
        var suitePath = Path.Combine(testsPath, suite);
        var suiteFiles = await SuiteScanBuilder.ReadAsync(suitePath, captureLayout: false, ct);
        var existingTests = SuiteScanBuilder.Tests(suiteFiles);

        var persistence = new TestPersistenceService(
            new TestFileWriter(), new IndexGenerator(), new IndexWriter());
        var ingestor = new GeneratedTestIngestor(persistence);

        // Spec 084 FR-027: take the baseline lock and load the suite baseline strictly BEFORE persisting, so an
        // unreadable baseline or a lock timeout persists nothing; record entries for the persisted tests after.
        IngestResult result;
        BaselineWriteSummary? baselineSummary = null;
        BaselineStoreException? afterPersist = null;
        var warnings = new List<string>();
        try
        {
            result = await BaselineStore.WithLockAsync(currentDir, async () =>
            {
                var baseline = await BaselineStore.LoadSuiteAsync(currentDir, testsPath, suite, ct);
                var ingested = await ingestor.IngestAsync(content, testsPath, suite, existingTests, ct);
                if (ingested.IsSuccess)
                {
                    try
                    {
                        // Spec 099 F1: the suite as read above plus the files just written — no second parse.
                        var scan = SuiteScanBuilder.Build(currentDir, suiteFiles, ingested.PersistedTests
                            .Select(t => new SuiteFile(TestFileWriter.GetFilePath(testsPath, suite, t.Id), t)));
                        baselineSummary = await BaselineRecorder.RecordPersistedAsync(
                            currentDir, config, testsPath, suite, baseline, ingested.PersistedTests, RecordMode.Rebuild, warnings, ct, scan);
                    }
                    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or BaselineStoreException)
                    {
                        afterPersist = ex as BaselineStoreException
                            ?? new BaselineStoreException(BaselineStore.ErrorBaselineWriteFailed, ex.Message);
                    }
                }

                return ingested;
            }, ct);
        }
        catch (BaselineStoreException ex)
        {
            CriteriaCommandIO.WriteFailure(json, "ingest-tests", ex.ErrorCode, ex.Message + " Nothing was persisted.");
            return ExitError;
        }

        if (result.IsSuccess && afterPersist is not null)
        {
            var ids = result.PersistedTests.Select(t => t.Id).ToList();
            CriteriaCommandIO.WriteFailure(json, "ingest-tests", afterPersist.ErrorCode,
                $"The tests were persisted ({string.Join(", ", ids)}) but their baseline was not recorded: {afterPersist.Message} "
                + $"Run 'spectra ai source-baseline --init --suite {suite}' to record it.",
                extras: new Dictionary<string, object?> { ["persisted"] = ids });
            return ExitError;
        }

        if (result.IsSuccess)
        {
            BaselineCommandIO.PrintWarnings(warnings);
            if (json)
            {
                Console.Out.WriteLine(JsonSerializer.Serialize(new
                {
                    success = true,
                    persisted = result.PersistedTests.Count,
                    ids = result.PersistedTests.Select(t => t.Id).ToArray(),
                    baseline = baselineSummary!.ToJson(),
                    warnings
                }));
            }
            else
            {
                Console.Out.WriteLine($"Persisted {result.PersistedTests.Count} test(s) into '{suite}': "
                    + string.Join(", ", result.PersistedTests.Select(t => t.Id)));
                Console.Out.WriteLine(baselineSummary!.ToHuman());
            }
            return ExitSuccess;
        }

        // Fail loud — nothing persisted.
        if (json)
        {
            Console.Error.WriteLine(JsonSerializer.Serialize(new
            {
                success = false,
                error_code = result.ErrorCode,
                errors = result.Errors
            }));
        }
        else
        {
            Console.Error.WriteLine($"Ingest failed [{result.ErrorCode}] — nothing persisted:");
            foreach (var e in result.Errors)
                Console.Error.WriteLine($"  - {e}");
        }

        return result.ErrorCode == IngestErrorCode.SchemaInvalid ? ExitSchemaInvalid : ExitContentInvalid;
    }
}
