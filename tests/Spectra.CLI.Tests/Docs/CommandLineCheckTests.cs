namespace Spectra.CLI.Tests.Docs;

/// <summary>Spec 100 (T005): the command-tree checker accepts real lines and names what is missing in others.</summary>
public sealed class CommandLineCheckTests
{
    [Theory]
    [InlineData("spectra ai update-triage --suite X --dry-run")]
    [InlineData("spectra run list-suites|list-active|selections|history [--suite]")]
    [InlineData("spectra docs index [--force]")]
    [InlineData("spectra ai update-ack --suite X (--ids a,b | --group G2) [--dry-run]")]
    [InlineData("spectra ai ingest-update checkout --all --output-format json   # batch")]
    [InlineData("spectra run advance [<handle>] --status pass|fail|blocked|skip [--notes]")]
    [InlineData("spectra --help")]
    public void Structural_AcceptsRealLines(string line) => Assert.Null(CommandLineCheck.Structural(line));

    [Theory]
    [InlineData("spectra ai generate", "generate")]
    [InlineData("spectra ai analyze --extract-criteria", "--extract-criteria")]
    [InlineData("spectra docs index --bogus", "--bogus")]
    [InlineData("spectra auth", "auth")]
    [InlineData("spectra run list-suites|list-nothing", "list-nothing")]
    public void Structural_NamesTheMissingToken(string line, string token)
    {
        var problem = CommandLineCheck.Structural(line);
        Assert.NotNull(problem);
        Assert.Contains($"'{token}'", problem);
    }

    [Theory]
    [InlineData("spectra ai update-triage --suite checkout")]
    [InlineData("spectra ai ingest-update checkout --all")]
    [InlineData("spectra ai relink-sources --suite checkout --dry-run")]
    public void Parse_AcceptsConcreteLines(string line) => Assert.Null(CommandLineCheck.Parse(line));

    [Theory]
    [InlineData("spectra ai generate checkout")]
    [InlineData("spectra ai update-triage --suite checkout --bogus")]
    public void Parse_RejectsUnknownCommandsAndOptions(string line) => Assert.NotNull(CommandLineCheck.Parse(line));

    [Fact]
    public void CommandPaths_IncludeNestedCommands()
    {
        var paths = CommandLineCheck.CommandPaths().Select(p => string.Join(' ', p)).ToList();
        Assert.Contains("ai update-triage", paths);
        Assert.Contains("run list-active", paths);
        Assert.DoesNotContain("ai generate", paths);
    }
}
