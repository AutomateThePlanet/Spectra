using Spectra.CLI.Dashboard;

namespace Spectra.CLI.Tests.Dashboard;

public class SampleDataFactoryTests
{
    [Fact]
    public void CreateSampleData_ReturnsNonNullData()
    {
        var data = SampleDataFactory.CreateSampleData();

        Assert.NotNull(data);
    }

    [Fact]
    public void CreateSampleData_HasSuites()
    {
        var data = SampleDataFactory.CreateSampleData();

        Assert.Equal(3, data.Suites.Count);
        Assert.Contains(data.Suites, s => s.Name == "checkout");
        Assert.Contains(data.Suites, s => s.Name == "authentication");
        Assert.Contains(data.Suites, s => s.Name == "search");
    }

    [Fact]
    public void CreateSampleData_HasTests()
    {
        var data = SampleDataFactory.CreateSampleData();

        Assert.Equal(10, data.Tests.Count);
        Assert.All(data.Tests, t =>
        {
            Assert.NotEmpty(t.Id);
            Assert.NotEmpty(t.Title);
            Assert.NotEmpty(t.Suite);
            Assert.NotEmpty(t.Priority);
        });
    }

    [Fact]
    public void CreateSampleData_HasRuns()
    {
        var data = SampleDataFactory.CreateSampleData();

        Assert.Equal(2, data.Runs.Count);
        Assert.All(data.Runs, r =>
        {
            Assert.NotEmpty(r.RunId);
            Assert.NotEmpty(r.Suite);
            Assert.True(r.Total > 0);
        });
    }

    [Fact]
    public void CreateSampleData_HasTrends()
    {
        var data = SampleDataFactory.CreateSampleData();

        Assert.NotNull(data.Trends);
        Assert.Equal(5, data.Trends.Points.Count);
        Assert.True(data.Trends.OverallPassRate > 0);
    }

    [Fact]
    public void CreateSampleData_HasCoverageSummary()
    {
        var data = SampleDataFactory.CreateSampleData();

        Assert.NotNull(data.CoverageSummary);
        Assert.True(data.CoverageSummary.Documentation.Total > 0);
        Assert.True(data.CoverageSummary.AcceptanceCriteria.Total > 0);
        Assert.True(data.CoverageSummary.Automation.Total > 0);
    }

    [Fact]
    public void CreateSampleData_IsDeterministic()
    {
        var data1 = SampleDataFactory.CreateSampleData();
        var data2 = SampleDataFactory.CreateSampleData();

        Assert.Equal(data1.Suites.Count, data2.Suites.Count);
        Assert.Equal(data1.Tests.Count, data2.Tests.Count);
        Assert.Equal(data1.Runs.Count, data2.Runs.Count);
        Assert.Equal(data1.Trends!.Points.Count, data2.Trends!.Points.Count);
    }

    [Fact]
    public void CreateSampleData_PercentagesAreValid()
    {
        var data = SampleDataFactory.CreateSampleData();

        Assert.All(data.Suites, s =>
        {
            Assert.InRange(s.AutomationCoverage, 0, 100);
        });

        var coverage = data.CoverageSummary!;
        Assert.InRange(coverage.Documentation.Percentage, 0, 100);
        Assert.InRange(coverage.AcceptanceCriteria.Percentage, 0, 100);
        Assert.InRange(coverage.Automation.Percentage, 0, 100);
    }

    [Fact]
    public void CreateSampleData_TestsHaveVariedProperties()
    {
        var data = SampleDataFactory.CreateSampleData();

        // Should have a mix of automated and non-automated
        Assert.Contains(data.Tests, t => t.HasAutomation);
        Assert.Contains(data.Tests, t => !t.HasAutomation);

        // Should have multiple priorities
        Assert.Contains(data.Tests, t => t.Priority == "high");
        Assert.Contains(data.Tests, t => t.Priority == "medium");
    }
}
