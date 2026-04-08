using Spectra.CLI.Commands.Analyze;

namespace Spectra.CLI.Tests.Commands;

public class AnalyzeHandlerDocumentSkipTests
{
    [Theory]
    [InlineData("_index.md")]
    [InlineData("_index.yaml")]
    [InlineData("_index.json")]
    [InlineData("_INDEX.MD")]
    [InlineData("_Index.Yaml")]
    public void ShouldSkipDocument_IndexFiles_ReturnsTrue(string fileName)
    {
        Assert.True(AnalyzeHandler.ShouldSkipDocument(fileName));
    }

    [Theory]
    [InlineData("checkout.criteria.yaml")]
    [InlineData("payments.criteria.yaml")]
    [InlineData("CHECKOUT.CRITERIA.YAML")]
    public void ShouldSkipDocument_CriteriaFiles_ReturnsTrue(string fileName)
    {
        Assert.True(AnalyzeHandler.ShouldSkipDocument(fileName));
    }

    [Theory]
    [InlineData("_criteria_index.yaml")]
    [InlineData("_CRITERIA_INDEX.YAML")]
    public void ShouldSkipDocument_CriteriaIndex_ReturnsTrue(string fileName)
    {
        Assert.True(AnalyzeHandler.ShouldSkipDocument(fileName));
    }

    [Theory]
    [InlineData("checkout.md")]
    [InlineData("application-workflow.md")]
    [InlineData("README.md")]
    [InlineData("api-docs.yaml")]
    [InlineData("config.json")]
    public void ShouldSkipDocument_RegularDocuments_ReturnsFalse(string fileName)
    {
        Assert.False(AnalyzeHandler.ShouldSkipDocument(fileName));
    }
}
