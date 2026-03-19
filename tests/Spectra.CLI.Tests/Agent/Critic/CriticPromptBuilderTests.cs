using Spectra.CLI.Agent.Critic;
using Spectra.Core.Models;

namespace Spectra.CLI.Tests.Agent.Critic;

public class CriticPromptBuilderTests
{
    private readonly CriticPromptBuilder _builder = new();

    [Fact]
    public void BuildSystemPrompt_ContainsVerdictInstructions()
    {
        var prompt = _builder.BuildSystemPrompt();

        Assert.Contains("grounded", prompt);
        Assert.Contains("partial", prompt);
        Assert.Contains("hallucinated", prompt);
        Assert.Contains("JSON", prompt);
    }

    [Fact]
    public void BuildSystemPrompt_ContainsOutputFormat()
    {
        var prompt = _builder.BuildSystemPrompt();

        Assert.Contains("verdict", prompt);
        Assert.Contains("score", prompt);
        Assert.Contains("findings", prompt);
    }

    [Fact]
    public void BuildUserPrompt_IncludesTestCaseDetails()
    {
        var test = CreateTestCase();
        var docs = new List<SourceDocument>();

        var prompt = _builder.BuildUserPrompt(test, docs);

        Assert.Contains("TC-001", prompt);
        Assert.Contains("Test login functionality", prompt);
        Assert.Contains("Navigate to login page", prompt);
        Assert.Contains("User is logged in", prompt);
    }

    [Fact]
    public void BuildUserPrompt_IncludesPreconditions()
    {
        var test = CreateTestCase();
        var docs = new List<SourceDocument>();

        var prompt = _builder.BuildUserPrompt(test, docs);

        Assert.Contains("Preconditions", prompt);
        Assert.Contains("User has valid credentials", prompt);
    }

    [Fact]
    public void BuildUserPrompt_IncludesDocumentation()
    {
        var test = CreateTestCase();
        var docs = new List<SourceDocument>
        {
            new()
            {
                Path = "docs/auth/login.md",
                Title = "Login Feature",
                Content = "Users can log in with email and password."
            }
        };

        var prompt = _builder.BuildUserPrompt(test, docs);

        Assert.Contains("docs/auth/login.md", prompt);
        Assert.Contains("Login Feature", prompt);
        Assert.Contains("Users can log in", prompt);
    }

    [Fact]
    public void BuildUserPrompt_HandlesEmptyDocs()
    {
        var test = CreateTestCase();
        var docs = new List<SourceDocument>();

        var prompt = _builder.BuildUserPrompt(test, docs);

        Assert.Contains("No relevant documentation", prompt);
    }

    [Fact]
    public void BuildUserPrompt_TruncatesLargeContent()
    {
        var test = CreateTestCase();
        var largeContent = new string('x', 20000);
        var docs = new List<SourceDocument>
        {
            new()
            {
                Path = "docs/large.md",
                Title = "Large Document",
                Content = largeContent
            }
        };

        var prompt = _builder.BuildUserPrompt(test, docs);

        // Should be truncated and not contain full content
        Assert.Contains("truncated", prompt);
        Assert.True(prompt.Length < largeContent.Length);
    }

    private static TestCase CreateTestCase() => new()
    {
        Id = "TC-001",
        Title = "Test login functionality",
        Priority = Priority.High,
        Preconditions = "User has valid credentials",
        Steps = ["Navigate to login page", "Enter email", "Enter password", "Click login"],
        ExpectedResult = "User is logged in",
        FilePath = "checkout/TC-001.md"
    };
}
