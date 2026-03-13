using Spectra.Core.Index;
using Spectra.Core.Models;

namespace Spectra.Core.Tests.Index;

public class IndexGeneratorTests
{
    private readonly IndexGenerator _generator = new();

    [Fact]
    public void Generate_CreatesIndexWithCorrectSuiteName()
    {
        var tests = new[] { CreateTestCase("TC-001") };

        var index = _generator.Generate("checkout", tests);

        Assert.Equal("checkout", index.Suite);
    }

    [Fact]
    public void Generate_SetsGeneratedAt()
    {
        var tests = new[] { CreateTestCase("TC-001") };
        var before = DateTime.UtcNow;

        var index = _generator.Generate("checkout", tests);

        Assert.InRange(index.GeneratedAt, before, DateTime.UtcNow.AddSeconds(1));
    }

    [Fact]
    public void Generate_IncludesAllTests()
    {
        var tests = new[]
        {
            CreateTestCase("TC-001"),
            CreateTestCase("TC-002"),
            CreateTestCase("TC-003")
        };

        var index = _generator.Generate("checkout", tests);

        Assert.Equal(3, index.TestCount);
    }

    [Fact]
    public void Generate_SortsTestsById()
    {
        var tests = new[]
        {
            CreateTestCase("TC-003"),
            CreateTestCase("TC-001"),
            CreateTestCase("TC-002")
        };

        var index = _generator.Generate("checkout", tests);

        Assert.Equal("TC-001", index.Tests[0].Id);
        Assert.Equal("TC-002", index.Tests[1].Id);
        Assert.Equal("TC-003", index.Tests[2].Id);
    }

    [Fact]
    public void CreateEntry_MapsAllFields()
    {
        var test = new TestCase
        {
            Id = "TC-001",
            FilePath = "checkout/TC-001.md",
            Title = "Test checkout",
            Priority = Priority.High,
            Tags = ["smoke", "checkout"],
            Component = "cart",
            DependsOn = "TC-000",
            SourceRefs = ["docs/checkout.md"],
            Steps = [],
            ExpectedResult = "Success"
        };

        var entry = _generator.CreateEntry(test);

        Assert.Equal("TC-001", entry.Id);
        Assert.Equal("TC-001.md", entry.File);
        Assert.Equal("Test checkout", entry.Title);
        Assert.Equal("high", entry.Priority);
        Assert.Equal(["smoke", "checkout"], entry.Tags);
        Assert.Equal("cart", entry.Component);
        Assert.Equal("TC-000", entry.DependsOn);
        Assert.Equal(["docs/checkout.md"], entry.SourceRefs);
    }

    [Fact]
    public void Update_AddsNewTests()
    {
        var existing = CreateIndex("checkout", "TC-001");
        var newTests = new[] { CreateTestCase("TC-002") };

        var updated = _generator.Update(existing, newTests);

        Assert.Equal(2, updated.TestCount);
        Assert.Contains(updated.Tests, t => t.Id == "TC-001");
        Assert.Contains(updated.Tests, t => t.Id == "TC-002");
    }

    [Fact]
    public void Update_UpdatesExistingTests()
    {
        var existing = CreateIndex("checkout", "TC-001");
        var updatedTest = CreateTestCase("TC-001", "Updated Title");

        var updated = _generator.Update(existing, [updatedTest]);

        Assert.Single(updated.Tests);
        Assert.Equal("Updated Title", updated.Tests[0].Title);
    }

    [Fact]
    public void Update_MaintainsSortOrder()
    {
        var existing = CreateIndex("checkout", "TC-001", "TC-003");
        var newTests = new[] { CreateTestCase("TC-002") };

        var updated = _generator.Update(existing, newTests);

        Assert.Equal("TC-001", updated.Tests[0].Id);
        Assert.Equal("TC-002", updated.Tests[1].Id);
        Assert.Equal("TC-003", updated.Tests[2].Id);
    }

    [Fact]
    public void RemoveOrphans_RemovesNonexistentTests()
    {
        var existing = CreateIndex("checkout", "TC-001", "TC-002", "TC-003");
        var validIds = new[] { "TC-001", "TC-003" };

        var cleaned = _generator.RemoveOrphans(existing, validIds);

        Assert.Equal(2, cleaned.TestCount);
        Assert.Contains(cleaned.Tests, t => t.Id == "TC-001");
        Assert.Contains(cleaned.Tests, t => t.Id == "TC-003");
        Assert.DoesNotContain(cleaned.Tests, t => t.Id == "TC-002");
    }

    [Fact]
    public void RemoveOrphans_KeepsAllIfAllValid()
    {
        var existing = CreateIndex("checkout", "TC-001", "TC-002");
        var validIds = new[] { "TC-001", "TC-002" };

        var cleaned = _generator.RemoveOrphans(existing, validIds);

        Assert.Equal(2, cleaned.TestCount);
    }

    private static TestCase CreateTestCase(string id, string title = "Test") => new()
    {
        Id = id,
        Title = title,
        Priority = Priority.High,
        Steps = [],
        ExpectedResult = "Result",
        FilePath = $"{id}.md"
    };

    private static MetadataIndex CreateIndex(string suite, params string[] testIds)
    {
        return new MetadataIndex
        {
            Suite = suite,
            GeneratedAt = DateTime.UtcNow,
            Tests = testIds.Select(id => new TestIndexEntry
            {
                Id = id,
                File = $"{id}.md",
                Title = "Test",
                Priority = "high"
            }).ToList()
        };
    }
}
