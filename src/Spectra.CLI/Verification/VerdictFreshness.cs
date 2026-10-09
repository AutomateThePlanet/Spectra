using System.Text.Json;
using System.Text.Json.Serialization;
using Spectra.Core.Models;
using Spectra.Core.Parsing;

namespace Spectra.CLI.Verification;

/// <summary>
/// Spec 090 (FR-A2): what <c>compile-critic-prompt</c> records for one test — the content fingerprint the critic was
/// given — at <c>.spectra/verdicts/critic-input-&lt;id&gt;.json</c>. Written by the CLI, never by the model.
/// </summary>
public sealed record VerdictRecord(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("suite")] string? Suite,
    [property: JsonPropertyName("file")] string File,
    [property: JsonPropertyName("fingerprint")] string Fingerprint);

/// <summary>Spec 090: whether a verdict still describes the test's current content.</summary>
public enum Freshness
{
    /// <summary>The recorded fingerprint equals the test's current one.</summary>
    Fresh,

    /// <summary>The test changed since the critic prompt was compiled.</summary>
    Stale,

    /// <summary>No record (a verdict from before 090, stdin, or a file outside <c>.spectra/verdicts/</c>): accepted as before.</summary>
    NoRecord
}

public sealed record FreshnessResult(Freshness State, string? Recorded = null, string? Current = null)
{
    public bool IsStale => State == Freshness.Stale;

    /// <summary>The refusal message for a stale verdict.</summary>
    public string Message(string id) =>
        $"The verdict for {id} was given to different content (recorded {Recorded}, now {Current}); "
        + $"run the critic for {id} again.";
}

/// <summary>Spec 090 (FR-A2): the fingerprint record of each verdict, and the staleness check against it.</summary>
public static class VerdictFreshness
{
    public const string ErrorVerdictStale = "VERDICT_STALE";

    private const string VerdictPrefix = "critic-verdict-";
    private const string RecordPrefix = "critic-input-";

    public static string VerdictsDir(string root) => Path.Combine(root, ".spectra", "verdicts");

    public static string VerdictPath(string root, string id) => Path.Combine(VerdictsDir(root), $"{VerdictPrefix}{id}.json");

    public static string RecordPath(string root, string id) => Path.Combine(VerdictsDir(root), $"{RecordPrefix}{id}.json");

    /// <summary>Writes the record atomically (temp file, then move).</summary>
    public static async Task WriteRecordAsync(string root, VerdictRecord record, CancellationToken ct = default)
    {
        var path = RecordPath(root, record.Id);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temp = $"{path}.{Guid.NewGuid():N}.tmp";
        await File.WriteAllTextAsync(temp, JsonSerializer.Serialize(record), ct);
        File.Move(temp, path, overwrite: true);
    }

    public static VerdictRecord? ReadRecord(string root, string id)
    {
        var path = RecordPath(root, id);
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<VerdictRecord>(File.ReadAllText(path));
        }
        catch (JsonException)
        {
            return null; // an unreadable record is treated as no record
        }
    }

    /// <summary>Checks the verdict of <paramref name="id"/> against the test's current content.</summary>
    public static FreshnessResult Check(string root, string id, TestCase current)
    {
        var record = ReadRecord(root, id);
        if (record is null)
        {
            return new FreshnessResult(Freshness.NoRecord);
        }

        var now = TestContentFingerprint.Of(current);
        return string.Equals(record.Fingerprint, now, StringComparison.Ordinal)
            ? new FreshnessResult(Freshness.Fresh, record.Fingerprint, now)
            : new FreshnessResult(Freshness.Stale, record.Fingerprint, now);
    }

    /// <summary>Checks against the test file the record names (single-form <c>ingest-verdict --from</c>).</summary>
    public static async Task<FreshnessResult> CheckRecordedTestAsync(string root, string id, CancellationToken ct = default)
    {
        var record = ReadRecord(root, id);
        if (record is null)
        {
            return new FreshnessResult(Freshness.NoRecord);
        }

        var path = Path.Combine(root, record.File.Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(path))
        {
            return new FreshnessResult(Freshness.NoRecord);
        }

        var parsed = new TestCaseParser().Parse(await File.ReadAllTextAsync(path, ct), Path.GetFileName(path));
        return parsed.IsSuccess ? Check(root, id, parsed.Value!) : new FreshnessResult(Freshness.NoRecord);
    }

    /// <summary>The test id of a verdict file in <c>.spectra/verdicts/</c> named <c>critic-verdict-&lt;id&gt;.json</c>; else null.</summary>
    public static string? IdOfVerdictFile(string root, string path)
    {
        var full = Path.GetFullPath(path);
        if (!string.Equals(Path.GetDirectoryName(full), Path.GetFullPath(VerdictsDir(root)), StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var name = Path.GetFileNameWithoutExtension(full);
        return name.StartsWith(VerdictPrefix, StringComparison.OrdinalIgnoreCase) ? name[VerdictPrefix.Length..] : null;
    }
}

/// <summary>Spec 090 (FR-A1, FR-C2): removes a test's verdict file and its fingerprint record.</summary>
public static class VerdictFiles
{
    /// <summary>The paths that exist (and would be removed) for <paramref name="id"/>.</summary>
    public static IReadOnlyList<string> Existing(string root, string id) =>
        new[] { VerdictFreshness.VerdictPath(root, id), VerdictFreshness.RecordPath(root, id) }.Where(File.Exists).ToList();

    /// <summary>Deletes the verdict file and the record when present; returns the removed paths.</summary>
    public static IReadOnlyList<string> Remove(string root, string id)
    {
        var removed = new List<string>();
        foreach (var path in Existing(root, id))
        {
            File.Delete(path);
            removed.Add(path);
        }

        return removed;
    }
}
