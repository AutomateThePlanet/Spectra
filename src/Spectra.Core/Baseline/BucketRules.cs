namespace Spectra.Core.Baseline;

/// <summary>
/// Spec 084 (FR-015): a test's bucket is its worst state. Numeric order is precedence: RELINK (Spec 086) > REMOVED >
/// CRITERIA_REMOVED > LOAD > CRITERIA_CHANGED > LIGHT > COSMETIC > UNCHANGED. UNBASELINED is not a bucket (a test with
/// no entry stands apart and is never reported as changed, unless one of its refs is RELINK).
/// </summary>
public enum Bucket
{
    Unchanged,
    Cosmetic,
    Light,
    CriteriaChanged,
    Load,
    CriteriaRemoved,
    Removed,

    /// <summary>Spec 086: a ref whose file is missing and was renamed in git history.</summary>
    Relink
}

public static class BucketRules
{
    public static Bucket Of(IEnumerable<RefState> refs, IEnumerable<CriterionState> criteria)
    {
        var worst = Bucket.Unchanged;
        foreach (var state in refs)
        {
            worst = Max(worst, Of(state));
        }

        foreach (var state in criteria)
        {
            worst = Max(worst, Of(state));
        }

        return worst;
    }

    public static Bucket Of(RefState state) => state switch
    {
        RefState.Relink => Bucket.Relink,
        RefState.Removed => Bucket.Removed,
        RefState.Load => Bucket.Load,
        RefState.Light => Bucket.Light,
        RefState.Cosmetic => Bucket.Cosmetic,
        _ => Bucket.Unchanged
    };

    public static Bucket Of(CriterionState state) => state switch
    {
        CriterionState.Removed => Bucket.CriteriaRemoved,
        CriterionState.Changed => Bucket.CriteriaChanged,
        _ => Bucket.Unchanged
    };

    /// <summary>The wire name: <c>RELINK</c>, <c>REMOVED</c>, <c>CRITERIA_REMOVED</c>, <c>LOAD</c>, … .</summary>
    public static string Wire(this Bucket bucket) => bucket switch
    {
        Bucket.Relink => "RELINK",
        Bucket.Removed => "REMOVED",
        Bucket.CriteriaRemoved => "CRITERIA_REMOVED",
        Bucket.Load => "LOAD",
        Bucket.CriteriaChanged => "CRITERIA_CHANGED",
        Bucket.Light => "LIGHT",
        Bucket.Cosmetic => "COSMETIC",
        _ => "UNCHANGED"
    };

    public static string Wire(this RefState state) => Of(state).Wire();

    public static string Wire(this CriterionState state) => Of(state).Wire();

    /// <summary>All buckets, worst first (the order counts and groups are reported in).</summary>
    public static IReadOnlyList<Bucket> WorstFirst { get; } =
        Enum.GetValues<Bucket>().OrderByDescending(b => (int)b).ToList();

    private static Bucket Max(Bucket a, Bucket b) => (int)a >= (int)b ? a : b;
}
