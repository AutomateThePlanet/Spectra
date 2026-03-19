using Spectra.CLI.Output;
using Spectra.Core.Models;
using Spectra.Core.Models.Grounding;

namespace Spectra.CLI.Tests.Output;

public class VerificationPresenterTests
{
    [Theory]
    [InlineData(VerificationVerdict.Grounded, OutputSymbols.Success)]
    [InlineData(VerificationVerdict.Partial, OutputSymbols.Warning)]
    [InlineData(VerificationVerdict.Hallucinated, OutputSymbols.Error)]
    public void GetVerdictSymbol_ReturnsCorrectSymbol(VerificationVerdict verdict, string expected)
    {
        var symbol = VerificationPresenter.GetVerdictSymbol(verdict);

        Assert.Equal(expected, symbol);
    }

    [Theory]
    [InlineData(VerificationVerdict.Grounded, "[green]")]
    [InlineData(VerificationVerdict.Partial, "[yellow]")]
    [InlineData(VerificationVerdict.Hallucinated, "[red]")]
    public void GetVerdictMarkup_ContainsCorrectColor(VerificationVerdict verdict, string expectedColor)
    {
        var markup = VerificationPresenter.GetVerdictMarkup(verdict);

        Assert.Contains(expectedColor, markup);
    }

    [Fact]
    public void ShowSummary_HandlesEmptyResults()
    {
        var presenter = new VerificationPresenter();
        var results = new List<(TestCase, VerificationResult)>();

        // Should not throw
        presenter.ShowSummary(results);
    }

    [Fact]
    public void ShowPartialDetails_HandlesNoPartials()
    {
        var presenter = new VerificationPresenter();
        var results = new List<(TestCase Test, VerificationResult Result)>
        {
            (CreateTestCase("TC-001"), CreateResult(VerificationVerdict.Grounded))
        };

        // Should not throw
        presenter.ShowPartialDetails(results);
    }

    [Fact]
    public void ShowRejectedDetails_HandlesNoRejected()
    {
        var presenter = new VerificationPresenter();
        var results = new List<(TestCase Test, VerificationResult Result)>
        {
            (CreateTestCase("TC-001"), CreateResult(VerificationVerdict.Grounded))
        };

        // Should not throw
        presenter.ShowRejectedDetails(results);
    }

    private static TestCase CreateTestCase(string id) => new()
    {
        Id = id,
        Title = $"Test {id}",
        Priority = Priority.Medium,
        Steps = ["Step 1"],
        ExpectedResult = "Expected result",
        FilePath = $"suite/{id}.md"
    };

    private static VerificationResult CreateResult(VerificationVerdict verdict) => new()
    {
        Verdict = verdict,
        Score = verdict == VerificationVerdict.Grounded ? 0.95 :
                verdict == VerificationVerdict.Partial ? 0.7 : 0.3,
        Findings = verdict == VerificationVerdict.Partial ?
        [
            new CriticFinding
            {
                Element = "Step 1",
                Claim = "Some claim",
                Status = FindingStatus.Unverified,
                Reason = "Not in docs"
            }
        ] : [],
        CriticModel = "test-model"
    };
}
