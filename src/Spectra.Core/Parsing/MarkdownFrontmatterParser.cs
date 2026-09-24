using Markdig;
using Markdig.Extensions.Yaml;
using Markdig.Syntax;
using Spectra.Core.Models;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Spectra.Core.Parsing;

/// <summary>
/// Parses Markdown files with YAML frontmatter.
/// </summary>
public sealed class MarkdownFrontmatterParser
{
    // Cache pipelines and deserializers (thread-safe, reusable)
    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder()
        .UseYamlFrontMatter()
        .Build();

    private static readonly IDeserializer YamlDeserializer = new DeserializerBuilder()
        .WithNamingConvention(UnderscoredNamingConvention.Instance)
        .IgnoreUnmatchedProperties()
        .Build();

    /// <summary>
    /// Parses YAML frontmatter from a Markdown string.
    /// </summary>
    /// <typeparam name="T">The type to deserialize the frontmatter to</typeparam>
    /// <param name="markdown">The Markdown content</param>
    /// <param name="filePath">Optional file path for error messages</param>
    /// <returns>ParseResult containing the deserialized frontmatter or errors</returns>
    public ParseResult<T> ParseFrontmatter<T>(string markdown, string? filePath = null) where T : class
    {
        try
        {
            var document = Markdown.Parse(markdown, Pipeline);
            var yamlBlock = document.Descendants<YamlFrontMatterBlock>().FirstOrDefault();

            if (yamlBlock is null)
            {
                return ParseResult<T>.Failure(new ParseError(
                    "MISSING_FRONTMATTER",
                    "No YAML frontmatter found in the document",
                    filePath));
            }

            // Extract YAML content from the block
            var yaml = ExtractYamlContent(markdown, yamlBlock);

            if (string.IsNullOrWhiteSpace(yaml))
            {
                return ParseResult<T>.Failure(new ParseError(
                    "EMPTY_FRONTMATTER",
                    "YAML frontmatter is empty",
                    filePath));
            }

            var result = YamlDeserializer.Deserialize<T>(yaml);

            if (result is null)
            {
                return ParseResult<T>.Failure(new ParseError(
                    "DESERIALIZATION_FAILED",
                    "Failed to deserialize YAML frontmatter",
                    filePath));
            }

            return ParseResult<T>.Success(result);
        }
        catch (YamlDotNet.Core.YamlException ex)
        {
            return ParseResult<T>.Failure(new ParseError(
                "INVALID_YAML",
                $"Invalid YAML syntax: {ex.Message}",
                filePath,
                (int)ex.Start.Line,
                (int)ex.Start.Column));
        }
        catch (Exception ex)
        {
            return ParseResult<T>.Failure(new ParseError(
                "PARSE_ERROR",
                $"Unexpected error parsing frontmatter: {ex.Message}",
                filePath));
        }
    }

    /// <summary>
    /// Extracts the body content (after frontmatter) from a Markdown string.
    /// </summary>
    /// <param name="markdown">The Markdown content</param>
    /// <returns>The body content without frontmatter</returns>
    public string ExtractBody(string markdown)
    {
        var document = Markdown.Parse(markdown, Pipeline);
        var yamlBlock = document.Descendants<YamlFrontMatterBlock>().FirstOrDefault();

        if (yamlBlock is null)
        {
            return markdown;
        }

        // Find the end of the frontmatter (the closing ---)
        var endPosition = yamlBlock.Span.End;

        // Skip past the closing --- and any whitespace
        var body = markdown[(endPosition + 1)..].TrimStart('\r', '\n', '-');

        return body.TrimStart();
    }

    /// <summary>
    /// Parses both frontmatter and body from a Markdown string.
    /// </summary>
    public ParseResult<(T Frontmatter, string Body)> Parse<T>(string markdown, string? filePath = null) where T : class
    {
        var frontmatterResult = ParseFrontmatter<T>(markdown, filePath);

        if (frontmatterResult.IsFailure)
        {
            return ParseResult<(T, string)>.Failure(frontmatterResult.Errors);
        }

        var body = ExtractBody(markdown);

        return ParseResult<(T, string)>.Success((frontmatterResult.Value, body));
    }

    /// <summary>
    /// Splits a Markdown string into its raw frontmatter text (between the opening and closing
    /// <c>---</c> lines, including the last content line's line break) and its raw body (everything
    /// after the closing delimiter line's line break, untrimmed). Returns false when there is no
    /// frontmatter block.
    /// </summary>
    public bool TrySplitRaw(string markdown, out string frontmatter, out string body)
    {
        frontmatter = string.Empty;
        body = markdown;

        var document = Markdown.Parse(markdown, Pipeline);
        var yamlBlock = document.Descendants<YamlFrontMatterBlock>().FirstOrDefault();
        if (yamlBlock is null)
        {
            return false;
        }

        frontmatter = ExtractYamlContent(markdown, yamlBlock);

        var afterBlock = yamlBlock.Span.End + 1;
        if (afterBlock < markdown.Length && markdown[afterBlock] == '\r') afterBlock++;
        if (afterBlock < markdown.Length && markdown[afterBlock] == '\n') afterBlock++;
        body = afterBlock <= markdown.Length ? markdown[afterBlock..] : string.Empty;
        return true;
    }

    private static string ExtractYamlContent(string markdown, YamlFrontMatterBlock yamlBlock)
    {
        // The block spans "---<nl>...<nl>---". Keep the exact text between the delimiter lines.
        var rawText = markdown.Substring(yamlBlock.Span.Start, yamlBlock.Span.Length);

        var firstBreak = rawText.IndexOf('\n');
        if (firstBreak < 0)
        {
            return string.Empty;
        }

        var lastLineStart = rawText.LastIndexOf('\n') + 1;
        if (lastLineStart <= firstBreak + 1)
        {
            return string.Empty;
        }

        return rawText[(firstBreak + 1)..lastLineStart];
    }
}
