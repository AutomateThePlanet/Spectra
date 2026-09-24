using System.Globalization;
using System.Text.RegularExpressions;
using Spectra.Core.Models;

namespace Spectra.Core.Parsing;

/// <summary>
/// Parses test case Markdown files to TestCase objects.
/// </summary>
public sealed partial class TestCaseParser
{
    private readonly MarkdownFrontmatterParser _frontmatterParser = new();

    [GeneratedRegex(@"^#\s+(.+)$", RegexOptions.Multiline)]
    private static partial Regex TitleRegex();

    [GeneratedRegex(@"^##\s+Pre[-\s]?conditions?\s*$", RegexOptions.Multiline | RegexOptions.IgnoreCase)]
    private static partial Regex PreconditionsHeaderRegex();

    [GeneratedRegex(@"^##\s+(?:Test\s+)?Steps?\s*$", RegexOptions.Multiline | RegexOptions.IgnoreCase)]
    private static partial Regex StepsHeaderRegex();

    [GeneratedRegex(@"^##\s+Expected\s+Results?\s*$", RegexOptions.Multiline | RegexOptions.IgnoreCase)]
    private static partial Regex ExpectedResultHeaderRegex();

    [GeneratedRegex(@"^##\s+Test\s+Data\s*$", RegexOptions.Multiline | RegexOptions.IgnoreCase)]
    private static partial Regex TestDataHeaderRegex();

    [GeneratedRegex(@"^\d+\.\s+(.+)$", RegexOptions.Multiline)]
    private static partial Regex NumberedListRegex();

    [GeneratedRegex(@"^#\s+.+$")]
    private static partial Regex TitleLineRegex();

    [GeneratedRegex(@"^##\s+")]
    private static partial Regex SectionHeaderRegex();

    /// <summary>
    /// Parses a test case from Markdown content.
    /// </summary>
    /// <param name="markdown">The Markdown content</param>
    /// <param name="filePath">Relative path to the test file</param>
    /// <returns>ParseResult containing the TestCase or errors</returns>
    public ParseResult<TestCase> Parse(string markdown, string filePath)
    {
        var parseResult = _frontmatterParser.Parse<TestCaseFrontmatter>(markdown, filePath);

        if (parseResult.IsFailure)
        {
            return ParseResult<TestCase>.Failure(parseResult.Errors);
        }

        var (frontmatter, body) = parseResult.Value;

        // Extract title from first H1
        var title = ExtractTitle(body);
        if (string.IsNullOrWhiteSpace(title))
        {
            return ParseResult<TestCase>.Failure(new ParseError(
                "MISSING_TITLE",
                "No H1 title found in test case",
                filePath));
        }

        // Extract preconditions from body (falls back to frontmatter)
        var preconditions = ExtractPreconditions(body) ?? frontmatter.Preconditions;

        // Extract steps
        var steps = ExtractSteps(body);

        // Extract expected result (no longer required - return what we can parse)
        var expectedResult = ExtractExpectedResult(body);

        // Extract test data (optional)
        var testData = ExtractTestData(body);

        // Parse priority (default to medium if missing/invalid)
        if (!TryParsePriority(frontmatter.Priority, out var priority))
        {
            priority = Priority.Medium;
        }

        // Parse duration (optional)
        TimeSpan? duration = null;
        if (!string.IsNullOrWhiteSpace(frontmatter.EstimatedDuration))
        {
            if (TryParseDuration(frontmatter.EstimatedDuration, out var parsedDuration))
            {
                duration = parsedDuration;
            }
        }

        var testCase = new TestCase
        {
            Id = frontmatter.Id,
            FilePath = filePath,
            Priority = priority,
            Tags = frontmatter.Tags,
            Component = frontmatter.Component,
            Description = frontmatter.Description,
            Preconditions = preconditions,
            Environment = frontmatter.Environment,
            EstimatedDuration = duration,
            DependsOn = frontmatter.DependsOn,
            SourceRefs = frontmatter.SourceRefs,
            ScenarioFromDoc = frontmatter.ScenarioFromDoc,
            RelatedWorkItems = frontmatter.RelatedWorkItems,
            Custom = frontmatter.Custom,
            AutomatedBy = frontmatter.AutomatedBy,
            Requirements = frontmatter.Requirements,
            Criteria = frontmatter.Criteria,
            Bugs = frontmatter.Bugs,
            Grounding = frontmatter.Grounding?.ToMetadata(),
            Title = title,
            Steps = steps,
            ExpectedResult = expectedResult ?? "",
            TestData = testData,
            Status = frontmatter.Status,
            OrphanedReason = frontmatter.OrphanedReason,
            OrphanedDate = ParseOrphanedDate(frontmatter.OrphanedDate)
        };

        return ParseResult<TestCase>.Success(AttachLayout(testCase, markdown));
    }

