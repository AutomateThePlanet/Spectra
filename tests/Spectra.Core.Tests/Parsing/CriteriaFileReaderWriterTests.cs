using Spectra.Core.Models.Coverage;
using Spectra.Core.Parsing;

namespace Spectra.Core.Tests.Parsing;

public class CriteriaFileReaderWriterTests : IDisposable
{
    private readonly string _tempDir;
    private readonly CriteriaFileReader _reader;
    private readonly CriteriaFileWriter _writer;

    public CriteriaFileReaderWriterTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"criteria-file-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
        _reader = new CriteriaFileReader();
        _writer = new CriteriaFileWriter();
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    [Fact]
    public async Task ReadAsync_MissingFile_ReturnsEmptyList()
    {
        var path = Path.Combine(_tempDir, "nonexistent.criteria.yaml");

        var result = await _reader.ReadAsync(path);

        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    public async Task ReadAsync_ValidCriteriaFile_ParsesAll()
    {
        var path = Path.Combine(_tempDir, "valid.criteria.yaml");
        var yaml = """
            criteria:
              - id: AC-001
                text: User must provide valid email
                rfc2119: MUST
                source: docs/auth.md#login
                source_type: document
                source_doc: docs/auth.md
                source_section: Login
                component: auth
                priority: high
                tags:
                  - validation
                  - security
                linked_test_ids:
                  - TC-010
              - id: AC-002
                text: System should display error on invalid input
                rfc2119: SHOULD
                source_type: document
                priority: medium
            """;
        await File.WriteAllTextAsync(path, yaml);

        var result = await _reader.ReadAsync(path);

        Assert.Equal(2, result.Count);

        var first = result[0];
        Assert.Equal("AC-001", first.Id);
        Assert.Equal("User must provide valid email", first.Text);
        Assert.Equal("MUST", first.Rfc2119);
        Assert.Equal("docs/auth.md#login", first.Source);
        Assert.Equal("document", first.SourceType);
        Assert.Equal("docs/auth.md", first.SourceDoc);
        Assert.Equal("Login", first.SourceSection);
        Assert.Equal("auth", first.Component);
        Assert.Equal("high", first.Priority);
        Assert.Contains("validation", first.Tags);
        Assert.Contains("security", first.Tags);
        Assert.Contains("TC-010", first.LinkedTestIds);

        var second = result[1];
        Assert.Equal("AC-002", second.Id);
        Assert.Equal("System should display error on invalid input", second.Text);
        Assert.Equal("SHOULD", second.Rfc2119);
    }

    [Fact]
    public async Task WriteAsync_ThenReadAsync_RoundTrips()
    {
        var path = Path.Combine(_tempDir, "roundtrip.criteria.yaml");
        var criteria = new List<AcceptanceCriterion>
        {
            new()
            {
                Id = "AC-010",
                Text = "Payment must be processed within 3 seconds",
                Rfc2119 = "MUST",
                Source = "docs/payments.md#perf",
                SourceType = "document",
                SourceDoc = "docs/payments.md",
                SourceSection = "Performance",
                Component = "payments",
                Priority = "high",
                Tags = ["performance", "sla"],
                LinkedTestIds = ["TC-050", "TC-051"]
            },
            new()
            {
                Id = "AC-011",
                Text = "Receipt should be emailed after payment",
                Rfc2119 = "SHOULD",
                SourceType = "document",
                SourceDoc = "docs/payments.md",
                Priority = "medium",
                Tags = ["notification"]
            }
        };

        await _writer.WriteAsync(path, criteria, "docs/payments.md", "hash999");
        var result = await _reader.ReadAsync(path);

        Assert.Equal(2, result.Count);

        Assert.Equal("AC-010", result[0].Id);
        Assert.Equal("Payment must be processed within 3 seconds", result[0].Text);
        Assert.Equal("MUST", result[0].Rfc2119);
        Assert.Equal("docs/payments.md#perf", result[0].Source);
        Assert.Equal("document", result[0].SourceType);
        Assert.Equal("docs/payments.md", result[0].SourceDoc);
        Assert.Equal("Performance", result[0].SourceSection);
        Assert.Equal("payments", result[0].Component);
        Assert.Equal("high", result[0].Priority);
        Assert.Equal(2, result[0].Tags.Count);
        Assert.Equal(2, result[0].LinkedTestIds.Count);

        Assert.Equal("AC-011", result[1].Id);
        Assert.Equal("Receipt should be emailed after payment", result[1].Text);
    }

    [Fact]
    public async Task ReadAsync_MissingOptionalFields_ReturnsDefaults()
    {
        var path = Path.Combine(_tempDir, "minimal.criteria.yaml");
        var yaml = """
            criteria:
              - id: AC-099
                text: Minimal criterion with only required fields
            """;
        await File.WriteAllTextAsync(path, yaml);

        var result = await _reader.ReadAsync(path);

        Assert.Single(result);
        var criterion = result[0];
        Assert.Equal("AC-099", criterion.Id);
        Assert.Equal("Minimal criterion with only required fields", criterion.Text);
        Assert.Null(criterion.Rfc2119);
        Assert.Null(criterion.Source);
        Assert.Null(criterion.SourceDoc);
        Assert.Null(criterion.SourceSection);
        Assert.Null(criterion.Component);
        Assert.Empty(criterion.Tags);
        Assert.Empty(criterion.LinkedTestIds);
    }

    [Fact]
    public async Task ReadAsync_MalformedYaml_ReturnsEmptyList()
    {
        var path = Path.Combine(_tempDir, "malformed.criteria.yaml");
        await File.WriteAllTextAsync(path, "{{{{ not valid yaml !!! ::::");

        var result = await _reader.ReadAsync(path);

        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    public async Task WriteAsync_IncludesHeaderComments()
    {
        var path = Path.Combine(_tempDir, "header.criteria.yaml");
        var criteria = new List<AcceptanceCriterion>
        {
            new() { Id = "AC-001", Text = "Test criterion" }
        };

        await _writer.WriteAsync(path, criteria, "docs/test.md", "hash123");

        var content = await File.ReadAllTextAsync(path);
        Assert.StartsWith("#", content);
        Assert.Contains("Extracted from: docs/test.md", content);
        Assert.Contains("Doc hash: hash123", content);
        Assert.Contains("Generated at:", content);
    }
}
