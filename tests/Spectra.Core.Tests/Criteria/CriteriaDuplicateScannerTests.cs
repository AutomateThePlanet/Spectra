using Spectra.Core.Criteria;
using Spectra.Core.Models.Coverage;

namespace Spectra.Core.Tests.Criteria;

/// <summary>Spec 079 FR-010: duplicate criterion IDs are found and reported with their files.</summary>
public class CriteriaDuplicateScannerTests
{
    private static IReadOnlyList<AcceptanceCriterion> Ids(params string[] ids) =>
        ids.Select(id => new AcceptanceCriterion { Id = id, Text = id }).ToList();

    [Fact]
    public void Scan_FindsIdsAcrossFiles_CaseInsensitively()
    {
        var result = CriteriaDuplicateScanner.Scan(
        [
            ("docs/b/login.criteria.yaml", Ids("AC-LOGIN-001", "AC-LOGIN-002")),
            ("docs/a/login.criteria.yaml", Ids("ac-login-001")),
            ("docs/checkout.criteria.yaml", Ids("AC-CHECKOUT-001"))
        ]);

        var dup = Assert.Single(result);
        Assert.Equal("AC-LOGIN-001", dup.Id, ignoreCase: true);
        Assert.Equal(["docs/a/login.criteria.yaml", "docs/b/login.criteria.yaml"], dup.Files);
    }

    [Fact]
    public void Scan_FindsDuplicatesWithinOneFile()
    {
        var dup = Assert.Single(CriteriaDuplicateScanner.Scan([("x.criteria.yaml", Ids("AC-X-001", "AC-X-001"))]));

        Assert.Equal(["x.criteria.yaml"], dup.Files);
    }

    [Fact]
    public void Scan_NoDuplicates_IsEmpty()
    {
        Assert.Empty(CriteriaDuplicateScanner.Scan([("a", Ids("AC-A-001")), ("b", Ids("AC-B-001"))]));
    }
}