    /// <summary>
    /// Frontmatter keys the typed model reads. Everything else is preserved verbatim as an unknown key.
    /// </summary>
    public static readonly IReadOnlySet<string> KnownFrontmatterKeys = new HashSet<string>(StringComparer.Ordinal)
    {
        "id", "priority", "tags", "component", "description", "preconditions", "environment",
        "depends_on", "source_refs", "scenario_from_doc", "estimated_duration", "criteria",
        "requirements", "related_work_items", "bugs", "automated_by", "custom", "status",
        "orphaned_reason", "orphaned_date", "grounding"
    };

    private TestCase AttachLayout(TestCase baseline, string markdown)
    {
        if (!_frontmatterParser.TrySplitRaw(markdown, out var frontmatterText, out var rawBody))
        {
            return baseline;
        }

        IReadOnlyList<FrontmatterEntry> entries;
        try
        {
            entries = CaptureFrontmatterEntries(frontmatterText);
        }
        catch (YamlDotNet.Core.YamlException)
        {
            // The typed parse already succeeded, so this should not happen; without a reliable layout
            // the writer falls back to canonical output.
            return baseline;
        }

        var layout = new TestCaseLayout(
            baseline,
            entries,
            CaptureBodySegments(rawBody),
            DetectNewline(markdown));

        return baseline with { Layout = layout };
    }

    private static IReadOnlyList<FrontmatterEntry> CaptureFrontmatterEntries(string yaml)
    {
        var keyStarts = new List<(string Key, int Index)>();
        var parser = new YamlDotNet.Core.Parser(new StringReader(yaml));
        var depth = 0;
        var expectKey = true;

        while (parser.MoveNext())
        {
            switch (parser.Current)
            {
                case YamlDotNet.Core.Events.MappingStart:
                case YamlDotNet.Core.Events.SequenceStart:
                    depth++;
                    break;
                case YamlDotNet.Core.Events.MappingEnd:
                case YamlDotNet.Core.Events.SequenceEnd:
                    depth--;
                    if (depth == 1) expectKey = true; // a collection value finished
                    break;
                case YamlDotNet.Core.Events.Scalar scalar when depth == 1:
                    if (expectKey) keyStarts.Add((scalar.Value, (int)scalar.Start.Index));
                    expectKey = !expectKey;
                    break;
                case YamlDotNet.Core.Events.AnchorAlias when depth == 1:
                    expectKey = !expectKey;
                    break;
            }
        }

        var entries = new List<FrontmatterEntry>(keyStarts.Count);
        for (var i = 0; i < keyStarts.Count; i++)
        {
            // The first entry also carries any leading comment lines.
            var start = i == 0 ? 0 : keyStarts[i].Index;
            var end = i + 1 < keyStarts.Count ? keyStarts[i + 1].Index : yaml.Length;
            var key = keyStarts[i].Key;
            entries.Add(new FrontmatterEntry(key, yaml[start..end], KnownFrontmatterKeys.Contains(key)));
        }

        return entries;
    }

    private static IReadOnlyList<BodySegment> CaptureBodySegments(string body)
    {
        var boundaries = new List<(int Index, BodySegmentKind Kind)>();
        var seenKinds = new HashSet<BodySegmentKind>();
        var titleSeen = false;
        var titleLineEnd = -1;

        var pos = 0;
        while (pos < body.Length)
        {
            var lineBreak = body.IndexOf('\n', pos);
            var nextPos = lineBreak < 0 ? body.Length : lineBreak + 1;
            var line = body[pos..(lineBreak < 0 ? body.Length : lineBreak)].TrimEnd('\r');

            if (!titleSeen && TitleLineRegex().IsMatch(line))
            {
                boundaries.Add((pos, BodySegmentKind.Title));
                titleSeen = true;
                titleLineEnd = nextPos;
            }
            else if (SectionHeaderRegex().IsMatch(line))
            {
                var kind = ClassifySectionHeader(line);
                if (kind != BodySegmentKind.Unknown && !seenKinds.Add(kind))
                {
                    kind = BodySegmentKind.Unknown;
                }

                boundaries.Add((pos, kind));
            }

            pos = nextPos;
        }

        var segments = new List<BodySegment>();
        var firstBoundary = boundaries.Count > 0 ? boundaries[0].Index : body.Length;
        if (firstBoundary > 0)
        {
            segments.Add(new BodySegment(BodySegmentKind.Leading, body[..firstBoundary]));
        }

        for (var i = 0; i < boundaries.Count; i++)
        {
            var (start, kind) = boundaries[i];
            var end = i + 1 < boundaries.Count ? boundaries[i + 1].Index : body.Length;

            if (kind == BodySegmentKind.Title)
            {
                segments.Add(new BodySegment(BodySegmentKind.Title, body[start..titleLineEnd]));
                if (end > titleLineEnd)
                {
                    segments.Add(new BodySegment(BodySegmentKind.Intro, body[titleLineEnd..end]));
                }
            }
            else
            {
                segments.Add(new BodySegment(kind, body[start..end]));
            }
        }

        return segments;
    }

