#pragma warning disable CS0618 // Obsolete type usage — testing the legacy RequirementsWriter

using Spectra.Core.Models.Coverage;
using Spectra.Core.Parsing;

namespace Spectra.Core.Tests.Parsing;

public class RequirementsWriterTests : IDisposable
{
    private readonly string _tempDir;
    private readonly RequirementsWriter _writer;

    public RequirementsWriterTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"req-writer-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
        _writer = new RequirementsWriter();
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    [Fact]
    public async Task MergeAndWrite_NewFile_CreatesWithCorrectYaml()
    {
        var path = Path.Combine(_tempDir, "reqs.yaml");
        var reqs = new List<RequirementDefinition>
        {
            new() { Title = "User can log in", Source = "docs/auth.md", Priority = "high" },
            new() { Title = "System validates email", Source = "docs/auth.md", Priority = "medium" }
        };

        var result = await _writer.MergeAndWriteAsync(path, reqs);

        Assert.True(File.Exists(path));
        Assert.Equal(2, result.Merged.Count);
        Assert.Equal(2, result.TotalInFile);
        Assert.Equal(0, result.SkippedCount);

        var parser = new RequirementsParser();
        var parsed = await parser.ParseAsync(path);
        Assert.Equal(2, parsed.Count);
        Assert.Equal("REQ-001", parsed[0].Id);
        Assert.Equal("REQ-002", parsed[1].Id);
    }

    [Fact]
    public async Task MergeAndWrite_ExistingFile_PreservesExisting()
    {
        var path = Path.Combine(_tempDir, "reqs.yaml");
        var existing = new List<RequirementDefinition>
        {
            new() { Title = "Existing requirement", Source = "docs/old.md", Priority = "high" }
        };
        await _writer.MergeAndWriteAsync(path, existing);

        var newReqs = new List<RequirementDefinition>
        {
            new() { Title = "New requirement", Source = "docs/new.md", Priority = "medium" }
        };
        var result = await _writer.MergeAndWriteAsync(path, newReqs);

        Assert.Single(result.Merged);
        Assert.Equal(2, result.TotalInFile);

        var parser = new RequirementsParser();
        var parsed = await parser.ParseAsync(path);
        Assert.Equal("REQ-001", parsed[0].Id);
        Assert.Equal("Existing requirement", parsed[0].Title);
        Assert.Equal("REQ-002", parsed[1].Id);
        Assert.Equal("New requirement", parsed[1].Title);
    }

    [Fact]
    public void DetectDuplicates_ExactMatch_SkipsDuplicate()
    {
        var existing = new List<RequirementDefinition>
        {
            new() { Id = "REQ-001", Title = "User can log in" }
        };
        var candidates = new List<RequirementDefinition>
        {
            new() { Title = "User Can Log In", Source = "docs/auth.md" } // case-insensitive match
        };

        var result = _writer.DetectDuplicates(existing, candidates);

        Assert.Empty(result.Merged);
        Assert.Single(result.Duplicates);
        Assert.Equal("REQ-001", result.Duplicates[0].ExistingId);
    }

    [Fact]
    public void DetectDuplicates_SubstringMatch_SkipsDuplicate()
    {
        var existing = new List<RequirementDefinition>
        {
            new() { Id = "REQ-001", Title = "System must validate user email addresses" }
        };
        var candidates = new List<RequirementDefinition>
        {
            new() { Title = "validate user email addresses", Source = "docs/auth.md" }
        };

        var result = _writer.DetectDuplicates(existing, candidates);

        Assert.Empty(result.Merged);
        Assert.Single(result.Duplicates);
    }

    [Fact]
    public void AllocateIds_EmptyExisting_StartsFromOne()
    {
        var existing = new List<RequirementDefinition>();
        var newItems = new List<RequirementDefinition>
        {
            new() { Title = "Req 1" },
            new() { Title = "Req 2" },
            new() { Title = "Req 3" }
        };

        var result = _writer.AllocateIds(existing, newItems);

        Assert.Equal("REQ-001", result[0].Id);
        Assert.Equal("REQ-002", result[1].Id);
        Assert.Equal("REQ-003", result[2].Id);
    }

    [Fact]
    public void AllocateIds_ContinuesFromHighest()
    {
        var existing = new List<RequirementDefinition>
        {
            new() { Id = "REQ-010", Title = "Existing 10" },
            new() { Id = "REQ-012", Title = "Existing 12" }
        };
        var newItems = new List<RequirementDefinition>
        {
            new() { Title = "New one" }
        };

        var result = _writer.AllocateIds(existing, newItems);

        Assert.Equal("REQ-013", result[0].Id);
    }

    [Fact]
    public void AllocateIds_DoesNotReuseGaps()
    {
        // REQ-005 was deleted, highest is REQ-010
        var existing = new List<RequirementDefinition>
        {
            new() { Id = "REQ-003", Title = "Three" },
            new() { Id = "REQ-010", Title = "Ten" }
            // Gap: REQ-005 missing
        };
        var newItems = new List<RequirementDefinition>
        {
            new() { Title = "New" }
        };

        var result = _writer.AllocateIds(existing, newItems);

        Assert.Equal("REQ-011", result[0].Id); // Not REQ-004 or REQ-005
    }

    [Fact]
    public async Task MergeAndWrite_CreatesParentDirectories()
    {
        var path = Path.Combine(_tempDir, "nested", "deep", "reqs.yaml");

        var reqs = new List<RequirementDefinition>
        {
            new() { Title = "Test", Source = "test.md", Priority = "medium" }
        };

        var result = await _writer.MergeAndWriteAsync(path, reqs);

        Assert.True(File.Exists(path));
        Assert.Equal(1, result.TotalInFile);
    }

    [Fact]
    public async Task MergeAndWrite_EmptyNewList_WritesNothing()
    {
        var path = Path.Combine(_tempDir, "reqs.yaml");
        var reqs = new List<RequirementDefinition>();

        var result = await _writer.MergeAndWriteAsync(path, reqs);

        Assert.False(File.Exists(path));
        Assert.Empty(result.Merged);
        Assert.Equal(0, result.TotalInFile);
    }

    [Fact]
    public void DetectDuplicates_FiltersBlanks()
    {
        var existing = new List<RequirementDefinition>();
        var candidates = new List<RequirementDefinition>
        {
            new() { Title = "" },
            new() { Title = "  " },
            new() { Title = "Valid requirement" }
        };

        var result = _writer.DetectDuplicates(existing, candidates);

        Assert.Single(result.Merged);
        Assert.Equal("Valid requirement", result.Merged[0].Title);
    }

    [Fact]
    public void AllocateIds_DefaultsPriorityToMedium()
    {
        var result = _writer.AllocateIds([], [new RequirementDefinition { Title = "No priority" }]);

        Assert.Equal("medium", result[0].Priority);
    }
}
