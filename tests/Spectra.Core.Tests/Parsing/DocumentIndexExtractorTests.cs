using Spectra.Core.Parsing;

namespace Spectra.Core.Tests.Parsing;

public class DocumentIndexExtractorTests
{
    private readonly DocumentIndexExtractor _extractor = new();

    private const string SampleMarkdown = """
        # Citizen Registration

        This document describes the registration process.

        ## Registration Wizard Steps

        The citizen registration process follows a 6-step wizard that guides users through the required information.

        ### Step 1: Personal Information

        Citizens must provide first name, last name, date of birth, and EGN (national ID number).

        ### Step 2: Contact Details

        An email address and phone number are required for verification.

        ## Identity Verification

        Three methods are supported: `QES` signing, document upload via `/api/documents/upload`, and "in-person verification".

        Key Entity Example and Registration Wizard reference.
        """;

    [Fact]
    public void Extract_SetsTitle()
    {
        var fileInfo = CreateTempFile(SampleMarkdown);
        var entry = _extractor.Extract(SampleMarkdown, "docs/registration.md", fileInfo);

        Assert.Equal("Citizen Registration", entry.Title);
    }

    [Fact]
    public void Extract_ExtractsH2AndH3Sections()
    {
        var fileInfo = CreateTempFile(SampleMarkdown);
        var entry = _extractor.Extract(SampleMarkdown, "docs/registration.md", fileInfo);

        Assert.True(entry.Sections.Count >= 4);
        Assert.Contains(entry.Sections, s => s.Heading == "Registration Wizard Steps" && s.Level == 2);
        Assert.Contains(entry.Sections, s => s.Heading == "Step 1: Personal Information" && s.Level == 3);
        Assert.Contains(entry.Sections, s => s.Heading == "Step 2: Contact Details" && s.Level == 3);
        Assert.Contains(entry.Sections, s => s.Heading == "Identity Verification" && s.Level == 2);
    }

    [Fact]
    public void Extract_SectionSummariesAreTruncatedTo200Chars()
    {
        var longContent = "# Title\n\n## Section\n\n" + new string('x', 500);
        var fileInfo = CreateTempFile(longContent);
        var entry = _extractor.Extract(longContent, "docs/test.md", fileInfo);

        var section = entry.Sections.First();
        Assert.True(section.Summary.Length <= 203); // 200 + "..."
        Assert.EndsWith("...", section.Summary);
    }

    [Fact]
    public void Extract_ExtractsKeyEntities()
    {
        var fileInfo = CreateTempFile(SampleMarkdown);
        var entry = _extractor.Extract(SampleMarkdown, "docs/registration.md", fileInfo);

        Assert.Contains("QES", entry.KeyEntities);
        Assert.Contains("/api/documents/upload", entry.KeyEntities);
        Assert.Contains("in-person verification", entry.KeyEntities);
    }

    [Fact]
    public void Extract_ExtractsCapitalizedPhrases()
    {
        var fileInfo = CreateTempFile(SampleMarkdown);
        var entry = _extractor.Extract(SampleMarkdown, "docs/registration.md", fileInfo);

        Assert.Contains(entry.KeyEntities, e => e.Contains("Citizen Registration") || e.Contains("Registration Wizard"));
    }

    [Fact]
    public void Extract_ComputesWordCount()
    {
        var fileInfo = CreateTempFile(SampleMarkdown);
        var entry = _extractor.Extract(SampleMarkdown, "docs/registration.md", fileInfo);

        Assert.True(entry.WordCount > 0);
    }

    [Fact]
    public void Extract_EstimatesTokens()
    {
        var fileInfo = CreateTempFile(SampleMarkdown);
        var entry = _extractor.Extract(SampleMarkdown, "docs/registration.md", fileInfo);

        Assert.Equal((int)(entry.WordCount * 1.3), entry.EstimatedTokens);
    }

    [Fact]
    public void Extract_ComputesContentHash()
    {
        var fileInfo = CreateTempFile(SampleMarkdown);
        var entry = _extractor.Extract(SampleMarkdown, "docs/registration.md", fileInfo);

        Assert.NotEmpty(entry.ContentHash);
        Assert.Equal(64, entry.ContentHash.Length); // SHA-256 hex
    }

    [Fact]
    public void ComputeHash_IsDeterministic()
    {
        var hash1 = DocumentIndexExtractor.ComputeHash("hello world");
        var hash2 = DocumentIndexExtractor.ComputeHash("hello world");

        Assert.Equal(hash1, hash2);
    }

    [Fact]
    public void ComputeHash_DiffersForDifferentContent()
    {
        var hash1 = DocumentIndexExtractor.ComputeHash("hello world");
        var hash2 = DocumentIndexExtractor.ComputeHash("hello worlds");

        Assert.NotEqual(hash1, hash2);
    }

    [Fact]
    public void Extract_SetsSizeKb()
    {
        var fileInfo = CreateTempFile(SampleMarkdown);
        var entry = _extractor.Extract(SampleMarkdown, "docs/registration.md", fileInfo);

        Assert.True(entry.SizeKb >= 1);
    }

    [Fact]
    public void Extract_FallsBackToFileNameWhenNoTitle()
    {
        var content = "Some content without a title heading.";
        var fileInfo = CreateTempFile(content);
        var entry = _extractor.Extract(content, "docs/my-feature.md", fileInfo);

        Assert.Equal("my-feature", entry.Title);
    }

    private static FileInfo CreateTempFile(string content)
    {
        var path = Path.GetTempFileName();
        File.WriteAllText(path, content);
        return new FileInfo(path);
    }
}
