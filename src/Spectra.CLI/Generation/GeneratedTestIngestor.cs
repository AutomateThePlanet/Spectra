using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.RegularExpressions;
using Spectra.CLI.IO;
using Spectra.Core.Models;
using Spectra.Core.Validation;

namespace Spectra.CLI.Generation;

/// <summary>
/// The fail-loud validation boundary (Spec 053 FR-005/FR-006). Ingests agent-generated test
/// content, parses it with the relocated parse pipeline, validates the whole batch with the
/// unchanged <see cref="TestValidator"/>, and persists only when every test is valid — through
/// the unchanged <see cref="TestPersistenceService"/> (FR-008).
///
/// Hardened vs. the old in-CLI parse: there is <b>no</b> silent truncation salvage. Malformed,
/// truncated, empty, or schema-violating content yields a specific <see cref="IngestResult"/>
/// failure and persists nothing. The error is specific enough for a retry skill (Spec 055) to
/// re-prompt the agent against (FR-007).
/// </summary>
public sealed class GeneratedTestIngestor
{
    private readonly TestPersistenceService _persistence;
    private readonly TestValidator _validator;

    public GeneratedTestIngestor(TestPersistenceService persistence, TestValidator? validator = null)
    {
        ArgumentNullException.ThrowIfNull(persistence);
        _persistence = persistence;
        _validator = validator ?? new TestValidator();
    }

    /// <summary>
    /// Parses, validates, and (on success) persists agent content into <paramref name="suite"/>.
    /// On any failure nothing is written — <paramref name="testsPath"/> and every
    /// <c>_index.json</c> are left byte-for-byte unchanged.
    /// </summary>
    /// <param name="content">The agent's final message (expected to contain a JSON array).</param>
    /// <param name="testsPath">Root tests path (e.g. <c>test-cases</c>).</param>
    /// <param name="suite">Target suite.</param>
    /// <param name="existingTests">Existing suite tests, used to regenerate the index.</param>
    public async Task<IngestResult> IngestAsync(
        string? content,
        string testsPath,
        string suite,
        IReadOnlyList<TestCase> existingTests,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(testsPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(suite);
        ArgumentNullException.ThrowIfNull(existingTests);

        var parsed = ParseAndValidate(content, _validator);
        if (!parsed.IsSuccess)
            return parsed; // fail loud — nothing persisted

        var tests = parsed.PersistedTests; // validated, not yet written
        var allForIndex = MergeForIndex(existingTests, tests);

        await _persistence.PersistAsync(testsPath, suite, tests, allForIndex, ct);

        return IngestResult.Success(tests);
    }

    /// <summary>
    /// Pure parse + validate with no I/O. Returns a success result carrying the validated tests
    /// (not yet persisted) or a fail-loud result. Exposed for token-free unit testing of the
    /// boundary contract.
    /// </summary>
    public static IngestResult ParseAndValidate(string? content, TestValidator validator)
    {
        ArgumentNullException.ThrowIfNull(validator);

        if (string.IsNullOrWhiteSpace(content))
            return IngestResult.Failure(IngestErrorCode.EmptyContent,
                "Agent returned no content.");

        var json = ExtractJson(content);
        if (string.IsNullOrWhiteSpace(json))
            return IngestResult.Failure(IngestErrorCode.EmptyContent,
                "No JSON array found in the agent response.");

        if (!TryParseArray(json, out var array))
        {
            // No salvage (FR-006). Distinguish a token-truncated array (opened '[', never
            // closed) from genuinely malformed JSON so the skill can re-prompt precisely.
            var trimmed = json.TrimStart();
            if (trimmed.StartsWith('[') && !json.TrimEnd().EndsWith(']'))
                return IngestResult.Failure(IngestErrorCode.Truncated,
                    "The JSON array was not closed — the response appears truncated "
                    + "(token limit). Regenerate; do not omit the closing ']'.");

            return IngestResult.Failure(IngestErrorCode.MalformedJson,
                "The response did not parse as a JSON array of test cases.");
        }

        var tests = new List<TestCase>();
        foreach (var element in array.Value.EnumerateArray())
        {
            var test = ParseTestCase(element);
            if (test is not null)
                tests.Add(test);
        }

        // No upper cap on tests.Count by design: the generate `--count` is advisory (it only shapes the
        // prompt), so ingest accepts however many valid tests were produced. Only zero is an error.
        if (tests.Count == 0)
            return IngestResult.Failure(IngestErrorCode.NoTests,
                "A JSON array was parsed but contained no valid test objects "
                + "(each test needs at least an id and a title).");

        var validation = validator.ValidateAll(tests);
        if (!validation.IsValid)
        {
            var messages = validation.Errors
                .Select(e => $"{e.TestId ?? "(unknown)"}: [{e.Code}] {e.Message}")
                .ToList();
            return IngestResult.Failure(IngestErrorCode.SchemaInvalid, messages);
        }

        return IngestResult.Success(tests);
    }

    private static IReadOnlyList<TestCase> MergeForIndex(
        IReadOnlyList<TestCase> existing,
        IReadOnlyList<TestCase> incoming)
    {
        // Incoming wins on id collision; preserve a stable order (existing first, then new).
        var byId = new Dictionary<string, TestCase>(StringComparer.OrdinalIgnoreCase);
        var order = new List<string>();
        foreach (var t in existing)
        {
            if (!byId.ContainsKey(t.Id)) order.Add(t.Id);
            byId[t.Id] = t;
        }
        foreach (var t in incoming)
        {
            if (!byId.ContainsKey(t.Id)) order.Add(t.Id);
            byId[t.Id] = t;
        }
        return order.Select(id => byId[id]).ToList();
    }

    // --- Relocated parse pipeline (Spec 053). No TryRepairTruncatedArray. ---

    private static string ExtractJson(string response)
    {
        // Strategy 1: JSON array inside a markdown code block.
        var match = Regex.Match(response, @"```(?:json)?\s*(\[[\s\S]*)", RegexOptions.Singleline);
        if (match.Success)
        {
            var content = match.Groups[1].Value;
            var fenceEnd = content.LastIndexOf("```", StringComparison.Ordinal);
            if (fenceEnd > 0)
                content = content[..fenceEnd];
            return content.Trim();
        }

        // Strategy 2: the first '[' (start of a JSON array).
        var firstBracket = response.IndexOf('[');
        if (firstBracket >= 0)
            return response[firstBracket..].Trim();

        return "";
    }

    private static bool TryParseArray(string json, [NotNullWhen(true)] out JsonElement? array)
    {
        array = null;
        try
        {
            var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind == JsonValueKind.Array)
            {
                array = doc.RootElement.Clone();
                return true;
            }
            doc.Dispose();
        }
        catch (JsonException)
        {
            // not valid JSON
        }
        return false;
    }

