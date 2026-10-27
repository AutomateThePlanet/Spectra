using System.Text.Json;
using System.Text.Json.Serialization;
using Spectra.Core.Models;
using Spectra.Core.Parsing;

namespace Spectra.CLI.Verification;

/// <summary>
/// Spec 090 (FR-A2): what <c>compile-critic-prompt</c> records for one test — the content fingerprint the critic was
/// given — at <c>.spectra/verdicts/critic-input-&lt;id&gt;.json</c>. Written by the CLI, never by the model.
/// Spec 091 (FR-001): <see cref="CompiledAt"/> is when the record was written; a 090 record has none.
/// </summary>
public sealed record VerdictRecord(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("suite")] string? Suite,
    [property: JsonPropertyName("file")] string File,
    [property: JsonPropertyName("fingerprint")] string Fingerprint,
    [property: JsonPropertyName("compiled_at")] DateTimeOffset? CompiledAt = null);

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

/// <summary>Spec 091: why a verdict is stale.</summary>
public enum StaleReason
{
    /// <summary>Spec 090: the test changed since the critic prompt was compiled.</summary>
    ContentChanged,

    /// <summary>
    /// Spec 091 (FR-002): the verdict file was written before the last critic prompt for the test. Spec 097 (FR-013): "the
    /// last critic prompt" is the record file's write time.
    /// </summary>
    OlderThanPrompt
}

public sealed record FreshnessResult(
    Freshness State,
    string? Recorded = null,
    string? Current = null,
    StaleReason Reason = StaleReason.ContentChanged,
    DateTime? VerdictWrittenAt = null,
    DateTimeOffset? CompiledAt = null,
    DateTime? ReferenceAt = null)
{
    public bool IsStale => State == Freshness.Stale;

    /// <summary>The refusal message for a stale verdict.</summary>
    public string Message(string id) => Reason == StaleReason.OlderThanPrompt
        ? $"The verdict for {id} is older than the last critic prompt for {id} "
          + $"(verdict written {VerdictWrittenAt:u}, prompt compiled {ReferenceAt ?? CompiledAt?.UtcDateTime:u}); run the critic for {id} again."
        : $"The verdict for {id} was given to different content (recorded {Recorded}, now {Current}); "
          + $"run the critic for {id} again.";
}

/// <summary>Spec 090 (FR-A2): the fingerprint record of each verdict, and the staleness check against it.</summary>
public static class VerdictFreshness
{
    public const string ErrorVerdictStale = "VERDICT_STALE";

    /// <summary>
    /// Spec 091 (FR-002): how much older than the reference time a verdict file may look and still be fresh — coarse
    /// file-system timestamps. Spec 097: both times now come from the file system, so no clock gap needs covering.
    /// </summary>
    public static readonly TimeSpan TimestampAllowance = TimeSpan.FromSeconds(2);

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

    /// <summary>
    /// Checks the verdict of <paramref name="id"/> against the test's current content (Spec 090) and, when the record
    /// has <c>compiled_at</c> and <paramref name="verdictPath"/> is the file being ingested, against the time the
    /// critic prompt was compiled (Spec 091): a verdict written more than <see cref="TimestampAllowance"/> before it is
    /// stale. Spec 097 (FR-013): that time is the record file's own write time — set by the same file system as the
    /// verdict file — and <c>compiled_at</c> only when the record's file time can't be read, so a CLI clock that differs
    /// from the file system's (a network share, a container mount) no longer decides. A 090 record (no
    /// <c>compiled_at</c>), or a verdict without a file (stdin), gets the fingerprint rule only.
    /// </summary>
    public static FreshnessResult Check(string root, string id, TestCase current, string? verdictPath = null)
    {
        var record = ReadRecord(root, id);
        if (record is null)
        {
            return new FreshnessResult(Freshness.NoRecord);
        }

        var now = TestContentFingerprint.Of(current);
        if (!string.Equals(record.Fingerprint, now, StringComparison.Ordinal))
        {
            return new FreshnessResult(Freshness.Stale, record.Fingerprint, now);
        }

        if (record.CompiledAt is { } compiledAt && verdictPath is not null && System.IO.File.Exists(verdictPath))
        {
            var writtenAt = System.IO.File.GetLastWriteTimeUtc(verdictPath);
            var reference = RecordWrittenAt(root, id) ?? compiledAt.UtcDateTime;
            if (writtenAt < reference - TimestampAllowance)
            {
                return new FreshnessResult(Freshness.Stale, record.Fingerprint, now, StaleReason.OlderThanPrompt, writtenAt, compiledAt, reference);
            }
        }

        return new FreshnessResult(Freshness.Fresh, record.Fingerprint, now);
    }

    /// <summary>Spec 097 (FR-013): the record file's write time, or null when it can't be read.</summary>
    private static DateTime? RecordWrittenAt(string root, string id)
    {
        try
        {
            var path = RecordPath(root, id);
            return File.Exists(path) ? File.GetLastWriteTimeUtc(path) : null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>Checks against the test file the record names (single-form <c>ingest-verdict --from</c>).</summary>
    public static async Task<FreshnessResult> CheckRecordedTestAsync(
        string root, string id, string? verdictPath = null, CancellationToken ct = default)
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

        var parsed = new TestCaseParser(captureLayout: false).Parse(await File.ReadAllTextAsync(path, ct), Path.GetFileName(path));
        return parsed.IsSuccess ? Check(root, id, parsed.Value!, verdictPath) : new FreshnessResult(Freshness.NoRecord);
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
