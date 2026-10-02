using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace Spectra.Core.Baseline;

/// <summary>
/// Spec 084 (FR-007): one suite's <c>_source-baseline.json</c>. Per test, the fingerprint of each ref (as written) and
/// of each linked criterion when the test was last written, edited or acknowledged. Every map is sorted ordinally, so
/// the file is deterministic and one test's change is a local diff. The suite name is never stored (rename-safe).
/// </summary>
public sealed class SourceBaseline
{
    public const int CurrentVersion = 1;

    public const string FileName = "_source-baseline.json";

    public SortedDictionary<string, BaselineEntry> Tests { get; } = new(StringComparer.Ordinal);
}

/// <summary>One test's baseline: ref → fingerprint and criterion ID → fingerprint.</summary>
public sealed class BaselineEntry
{
    public SortedDictionary<string, string> Refs { get; } = new(StringComparer.Ordinal);

    public SortedDictionary<string, string> Criteria { get; } = new(StringComparer.Ordinal);

    public bool ContentEquals(BaselineEntry other) =>
        Refs.SequenceEqual(other.Refs) && Criteria.SequenceEqual(other.Criteria);

    public BaselineEntry Clone()
    {
        var copy = new BaselineEntry();
        foreach (var (key, value) in Refs) copy.Refs[key] = value;
        foreach (var (key, value) in Criteria) copy.Criteria[key] = value;
        return copy;
    }
}

/// <summary>A baseline file that is not valid JSON or not the documented shape. Never treated as empty.</summary>
public sealed class BaselineFormatException(string file, string problem)
    : Exception($"{file} is not a valid source baseline: {problem}")
{
    public string File { get; } = file;
}

/// <summary>
/// Deterministic writer and strict reader for <see cref="SourceBaseline"/>: UTF-8 without BOM, 2-space indent, LF line
/// endings (also on Windows, where the .NET 8 writer would use CRLF), one trailing newline, relaxed escaping so
/// <c>#</c>, <c>→</c> and non-ASCII text stay readable.
/// </summary>
public static class SourceBaselineSerializer
{
    private static readonly JsonWriterOptions WriterOptions = new()
    {
        Indented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public static string Serialize(SourceBaseline baseline)
    {
        ArgumentNullException.ThrowIfNull(baseline);

        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, WriterOptions))
        {
            writer.WriteStartObject();
            writer.WriteNumber("version", SourceBaseline.CurrentVersion);
            writer.WriteStartObject("tests");
            foreach (var (id, entry) in baseline.Tests)
            {
                writer.WriteStartObject(id);
                WriteMap(writer, "refs", entry.Refs);
                WriteMap(writer, "criteria", entry.Criteria);
                writer.WriteEndObject();
            }

            writer.WriteEndObject();
            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(stream.ToArray()).Replace("\r\n", "\n") + "\n";
    }

    /// <summary>Parses a baseline file strictly. <paramref name="file"/> is only used in error messages.</summary>
    public static SourceBaseline Parse(string json, string file)
    {
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(json);
        }
        catch (JsonException ex)
        {
            throw new BaselineFormatException(file, $"invalid JSON ({ex.Message})");
        }

        using (document)
        {
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                throw new BaselineFormatException(file, "the root must be an object");
            }

            if (!root.TryGetProperty("version", out var version) || version.ValueKind != JsonValueKind.Number
                || !version.TryGetInt32(out var v) || v != SourceBaseline.CurrentVersion)
            {
                throw new BaselineFormatException(file, $"'version' must be {SourceBaseline.CurrentVersion}");
            }

            if (!root.TryGetProperty("tests", out var tests) || tests.ValueKind != JsonValueKind.Object)
            {
                throw new BaselineFormatException(file, "'tests' must be an object");
            }

            var baseline = new SourceBaseline();
            foreach (var test in tests.EnumerateObject())
            {
                if (test.Value.ValueKind != JsonValueKind.Object)
                {
                    throw new BaselineFormatException(file, $"the entry for '{test.Name}' must be an object");
                }

                var entry = new BaselineEntry();
                ReadMap(file, test.Name, test.Value, "refs", entry.Refs);
                ReadMap(file, test.Name, test.Value, "criteria", entry.Criteria);
                if (!baseline.Tests.TryAdd(test.Name, entry))
                {
                    throw new BaselineFormatException(file, $"test '{test.Name}' appears more than once");
                }
            }

            return baseline;
        }
    }

    private static void WriteMap(Utf8JsonWriter writer, string name, SortedDictionary<string, string> map)
    {
        writer.WriteStartObject(name);
        foreach (var (key, value) in map)
        {
            writer.WriteString(key, value);
        }

        writer.WriteEndObject();
    }

    private static void ReadMap(
        string file, string test, JsonElement entry, string name, SortedDictionary<string, string> target)
    {
        if (!entry.TryGetProperty(name, out var map) || map.ValueKind != JsonValueKind.Object)
        {
            throw new BaselineFormatException(file, $"the entry for '{test}' must have a '{name}' object");
        }

        foreach (var item in map.EnumerateObject())
        {
            if (item.Value.ValueKind != JsonValueKind.String || !SourceFingerprint.IsValid(item.Value.GetString()))
            {
                throw new BaselineFormatException(file,
                    $"'{test}' {name} '{item.Name}' must be a fingerprint of the form sha256:<64 lowercase hex digits>");
            }

            if (!target.TryAdd(item.Name, item.Value.GetString()!))
            {
                throw new BaselineFormatException(file, $"'{test}' {name} '{item.Name}' appears more than once");
            }
        }
    }
}
