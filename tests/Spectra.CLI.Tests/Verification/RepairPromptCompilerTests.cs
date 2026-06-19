using Spectra.CLI.Verification;
using Spectra.Core.Models;
using Spectra.Core.Models.Grounding;

namespace Spectra.CLI.Tests.Verification;

public class RepairPromptCompilerTests
{
    private static TestCase ValidTest() => new()
    {
        Id = "TC-113",
        Title = "Verify file size conversion from bytes to KB",
        Priority = Priority.Medium,
        Steps = ["Navigate to file settings", "Upload a 1024-byte file", "Note the displayed size"],
        ExpectedResult = "Size is shown as 1.0 KB",
        FilePath = "file-management/TC-113.md",
        SourceRefs = ["docs/file-sizes.md"]
    };

    private static CriticFinding UnverifiedFinding(string element = "Expected Result", string claim = "1 KB = 1024 bytes") =>
        new() { Element = element, Claim = claim, Status = FindingStatus.Unverified, Reason = "Not found in docs" };

    private static CriticFinding HallucinatedFinding(string element = "Step 2") =>
        new() { Element = element, Claim = "1 KB = 1000 bytes", Status = FindingStatus.Hallucinated, Reason = "Contradicts docs" };

    private static CriticFinding GroundedFinding(string element = "Step 1") =>
        new() { Element = element, Claim = "Navigate to file settings", Status = FindingStatus.Grounded, Evidence = "Section 3.1" };

    private static SourceDocument MakeDoc(string path = "docs/file-sizes.md", string content = "# File Sizes\n\n1 KB = 1024 bytes.") =>
        new() { Path = path, Title = "File Sizes", Content = content };

    // --- Refusal cases ---

