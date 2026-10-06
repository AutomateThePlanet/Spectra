namespace Spectra.Core.Baseline;

public enum RefState
{
    Unchanged,
    Cosmetic,
    Light,
    Load,
    Removed,

    /// <summary>Spec 086: the file is missing and git history names the file it was renamed to.</summary>
    Relink
}

public enum CriterionState
{
    Unchanged,
    Changed,
    Removed
}

/// <summary>
/// The current content of a ref: either missing (with the reason: the resolver's, or <c>anchor not found</c>), or its
/// fingerprint and normalised text.
/// </summary>
public sealed record CurrentSection(bool Missing, string? MissingReason, string? Fingerprint, string? Normalised)
{
    public static CurrentSection Gone(string reason) => new(true, reason, null, null);

    public static CurrentSection Of(string fingerprint, string normalised) => new(false, null, fingerprint, normalised);
}

/// <summary>
/// A ref's state against its baseline, with the reasons (<see cref="Why"/>) and the word diff. <see cref="Baseline"/>
/// is null only for a RELINK ref that was never baselined (Spec 086 FR-012).
/// </summary>
public sealed record RefVerdict(
    string Ref, RefState State, string? Baseline, string? Current, IReadOnlyList<string> Why, IReadOnlyList<DiffRun> Diff);

/// <summary>
/// Spec 084 (FR-012–FR-014, research R4): deterministic per-ref and per-criterion states. Checks, in order:
/// missing → REMOVED; same fingerprint → UNCHANGED; no snapshot → LOAD (no diff); snapshot equals the current
/// normalised text → COSMETIC; a side over the word cap → LOAD (no diff); otherwise the word diff decides LOAD (a
/// number, a modal word, or more than <c>light_threshold_words</c> words changed) or LIGHT.
/// </summary>
public static class ChangeClassifier
{
    public const string SnapshotMissing = "no diff: snapshot missing";

    /// <summary>
    /// A changed word from this set makes the change load-bearing: modal words, negations, exceptions and quantifiers
    /// (Spec 085 F4; "must not" is covered by must and not). Compared after <see cref="Fold"/>.
    /// </summary>
    public static readonly IReadOnlySet<string> ModalWords = new HashSet<string>(StringComparer.Ordinal)
    {
        "must", "should", "may", "required",
        "no", "not", "never", "none", "nothing", "cannot", "can't", "don't", "doesn't", "won't", "isn't", "aren't",
        "shouldn't", "mustn't", "without", "unless", "except",
        "only", "always", "all", "any", "every"
    };

    /// <summary>Spec 085 FR-013: a curly apostrophe (’ or ‘) counts as a straight one, for the LOAD word check only.</summary>
    public static string Fold(string word) => word.Replace('\u2019', '\'').Replace('\u2018', '\'');

    private const int MaxListed = 4;

    public static RefVerdict ClassifyRef(
        string sourceRef, string baseline, CurrentSection current, string? snapshot, int lightThreshold,
        int cap = WordDiff.DefaultCap)
    {
        if (current.Missing)
        {
            return new RefVerdict(sourceRef, RefState.Removed, baseline, null, [current.MissingReason ?? "not found"], []);
        }

        if (string.Equals(current.Fingerprint, baseline, StringComparison.Ordinal))
        {
            return new RefVerdict(sourceRef, RefState.Unchanged, baseline, current.Fingerprint, [], []);
        }

        if (snapshot is null)
        {
            return new RefVerdict(sourceRef, RefState.Load, baseline, current.Fingerprint, [SnapshotMissing], []);
        }

        if (string.Equals(snapshot, current.Normalised, StringComparison.Ordinal))
        {
            return new RefVerdict(sourceRef, RefState.Cosmetic, baseline, current.Fingerprint, [], []);
        }

        var diff = WordDiff.Compute(snapshot, current.Normalised ?? string.Empty, cap);
        if (!diff.Computed)
        {
            return new RefVerdict(sourceRef, RefState.Load, baseline, current.Fingerprint, [$"no diff: {diff.Reason}"], []);
        }

        var why = new List<string>();
        var removedNumbers = diff.RemovedWords.Where(ContainsDigit).ToList();
        var addedNumbers = diff.AddedWords.Where(ContainsDigit).ToList();
        if (removedNumbers.Count > 0 || addedNumbers.Count > 0)
        {
            why.Add($"numbers: {Listed(removedNumbers)}→{Listed(addedNumbers)}");
        }

        var removedModals = diff.RemovedWords.Select(Fold).Where(ModalWords.Contains).ToList();
        var addedModals = diff.AddedWords.Select(Fold).Where(ModalWords.Contains).ToList();
        if (removedModals.Count > 0 || addedModals.Count > 0)
        {
            why.Add($"modal: {Listed(removedModals)}→{Listed(addedModals)}");
        }

        var size = Math.Max(diff.Removed, diff.Added);
        var load = why.Count > 0 || size > lightThreshold;
        if (size > lightThreshold || !load)
        {
            why.Add($"words: {size}");
        }

        return new RefVerdict(sourceRef, load ? RefState.Load : RefState.Light, baseline, current.Fingerprint, why, diff.Runs);
    }

    public static CriterionState ClassifyCriterion(string baseline, bool exists, string? current) =>
        !exists ? CriterionState.Removed
        : string.Equals(baseline, current, StringComparison.Ordinal) ? CriterionState.Unchanged
        : CriterionState.Changed;

    private static bool ContainsDigit(string word) => word.Any(char.IsDigit);

    private static string Listed(IReadOnlyList<string> words) =>
        words.Count == 0 ? "∅"
        : words.Count <= MaxListed ? string.Join(",", words)
        : string.Join(",", words.Take(MaxListed)) + ",…";
}
