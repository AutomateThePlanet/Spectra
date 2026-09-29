using Spectra.Core.Criteria;
using Spectra.Core.Models.Coverage;

namespace Spectra.Core.Tests.Criteria;

/// <summary>Spec 079 FR-007: IDs come from a committed per-prefix high-water mark and are never reused.</summary>
public class CriteriaIdAllocatorTests
{
    [Theory]
    [InlineData("AC-CHECKOUT-013", "AC-CHECKOUT", 13)]
    [InlineData("AC-042", "AC", 42)]
    [InlineData("AC-MY-COMP-7", "AC-MY-COMP", 7)]
    [InlineData("ac-login-001", "AC-LOGIN", 1)]
    public void ParseId_SplitsPrefixAndNumber(string id, string prefix, int number)
    {
        Assert.Equal((prefix, number), CriteriaIdAllocator.ParseId(id));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("AC")]
    [InlineData("AC-")]
    [InlineData("AC-X1")]
    [InlineData("-12")]
    [InlineData("AC-99999999999999")]
    public void ParseId_Junk_IsNull(string? id)
    {
        Assert.Null(CriteriaIdAllocator.ParseId(id));
    }

    [Fact]
    public void EffectiveMark_IsMaxOfStoredAndOnDisk()
    {
        var index = new CriteriaIndex { IdHighWaterMarks = new() { ["AC-CHECKOUT"] = 5 } };

        Assert.Equal(9, CriteriaIdAllocator.EffectiveMark(index, "AC-CHECKOUT", ["AC-CHECKOUT-009", "AC-LOGIN-020"]));
        Assert.Equal(5, CriteriaIdAllocator.EffectiveMark(index, "AC-CHECKOUT", ["AC-CHECKOUT-002"]));
        Assert.Equal(20, CriteriaIdAllocator.EffectiveMark(index, "ac-login", ["AC-LOGIN-020"]));
    }

    [Fact]
    public void Allocate_ContinuesAboveAStoredMarkHigherThanAnyFile()
    {
        var index = new CriteriaIndex { IdHighWaterMarks = new() { ["AC-CHECKOUT"] = 12 } };

        var ids = CriteriaIdAllocator.Allocate(index, "AC-CHECKOUT", 2, ["AC-CHECKOUT-003"]);

        Assert.Equal(["AC-CHECKOUT-013", "AC-CHECKOUT-014"], ids);
        Assert.Equal(14, index.IdHighWaterMarks!["AC-CHECKOUT"]);
    }

    [Fact]
    public void Allocate_ContinuesAboveFileIds_WhenNoMarkIsStored()
    {
        var index = new CriteriaIndex();

        var ids = CriteriaIdAllocator.Allocate(index, "ac-login", 1, ["AC-LOGIN-007"]);

        Assert.Equal(["AC-LOGIN-008"], ids);
        Assert.Equal(8, index.IdHighWaterMarks!["AC-LOGIN"]);
    }

    [Fact]
    public void Allocate_WidensPastThreeDigits()
    {
        var index = new CriteriaIndex { IdHighWaterMarks = new() { ["AC-X"] = 999 } };

        Assert.Equal(["AC-X-1000"], CriteriaIdAllocator.Allocate(index, "AC-X", 1, []));
    }

    [Fact]
    public void Allocate_Zero_DoesNotTouchTheIndex()
    {
        var index = new CriteriaIndex();

        Assert.Empty(CriteriaIdAllocator.Allocate(index, "AC-X", 0, ["AC-X-004"]));
        Assert.Null(index.IdHighWaterMarks);
    }

    [Fact]
    public void RemovingTheHighestId_NeverReissuesIt()
    {
        var index = new CriteriaIndex();
        var first = CriteriaIdAllocator.Allocate(index, "AC-CHECKOUT", 3, []);

        // AC-CHECKOUT-003 is removed from the files; only 001 and 002 remain.
        var next = CriteriaIdAllocator.Allocate(index, "AC-CHECKOUT", 1, first.Take(2));

        Assert.Equal(["AC-CHECKOUT-004"], next);
    }

    [Fact]
    public void Seed_RaisesMarksToTheHighestIdPresent_NeverLowers()
    {
        var index = new CriteriaIndex { IdHighWaterMarks = new() { ["AC-A"] = 50 } };

        var changed = CriteriaIdAllocator.Seed(index, ["AC-A-010", "AC-B-004", "AC-B-002", "junk"]);

        Assert.Equal(50, index.IdHighWaterMarks!["AC-A"]);
        Assert.Equal(4, index.IdHighWaterMarks["AC-B"]);
        Assert.Equal(new Dictionary<string, int> { ["AC-B"] = 4 }, changed);
    }
}
