using Spectra.Core.BugReporting;

namespace Spectra.Core.Tests.BugReporting;

public class BugReportContextTests
{
    [Theory]
    [InlineData("high", "critical")]
    [InlineData("medium", "major")]
    [InlineData("low", "minor")]
    [InlineData("HIGH", "critical")]
    [InlineData("Medium", "major")]
    public void MapPriorityToSeverity_MapsCorrectly(string priority, string expected)
    {
        Assert.Equal(expected, BugReportContext.MapPriorityToSeverity(priority));
    }

    [Fact]
    public void MapPriorityToSeverity_NullPriority_UsesDefault()
    {
        Assert.Equal("medium", BugReportContext.MapPriorityToSeverity(null));
    }

    [Fact]
    public void MapPriorityToSeverity_NullPriority_UsesCustomDefault()
    {
        Assert.Equal("critical", BugReportContext.MapPriorityToSeverity(null, "critical"));
    }

    [Fact]
    public void MapPriorityToSeverity_UnknownPriority_UsesDefault()
    {
        Assert.Equal("major", BugReportContext.MapPriorityToSeverity("unknown", "major"));
    }

    [Fact]
    public void MapPriorityToSeverity_InvalidDefault_FallsBackToMedium()
    {
        Assert.Equal("medium", BugReportContext.MapPriorityToSeverity(null, "invalid"));
    }

    [Fact]
    public void GenerateTitle_WithFailedSteps()
    {
        var context = new BugReportContext
        {
            TestId = "TC-101",
            TestTitle = "Login test",
            SuiteName = "auth",
            Severity = "major",
            RunId = "run-1",
            FailedSteps = "3. Click submit button"
        };

        var title = context.GenerateTitle();

        Assert.StartsWith("Bug: Login test", title);
    }

    [Fact]
    public void GenerateTitle_WithoutFailedSteps()
    {
        var context = new BugReportContext
        {
            TestId = "TC-101",
            TestTitle = "Login test",
            SuiteName = "auth",
            Severity = "major",
            RunId = "run-1",
            FailedSteps = ""
        };

        Assert.Equal("Bug: Login test", context.GenerateTitle());
    }

    [Fact]
    public void GenerateTitle_TruncatesLongSteps()
    {
        var longSteps = new string('x', 100);
        var context = new BugReportContext
        {
            TestId = "TC-101",
            TestTitle = "Login test",
            SuiteName = "auth",
            Severity = "major",
            RunId = "run-1",
            FailedSteps = longSteps
        };

        var title = context.GenerateTitle();

        Assert.Contains("...", title);
    }
}
