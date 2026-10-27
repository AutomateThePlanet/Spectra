using System.Text.Json;
using Spectra.CLI.Commands.Baseline;
using Spectra.Core.Models;

namespace Spectra.CLI.Baseline;

/// <summary>
/// Spec 088 (research R3): resolves a triage group (<c>G1</c>, …) to its tests through the last triage's detail file,
/// for <c>update-ack --group</c> and <c>compile-update-prompt --group</c>. Refused when there is no triage for the suite
/// (<c>TRIAGE_MISSING</c>), the group isn't in it (<c>GROUP_NOT_FOUND</c>), or a member's sections or criteria changed
/// since (<c>TRIAGE_STALE</c>). <c>checkBucket</c> runs before the staleness check (update-ack's bucket refusals).
/// Extracted unchanged from Spec 084's <c>UpdateAckHandler</c>.
/// </summary>
public static class TriageGroupResolver
{
    public static async Task<(string Bucket, List<TestCase> Tests)> ResolveAsync(
        string root, string suite, string group, IReadOnlyDictionary<string, TestCase> byId, BaselineContext context,
        Action<string>? checkBucket, CancellationToken ct)
    {
        var path = Path.Combine(root, ".spectra", "update-triage.json");
        if (!File.Exists(path))
        {
            throw new BaselineStoreException(BaselineCommandIO.ErrorTriageMissing,
                $"No triage to resolve group {group} from: run 'spectra ai update-triage --suite {suite}' first.");
        }

        JsonElement detail;
        try
        {
            using var document = JsonDocument.Parse(await File.ReadAllTextAsync(path, ct));
            detail = document.RootElement.Clone();
        }
        catch (JsonException)
        {
            throw new BaselineStoreException(BaselineCommandIO.ErrorTriageMissing,
                $"{UpdateTriageHandler.DetailFile} is not a triage result: run 'spectra ai update-triage --suite {suite}' again.");
        }

        if (detail.ValueKind != JsonValueKind.Object
            || !detail.TryGetProperty("groups", out var groupsElement) || groupsElement.ValueKind != JsonValueKind.Array
            || !detail.TryGetProperty("tests", out var testsElement) || testsElement.ValueKind != JsonValueKind.Array)
        {
            throw new BaselineStoreException(BaselineCommandIO.ErrorTriageMissing,
                $"{UpdateTriageHandler.DetailFile} is not a triage result: run 'spectra ai update-triage --suite {suite}' again.");
        }

        var scope = detail.TryGetProperty("scope", out var s) ? s : default;
        var covers = scope.ValueKind == JsonValueKind.Object
            && ((scope.TryGetProperty("all_suites", out var all) && all.ValueKind == JsonValueKind.True)
                || (scope.TryGetProperty("suite", out var only) && only.GetString() == suite));
        if (!covers)
        {
            throw new BaselineStoreException(BaselineCommandIO.ErrorTriageMissing,
                $"The last triage did not cover suite '{suite}': run 'spectra ai update-triage --suite {suite}' first.");
        }

        var match = groupsElement.EnumerateArray()
            .FirstOrDefault(g => string.Equals(g.GetProperty("id").GetString(), group, StringComparison.OrdinalIgnoreCase));
        if (match.ValueKind != JsonValueKind.Object)
        {
            throw new BaselineStoreException(BaselineCommandIO.ErrorGroupNotFound, $"Group {group} is not in the last triage.");
        }

        var groupSuite = match.GetProperty("suite").GetString();
        if (groupSuite != suite)
        {
            throw new BaselineStoreException(BaselineCommandIO.ErrorGroupNotFound,
                $"Group {group} is in suite '{groupSuite}', not '{suite}'.");
        }

        var bucket = match.GetProperty("bucket").GetString()!;
        checkBucket?.Invoke(bucket);

        var recorded = testsElement.EnumerateArray()
            .Where(t => t.GetProperty("suite").GetString() == suite)
            .ToDictionary(t => t.GetProperty("id").GetString()!, t => t.GetProperty("current"), StringComparer.Ordinal);

        var targets = new List<TestCase>();
        var stale = new List<string>();
        foreach (var id in match.GetProperty("tests").EnumerateArray().Select(t => t.GetString()!))
        {
            if (!byId.TryGetValue(id, out var test) || !recorded.TryGetValue(id, out var current)
                || !await MatchesAsync(test, current, context, ct))
            {
                stale.Add(id);
                continue;
            }

            targets.Add(test);
        }

        if (stale.Count > 0)
        {
            throw new BaselineStoreException(BaselineCommandIO.ErrorTriageStale,
                $"Group {group} changed since the last triage ({string.Join(", ", stale)}): run 'spectra ai update-triage --suite {suite}' again and review.");
        }

        return (bucket, targets);
    }

    /// <summary>Whether the test's current refs and criteria (and their fingerprints) are what triage recorded.</summary>
    private static async Task<bool> MatchesAsync(TestCase test, JsonElement recorded, BaselineContext context, CancellationToken ct)
    {
        var refs = new SortedDictionary<string, string?>(StringComparer.Ordinal);
        foreach (var sourceRef in test.SourceRefs.Where(r => !string.IsNullOrWhiteSpace(r)).Distinct(StringComparer.Ordinal))
        {
            refs[sourceRef] = (await context.GetSectionAsync(sourceRef, ct)).Current.Fingerprint;
        }

        var criteria = new SortedDictionary<string, string?>(StringComparer.Ordinal);
        foreach (var id in test.Criteria.Where(c => !string.IsNullOrWhiteSpace(c)).Distinct(StringComparer.Ordinal))
        {
            criteria[id] = context.GetCriterion(id).Fingerprint;
        }

        return SameMap(refs, recorded.GetProperty("refs")) && SameMap(criteria, recorded.GetProperty("criteria"));
    }

    private static bool SameMap(SortedDictionary<string, string?> now, JsonElement recorded)
    {
        var then = recorded.EnumerateObject().ToDictionary(p => p.Name, p => p.Value.ValueKind == JsonValueKind.Null ? null : p.Value.GetString());
        return then.Count == now.Count && now.All(kv => then.TryGetValue(kv.Key, out var v) && v == kv.Value);
    }

    /// <summary>The recovery for a RELINK refusal.</summary>
}
