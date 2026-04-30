using Spectra.CLI.Index;

namespace Spectra.CLI.Tests.Index;

public class PreFlightTokenCheckerTests
{
    [Fact]
    public void EnforceBudget_UnderBudget_DoesNotThrow()
    {
        var checker = new PreFlightTokenChecker();

        checker.EnforceBudget(
            estimatedTokens: 50_000,
            budgetTokens: 96_000,
            overflowingSuites: Array.Empty<SuiteTokenEstimate>(),
            commandHint: "spectra ai generate");
    }

    [Fact]
    public void EnforceBudget_AtBudgetBoundary_DoesNotThrow()
    {
        var checker = new PreFlightTokenChecker();

        // Exactly at the budget — passes (>, not >=).
        checker.EnforceBudget(
            estimatedTokens: 96_000,
            budgetTokens: 96_000,
            overflowingSuites: Array.Empty<SuiteTokenEstimate>(),
            commandHint: "spectra ai generate");
    }

    [Fact]
    public void EnforceBudget_ZeroOrNegativeBudget_DisablesCheck()
    {
        var checker = new PreFlightTokenChecker();

        checker.EnforceBudget(
            estimatedTokens: 1_000_000,
            budgetTokens: 0,
            overflowingSuites: Array.Empty<SuiteTokenEstimate>(),
            commandHint: "spectra ai generate");
    }

    [Fact]
    public void EnforceBudget_OverBudget_ThrowsTypedException()
    {
        var checker = new PreFlightTokenChecker();
        var suites = new[]
        {
            new SuiteTokenEstimate("SM_GSG_Topics", 10_877),
            new SuiteTokenEstimate("RD_Topics", 9_911),
        };

        var ex = Assert.Throws<PreFlightBudgetExceededException>(() =>
            checker.EnforceBudget(
                estimatedTokens: 200_000,
                budgetTokens: 96_000,
                overflowingSuites: suites,
                commandHint: "spectra ai generate"));

        Assert.Equal(200_000, ex.EstimatedTokens);
        Assert.Equal(96_000, ex.BudgetTokens);
        Assert.Equal(2, ex.OverflowingSuites.Count);
        Assert.Contains("SM_GSG_Topics", ex.Message);
        Assert.Contains("RD_Topics", ex.Message);
        Assert.Contains("--analyze-only", ex.Message);
        Assert.Contains("ai.analysis.max_prompt_tokens", ex.Message);
    }

    [Fact]
    public void EnforceBudget_MessageListsAllSuitesSortedDescending()
    {
        var checker = new PreFlightTokenChecker();
        var suites = new[]
        {
            new SuiteTokenEstimate("small", 1_000),
            new SuiteTokenEstimate("large", 50_000),
            new SuiteTokenEstimate("medium", 10_000),
        };

        var ex = Assert.Throws<PreFlightBudgetExceededException>(() =>
            checker.EnforceBudget(
                estimatedTokens: 200_000,
                budgetTokens: 96_000,
                overflowingSuites: suites,
                commandHint: "spectra ai generate"));

        var largeIdx = ex.Message.IndexOf("large", StringComparison.Ordinal);
        var mediumIdx = ex.Message.IndexOf("medium", StringComparison.Ordinal);
        var smallIdx = ex.Message.IndexOf("small", StringComparison.Ordinal);

        Assert.True(largeIdx > 0);
        Assert.True(largeIdx < mediumIdx);
        Assert.True(mediumIdx < smallIdx);
    }

    [Fact]
    public async Task EnforceBudgetFromLegacyIndex_FileMissing_ReturnsZeroAndDoesNotThrow()
    {
        var checker = new PreFlightTokenChecker();

        var estimated = await checker.EnforceBudgetFromLegacyIndexAsync(
            legacyIndexPath: Path.Combine(Path.GetTempPath(), "nonexistent_" + Guid.NewGuid() + ".md"),
            budgetTokens: 96_000,
            commandHint: "spectra ai generate");

        Assert.Equal(0, estimated);
    }

    [Fact]
    public async Task EnforceBudgetFromLegacyIndex_FileWithinBudget_ReturnsEstimateWithoutThrowing()
    {
        var checker = new PreFlightTokenChecker();
        var temp = Path.Combine(Path.GetTempPath(), "spectra_test_" + Guid.NewGuid() + ".md");
        try
        {
            await File.WriteAllTextAsync(temp, new string('x', 4000)); // ~1000 estimated tokens

            var estimated = await checker.EnforceBudgetFromLegacyIndexAsync(
                legacyIndexPath: temp,
                budgetTokens: 96_000,
                commandHint: "spectra ai generate");

            Assert.True(estimated > 0);
            Assert.True(estimated < 96_000);
        }
        finally
        {
            if (File.Exists(temp)) File.Delete(temp);
        }
    }

    [Fact]
    public async Task EnforceBudgetFromLegacyIndex_FileOverBudget_Throws()
    {
        var checker = new PreFlightTokenChecker();
        var temp = Path.Combine(Path.GetTempPath(), "spectra_test_" + Guid.NewGuid() + ".md");
        try
        {
            // ~250K estimated tokens (1M chars / 4) — well over the 96K budget.
            await File.WriteAllTextAsync(temp, new string('x', 1_000_000));

            await Assert.ThrowsAsync<PreFlightBudgetExceededException>(() =>
                checker.EnforceBudgetFromLegacyIndexAsync(
                    legacyIndexPath: temp,
                    budgetTokens: 96_000,
                    commandHint: "spectra ai generate"));
        }
        finally
        {
            if (File.Exists(temp)) File.Delete(temp);
        }
    }
}
