using Spectra.CLI.Commands.Docs;

namespace Spectra.CLI.Tests.Commands;

/// <summary>
/// Spec 048 test plan rows 3-5. Exercises <see cref="DocsIndexHandler.ComputeCriteriaWarning"/>
/// — the pure projection from corpus counts to the zero-criteria warning string.
/// The call-site gate (<c>if (!_skipCriteria)</c>) means a real
/// <c>--skip-criteria</c> run never reaches this helper; the "skip suppresses"
/// case is therefore documented at the call site (DocsIndexHandler.cs) and
/// validated end-to-end via quickstart.md §B, not duplicated as a unit test
/// of the helper itself.
/// </summary>
public class DocsIndexZeroCriteriaTests
{
    [Fact]
    public void DocsIndex_ZeroCriteriaAcrossCorpus_WarnsNonBlocking()
    {
        // documentsIndexed > 0 AND extracted == 0 → the warning fires.
        var warning = DocsIndexHandler.ComputeCriteriaWarning(documentsIndexed: 12, criteriaExtractedTotal: 0);

        Assert.NotNull(warning);
        Assert.Contains("12 document(s)", warning);
        Assert.Contains("0 acceptance criteria", warning);
        Assert.Contains("spectra ai analyze --extract-criteria", warning);
    }

    [Fact]
    public void DocsIndex_CriteriaFound_NoWarning()
    {
        // Any extracted > 0 suppresses the warning regardless of doc count.
        Assert.Null(DocsIndexHandler.ComputeCriteriaWarning(documentsIndexed: 12, criteriaExtractedTotal: 1));
        Assert.Null(DocsIndexHandler.ComputeCriteriaWarning(documentsIndexed: 12, criteriaExtractedTotal: 47));
    }

    [Fact]
    public void DocsIndex_RealEmptyDocs_NoFalseWarning()
    {
        // Mixed corpus: some docs are affirmed-empty (contribute 0) but others
        // produced criteria so the corpus total is > 0. The helper sees only
        // the totals — so the > 0 total suppresses the warning correctly.
        Assert.Null(DocsIndexHandler.ComputeCriteriaWarning(documentsIndexed: 5, criteriaExtractedTotal: 2));
    }

    [Fact]
    public void DocsIndex_NoDocumentsIndexed_NoWarning()
    {
        // documentsIndexed == 0 means there was nothing to extract from in the
        // first place. Suppress the warning — the user did not "lose" criteria
        // they ought to have had.
        Assert.Null(DocsIndexHandler.ComputeCriteriaWarning(documentsIndexed: 0, criteriaExtractedTotal: 0));
    }

    [Fact]
    public void DocsIndex_WarningMessage_NamesRecoveryCommand()
    {
        // FR-014: the warning MUST name the exact recovery command. Asserts on
        // the literal command so an accidental wording change that drops it is
        // caught here.
        var warning = DocsIndexHandler.ComputeCriteriaWarning(documentsIndexed: 1, criteriaExtractedTotal: 0);
        Assert.NotNull(warning);
        Assert.Contains("Run: spectra ai analyze --extract-criteria", warning);
    }
}
