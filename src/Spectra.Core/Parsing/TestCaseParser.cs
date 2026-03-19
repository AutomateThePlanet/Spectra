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

    [GeneratedRegex(@"^##\s+Steps\s*$", RegexOptions.Multiline | RegexOptions.IgnoreCase)]
    private static partial Regex StepsHeaderRegex();

    [GeneratedRegex(@"^##\s+Expected\s+Result\s*$", RegexOptions.Multiline | RegexOptions.IgnoreCase)]
    private static partial Regex ExpectedResultHeaderRegex();

    [GeneratedRegex(@"^##\s+Test\s+Data\s*$", RegexOptions.Multiline | RegexOptions.IgnoreCase)]
    private static partial Regex TestDataHeaderRegex();

    [GeneratedRegex(@"^\d+\.\s+(.+)$", RegexOptions.Multiline)]
    private static partial Regex NumberedListRegex();

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

        // Extract steps
        var steps = ExtractSteps(body);

        // Extract expected result
        var expectedResult = ExtractExpectedResult(body);
        if (string.IsNullOrWhiteSpace(expectedResult))
        {
            return ParseResult<TestCase>.Failure(new ParseError(
                "MISSING_EXPECTED_RESULT",
                "No expected result section found in test case",
                filePath));
        }

        // Extract test data (optional)
        var testData = ExtractTestData(body);

        // Parse priority
        if (!TryParsePriority(frontmatter.Priority, out var priority))
        {
            return ParseResult<TestCase>.Failure(new ParseError(
                "INVALID_PRIORITY",
                $"Invalid priority value: {frontmatter.Priority}. Expected: high, medium, or low",
                filePath));
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
            Preconditions = frontmatter.Preconditions,
            Environment = frontmatter.Environment,
            EstimatedDuration = duration,
            DependsOn = frontmatter.DependsOn,
            SourceRefs = frontmatter.SourceRefs,
            RelatedWorkItems = frontmatter.RelatedWorkItems,
            Custom = frontmatter.Custom,
            Grounding = frontmatter.Grounding?.ToMetadata(),
            Title = title,
            Steps = steps,
            ExpectedResult = expectedResult,
            TestData = testData
        };

        return ParseResult<TestCase>.Success(testCase);
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
