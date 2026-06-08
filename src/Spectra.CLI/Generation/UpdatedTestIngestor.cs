using Spectra.CLI.IO;
using Spectra.Core.Models;
using Spectra.Core.Models.Grounding;
using Spectra.Core.Validation;

namespace Spectra.CLI.Generation;

/// <summary>
/// Spec 063: the fail-loud boundary for the inverted <b>update</b> seam. Ingests the model's
/// whole-test edit of ONE OUTDATED test, then deterministically protects invariants the model is
/// never trusted with, and persists through the unchanged <see cref="TestPersistenceService"/>.
///
/// Mirrors <see cref="GeneratedTestIngestor"/> (it reuses its parse/validate pipeline verbatim)
/// and adds three deterministic steps before persist (FR-003):
/// <list type="number">
/// <item><b>Id from original</b> — the persisted id (and file path) is the original's; the model's
/// id is ignored. This is an edit, not a create: no new id is allocated.</item>
/// <item><b>Manual/grounding re-assertion</b> — the original's grounding (including a
/// <see cref="VerificationVerdict.Manual"/> verdict and its note) is re-asserted onto the edited
/// test, so an edit can never silently strip human-curated content.</item>
/// <item><b>Drift guard</b> — a change to a protected field not implicated by the doc change
/// (priority, component, tags) is surfaced fail-loud (<see cref="DriftDetected"/>) rather than
/// persisted.</item>
/// </list>
/// On any failure nothing is written — the suite and every <c>_index.json</c> are left unchanged.
/// </summary>
public sealed class UpdatedTestIngestor
{
    /// <summary>The drift-guard failure class. Maps to the content-invalid exit class (5).</summary>
    public const string DriftDetected = "DRIFT_DETECTED";

    private readonly TestPersistenceService _persistence;
    private readonly TestValidator _validator;

    public UpdatedTestIngestor(TestPersistenceService persistence, TestValidator? validator = null)
    {
        ArgumentNullException.ThrowIfNull(persistence);
        _persistence = persistence;
        _validator = validator ?? new TestValidator();
    }

