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

    private static string ExtractYamlContent(string markdown, YamlFrontMatterBlock yamlBlock)
    {
        // Extract the raw text of the YAML block
        var span = yamlBlock.Span;
        var rawText = markdown.Substring(span.Start, span.Length);

        // Split into lines and remove --- delimiters
        var lines = rawText.Split('\n')
            .Select(l => l.TrimEnd('\r'))
            .Where(l => l.Trim() != "---")
            .ToList();

        return string.Join('\n', lines);
    }
}
