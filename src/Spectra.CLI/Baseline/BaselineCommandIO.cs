using System.Text.Json;
using Spectra.CLI.Criteria;
using Spectra.CLI.Infrastructure;
using Spectra.Core.Config;
using Spectra.Core.Models;
using Spectra.Core.Models.Config;
using Spectra.Core.Parsing;

namespace Spectra.CLI.Baseline;

/// <summary>A suite's tests as parsed now, and the scan they came from (Spec 085: it decides pruning).</summary>
public sealed record SuiteTests(string Suite, IReadOnlyList<TestCase> Tests, SuiteScan Scan);

/// <summary>
/// Spec 084: shared plumbing for <c>update-triage</c>, <c>update-ack</c> and <c>source-baseline</c> — config loading
/// (write commands under the 083 rule, reading <c>source</c> and <c>tests</c>), suite discovery, test loading, and the
/// shared failure shape with the error codes' exit codes (refusals 4, environment errors 1).
/// </summary>
public static class BaselineCommandIO
{
    public const string ErrorInvalidArguments = "INVALID_ARGUMENTS";
    public const string ErrorSuiteNotFound = "SUITE_NOT_FOUND";
    public const string ErrorTestNotFound = "TEST_NOT_FOUND";
    public const string ErrorTriageMissing = "TRIAGE_MISSING";
    public const string ErrorGroupNotFound = "GROUP_NOT_FOUND";
    public const string ErrorTriageStale = "TRIAGE_STALE";
    public const string ErrorRefRemoved = "REF_REMOVED";
    public const string ErrorCriterionRemoved = "CRITERION_REMOVED";

    private static readonly HashSet<string> Refusals = new(StringComparer.Ordinal)
    {
        ErrorInvalidArguments, ErrorSuiteNotFound, ErrorTestNotFound, ErrorTriageMissing, ErrorGroupNotFound,
        ErrorTriageStale, ErrorRefRemoved, ErrorCriterionRemoved, HistoryBootstrap.ErrorGitRefUnavailable,
        "NOTHING_TO_RECONCILE", "PROMPT_TOO_LARGE", "TEMPLATE_OUTDATED"
    };

    /// <summary>Compact JSON with relaxed escaping (so <c>#</c>, <c>→</c> and non-ASCII stay readable).</summary>
    public static readonly JsonSerializerOptions Json = CriteriaCommandIO.Json;

    /// <summary>
    /// Spec 087 (F5): the one validation of <c>--min-similarity</c> (relink-sources, source-baseline --from-*): the
    /// refusal message, or null when the value is in 50–100.
    /// </summary>
    public static string? ValidateMinSimilarity(int value) =>
        value is < 50 or > 100 ? $"--min-similarity must be between 50 and 100 (got {value})." : null;

    public static int ExitCodeOf(string errorCode) => Refusals.Contains(errorCode) ? ExitCodes.NotFound : ExitCodes.Error;

    /// <summary>Writes the failure (one JSON document on stderr in JSON mode) and returns its exit code.</summary>
    public static int Fail(
        bool json, string command, string errorCode, string message,
        IReadOnlyList<string>? warnings = null, IReadOnlyDictionary<string, object?>? extras = null)
    {
        CriteriaCommandIO.WriteFailure(json, command, errorCode, message, warnings, extras);
        return ExitCodeOf(errorCode);
    }

    /// <summary>The 083 rule for a write command that reads <c>source</c> and <c>tests</c>.</summary>
    public static Task<ConfigLoadResult> LoadConfigAsync(string repoRoot, CancellationToken ct) =>
        ConfigLoader.LoadForWriteAsync(repoRoot, ConfigSections.Source | ConfigSections.Tests, ct);

    /// <summary>Prints config warnings: <c>Warning:</c> lines on stderr in every mode.</summary>
    public static void PrintWarnings(IEnumerable<string> warnings)
    {
        foreach (var warning in warnings)
        {
            Console.Error.WriteLine($"Warning: {warning}");
        }
    }

    public static string TestsPath(string repoRoot, SpectraConfig config) =>
        Path.Combine(repoRoot, string.IsNullOrWhiteSpace(config.Tests?.Dir) ? "test-cases" : config.Tests.Dir);

    /// <summary>The suites: directories under the tests dir that don't start with <c>_</c>, in ordinal order.</summary>
    public static IReadOnlyList<string> ListSuites(string testsPath) =>
        Directory.Exists(testsPath)
            ? Directory.GetDirectories(testsPath)
                .Select(Path.GetFileName)
                .Where(n => n is not null && !n.StartsWith('_'))
                .Select(n => n!)
                .OrderBy(n => n, StringComparer.Ordinal)
                .ToList()
            : [];

    /// <summary>
    /// Resolves <c>--suite</c> / <c>--all-suites</c> to suite names, or returns the refusal (code, message).
    /// </summary>
    public static (IReadOnlyList<string>? Suites, string? ErrorCode, string? Message) ResolveScope(
        string testsPath, string? suite, bool allSuites)
    {
        if (allSuites == !string.IsNullOrWhiteSpace(suite))
        {
            return (null, ErrorInvalidArguments, "Give exactly one of --suite <name> and --all-suites.");
        }

        if (allSuites)
        {
            return (ListSuites(testsPath), null, null);
        }

        return Directory.Exists(Path.Combine(testsPath, suite!)) && !suite!.StartsWith('_')
            ? ([suite], null, null)
            : (null, ErrorSuiteNotFound, $"Suite '{suite}' was not found under {Path.GetFileName(testsPath.TrimEnd('/', '\\'))}/.");
    }

    /// <summary>
    /// Parses the suite's tests (<c>*.md</c> without a leading <c>_</c>), ordered by ID. Files that don't parse are
    /// skipped with a warning naming them.
    /// </summary>
    public static async Task<SuiteTests> LoadSuiteTestsAsync(
        string repoRoot, string testsPath, string suite, List<string> warnings, CancellationToken ct)
    {
        var scan = await BaselineStore.ScanSuiteAsync(repoRoot, testsPath, suite, ct);
        foreach (var file in scan.UnparseableFiles)
        {
            warnings.Add($"{file} could not be parsed and was skipped.");
        }

        return new SuiteTests(suite, scan.Tests, scan);
    }

    /// <summary>
    /// Spec 085 FR-008: prints the warnings raised after config loading (parse and prune warnings) as <c>Warning:</c>
    /// lines on stderr, as config warnings already are; the first <paramref name="alreadyPrinted"/> were printed then.
    /// They stay in the JSON <c>warnings</c> too.
    /// </summary>
    public static void PrintLateWarnings(IReadOnlyList<string> warnings, int alreadyPrinted) =>
        PrintWarnings(warnings.Skip(alreadyPrinted));

    /// <summary>
    /// Success output: in JSON mode the document on stdout (one compact line) and the human summary on stderr;
    /// otherwise the human summary on stdout. The summary is never suppressed.
    /// </summary>
    public static void Emit(bool json, object document, Action<TextWriter> human)
    {
        if (json)
        {
            Console.Out.WriteLine(JsonSerializer.Serialize(document, Json));
            human(Console.Error);
        }
        else
        {
            human(Console.Out);
        }
    }

    /// <summary>The scope object of the JSON output: <c>{"suite":"s"}</c> or <c>{"all_suites":true}</c>.</summary>
    public static Dictionary<string, object?> ScopeJson(string? suite) =>
        string.IsNullOrWhiteSpace(suite)
            ? new Dictionary<string, object?> { ["all_suites"] = true }
            : new Dictionary<string, object?> { ["suite"] = suite };
}
