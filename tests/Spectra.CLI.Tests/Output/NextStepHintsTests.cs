using Spectra.CLI.Infrastructure;
using Spectra.CLI.Output;

namespace Spectra.CLI.Tests.Output;

public class NextStepHintsTests
{
    [Fact]
    public void GetHints_Init_ReturnsGenerateAndProfile()
    {
        var hints = NextStepHints.GetHints("init", true, new HintContext());

        Assert.Contains(hints, h => h.Contains("spectra ai generate"));
        Assert.Contains(hints, h => h.Contains("spectra init-profile"));
        Assert.Contains(hints, h => h.Contains("Copilot Space"));
    }

    [Fact]
    public void GetHints_Generate_Success_ReturnsCoverageHint()
    {
        var hints = NextStepHints.GetHints("generate", true, new HintContext { SuiteName = "checkout" });

        Assert.Contains(hints, h => h.Contains("spectra ai analyze --coverage"));
        Assert.Contains(hints, h => h.Contains("Interactive mode"));
    }

    [Fact]
    public void GetHints_Generate_Failure_ReturnsEmpty()
    {
        var hints = NextStepHints.GetHints("generate", false, new HintContext());

        Assert.Empty(hints);
    }

    [Fact]
    public void GetHints_Analyze_WithoutAutoLink_SuggestsAutoLink()
    {
        var hints = NextStepHints.GetHints("analyze", true, new HintContext { HasAutoLink = false });

        Assert.Contains(hints, h => h.Contains("--auto-link"));
    }

    [Fact]
    public void GetHints_Analyze_WithAutoLink_OmitsAutoLinkHint()
    {
        var hints = NextStepHints.GetHints("analyze", true, new HintContext { HasAutoLink = true });

        Assert.DoesNotContain(hints, h => h.Contains("--auto-link"));
    }

    [Fact]
    public void GetHints_Analyze_WithGaps_SuggestsGenerate()
    {
        var hints = NextStepHints.GetHints("analyze", true, new HintContext { HasGaps = true });

        Assert.Contains(hints, h => h.Contains("spectra ai generate"));
    }

    [Fact]
    public void GetHints_Dashboard_Success_SuggestsOpenBrowser()
    {
        var hints = NextStepHints.GetHints("dashboard", true, new HintContext { OutputPath = "./site" });

        Assert.Contains(hints, h => h.Contains("./site/index.html"));
        Assert.Contains(hints, h => h.Contains("cloudflare-pages-setup.md"));
    }

    [Fact]
    public void GetHints_Validate_Success_SuggestsGenerate()
    {
        var hints = NextStepHints.GetHints("validate", true, new HintContext { ErrorCount = 0 });

        Assert.Contains(hints, h => h.Contains("spectra ai generate"));
        Assert.Contains(hints, h => h.Contains("spectra index"));
    }

    [Fact]
    public void GetHints_Validate_WithErrors_SuggestsFix()
    {
        var hints = NextStepHints.GetHints("validate", false, new HintContext { ErrorCount = 3 });

        Assert.Contains(hints, h => h.Contains("Fix the errors"));
        Assert.Contains(hints, h => h.Contains("spectra validate"));
    }

    [Fact]
    public void GetHints_DocsIndex_SuggestsGenerate()
    {
        var hints = NextStepHints.GetHints("docs-index", true, new HintContext());

        Assert.Contains(hints, h => h.Contains("spectra ai generate"));
    }

    [Fact]
    public void GetHints_Index_SuggestsValidateAndGenerate()
    {
        var hints = NextStepHints.GetHints("index", true, new HintContext());

        Assert.Contains(hints, h => h.Contains("spectra validate"));
        Assert.Contains(hints, h => h.Contains("spectra ai generate"));
    }

    [Fact]
    public void GetHints_UnknownCommand_ReturnsEmpty()
    {
        var hints = NextStepHints.GetHints("unknown-command", true, new HintContext());

        Assert.Empty(hints);
    }
}