    private static BodySegmentKind ClassifySectionHeader(string line)
    {
        if (PreconditionsHeaderRegex().IsMatch(line)) return BodySegmentKind.Preconditions;
        if (StepsHeaderRegex().IsMatch(line)) return BodySegmentKind.Steps;
        if (ExpectedResultHeaderRegex().IsMatch(line)) return BodySegmentKind.ExpectedResult;
        if (TestDataHeaderRegex().IsMatch(line)) return BodySegmentKind.TestData;
        return BodySegmentKind.Unknown;
    }

    private static string DetectNewline(string markdown)
    {
        var lineBreak = markdown.IndexOf('\n');
        return lineBreak > 0 && markdown[lineBreak - 1] == '\r' ? "\r\n" : "\n";
    }

    private static DateTimeOffset? ParseOrphanedDate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (DateTime.TryParseExact(value.Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var date))
        {
            return new DateTimeOffset(date, TimeSpan.Zero);
        }

        return DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal,
            out var parsed)
            ? parsed
            : null;
    }

    /// <summary>
    /// Parses a test case from a file.
    /// </summary>
    public async Task<ParseResult<TestCase>> ParseFileAsync(string absolutePath, string relativePath, CancellationToken ct = default)
    {
        try
        {
            var content = await File.ReadAllTextAsync(absolutePath, ct);
            return Parse(content, relativePath);
        }
        catch (FileNotFoundException)
        {
            return ParseResult<TestCase>.Failure(new ParseError(
                "FILE_NOT_FOUND",
                $"File not found: {absolutePath}",
                relativePath));
        }
        catch (Exception ex)
        {
            return ParseResult<TestCase>.Failure(new ParseError(
                "READ_ERROR",
                $"Error reading file: {ex.Message}",
                relativePath));
        }
    }

    private static string? ExtractTitle(string body)
    {
        var match = TitleRegex().Match(body);
        return match.Success ? match.Groups[1].Value.Trim() : null;
    }

    private static string? ExtractPreconditions(string body)
    {
        var section = ExtractSection(body, PreconditionsHeaderRegex());
        return section?.Trim();
    }

    private static IReadOnlyList<string> ExtractSteps(string body)
    {
        var stepsSection = ExtractSection(body, StepsHeaderRegex());
        if (stepsSection is null) return [];

        var matches = NumberedListRegex().Matches(stepsSection);
        return matches.Select(m => m.Groups[1].Value.Trim()).ToList();
    }

    private static string? ExtractExpectedResult(string body)
    {
        var section = ExtractSection(body, ExpectedResultHeaderRegex());
        return section?.Trim();
    }

    private static string? ExtractTestData(string body)
    {
        var section = ExtractSection(body, TestDataHeaderRegex());
        return section?.Trim();
    }

    private static string? ExtractSection(string body, Regex headerRegex)
    {
        var match = headerRegex.Match(body);
        if (!match.Success) return null;

        var startIndex = match.Index + match.Length;

        // Find the next ## header or end of content
        var nextHeaderMatch = Regex.Match(body[startIndex..], @"^##\s+", RegexOptions.Multiline);
        var endIndex = nextHeaderMatch.Success
            ? startIndex + nextHeaderMatch.Index
            : body.Length;

        return body[startIndex..endIndex].Trim();
    }

    private static bool TryParsePriority(string? value, out Priority priority)
    {
        priority = Priority.Medium;

        if (string.IsNullOrWhiteSpace(value))
            return false;

        return value.ToLowerInvariant() switch
        {
            "high" => SetAndReturn(Priority.High, ref priority),
            "medium" => SetAndReturn(Priority.Medium, ref priority),
            "low" => SetAndReturn(Priority.Low, ref priority),
            _ => false
        };

        static bool SetAndReturn(Priority p, ref Priority target)
        {
            target = p;
            return true;
        }
    }

    private static bool TryParseDuration(string value, out TimeSpan duration)
    {
        duration = TimeSpan.Zero;

        // Support formats: "5m", "30s", "1h30m", "00:05:00"
        if (TimeSpan.TryParse(value, out duration))
            return true;

        var match = Regex.Match(value, @"^(?:(\d+)h)?(?:(\d+)m)?(?:(\d+)s)?$");
        if (!match.Success)
            return false;

        var hours = match.Groups[1].Success ? int.Parse(match.Groups[1].Value) : 0;
        var minutes = match.Groups[2].Success ? int.Parse(match.Groups[2].Value) : 0;
        var seconds = match.Groups[3].Success ? int.Parse(match.Groups[3].Value) : 0;

        duration = new TimeSpan(hours, minutes, seconds);
        return true;
    }
}
