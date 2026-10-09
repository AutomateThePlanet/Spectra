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
/// <see cref="VerificationVerdict.Manual"/> verdict with its source, author and note) and every
/// non-edited field are kept from the original, so an edit can never silently strip human-curated
/// content.</item>
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

    /// <summary>Spec 089 (F2): the edit holds more than one test object. Content-invalid exit class (5).</summary>
    public const string MultipleTests = "MULTIPLE_TESTS";

    /// <summary>Spec 089 (F2): names the count and every object's id (<c>(no id)</c> when it has none).</summary>
    public static string MultipleTestsMessage(IReadOnlyList<string?> ids) =>
        $"The edit holds {ids.Count} test objects ({string.Join(", ", ids.Select(id => id ?? "(no id)"))}); "
        + "a staged file holds exactly one test.";

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
        CancellationToken ct = default,
        bool persist = true)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(testsPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(suite);
        ArgumentNullException.ThrowIfNull(originalTest);
        ArgumentNullException.ThrowIfNull(existingTests);

        // Spec 089 (F2): one test per edit. Counted before validation, so an array with a second (even invalid) object
        // never slips through as "one test".
        if (GeneratedTestIngestor.ArrayObjects(content) is { Count: > 1 } objects)
            return IngestResult.Failure(MultipleTests, MultipleTestsMessage(objects));

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

        // Spec 088 FR-015: an edit that changes nothing is not written (the caller records it as reviewed).
        if (IsUnchanged(originalTest, edited))
            return IngestResult.NoChange(originalTest);

        // 4. Persist through the single write+index path. allForIndex = existing with the original
        //    replaced by the edited test (same id ⇒ replace in place).
        var allForIndex = ReplaceById(existingTests, edited);
        // Spec 090 (FR-B2): a dry run returns exactly what a real run would, without writing.
        if (persist)
        {
            await _persistence.PersistAsync(testsPath, suite, [edited], allForIndex, ct);
        }

        return IngestResult.Success([edited], manualVerdictKept: edited.Grounding is not null);
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
    /// Produces the test to persist: <paramref name="original"/> with only the editable content fields
    /// replaced from <paramref name="candidate"/>. Everything else — id, file path, priority, component,
    /// tags, a Manual verdict, estimated duration, links, custom metadata, lifecycle fields and the source
    /// layout (unknown frontmatter keys and body sections) — stays the original's. Spec 090 (FR-A1): a critic
    /// grounding block is dropped, because it judged the old content.
    /// </summary>
    public static TestCase ApplyEdit(TestCase original, TestCase candidate)
    {
        ArgumentNullException.ThrowIfNull(original);
        ArgumentNullException.ThrowIfNull(candidate);

        return original with
        {
            // Spec 090 (FR-A1): a critic verdict described the old content, so an edit drops it; a Manual verdict (a
            // human judgement) is kept.
            Grounding = original.Grounding?.Verdict == VerificationVerdict.Manual ? original.Grounding : null,
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

    /// <summary>
    /// Spec 088 FR-015: true when <paramref name="candidate"/> equals <paramref name="original"/> in every editable
    /// field (the <see cref="ApplyEdit"/> fields). For the comparison only, CRLF / CR count as LF, trailing whitespace
    /// of each line and of each field is ignored, and null equals empty; whitespace inside a line counts. Lists
    /// compare by sequence. Pure; nothing is altered.
    /// </summary>
    public static bool IsUnchanged(TestCase original, TestCase candidate)
    {
        ArgumentNullException.ThrowIfNull(original);
        ArgumentNullException.ThrowIfNull(candidate);

        return Same(original.Title, candidate.Title)
            && Same(original.Preconditions, candidate.Preconditions)
            && SameList(original.Steps, candidate.Steps)
            && Same(original.ExpectedResult, candidate.ExpectedResult)
            && Same(original.TestData, candidate.TestData)
            && Same(original.ScenarioFromDoc, candidate.ScenarioFromDoc)
            && SameList(original.SourceRefs, candidate.SourceRefs)
            && SameList(original.Criteria, candidate.Criteria);

        static bool SameList(IReadOnlyList<string> a, IReadOnlyList<string> b) =>
            a.Count == b.Count && a.Zip(b).All(p => Same(p.First, p.Second));

        static bool Same(string? a, string? b) => string.Equals(Comparable(a), Comparable(b), StringComparison.Ordinal);

        static string Comparable(string? value)
        {
            if (string.IsNullOrEmpty(value)) return "";
            var lines = value.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
            return string.Join('\n', lines.Select(l => l.TrimEnd())).TrimEnd();
        }
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
