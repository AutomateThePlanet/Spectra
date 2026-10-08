using Spectra.Core.Baseline;
using Spectra.Core.Models;

namespace Spectra.CLI.Baseline;

/// <summary>Spec 089 (F3): the change still pending for a test returned unchanged: its triage bucket and reasons.</summary>
public sealed record PendingChange(Bucket Bucket, IReadOnlyList<string> Why);

/// <summary>
/// Spec 089 (F3, FR-009–FR-012): before an edit returned unchanged is recorded as reviewed, the test is classified
/// exactly as <c>update-triage</c> would classify it (<see cref="TriageEngine.TriageTestAsync"/>, COSMETIC not
/// auto-acknowledged). A bucket in <see cref="NeedsEdit"/> means the model missed a change that needs an edit, so the
/// baseline must not move; UNCHANGED, COSMETIC and LIGHT count as reviewed.
/// </summary>
public static class PendingChangeCheck
{
    /// <summary>The buckets an unchanged edit can't resolve.</summary>
    public static IReadOnlySet<Bucket> NeedsEdit { get; } =
        new HashSet<Bucket> { Bucket.Load, Bucket.CriteriaChanged, Bucket.Removed, Bucket.CriteriaRemoved, Bucket.Relink };

    /// <summary>
    /// The test's current bucket and reasons, or null when it has no baseline entry (it is then reviewed against the
    /// current text and recorded, as in 088).
    /// </summary>
    public static async Task<PendingChange?> ClassifyAsync(
        string suite, TestCase original, SourceBaseline baseline, BaselineContext context, int threshold, CancellationToken ct)
    {
        if (!baseline.Tests.TryGetValue(original.Id, out var entry))
        {
            return null;
        }

        var triaged = await TriageEngine.TriageTestAsync(suite, original, entry, context, threshold, cosmeticAcked: false, ct);
        return new PendingChange(triaged.Verdict.Bucket, triaged.Verdict.Why);
    }

    /// <summary>True when the change still needs an edit.</summary>
    public static bool IsPending(PendingChange? change) => change is not null && NeedsEdit.Contains(change.Bucket);
}
