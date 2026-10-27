using System.Collections;
using System.Globalization;
using System.Text;
using Spectra.Core.Models;
using Spectra.Core.Models.Grounding;
using YamlDotNet.Core;
using YamlDotNet.Core.Events;

namespace Spectra.CLI.IO;

/// <summary>
/// Writes test case files in Markdown format.
/// </summary>
/// <remarks>
/// Lossless for parsed tests (Spec 078). When a test carries a <see cref="TestCaseLayout"/>, every value that
/// is unchanged since parsing is reproduced verbatim from the source text, unknown frontmatter keys and unknown
/// body sections are kept, and only changed values are rendered canonically. Known frontmatter keys are always
/// emitted in <see cref="KeyOrder"/>, so the same input gives byte-identical output.
/// </remarks>
public sealed class TestFileWriter
{
    /// <summary>
    /// Fixed emission order of the frontmatter keys the typed model reads (the shared
    /// <see cref="TestCaseFrontmatterKeys.Ordered"/>). Unknown keys follow, verbatim.
    /// </summary>
    public static IReadOnlyList<string> KeyOrder => TestCaseFrontmatterKeys.Ordered;

    private static readonly BodySegmentKind[] SectionOrder =
    [
        BodySegmentKind.Preconditions, BodySegmentKind.Steps, BodySegmentKind.ExpectedResult, BodySegmentKind.TestData
    ];

    private static readonly EmitterSettings YamlSettings = new EmitterSettings()
        .WithBestIndent(2)
        .WithBestWidth(int.MaxValue)
        .WithIndentedSequences()
        .WithNewLine("\n");

    /// <summary>
    /// Writes a test case to a file.
    /// </summary>
    public async Task WriteAsync(string path, TestCase testCase, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(testCase);

        // Format first: a refused test (Spec 099 F3) leaves no trace.
        var content = FormatTestCase(testCase);

        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await File.WriteAllTextAsync(path, content, ct);
    }

    /// <summary>
    /// Formats a test case as Markdown with YAML frontmatter.
    /// </summary>
    public string FormatTestCase(TestCase testCase)
    {
        // Spec 099 F3: a test parsed for reading only would be written canonically and lose what only its layout
        // holds (unknown keys, comments, sections). That is a programming error in the caller, never a user outcome.
        if (testCase.LayoutOmitted)
        {
            throw new InvalidOperationException(
                $"Test {testCase.Id} was parsed without layout; a writer path must parse with layout capture.");
        }

        var layout = testCase.Layout;
        var sb = new StringBuilder();

        sb.Append("---\n");
        AppendFrontmatter(sb, testCase, layout);
        sb.Append("---\n");

        if (layout is null)
        {
            AppendCanonicalBody(sb, testCase);
        }
        else
        {
            AppendBody(sb, testCase, layout);
        }

        var text = sb.ToString();
        var newline = layout?.Newline ?? "\n";
        return newline == "\n" ? text : text.Replace("\r\n", "\n").Replace("\n", newline);
    }

    /// <summary>
    /// Gets the standard file path for a test case.
    /// </summary>
    public static string GetFilePath(string testsDir, string suite, string testId)
    {
        return Path.Combine(testsDir, suite, $"{testId}.md");
    }

    // ---------------------------------------------------------------- frontmatter

    private static void AppendFrontmatter(StringBuilder sb, TestCase test, TestCaseLayout? layout)
    {
        var known = new Dictionary<string, FrontmatterEntry>(StringComparer.Ordinal);
        if (layout is not null)
        {
            // Spec 080 A4: the leading block (header comment) stays at the top, whatever key comes first.
            sb.Append(layout.FrontmatterLeading);
            foreach (var entry in layout.FrontmatterEntries.Where(e => e.IsKnown))
            {
                known.TryAdd(entry.Key, entry);
            }
        }

        foreach (var key in KeyOrder)
        {
            if (layout is not null && IsUnchanged(key, test, layout.Baseline))
            {
                // Unchanged: reproduce the original exactly — its prelude and text if the key was present,
                // nothing if absent.
                if (known.TryGetValue(key, out var raw))
                {
                    sb.Append(raw.Prelude);
                    AppendRaw(sb, raw.RawText);
                }

                continue;
            }

            // Preconditions live in the body; the frontmatter key is only ever reproduced verbatim.
            if (key == "preconditions")
            {
                continue;
            }

            var rendered = RenderKey(key, test, emitEmptyCriteria: layout is null || known.ContainsKey("criteria"));
            if (rendered is not null)
            {
                // A changed key keeps the comments above it; a removed key (rendered is null) drops them.
                if (known.TryGetValue(key, out var original))
                {
                    sb.Append(original.Prelude);
                }

                sb.Append(rendered);
            }
        }

        if (layout is not null)
        {
            foreach (var entry in layout.FrontmatterEntries.Where(e => !e.IsKnown))
            {
                sb.Append(entry.Prelude);
                AppendRaw(sb, entry.RawText);
            }

            sb.Append(layout.FrontmatterTrailing);
        }
    }

