using Spectra.Core.BugReporting;

namespace Spectra.Core.Tests.BugReporting;

public class BugReportTemplateEngineTests
{
    private readonly BugReportTemplateEngine _engine = new();

    private static BugReportContext CreateContext(
        string testId = "TC-101",
        string testTitle = "Login with valid credentials",
        string suiteName = "authentication",
        string environment = "staging",
        string severity = "major",
        string runId = "run-abc-123",
        string failedSteps = "1. Open login page\n2. Enter credentials\n3. Click submit",
        string expectedResult = "User is redirected to dashboard",
        IReadOnlyList<string>? attachments = null,
        IReadOnlyList<string>? sourceRefs = null,
        IReadOnlyList<string>? requirements = null,
        string? component = "auth-module")
    {
        return new BugReportContext
        {
            TestId = testId,
            TestTitle = testTitle,
            SuiteName = suiteName,
            Environment = environment,
            Severity = severity,
            RunId = runId,
            FailedSteps = failedSteps,
            ExpectedResult = expectedResult,
            Attachments = attachments ?? [],
            SourceRefs = sourceRefs ?? [],
            Requirements = requirements ?? [],
            Component = component
        };
    }

    [Fact]
    public void PopulateTemplate_ReplacesAllKnownVariables()
    {
        var template = "{{test_id}} - {{test_title}} ({{suite_name}}, {{environment}}, {{severity}}, {{run_id}})";
        var context = CreateContext();

        var result = _engine.PopulateTemplate(template, context);

        Assert.Contains("TC-101", result);
        Assert.Contains("Login with valid credentials", result);
        Assert.Contains("authentication", result);
        Assert.Contains("staging", result);
        Assert.Contains("major", result);
        Assert.Contains("run-abc-123", result);
    }

    [Fact]
    public void PopulateTemplate_ReplacesTitle()
    {
        var template = "# {{title}}";
        var context = CreateContext();

        var result = _engine.PopulateTemplate(template, context);

        Assert.Contains("Bug: Login with valid credentials", result);
    }

    [Fact]
    public void PopulateTemplate_ReplacesFailedStepsAndExpectedResult()
    {
        var template = "Steps: {{failed_steps}} | Expected: {{expected_result}}";
        var context = CreateContext();

        var result = _engine.PopulateTemplate(template, context);

        Assert.Contains("1. Open login page", result);
        Assert.Contains("User is redirected to dashboard", result);
    }

    [Fact]
    public void PopulateTemplate_LeavesUnknownVariablesAsIs()
    {
        var template = "Known: {{test_id}} | Unknown: {{custom_field}}";
        var context = CreateContext();

        var result = _engine.PopulateTemplate(template, context);

        Assert.Contains("TC-101", result);
        Assert.Contains("{{custom_field}}", result);
    }

    [Fact]
    public void PopulateTemplate_HandlesEmptyValues()
    {
        var template = "Env: [{{environment}}] Steps: [{{failed_steps}}]";
        var context = CreateContext(environment: "", failedSteps: "");

        var result = _engine.PopulateTemplate(template, context);

        Assert.Contains("Env: []", result);
        Assert.Contains("Steps: []", result);
    }

    [Fact]
    public void PopulateTemplate_FormatsAttachments()
    {
        var template = "{{attachments}}";
        var context = CreateContext(attachments: ["screenshots/step3.png", "logs/error.txt"]);

        var result = _engine.PopulateTemplate(template, context);

        Assert.Contains("![step3.png]", result);
        Assert.Contains("[error.txt]", result);
    }

    [Fact]
    public void PopulateTemplate_FormatsSourceRefsAndRequirements()
    {
        var template = "Refs: {{source_refs}} | Reqs: {{requirements}}";
        var context = CreateContext(
            sourceRefs: ["docs/auth.md", "docs/security.md"],
            requirements: ["REQ-001", "REQ-002"]);

        var result = _engine.PopulateTemplate(template, context);

        Assert.Contains("docs/auth.md, docs/security.md", result);
        Assert.Contains("REQ-001, REQ-002", result);
    }

    [Fact]
    public void PopulateTemplate_ComponentNullShowsNA()
    {
        var template = "Component: {{component}}";
        var context = CreateContext(component: null);

        var result = _engine.PopulateTemplate(template, context);

        Assert.Contains("N/A", result);
    }

    [Fact]
    public void ComposeReport_GeneratesCompleteReport()
    {
        var context = CreateContext(
            sourceRefs: ["docs/auth.md"],
            requirements: ["REQ-001"]);

        var result = _engine.ComposeReport(context);

        Assert.Contains("## Bug: Login with valid credentials", result);
        Assert.Contains("**Test Case:** TC-101 - Login with valid credentials", result);
        Assert.Contains("**Suite:** authentication", result);
        Assert.Contains("**Severity:** major", result);
        Assert.Contains("### Steps to Reproduce", result);
        Assert.Contains("### Expected Result", result);
        Assert.Contains("### Traceability", result);
        Assert.Contains("docs/auth.md", result);
        Assert.Contains("REQ-001", result);
    }

    [Fact]
    public void ComposeReport_OmitsEmptySections()
    {
        var context = CreateContext(sourceRefs: [], requirements: [], component: null);

        var result = _engine.ComposeReport(context);

        Assert.DoesNotContain("Source Documentation", result);
        Assert.DoesNotContain("Requirements", result);
        Assert.DoesNotContain("Component", result);
    }

    [Fact]
    public void ComposeReport_IncludesScreenshots()
    {
        var context = CreateContext(attachments: ["screenshot.png"]);

        var result = _engine.ComposeReport(context);

        Assert.Contains("### Screenshots", result);
        Assert.Contains("![screenshot.png]", result);
    }
}
