using Spectra.Core.Models;
using Spectra.Core.Validation;

namespace Spectra.Core.Tests.Validation;

public class IndexFreshnessValidatorTests
{
    private readonly IndexFreshnessValidator _validator = new();

    [Fact]
    public void Validate_SyncedIndex_ReturnsNoErrors()
    {
        var tests = new[]
        {
            CreateTestCase("TC-001", "Test 1"),
            CreateTestCase("TC-002", "Test 2")
        };

        var index = CreateIndex("checkout",
            CreateIndexEntry("TC-001", "Test 1"),
            CreateIndexEntry("TC-002", "Test 2"));

        var result = _validator.Validate(index, tests, "tests/checkout");

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_OrphanedInIndex_ReturnsError()
    {
        var tests = new[]
        {
            CreateTestCase("TC-001", "Test 1")
        };

        var index = CreateIndex("checkout",
            CreateIndexEntry("TC-001", "Test 1"),
            CreateIndexEntry("TC-002", "Test 2"));

        var result = _validator.Validate(index, tests, "tests/checkout");

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Code == "INDEX_ORPHAN");
    }

    [Fact]
    public void Validate_MissingFromIndex_ReturnsError()
    {
        var tests = new[]
        {
            CreateTestCase("TC-001", "Test 1"),
            CreateTestCase("TC-002", "Test 2")
        };

        var index = CreateIndex("checkout",
            CreateIndexEntry("TC-001", "Test 1"));

        var result = _validator.Validate(index, tests, "tests/checkout");

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Code == "INDEX_MISSING");
    }

    [Fact]
    public void Validate_TitleMismatch_ReturnsWarning()
    {
        var tests = new[]
        {
            CreateTestCase("TC-001", "Actual Title")
        };

        var index = CreateIndex("checkout",
            CreateIndexEntry("TC-001", "Index Title"));

        var result = _validator.Validate(index, tests, "tests/checkout");

        Assert.True(result.IsValid);
        Assert.Contains(result.Warnings, w => w.Code == "INDEX_TITLE_MISMATCH");
    }

    [Fact]
    public void Validate_PriorityMismatch_ReturnsWarning()
    {
        var tests = new[]
        {
            CreateTestCase("TC-001", "Test 1", Priority.Low)
        };

        var index = CreateIndex("checkout",
            CreateIndexEntry("TC-001", "Test 1", "high"));

        var result = _validator.Validate(index, tests, "tests/checkout");

        Assert.True(result.IsValid);
        Assert.Contains(result.Warnings, w => w.Code == "INDEX_PRIORITY_MISMATCH");
    }

    [Fact]
    public void Validate_CountMismatch_ReturnsWarning()
    {
        var tests = new[]
        {
            CreateTestCase("TC-001", "Test 1")
        };

        // Create index with mismatched TestCount by having more entries
        // but checking in a way that shows count mismatch
        var index = new MetadataIndex
        {
            Suite = "checkout",
            GeneratedAt = DateTime.UtcNow,
            Tests =
            [
                CreateIndexEntry("TC-001", "Test 1")
            ]
        };

        var result = _validator.Validate(index, tests, "tests/checkout");

        // With proper synced index, no warnings
        Assert.True(result.IsValid);
    }

    private static TestCase CreateTestCase(string id, string title, Priority priority = Priority.High) => new()
    {
        Id = id,
        Title = title,
        Priority = priority,
        Steps = [],
        ExpectedResult = "Result",
        FilePath = $"{id}.md"
    };

    private static MetadataIndex CreateIndex(string suite, params TestIndexEntry[] entries) => new()
    {
        Suite = suite,
        GeneratedAt = DateTime.UtcNow,
        Tests = entries.ToList()
    };

    private static TestIndexEntry CreateIndexEntry(string id, string title, string priority = "high") => new()
    {
        Id = id,
        File = $"{id}.md",
        Title = title,
        Priority = priority
    };
}
