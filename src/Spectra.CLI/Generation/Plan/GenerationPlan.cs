using System.Text;
using System.Text.Json;

namespace Spectra.CLI.Generation.Plan;

/// <summary>Spec 094: one behaviour of the analysis, as the plan records it.</summary>
/// <param name="Ref">The canonical section ref (null when unresolved).</param>
/// <param name="Reason">Why the source did not resolve (null when resolved).</param>
public sealed record PlannedBehavior(
    string Id, string Title, string Category, string Technique, string Source, string? Ref, bool Resolved, string? Reason);

/// <summary>Spec 094: a batch — its behaviours, the section refs they come from (in order) and how many tests to write.</summary>
public sealed record PlannedBatch(string BatchId, IReadOnlyList<string> BehaviorIds, IReadOnlyList<string> SourceRefs, int Count);

/// <summary>The plan file could not be read (invalid JSON, a missing key, a wrong type or an unknown version).</summary>
public sealed class GenerationPlanFormatException(string message) : Exception(message);

/// <summary>
/// Spec 094 (FR-003, data-model.md): the generation plan of one suite — the behaviours of the last successful analysis,
/// the fingerprints of their sections and the batches. Scratch: <c>.spectra/generation-plan/&lt;suite&gt;.json</c>,
/// replaced by the next analysis. Serialised with a fixed key order, sections sorted, LF and no timestamps, so identical
/// inputs give identical bytes; parsed strictly.
/// </summary>
public sealed record GenerationPlan(
    string Suite,
    string? Focus,
    int BatchSize,
    int Recommended,
    int PlannedTotal,
    string CountRule,
    IReadOnlyList<PlannedBehavior> Behaviors,
    IReadOnlyDictionary<string, string> Sections,
    IReadOnlyList<PlannedBatch> Batches,
    IReadOnlyList<string> Notes)
{
    public const int CurrentVersion = 1;

    /// <summary>Counts add up to <c>recommended</c>.</summary>
    public const string Proportional = "proportional";

    /// <summary><c>recommended</c> was smaller than the number of batches: every batch got 1.</summary>
    public const string OnePerBatch = "one_per_batch";

    /// <summary>The id of the batch of behaviours whose source did not resolve.</summary>
    public const string Unsourced = "unsourced";

    public PlannedBatch? FindBatch(string id) => Batches.FirstOrDefault(b => b.BatchId == id);

    public PlannedBehavior Behavior(string id) => Behaviors.First(b => b.Id == id);

    public static string Serialize(GenerationPlan plan)
    {
        using var stream = new MemoryStream();
        using (var w = new Utf8JsonWriter(stream, new JsonWriterOptions
        {
            Indented = true,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        }))
        {
            w.WriteStartObject();
            w.WriteNumber("version", CurrentVersion);
            w.WriteString("suite", plan.Suite);
            w.WriteString("focus", plan.Focus);
            w.WriteNumber("batch_size", plan.BatchSize);
            w.WriteNumber("recommended", plan.Recommended);
            w.WriteNumber("planned_total", plan.PlannedTotal);
            w.WriteString("count_rule", plan.CountRule);

            w.WriteStartArray("behaviors");
            foreach (var b in plan.Behaviors)
            {
                w.WriteStartObject();
                w.WriteString("id", b.Id);
                w.WriteString("title", b.Title);
                w.WriteString("category", b.Category);
                w.WriteString("technique", b.Technique);
                w.WriteString("source", b.Source);
                w.WriteString("ref", b.Ref);
                w.WriteBoolean("resolved", b.Resolved);
                if (!b.Resolved)
                {
                    w.WriteString("reason", b.Reason);
                }

                w.WriteEndObject();
            }

            w.WriteEndArray();

            w.WriteStartObject("sections");
            foreach (var (key, fingerprint) in plan.Sections.OrderBy(s => s.Key, StringComparer.Ordinal))
            {
                w.WriteString(key, fingerprint);
            }

            w.WriteEndObject();

            w.WriteStartArray("batches");
            foreach (var batch in plan.Batches)
            {
                w.WriteStartObject();
                w.WriteString("batch_id", batch.BatchId);
                WriteStrings(w, "behavior_ids", batch.BehaviorIds);
                WriteStrings(w, "source_refs", batch.SourceRefs);
                w.WriteNumber("count", batch.Count);
                w.WriteEndObject();
            }

            w.WriteEndArray();
            WriteStrings(w, "notes", plan.Notes);
            w.WriteEndObject();
        }

        return Encoding.UTF8.GetString(stream.ToArray()).Replace("\r\n", "\n") + "\n";
    }

    public static GenerationPlan Parse(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                throw new GenerationPlanFormatException("the plan is not a JSON object");
            }

            var version = Int(root, "version");
            if (version != CurrentVersion)
            {
                throw new GenerationPlanFormatException($"unknown plan version {version}");
            }

            var behaviors = Array(root, "behaviors").Select(b => new PlannedBehavior(
                Str(b, "id"), Str(b, "title"), Str(b, "category"), Str(b, "technique"), Str(b, "source"),
                NullableStr(b, "ref"), Bool(b, "resolved"),
                b.TryGetProperty("reason", out var r) && r.ValueKind == JsonValueKind.String ? r.GetString() : null)).ToList();

            var sectionsElement = Required(root, "sections", JsonValueKind.Object);
            var sections = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var property in sectionsElement.EnumerateObject())
            {
                if (property.Value.ValueKind != JsonValueKind.String)
                {
                    throw new GenerationPlanFormatException($"sections.{property.Name} is not a string");
                }

                sections[property.Name] = property.Value.GetString()!;
            }

            var batches = Array(root, "batches").Select(b => new PlannedBatch(
                Str(b, "batch_id"), Strings(b, "behavior_ids"), Strings(b, "source_refs"), Int(b, "count"))).ToList();

            return new GenerationPlan(
                Str(root, "suite"), NullableStr(root, "focus"), Int(root, "batch_size"), Int(root, "recommended"),
                Int(root, "planned_total"), Str(root, "count_rule"), behaviors, sections, batches, Strings(root, "notes"));
        }
        catch (JsonException ex)
        {
            throw new GenerationPlanFormatException($"invalid JSON: {ex.Message}");
        }
        catch (InvalidOperationException ex)
        {
            throw new GenerationPlanFormatException(ex.Message);
        }
    }

    private static void WriteStrings(Utf8JsonWriter w, string name, IEnumerable<string> values)
    {
        w.WriteStartArray(name);
        foreach (var value in values)
        {
            w.WriteStringValue(value);
        }

        w.WriteEndArray();
    }

    private static JsonElement Required(JsonElement e, string name, JsonValueKind kind)
    {
        if (!e.TryGetProperty(name, out var value) || value.ValueKind != kind)
        {
            throw new GenerationPlanFormatException($"'{name}' is missing or not {kind.ToString().ToLowerInvariant()}");
        }

        return value;
    }

    private static string Str(JsonElement e, string name) => Required(e, name, JsonValueKind.String).GetString()!;

    private static string? NullableStr(JsonElement e, string name)
    {
        if (!e.TryGetProperty(name, out var value) || value.ValueKind is not (JsonValueKind.String or JsonValueKind.Null))
        {
            throw new GenerationPlanFormatException($"'{name}' is missing or not a string");
        }

        return value.ValueKind == JsonValueKind.Null ? null : value.GetString();
    }

    private static int Int(JsonElement e, string name)
    {
        var value = Required(e, name, JsonValueKind.Number);
        return value.TryGetInt32(out var n) ? n : throw new GenerationPlanFormatException($"'{name}' is not an integer");
    }

    private static bool Bool(JsonElement e, string name)
    {
        if (!e.TryGetProperty(name, out var value) || value.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
        {
            throw new GenerationPlanFormatException($"'{name}' is missing or not a boolean");
        }

        return value.GetBoolean();
    }

    private static IEnumerable<JsonElement> Array(JsonElement e, string name)
    {
        var array = Required(e, name, JsonValueKind.Array);
        return array.EnumerateArray().Select(item => item.ValueKind == JsonValueKind.Object
            ? item.Clone()
            : throw new GenerationPlanFormatException($"an entry of '{name}' is not an object")).ToList();
    }

    private static IReadOnlyList<string> Strings(JsonElement e, string name) =>
        Required(e, name, JsonValueKind.Array).EnumerateArray().Select(item => item.ValueKind == JsonValueKind.String
            ? item.GetString()!
            : throw new GenerationPlanFormatException($"an entry of '{name}' is not a string")).ToList();
}
