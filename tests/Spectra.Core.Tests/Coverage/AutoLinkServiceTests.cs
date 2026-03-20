using Spectra.Core.Coverage;

namespace Spectra.Core.Tests.Coverage;

public class AutoLinkServiceTests
{
    private readonly AutoLinkService _service = new();

    [Fact]
    public void GenerateLinks_MatchingTests_ReturnsLinks()
    {
        var automationFiles = new Dictionary<string, AutomationFileInfo>
        {
            ["tests/LoginTests.cs"] = new("tests/LoginTests.cs", ["TC-001", "TC-002"], [])
        };

        var testFileMap = new Dictionary<string, (string Suite, string FilePath)>
        {
            ["TC-001"] = ("auth", "/tests/auth/TC-001.md"),
            ["TC-002"] = ("auth", "/tests/auth/TC-002.md")
        };

        var result = _service.GenerateLinks(automationFiles, testFileMap);

        Assert.Equal(2, result.Count);
        Assert.Contains(result, r => r.TestId == "TC-001");
        Assert.Contains(result, r => r.TestId == "TC-002");
        Assert.All(result, r => Assert.Equal("tests/LoginTests.cs", r.AutomationFilePath));
    }

    [Fact]
    public void GenerateLinks_NoMatches_ReturnsEmpty()
    {
        var automationFiles = new Dictionary<string, AutomationFileInfo>
        {
            ["tests/Test.cs"] = new("tests/Test.cs", ["TC-999"], [])
        };

        var testFileMap = new Dictionary<string, (string Suite, string FilePath)>
        {
            ["TC-001"] = ("auth", "/tests/auth/TC-001.md")
        };

        var result = _service.GenerateLinks(automationFiles, testFileMap);

        Assert.Empty(result);
    }

    [Fact]
    public void GenerateLinks_DuplicateReferences_Deduplicates()
    {
        var automationFiles = new Dictionary<string, AutomationFileInfo>
        {
            ["tests/A.cs"] = new("tests/A.cs", ["TC-001"], []),
            ["tests/B.cs"] = new("tests/B.cs", ["TC-001"], [])
        };

        var testFileMap = new Dictionary<string, (string Suite, string FilePath)>
        {
            ["TC-001"] = ("auth", "/tests/auth/TC-001.md")
        };

        var result = _service.GenerateLinks(automationFiles, testFileMap);

        // Two different automation files both referencing TC-001 = 2 links
        Assert.Equal(2, result.Count);
    }

    [Fact]
    public void GenerateLinks_EmptyInputs_ReturnsEmpty()
    {
        var result = _service.GenerateLinks(
            new Dictionary<string, AutomationFileInfo>(),
            new Dictionary<string, (string Suite, string FilePath)>());

        Assert.Empty(result);
    }

    [Fact]
    public void GenerateLinks_ResultsAreSorted()
    {
        var automationFiles = new Dictionary<string, AutomationFileInfo>
        {
            ["tests/Test.cs"] = new("tests/Test.cs", ["TC-003", "TC-001", "TC-002"], [])
        };

        var testFileMap = new Dictionary<string, (string Suite, string FilePath)>
        {
            ["TC-001"] = ("auth", "/path"),
            ["TC-002"] = ("auth", "/path"),
            ["TC-003"] = ("auth", "/path")
        };

        var result = _service.GenerateLinks(automationFiles, testFileMap);

        Assert.Equal("TC-001", result[0].TestId);
        Assert.Equal("TC-002", result[1].TestId);
        Assert.Equal("TC-003", result[2].TestId);
    }
}