    private static bool IsUnchanged(string key, TestCase t, TestCase b) => key switch
    {
        "id" => t.Id == b.Id,
        "priority" => t.Priority == b.Priority,
        "tags" => t.Tags.SequenceEqual(b.Tags),
        "component" => t.Component == b.Component,
        "description" => t.Description == b.Description,
        "preconditions" => t.Preconditions == b.Preconditions,
        "environment" => t.Environment.SequenceEqual(b.Environment),
        "depends_on" => t.DependsOn == b.DependsOn,
        "source_refs" => t.SourceRefs.SequenceEqual(b.SourceRefs),
        "scenario_from_doc" => t.ScenarioFromDoc == b.ScenarioFromDoc,
        "estimated_duration" => t.EstimatedDuration == b.EstimatedDuration,
        "criteria" => t.Criteria.SequenceEqual(b.Criteria),
        "requirements" => t.Requirements.SequenceEqual(b.Requirements),
        "related_work_items" => t.RelatedWorkItems.SequenceEqual(b.RelatedWorkItems),
        "bugs" => t.Bugs.SequenceEqual(b.Bugs),
        "automated_by" => t.AutomatedBy.SequenceEqual(b.AutomatedBy),
        "custom" => ReferenceEquals(t.Custom, b.Custom),
        "status" => t.Status == b.Status,
        "orphaned_reason" => t.OrphanedReason == b.OrphanedReason,
        "orphaned_date" => t.OrphanedDate == b.OrphanedDate,
        "grounding" => ReferenceEquals(t.Grounding, b.Grounding),
        _ => throw new ArgumentOutOfRangeException(nameof(key), key, "Unknown frontmatter key")
    };

