using Spectra.CLI.Criteria;
using Spectra.CLI.Tests.TestFixtures;
using Spectra.Core.Models.Config;

namespace Spectra.CLI.Tests.Criteria;

/// <summary>Spec 079 FR-012: tests still referencing each criterion ID, from every suite's <c>_index.json</c>.</summary>
public sealed class CriteriaReferenceCounterTests : IDisposable
{
    private readonly CriteriaTestWorkspace _ws = new();

    public void Dispose() => _ws.Dispose();

    [Fact]
    public async Task CountsAcrossSuites_CaseInsensitively()
    {
        _ws.WriteTestIndex("checkout", ("TC-101", ["AC-CHECKOUT-001", "AC-CHECKOUT-002"]), ("TC-140", ["ac-checkout-001"]));
        _ws.WriteTestIndex("login", ("TC-200", ["AC-LOGIN-001"]), ("TC-201", []));

        var (map, warnings) = await CriteriaReferenceCounter.LoadAsync(_ws.Root, SpectraConfig.Default, default);

        Assert.Empty(warnings);
        Assert.Equal(["TC-101", "TC-140"], map["AC-CHECKOUT-001"]);
        Assert.Equal(["TC-101"], map["ac-checkout-002"]);
        Assert.Equal(["TC-200"], map["AC-LOGIN-001"]);
    }

    [Fact]
    public async Task UnreadableIndex_IsSkippedWithAWarning()
    {
        _ws.WriteTestIndex("checkout", ("TC-101", ["AC-CHECKOUT-001"]));
        _ws.WriteDoc("test-cases/broken/_index.json", "{ not json");

        var (map, warnings) = await CriteriaReferenceCounter.LoadAsync(_ws.Root, SpectraConfig.Default, default);

        Assert.Equal(["TC-101"], map["AC-CHECKOUT-001"]);
        Assert.Contains(warnings, w => w.Contains("broken"));
    }

    [Fact]
    public async Task NoTestsDir_IsEmpty()
    {
        Directory.Delete(_ws.PathOf("test-cases"));

        var (map, warnings) = await CriteriaReferenceCounter.LoadAsync(_ws.Root, SpectraConfig.Default, default);

        Assert.Empty(map);
        Assert.Empty(warnings);
    }
}