    /// <summary>
    /// Parses + validates the edited content, protects invariants against
    /// <paramref name="originalTest"/>, runs the drift guard, and (on success) persists the edited
    /// test into <paramref name="suite"/> — keeping the original id. On any failure nothing is
    /// written.
    /// </summary>
    /// <param name="content">The model's final message (expected: a JSON array with one edited test).</param>
    /// <param name="testsPath">Root tests path (e.g. <c>test-cases</c>).</param>
    /// <param name="suite">Suite the edited test belongs to.</param>
    /// <param name="originalTest">The on-disk test being edited — the source of truth for invariants.</param>
    /// <param name="existingTests">Existing suite tests, used to regenerate the index.</param>
    public async Task<IngestResult> IngestAsync(
        string? content,
        string testsPath,
        string suite,
        TestCase originalTest,
        IReadOnlyList<TestCase> existingTests,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(testsPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(suite);
        ArgumentNullException.ThrowIfNull(originalTest);
        ArgumentNullException.ThrowIfNull(existingTests);

        // 1. Reuse the generation parse/validate pipeline verbatim (fail-loud on any defect).
        var parsed = GeneratedTestIngestor.ParseAndValidate(content, _validator);
        if (!parsed.IsSuccess)
            return parsed; // fail loud — nothing persisted

        var candidate = parsed.PersistedTests[0]; // at least one guaranteed by NO_TESTS check

        // 2. Drift guard BEFORE any merge: a protected field the model changed must fail loud.
        var drift = CompareForDrift(originalTest, candidate);
        if (drift.HasDrift)
            return IngestResult.Failure(DriftDetected, drift.Describe());

        // 3. Build the persisted test: original copied, editable fields taken from the edit.
        //    Id, file path, grounding (incl. Manual), and every non-edited field come from the
        //    original — invariants are enforced here, not trusted to the model.
        var edited = ApplyEdit(originalTest, candidate);

        // 4. Persist through the single write+index path. allForIndex = existing with the original
        //    replaced by the edited test (same id ⇒ replace in place).
        var allForIndex = ReplaceById(existingTests, edited);
        await _persistence.PersistAsync(testsPath, suite, [edited], allForIndex, ct);

        return IngestResult.Success([edited]);
    }

    /// <summary>
    /// Deterministic drift check over the protected fields the model echoes but must not change
    /// (priority, component, tags). Returns the set of out-of-scope changes. Pure; no I/O.
    /// </summary>
    public static DriftReport CompareForDrift(TestCase original, TestCase candidate)
    {
        ArgumentNullException.ThrowIfNull(original);
        ArgumentNullException.ThrowIfNull(candidate);

        var entries = new List<DriftEntry>();

        if (original.Priority != candidate.Priority)
            entries.Add(new DriftEntry("priority", original.Priority.ToString(), candidate.Priority.ToString()));

        if (!string.Equals(original.Component ?? "", candidate.Component ?? "", StringComparison.Ordinal))
            entries.Add(new DriftEntry("component", original.Component, candidate.Component));

        if (!TagsEqual(original.Tags, candidate.Tags))
            entries.Add(new DriftEntry("tags",
                string.Join(", ", original.Tags), string.Join(", ", candidate.Tags)));

        return new DriftReport(entries);
    }

    /// <summary>
    /// Produces the test to persist: a copy of <paramref name="original"/> with only the editable
    /// content fields replaced from <paramref name="candidate"/>. Id, file path, grounding, and
    /// every protected / non-round-tripped field are preserved from the original.
    /// </summary>
    public static TestCase ApplyEdit(TestCase original, TestCase candidate)
    {
        ArgumentNullException.ThrowIfNull(original);
        ArgumentNullException.ThrowIfNull(candidate);

        return new TestCase
        {
            // Invariants — always from the original.
            Id = original.Id,
            FilePath = original.FilePath,
            Priority = original.Priority,
            Component = original.Component,
            Tags = original.Tags,
            // Manual / grounding re-asserted from the original (incl. Manual verdict + note).
            Grounding = original.Grounding,
            // Non-round-tripped fields preserved from the original.
            Description = original.Description,
            Environment = original.Environment,
            DependsOn = original.DependsOn,
            RelatedWorkItems = original.RelatedWorkItems,
            Custom = original.Custom,
            AutomatedBy = original.AutomatedBy,
            Requirements = original.Requirements,
            Bugs = original.Bugs,
            Status = original.Status,
            OrphanedReason = original.OrphanedReason,
            OrphanedDate = original.OrphanedDate,
            // Editable content — taken from the model's edit.
            Title = candidate.Title,
            Preconditions = candidate.Preconditions,
            Steps = candidate.Steps,
            ExpectedResult = candidate.ExpectedResult,
            TestData = candidate.TestData,
            ScenarioFromDoc = candidate.ScenarioFromDoc,
            SourceRefs = candidate.SourceRefs,
            Criteria = candidate.Criteria
        };
    }

    private static IReadOnlyList<TestCase> ReplaceById(IReadOnlyList<TestCase> existing, TestCase edited)
    {
        var replaced = false;
        var result = new List<TestCase>(existing.Count);
        foreach (var t in existing)
        {
            if (string.Equals(t.Id, edited.Id, StringComparison.OrdinalIgnoreCase))
            {
                result.Add(edited);
                replaced = true;
            }
            else
            {
                result.Add(t);
            }
        }
        if (!replaced)
            result.Add(edited); // original not in the supplied set — still index the edit
        return result;
    }

    private static bool TagsEqual(IReadOnlyList<string> a, IReadOnlyList<string> b)
    {
        // Order-insensitive — re-ordering tags is not drift.
        if (a.Count != b.Count) return false;
        var setA = new HashSet<string>(a, StringComparer.Ordinal);
        return b.All(setA.Contains);
    }
}

/// <summary>One out-of-scope field change found by the drift guard.</summary>
public sealed record DriftEntry(string FieldName, string? OriginalValue, string? EditedValue)
{
    /// <summary>A specific, retry-actionable message naming the field and the before/after.</summary>
    public string Describe() =>
        $"{FieldName}: original '{OriginalValue ?? ""}' -> edited '{EditedValue ?? ""}' "
        + "(out-of-scope change not implicated by the doc update)";
}

/// <summary>The result of comparing an edited candidate against the original for protected drift.</summary>
public sealed record DriftReport(IReadOnlyList<DriftEntry> Entries)
{
    /// <summary>True when any protected field changed.</summary>
    public bool HasDrift => Entries.Count > 0;

    /// <summary>The drift entries rendered as fail-loud messages.</summary>
    public IReadOnlyList<string> Describe() => Entries.Select(e => e.Describe()).ToList();
}