    private static string? RenderKey(string key, TestCase t, bool emitEmptyCriteria) => key switch
    {
        "id" => EmitEntry(key, e => EmitString(e, t.Id)),
        "priority" => EmitEntry(key, e => EmitPlain(e, t.Priority.ToString().ToLowerInvariant())),
        "tags" => RenderList(key, t.Tags),
        "component" => RenderString(key, t.Component),
        "description" => RenderString(key, t.Description, freeText: true),
        "environment" => RenderList(key, t.Environment),
        "depends_on" => RenderString(key, t.DependsOn),
        "source_refs" => RenderList(key, t.SourceRefs),
        "scenario_from_doc" => RenderString(key, t.ScenarioFromDoc, freeText: true),
        "estimated_duration" => t.EstimatedDuration is { } d ? EmitEntry(key, e => EmitPlain(e, FormatDuration(d))) : null,
        "criteria" => t.Criteria.Count > 0
            ? RenderList(key, t.Criteria)
            : emitEmptyCriteria ? EmitEntry(key, e => { EmitSequenceStart(e, SequenceStyle.Flow); e.Emit(new SequenceEnd()); }) : null,
        "requirements" => RenderList(key, t.Requirements),
        "related_work_items" => RenderList(key, t.RelatedWorkItems),
        "bugs" => RenderList(key, t.Bugs),
        "automated_by" => RenderList(key, t.AutomatedBy),
        "custom" => t.Custom is { Count: > 0 } custom ? EmitEntry(key, e => EmitNode(e, custom)) : null,
        "status" => RenderString(key, t.Status),
        "orphaned_reason" => RenderString(key, t.OrphanedReason, freeText: true),
        "orphaned_date" => t.OrphanedDate is { } date
            ? EmitEntry(key, e => EmitPlain(e, date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)))
            : null,
        "grounding" => t.Grounding is { } g ? EmitEntry(key, e => EmitGrounding(e, g)) : null,
        _ => null
    };

    private static string? RenderString(string key, string? value, bool freeText = false) =>
        string.IsNullOrWhiteSpace(value) ? null : EmitEntry(key, e => EmitString(e, value, freeText));

    private static string? RenderList(string key, IReadOnlyList<string> values) =>
        values.Count == 0 ? null : EmitEntry(key, e => EmitStringList(e, values));

    private static void EmitGrounding(IEmitter e, GroundingMetadata g)
    {
        e.Emit(new MappingStart(null, null, true, MappingStyle.Block));

        EmitPlain(e, "verdict");
        EmitPlain(e, g.Verdict.ToString().ToLowerInvariant());
        EmitPlain(e, "score");
        EmitPlain(e, g.Score.ToString("0.00##############", CultureInfo.InvariantCulture));
        EmitPlain(e, "generator");
        EmitString(e, g.Generator);
        EmitPlain(e, "critic");
        EmitString(e, g.Critic);
        EmitPlain(e, "verified_at");
        EmitPlain(e, FormatTimestamp(g.VerifiedAt));

        if (g.UnverifiedClaims.Count > 0)
        {
            EmitPlain(e, "unverified_claims");
            EmitStringList(e, g.UnverifiedClaims, freeText: true);
        }

        if (g.FlaggedForReview)
        {
            EmitPlain(e, "flagged_for_review");
            EmitPlain(e, "true");
        }

        if (g.RepairAttempts > 0)
        {
            EmitPlain(e, "repair_attempts");
            EmitPlain(e, g.RepairAttempts.ToString(CultureInfo.InvariantCulture));
        }

        if (g.Repaired)
        {
            EmitPlain(e, "repaired");
            EmitPlain(e, "true");
        }

        if (g.CondensedFindings.Count > 0)
        {
            EmitPlain(e, "condensed_findings");
            EmitSequenceStart(e, SequenceStyle.Block);
            foreach (var finding in g.CondensedFindings)
            {
                e.Emit(new MappingStart(null, null, true, MappingStyle.Block));
                EmitPlain(e, "element");
                EmitString(e, finding.Element, freeText: true);
                EmitPlain(e, "reason");
                EmitString(e, finding.Reason, freeText: true);
                e.Emit(new MappingEnd());
            }

            e.Emit(new SequenceEnd());
        }

        if (!string.IsNullOrWhiteSpace(g.Source))
        {
            EmitPlain(e, "source");
            EmitString(e, g.Source);
        }

        if (!string.IsNullOrWhiteSpace(g.CreatedBy))
        {
            EmitPlain(e, "created_by");
            EmitString(e, g.CreatedBy);
        }

        if (!string.IsNullOrWhiteSpace(g.Note))
        {
            EmitPlain(e, "note");
            EmitString(e, g.Note, freeText: true);
        }

        // Spec 103: the human review record. Every field is conditional, so a block without one is unchanged.
        if (!string.IsNullOrWhiteSpace(g.ReviewedBy))
        {
            EmitPlain(e, "reviewed_by");
            EmitString(e, g.ReviewedBy);
        }

        if (g.ReviewedAt is { } reviewedAt)
        {
            EmitPlain(e, "reviewed_at");
            EmitPlain(e, FormatTimestamp(reviewedAt));
        }

        if (g.ReraiseSuppressedAt is { } suppressedAt)
        {
            EmitPlain(e, "reraise_suppressed_at");
            EmitPlain(e, FormatTimestamp(suppressedAt));
        }

        e.Emit(new MappingEnd());
    }

    private static string FormatTimestamp(DateTimeOffset value) =>
        value.UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ss.FFFFFFFZ", CultureInfo.InvariantCulture);

    /// <summary>
    /// Spec 103 (FR-006): an error when <paramref name="value"/> holds a control character other than tab, LF or
    /// CR, which a review note refuses before anything is written; null when it can be stored.
    /// </summary>
    public static string? ValidateAnnotation(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return null;
        }

        foreach (var ch in value)
        {
            if (char.IsControl(ch) && ch is not ('\n' or '\r' or '\t'))
            {
                return $"contains a control character (U+{(int)ch:X4}) that cannot be represented in frontmatter";
            }
        }

        return null;
    }

    private static void EmitNode(IEmitter e, object? value)
    {
        switch (value)
        {
            case null:
                EmitPlain(e, "~");
                break;
            case string s:
                EmitString(e, s);
                break;
            case IDictionary map:
                e.Emit(new MappingStart(null, null, true, MappingStyle.Block));
                foreach (DictionaryEntry pair in map)
                {
                    EmitNode(e, pair.Key);
                    EmitNode(e, pair.Value);
                }

                e.Emit(new MappingEnd());
                break;
            case IEnumerable<KeyValuePair<string, object>> pairs:
                e.Emit(new MappingStart(null, null, true, MappingStyle.Block));
                foreach (var pair in pairs)
                {
                    EmitString(e, pair.Key);
                    EmitNode(e, pair.Value);
                }

                e.Emit(new MappingEnd());
                break;
            case IEnumerable items:
                EmitSequenceStart(e, SequenceStyle.Block);
                foreach (var item in items)
                {
                    EmitNode(e, item);
                }

                e.Emit(new SequenceEnd());
                break;
            case bool b:
                EmitPlain(e, b ? "true" : "false");
                break;
            case IFormattable formattable:
                EmitString(e, formattable.ToString(null, CultureInfo.InvariantCulture));
                break;
            default:
                EmitString(e, value.ToString() ?? string.Empty);
                break;
        }
    }

    private static void EmitStringList(IEmitter e, IReadOnlyList<string> values, bool freeText = false)
    {
        EmitSequenceStart(e, SequenceStyle.Block);
        foreach (var value in values)
        {
            EmitString(e, value, freeText);
        }

        e.Emit(new SequenceEnd());
    }

    private static void EmitSequenceStart(IEmitter e, SequenceStyle style) =>
        e.Emit(new SequenceStart(null, null, true, style));

    private static void EmitPlain(IEmitter e, string value) =>
        e.Emit(new Scalar(null, null, value, ScalarStyle.Plain, true, false));

    /// <summary>
    /// Emits a string so it parses back to exactly the same string. Free text and anything with a line break
    /// is double-quoted; values a YAML reader would resolve to null are single-quoted; everything else lets
    /// the emitter pick plain style when that is safe and quote otherwise.
    /// </summary>
    private static void EmitString(IEmitter e, string value, bool freeText = false)
    {
        var style = freeText || value.Contains('\n') || value.Contains('\r')
            ? ScalarStyle.DoubleQuoted
            : value is "" or "~" or "null" or "Null" or "NULL"
                ? ScalarStyle.SingleQuoted
                : ScalarStyle.Any;

        e.Emit(new Scalar(null, null, value, style, true, true));
    }

    /// <summary>
    /// Emits one top-level <c>key: value</c> entry as YAML text ending in a line break.
    /// </summary>
    private static string EmitEntry(string key, Action<IEmitter> emitValue)
    {
        using var writer = new StringWriter(CultureInfo.InvariantCulture);
        var emitter = new Emitter(writer, YamlSettings);

        emitter.Emit(new StreamStart());
        emitter.Emit(new DocumentStart(null, null, true));
        emitter.Emit(new MappingStart(null, null, true, MappingStyle.Block));
        EmitPlain(emitter, key);
        emitValue(emitter);
        emitter.Emit(new MappingEnd());
        emitter.Emit(new DocumentEnd(true));
        emitter.Emit(new StreamEnd());

        var text = writer.ToString().Replace("\r\n", "\n");
        return text.EndsWith('\n') ? text : text + "\n";
    }

    private static void AppendRaw(StringBuilder sb, string raw)
    {
        sb.Append(raw);
        if (!raw.EndsWith('\n'))
        {
            sb.Append('\n');
        }
    }

    private static string FormatDuration(TimeSpan duration)
    {
        var sb = new StringBuilder();
        var hours = (int)duration.TotalHours;
        if (hours > 0) sb.Append(hours).Append('h');
        if (duration.Minutes > 0) sb.Append(duration.Minutes).Append('m');
        if (duration.Seconds > 0 || sb.Length == 0) sb.Append(duration.Seconds).Append('s');
        return sb.ToString();
    }

    // ---------------------------------------------------------------- body

    private static void AppendCanonicalBody(StringBuilder sb, TestCase testCase)
    {
        sb.Append('\n');
        sb.Append("# ").Append(testCase.Title).Append("\n\n");

        if (!string.IsNullOrWhiteSpace(testCase.Preconditions))
        {
            sb.Append(RenderSection(BodySegmentKind.Preconditions, testCase)).Append('\n');
        }

        sb.Append("## Steps\n\n");
        for (var i = 0; i < testCase.Steps.Count; i++)
        {
            sb.Append(i + 1).Append(". ").Append(testCase.Steps[i]).Append('\n');
        }

        sb.Append('\n');
        sb.Append("## Expected Result\n\n").Append(testCase.ExpectedResult).Append('\n');

        if (!string.IsNullOrWhiteSpace(testCase.TestData))
        {
            sb.Append('\n').Append(RenderSection(BodySegmentKind.TestData, testCase));
        }
    }

    private static void AppendBody(StringBuilder sb, TestCase test, TestCaseLayout layout)
    {
        var baseline = layout.Baseline;
        var present = layout.BodySegments.Select(s => s.Kind).ToHashSet();

        // Known sections that are new in the model: insert after the nearest preceding known section that is
        // present (canonical order), or before the first section when none precedes them.
        var pending = SectionOrder
            .Where(k => !present.Contains(k) && SectionChanged(k, test, baseline) && HasContent(k, test))
            .ToList();

        BodySegmentKind? AnchorOf(BodySegmentKind kind)
        {
            BodySegmentKind? anchor = null;
            foreach (var candidate in SectionOrder)
            {
                if (candidate == kind) break;
                if (present.Contains(candidate)) anchor = candidate;
            }

            return anchor;
        }

        var pieces = new List<(string Text, bool Canonical)>();

        void AddPending(BodySegmentKind? anchor)
        {
            foreach (var kind in pending.Where(k => AnchorOf(k) == anchor))
            {
                pieces.Add((RenderSection(kind, test), true));
            }
        }

        var unanchoredInserted = false;
        foreach (var segment in layout.BodySegments)
        {
            var isSection = segment.Kind is not (BodySegmentKind.Leading or BodySegmentKind.Title or BodySegmentKind.Intro);
            if (isSection && !unanchoredInserted)
            {
                AddPending(null);
                unanchoredInserted = true;
            }

            switch (segment.Kind)
            {
                case BodySegmentKind.Title:
                    pieces.Add(test.Title == baseline.Title
                        ? (segment.RawText, false)
                        : ($"# {test.Title}\n", true));
                    break;

                case BodySegmentKind.Preconditions:
                case BodySegmentKind.Steps:
                case BodySegmentKind.ExpectedResult:
                case BodySegmentKind.TestData:
                    if (!SectionChanged(segment.Kind, test, baseline))
                    {
                        pieces.Add((segment.RawText, false));
                    }
                    else if (HasContent(segment.Kind, test))
                    {
                        pieces.Add((RenderSection(segment.Kind, test), true));
                    }

                    AddPending(segment.Kind);
                    break;

                default:
                    pieces.Add((segment.RawText, false));
                    break;
            }
        }

        if (!unanchoredInserted)
        {
            AddPending(null);
        }

        var body = new StringBuilder();
        var previousCanonical = false;
        foreach (var (text, canonical) in pieces)
        {
            if (body.Length > 0)
            {
                if (previousCanonical)
                {
                    body.Append('\n');
                }
                else if (canonical)
                {
                    EnsureBlankLine(body);
                }
            }

            body.Append(text);
            previousCanonical = canonical;
        }

        sb.Append(body);
    }

    private static bool SectionChanged(BodySegmentKind kind, TestCase t, TestCase b) => kind switch
    {
        BodySegmentKind.Preconditions => t.Preconditions != b.Preconditions,
        BodySegmentKind.Steps => !t.Steps.SequenceEqual(b.Steps),
        BodySegmentKind.ExpectedResult => t.ExpectedResult != b.ExpectedResult,
        BodySegmentKind.TestData => t.TestData != b.TestData,
        _ => false
    };

    private static bool HasContent(BodySegmentKind kind, TestCase t) => kind switch
    {
        BodySegmentKind.Preconditions => !string.IsNullOrWhiteSpace(t.Preconditions),
        BodySegmentKind.Steps => t.Steps.Count > 0,
        BodySegmentKind.ExpectedResult => !string.IsNullOrWhiteSpace(t.ExpectedResult),
        BodySegmentKind.TestData => !string.IsNullOrWhiteSpace(t.TestData),
        _ => false
    };

    private static string RenderSection(BodySegmentKind kind, TestCase t)
    {
        switch (kind)
        {
            case BodySegmentKind.Preconditions:
                return $"## Preconditions\n\n{t.Preconditions}\n";
            case BodySegmentKind.Steps:
                var sb = new StringBuilder("## Steps\n\n");
                for (var i = 0; i < t.Steps.Count; i++)
                {
                    sb.Append(i + 1).Append(". ").Append(t.Steps[i]).Append('\n');
                }

                return sb.ToString();
            case BodySegmentKind.ExpectedResult:
                return $"## Expected Result\n\n{t.ExpectedResult}\n";
            case BodySegmentKind.TestData:
                return $"## Test Data\n\n{t.TestData}\n";
            default:
                throw new ArgumentOutOfRangeException(nameof(kind), kind, null);
        }
    }

    private static void EnsureBlankLine(StringBuilder sb)
    {
        var tail = sb.Length >= 4 ? sb.ToString(sb.Length - 4, 4) : sb.ToString();
        if (tail.EndsWith("\n\n") || tail.EndsWith("\r\n\r\n"))
        {
            return;
        }

        sb.Append(tail.EndsWith('\n') ? "\n" : "\n\n");
    }
}
