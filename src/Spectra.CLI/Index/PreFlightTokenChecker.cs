using System.Text;
using Spectra.CLI.Services;

namespace Spectra.CLI.Index;

/// <summary>
/// Checks the documentation-index prompt against a configured token budget
/// before any AI call. Throws <see cref="PreFlightBudgetExceededException"/>
/// (a typed <see cref="InvalidOperationException"/>) on overflow with an
/// actionable message that names the offending suite(s) and suggests how
/// to narrow the scope.
/// </summary>
/// <remarks>
/// <para>Phase 2 estimation source: the legacy single-file <c>_index.md</c>.
/// In Phase 3 this class gains a manifest-driven overload that estimates from
/// per-suite token totals. The budget-violation message format is the same in
/// both paths.</para>
/// <para>The default budget (96,000 tokens) sits 32K below the model's 128K
/// window. The 32K margin covers response (~8K), prompt template (~4K),
/// existing-test frontmatter (~5K), coverage snapshot (~5K), category and
/// technique guidance (~5K), and retry buffer (~5K). Tune via
/// <c>ai.analysis.max_prompt_tokens</c>.</para>
/// </remarks>
public sealed class PreFlightTokenChecker
{
    /// <summary>
    /// Default budget when no config value is supplied. Conservative — favors
    /// "fail fast with an actionable error" over "succeed but barely fit".
    /// </summary>
    public const int DefaultBudgetTokens = 96_000;

    /// <summary>
    /// Estimates the token cost of the legacy <c>_index.md</c> at
    /// <paramref name="legacyIndexPath"/> and throws if the cost exceeds
    /// <paramref name="budgetTokens"/>. Returns the estimate on success.
    /// </summary>
    /// <param name="legacyIndexPath">Path to <c>docs/_index.md</c>.
    /// File-not-found is treated as zero tokens (no overflow possible).</param>
    /// <param name="budgetTokens">Configured budget (e.g. from
    /// <c>ai.analysis.max_prompt_tokens</c>). Negative or zero means no check.</param>
    /// <param name="commandHint">A free-form hint surfaced in the error message,
    /// e.g. <c>"spectra ai generate"</c>. Used to compose the suggestion block.</param>
    public async Task<int> EnforceBudgetFromLegacyIndexAsync(
        string legacyIndexPath,
        int budgetTokens,
        string commandHint,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(legacyIndexPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(commandHint);

        if (budgetTokens <= 0) return 0;

        if (!File.Exists(legacyIndexPath)) return 0;

        var content = await File.ReadAllTextAsync(legacyIndexPath, ct);
        var estimated = TokenEstimator.Estimate(content);

        EnforceBudget(
            estimatedTokens: estimated,
            budgetTokens: budgetTokens,
            overflowingSuites: Array.Empty<SuiteTokenEstimate>(),
            commandHint: commandHint);

        return estimated;
    }

    /// <summary>
    /// Manifest-driven overload (used by Phase 3+ once the manifest replaces
    /// the legacy file). When <paramref name="estimatedTokens"/> exceeds
    /// <paramref name="budgetTokens"/>, throws with a message that itemizes
    /// every suite in <paramref name="overflowingSuites"/>.
    /// </summary>
    public void EnforceBudget(
        int estimatedTokens,
        int budgetTokens,
        IReadOnlyList<SuiteTokenEstimate> overflowingSuites,
        string commandHint)
    {
        ArgumentNullException.ThrowIfNull(overflowingSuites);
        ArgumentException.ThrowIfNullOrWhiteSpace(commandHint);

        if (budgetTokens <= 0) return;
        if (estimatedTokens <= budgetTokens) return;

        throw new PreFlightBudgetExceededException(
            estimatedTokens,
            budgetTokens,
            overflowingSuites,
            commandHint);
    }
}

/// <summary>
/// Per-suite token estimate emitted in the budget-violation message.
/// </summary>
public readonly record struct SuiteTokenEstimate(string Id, int Tokens);

/// <summary>
/// Thrown when the pre-flight budget check fails. The message is composed
/// deterministically from the parameters so consumers (CLI and tests) can
/// rely on the format described in <c>contracts/cli-surface.md</c>.
/// </summary>
public sealed class PreFlightBudgetExceededException : InvalidOperationException
{
    public PreFlightBudgetExceededException(
        int estimatedTokens,
        int budgetTokens,
        IReadOnlyList<SuiteTokenEstimate> overflowingSuites,
        string commandHint)
        : base(BuildMessage(estimatedTokens, budgetTokens, overflowingSuites, commandHint))
    {
        EstimatedTokens = estimatedTokens;
        BudgetTokens = budgetTokens;
        OverflowingSuites = overflowingSuites;
        CommandHint = commandHint;
    }

    public int EstimatedTokens { get; }
    public int BudgetTokens { get; }
    public IReadOnlyList<SuiteTokenEstimate> OverflowingSuites { get; }
    public string CommandHint { get; }

    private static string BuildMessage(
        int estimatedTokens,
        int budgetTokens,
        IReadOnlyList<SuiteTokenEstimate> suites,
        string commandHint)
    {
        var sb = new StringBuilder();
        sb.AppendLine(
            $"Analyzer prompt would be ~{estimatedTokens:N0} tokens, " +
            $"exceeding the configured {budgetTokens:N0}-token budget.");

        if (suites.Count > 0)
        {
            sb.AppendLine("Candidate suites (sorted by token cost):");
            var sorted = suites.OrderByDescending(s => s.Tokens).ToList();
            foreach (var suite in sorted)
            {
                sb.AppendLine($"  {suite.Id,-30} {suite.Tokens,8:N0} tokens");
            }
        }

        sb.AppendLine("Suggested:");
        if (suites.Count > 0)
        {
            // Pick the largest suite as the example narrowing arg.
            var largest = suites.OrderByDescending(s => s.Tokens).First();
            sb.AppendLine($"  {commandHint} --suite {largest.Id}");
        }
        else
        {
            sb.AppendLine($"  {commandHint} --suite <id>");
        }
        sb.AppendLine($"  {commandHint} --analyze-only");
        sb.Append("Or raise ai.analysis.max_prompt_tokens in spectra.config.json.");

        return sb.ToString();
    }
}
