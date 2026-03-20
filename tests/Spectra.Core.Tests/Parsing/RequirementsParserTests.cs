using Spectra.Core.Parsing;

namespace Spectra.Core.Tests.Parsing;

public class RequirementsParserTests
{
    private readonly RequirementsParser _parser = new();

    [Fact]
    public void Parse_ValidYaml_ReturnsRequirements()
    {
        var yaml = """
            requirements:
              - id: REQ-001
                title: "User can log in"
                source: docs/auth.md
                priority: high
              - id: REQ-002
                title: "System rejects invalid passwords"
                source: docs/auth.md
                priority: high
            """;

        var result = _parser.Parse(yaml);

        Assert.Equal(2, result.Count);
        Assert.Equal("REQ-001", result[0].Id);
        Assert.Equal("User can log in", result[0].Title);
        Assert.Equal("docs/auth.md", result[0].Source);
        Assert.Equal("high", result[0].Priority);
    }

    [Fact]
    public void Parse_EmptyContent_ReturnsEmpty()
    {
        var result = _parser.Parse("");

        Assert.Empty(result);
    }

    [Fact]
    public void Parse_NullContent_ReturnsEmpty()
    {
        var result = _parser.Parse(null!);

        Assert.Empty(result);
    }

    [Fact]
    public void Parse_MalformedYaml_ReturnsEmpty()
    {
        var result = _parser.Parse("{{{{invalid yaml");

        Assert.Empty(result);
    }

    [Fact]
    public void Parse_CommentedOut_ReturnsEmpty()
    {
        var yaml = """
            # requirements:
            #   - id: REQ-001
            #     title: "Test"
            """;

        var result = _parser.Parse(yaml);

        Assert.Empty(result);
    }

    [Fact]
    public void Parse_MinimalFields_DefaultsWork()
    {
        var yaml = """
            requirements:
              - id: REQ-001
                title: "Minimal"
            """;

        var result = _parser.Parse(yaml);

        Assert.Single(result);
        Assert.Equal("REQ-001", result[0].Id);
        Assert.Null(result[0].Source);
        Assert.Null(result[0].Priority);
    }

    [Fact]
    public async Task ParseAsync_MissingFile_ReturnsEmpty()
    {
        var result = await _parser.ParseAsync("/nonexistent/path.yaml");

        Assert.Empty(result);
    }

    [Fact]
    public async Task ParseAsync_ValidFile_ReturnsRequirements()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(tempFile, """
                requirements:
                  - id: REQ-042
                    title: "Payment with expired card must be rejected"
                    source: docs/payment-processing.md
                    priority: high
                """);

            var result = await _parser.ParseAsync(tempFile);

            Assert.Single(result);
            Assert.Equal("REQ-042", result[0].Id);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }
}