    private static TestCase? ParseTestCase(JsonElement element)
    {
        try
        {
            var id = element.GetProperty("id").GetString() ?? "";
            var title = element.GetProperty("title").GetString() ?? "";

            if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(title))
                return null;

            var priorityStr = element.TryGetProperty("priority", out var p) ? p.GetString() : "medium";
            var priority = priorityStr?.ToLowerInvariant() switch
            {
                "high" => Priority.High,
                "low" => Priority.Low,
                _ => Priority.Medium
            };

            var tags = ReadStringArray(element, "tags");
            var steps = ReadStringArray(element, "steps");
            var sourceRefs = ReadStringArray(element, "source_refs");
            var criteria = ReadStringArray(element, "criteria");

            TimeSpan? estimatedDuration = null;
            if (element.TryGetProperty("estimated_duration", out var durElement))
            {
                var durStr = durElement.GetString();
                if (!string.IsNullOrEmpty(durStr))
                    estimatedDuration = ParseDuration(durStr);
            }

            string? scenarioFromDoc = null;
            if (element.TryGetProperty("scenario_from_doc", out var scenarioElement))
                scenarioFromDoc = scenarioElement.GetString();

            return new TestCase
            {
                Id = id,
                Title = title,
                Priority = priority,
                Tags = tags,
                Component = element.TryGetProperty("component", out var c) ? c.GetString() : null,
                Preconditions = element.TryGetProperty("preconditions", out var pre) ? pre.GetString() : null,
                Steps = steps,
                ExpectedResult = element.TryGetProperty("expected_result", out var er) ? er.GetString() ?? "" : "",
                TestData = element.TryGetProperty("test_data", out var td) ? td.GetString() : null,
                SourceRefs = sourceRefs,
                ScenarioFromDoc = scenarioFromDoc,
                EstimatedDuration = estimatedDuration,
                Criteria = criteria,
                FilePath = $"{id}.md"
            };
        }
        catch
        {
            return null;
        }
    }

    private static List<string> ReadStringArray(JsonElement element, string property)
    {
        var values = new List<string>();
        if (element.TryGetProperty(property, out var arr) && arr.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in arr.EnumerateArray())
            {
                if (item.GetString() is string s)
                    values.Add(s);
            }
        }
        return values;
    }

    private static TimeSpan? ParseDuration(string duration)
    {
        if (string.IsNullOrEmpty(duration)) return null;

        var match = Regex.Match(duration.Trim(), @"^(\d+)(s|m|h)$", RegexOptions.IgnoreCase);
        if (!match.Success) return null;

        var value = int.Parse(match.Groups[1].Value);
        return match.Groups[2].Value.ToLowerInvariant() switch
        {
            "s" => TimeSpan.FromSeconds(value),
            "m" => TimeSpan.FromMinutes(value),
            "h" => TimeSpan.FromHours(value),
            _ => null
        };
    }
}
