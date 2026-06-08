using Spectra.CLI.Prompts;

namespace Spectra.CLI.Tests.Prompts;

/// <summary>
/// Spec 063: the test-update template drives an EDIT of one already-OUTDATED test (the deterministic
/// TestClassifier is the selector — classification no longer lives in the prompt). It must instruct
/// edit-not-regenerate, preserve the id / structure / protected fields, reconcile against the changed
/// source/criteria, and output a JSON array with the single edited test.
///
/// (Supersedes the Spec 037 "Technique Completeness Check / classify as OUTDATED" assertions — that
/// classification responsibility moved out of the template when the update seam was inverted.)
/// </summary>
public class TestUpdateTemplateTests
{
    private static string Body() =>
        BuiltInTemplates.GetRawContent("test-update")
        ?? throw new Xunit.Sdk.XunitException("test-update template not found");

    [Fact]
    public void Template_InstructsEditNotRegenerate()
    {
        var body = Body();
        Assert.Contains("do not regenerate", body, System.StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Template_PreservesId()
    {
        var body = Body();
        Assert.Contains("id", body, System.StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Keep the original", body, System.StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Template_KeepsProtectedFieldsUnchanged()
    {
        var body = Body();
        Assert.Contains("priority", body, System.StringComparison.OrdinalIgnoreCase);
        Assert.Contains("component", body, System.StringComparison.OrdinalIgnoreCase);
        Assert.Contains("tags", body, System.StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Template_ReconcilesAgainstChangedSourceAndCriteria()
    {
        var body = Body();
        Assert.Contains("{{test_case}}", body);
        Assert.Contains("{{current_source}}", body);
        Assert.Contains("{{acceptance_criteria}}", body);
    }

    [Fact]
    public void Template_OutputsJsonArrayOfTheEditedTest()
    {
        var body = Body();
        Assert.Contains("JSON array", body, System.StringComparison.OrdinalIgnoreCase);
        Assert.Contains("{{profile_format}}", body);
    }
}
