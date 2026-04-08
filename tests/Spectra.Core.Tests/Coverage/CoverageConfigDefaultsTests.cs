using Spectra.Core.Models.Config;

namespace Spectra.Core.Tests.Coverage;

public class CoverageConfigDefaultsTests
{
    [Fact]
    public void CriteriaDir_Default_UsesDocsCriteria()
    {
        var config = new CoverageConfig();
        Assert.Equal("docs/criteria", config.CriteriaDir);
    }

    [Fact]
    public void CriteriaFile_Default_UsesDocsCriteria()
    {
        var config = new CoverageConfig();
        Assert.Equal("docs/criteria/_criteria_index.yaml", config.CriteriaFile);
    }

    [Fact]
    public void CriteriaDir_DoesNotContainRequirements()
    {
        var config = new CoverageConfig();
        Assert.DoesNotContain("requirements", config.CriteriaDir);
        Assert.DoesNotContain("requirements", config.CriteriaFile);
    }
}