    [Fact]
    public void Compile_MissingId_RefusesWithReason()
    {
        var test = ValidTest();
        var noId = new TestCase
        {
            Id = "",
            Title = test.Title,
            Priority = test.Priority,
            Steps = test.Steps,
            ExpectedResult = test.ExpectedResult,
            FilePath = test.FilePath
        };

        var (prompt, reason) = RepairPromptCompiler.Compile(noId, [UnverifiedFinding()], []);

        Assert.Null(prompt);
        Assert.NotNull(reason);
        Assert.Contains("id", reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Compile_MissingTitle_RefusesWithReason()
    {
        var noTitle = new TestCase
        {
            Id = "TC-113",
            Title = "",
            Priority = Priority.Medium,
            Steps = ["step"],
            ExpectedResult = "result",
            FilePath = "x/TC-113.md"
        };

        var (prompt, reason) = RepairPromptCompiler.Compile(noTitle, [UnverifiedFinding()], []);

        Assert.Null(prompt);
        Assert.NotNull(reason);
    }

    [Fact]
    public void Compile_NoNonGroundedFindings_RefusesWithReason()
    {
        var (prompt, reason) = RepairPromptCompiler.Compile(ValidTest(), [], []);

        Assert.Null(prompt);
        Assert.NotNull(reason);
        Assert.Contains("grounded", reason, StringComparison.OrdinalIgnoreCase);
    }

    // --- Valid cases ---

    [Fact]
    public void Compile_ValidInputs_ReturnsNonNullPrompt()
    {
        var (prompt, reason) = RepairPromptCompiler.Compile(ValidTest(), [UnverifiedFinding()], [MakeDoc()]);

        Assert.NotNull(prompt);
        Assert.Null(reason);
    }

    [Fact]
    public void Compile_PromptContainsTestId()
    {
        var (prompt, _) = RepairPromptCompiler.Compile(ValidTest(), [UnverifiedFinding()], []);
        Assert.Contains("TC-113", prompt);
    }

    [Fact]
    public void Compile_PromptContainsTestTitle()
    {
        var (prompt, _) = RepairPromptCompiler.Compile(ValidTest(), [UnverifiedFinding()], []);
        Assert.Contains("Verify file size conversion from bytes to KB", prompt);
    }

    [Fact]
    public void Compile_PromptContainsSteps()
    {
        var (prompt, _) = RepairPromptCompiler.Compile(ValidTest(), [UnverifiedFinding()], []);
        Assert.Contains("Upload a 1024-byte file", prompt);
    }

    [Fact]
    public void Compile_PromptContainsExpectedResult()
    {
        var (prompt, _) = RepairPromptCompiler.Compile(ValidTest(), [UnverifiedFinding()], []);
        Assert.Contains("Size is shown as 1.0 KB", prompt);
    }

    [Fact]
    public void Compile_UnverifiedFinding_LabeledAsUnverified()
    {
        var (prompt, _) = RepairPromptCompiler.Compile(ValidTest(), [UnverifiedFinding()], []);
        Assert.Contains("[UNVERIFIED]", prompt);
    }

    [Fact]
    public void Compile_HallucinatedFinding_LabeledAsHallucinated()
    {
        var (prompt, _) = RepairPromptCompiler.Compile(ValidTest(), [HallucinatedFinding()], []);
        Assert.Contains("[HALLUCINATED]", prompt);
    }

    [Fact]
    public void Compile_FindingReason_IncludedInPrompt()
    {
        var finding = new CriticFinding
        {
            Element = "Step 2",
            Claim = "some claim",
            Status = FindingStatus.Unverified,
            Reason = "Not traceable to any source"
        };

        var (prompt, _) = RepairPromptCompiler.Compile(ValidTest(), [finding], []);
        Assert.Contains("Not traceable to any source", prompt);
    }

    [Fact]
    public void Compile_SourceDoc_IncludedInPrompt()
    {
        var doc = MakeDoc(content: "# File Sizes\n\n1 KB = 1024 bytes.");
        var (prompt, _) = RepairPromptCompiler.Compile(ValidTest(), [UnverifiedFinding()], [doc]);
        Assert.Contains("1 KB = 1024 bytes", prompt);
        Assert.Contains("docs/file-sizes.md", prompt);
    }

    [Fact]
    public void Compile_NoSourceDocs_IncludesFallbackMessage()
    {
        var (prompt, _) = RepairPromptCompiler.Compile(ValidTest(), [UnverifiedFinding()], []);
        Assert.Contains("No source documents available", prompt, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Compile_DocContentExceedsMaxChars_Truncated()
    {
        var longContent = new string('x', 10000);
        var doc = new SourceDocument { Path = "docs/x.md", Title = "Big Doc", Content = longContent };

        var (prompt, _) = RepairPromptCompiler.Compile(ValidTest(), [UnverifiedFinding()], [doc]);
        Assert.Contains("truncated", prompt, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(longContent, prompt);
    }

    [Fact]
    public void Compile_MoreThanMaxDocs_OnlyFirst5Included()
    {
        var docs = Enumerable.Range(1, 8)
            .Select(i => new SourceDocument { Path = $"docs/doc{i}.md", Title = $"Doc {i}", Content = $"unique-content-{i}" })
            .ToList();

        var (prompt, _) = RepairPromptCompiler.Compile(ValidTest(), [UnverifiedFinding()], docs);

        // First 5 included
        for (var i = 1; i <= 5; i++)
            Assert.Contains($"unique-content-{i}", prompt);

        // 6–8 not included
        for (var i = 6; i <= 8; i++)
            Assert.DoesNotContain($"unique-content-{i}", prompt);
    }

    [Fact]
    public void Compile_MultipleFindings_AllListed()
    {
        IReadOnlyList<CriticFinding> findings =
        [
            UnverifiedFinding("Expected Result", "1 KB = 1024 bytes"),
            HallucinatedFinding("Step 3")
        ];

        var (prompt, _) = RepairPromptCompiler.Compile(ValidTest(), findings, []);

        Assert.Contains("Expected Result", prompt);
        Assert.Contains("Step 3", prompt);
        Assert.Contains("[UNVERIFIED]", prompt);
        Assert.Contains("[HALLUCINATED]", prompt);
    }

    [Fact]
    public void Compile_PromptContainsJsonArrayInstruction()
    {
        var (prompt, _) = RepairPromptCompiler.Compile(ValidTest(), [UnverifiedFinding()], []);
        Assert.Contains("JSON array", prompt, StringComparison.OrdinalIgnoreCase);
    }
}
