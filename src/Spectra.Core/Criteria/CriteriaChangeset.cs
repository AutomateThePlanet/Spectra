using System.Text.Json.Serialization;
using Spectra.Core.Models.Coverage;
using Spectra.Core.Text;

namespace Spectra.Core.Criteria;

/// <summary>
/// What changed in one document's acceptance criteria between the previous and the new set (Spec 079 FR-012).
/// Lists are sorted by ID so the result is deterministic.
/// </summary>
public sealed record CriteriaChangeset(
    [property: JsonPropertyName("doc")] string Doc,
    [property: JsonPropertyName("added")] IReadOnlyList<AddedCriterion> Added,
    [property: JsonPropertyName("modified")] IReadOnlyList<ModifiedCriterion> Modified,
    [property: JsonPropertyName("removed")] IReadOnlyList<RemovedCriterion> Removed,
    [property: JsonPropertyName("metadata")] IReadOnlyList<MetadataChange> Metadata)
{
    [JsonPropertyName("is_empty")]
    public bool IsEmpty => Added.Count == 0 && Modified.Count == 0 && Removed.Count == 0 && Metadata.Count == 0;

    /// <summary>Metadata fields compared on kept IDs, in report order.</summary>
    public static readonly IReadOnlyList<string> MetadataFields =
        ["priority", "rfc2119", "source_section", "tags", "technique_hint"];

    public static CriteriaChangeset Build(
        string doc, IReadOnlyList<AcceptanceCriterion> previous, IReadOnlyList<AcceptanceCriterion> next)
    {
        ArgumentNullException.ThrowIfNull(previous);
        ArgumentNullException.ThrowIfNull(next);

        var before = ById(previous);
        var after = ById(next);

        var added = after.Keys.Where(id => !before.ContainsKey(id))
            .Select(id => new AddedCriterion(id, after[id].Text));
        var removed = before.Keys.Where(id => !after.ContainsKey(id))
            .Select(id => new RemovedCriterion(id, before[id].Text, 0, []));

        var modified = new List<ModifiedCriterion>();
        var metadata = new List<MetadataChange>();
        foreach (var id in after.Keys.Where(before.ContainsKey))
        {
            var (old, now) = (before[id], after[id]);
            if (TextNormalizer.Normalize(old.Text) != TextNormalizer.Normalize(now.Text))
            {
                modified.Add(new ModifiedCriterion(id, old.Text, now.Text));
            }

            foreach (var field in MetadataFields)
            {
                var (o, n) = (Field(old, field), Field(now, field));
                var same = field == "tags"
                    ? string.Equals(TagsKey(old.Tags), TagsKey(now.Tags), StringComparison.Ordinal)
                    : string.Equals(o, n, StringComparison.Ordinal);
                if (!same)
                {
                    metadata.Add(new MetadataChange(id, field, o, n));
                }
            }
        }

        return new CriteriaChangeset(
            doc,
            added.OrderBy(a => a.Id, StringComparer.Ordinal).ToList(),
            modified.OrderBy(m => m.Id, StringComparer.Ordinal).ToList(),
            removed.OrderBy(r => r.Id, StringComparer.Ordinal).ToList(),
            metadata.OrderBy(m => m.Id, StringComparer.Ordinal).ToList());
    }

    /// <summary>Fills each removed criterion's referencing-test count and IDs from <paramref name="testsById"/>.</summary>
    public CriteriaChangeset WithReferences(IReadOnlyDictionary<string, IReadOnlyList<string>> testsById)
    {
        ArgumentNullException.ThrowIfNull(testsById);
        var removed = Removed
            .Select(r => testsById.TryGetValue(r.Id, out var tests)
                ? r with { ReferencingTests = tests.Count, TestIds = tests }
                : r)
            .ToList();
        return this with { Removed = removed };
    }

    private static Dictionary<string, AcceptanceCriterion> ById(IReadOnlyList<AcceptanceCriterion> criteria) =>
        criteria
            .Where(c => !string.IsNullOrWhiteSpace(c.Id))
            .GroupBy(c => c.Id, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Spec 080 B4: tags compare as a case-insensitive set (order and duplicates ignored). The reported old/new
    /// values keep the original text.
    /// </summary>
    private static string TagsKey(IEnumerable<string> tags) => string.Join(", ", tags
        .Select(t => t.Trim())
        .Where(t => t.Length > 0)
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .OrderBy(t => t, StringComparer.OrdinalIgnoreCase)
        .Select(t => t.ToLowerInvariant()));

    private static string? Field(AcceptanceCriterion c, string field) => field switch
    {
        "priority" => c.Priority,
        "rfc2119" => c.Rfc2119,
        "source_section" => c.SourceSection,
        "tags" => c.Tags.Count == 0 ? null : string.Join(", ", c.Tags),
        "technique_hint" => c.TechniqueHint,
        _ => null
    };
}

public sealed record AddedCriterion(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("text")] string Text);

public sealed record ModifiedCriterion(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("old_text")] string OldText,
    [property: JsonPropertyName("new_text")] string NewText);

public sealed record RemovedCriterion(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("old_text")] string OldText,
    [property: JsonPropertyName("referencing_tests")] int ReferencingTests,
    [property: JsonPropertyName("test_ids")] IReadOnlyList<string> TestIds);

public sealed record MetadataChange(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("field")] string Field,
    [property: JsonPropertyName("old")] string? Old,
    [property: JsonPropertyName("new")] string? New);
