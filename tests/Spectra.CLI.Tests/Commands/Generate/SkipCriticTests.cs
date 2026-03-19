using System.CommandLine;
using System.CommandLine.Parsing;
using Spectra.CLI.Commands.Generate;

namespace Spectra.CLI.Tests.Commands.Generate;

public class SkipCriticTests
{
    [Fact]
    public void GenerateCommand_HasSkipCriticOption()
    {
        var command = new GenerateCommand();

        var option = command.Options.FirstOrDefault(o =>
            o.Aliases.Contains("--skip-critic"));

        Assert.NotNull(option);
    }

    [Fact]
    public void SkipCriticOption_DefaultsToFalse()
    {
        var command = new GenerateCommand();

        var option = command.Options.First(o =>
            o.Aliases.Contains("--skip-critic")) as Option<bool>;

        Assert.NotNull(option);

        // Get default value by parsing empty command line
        var parser = new Parser(command);
        var result = parser.Parse("");

        var value = result.GetValueForOption(option);

        Assert.False(value);
    }

    [Fact]
    public void SkipCriticOption_ParsesTrue()
    {
        var command = new GenerateCommand();

        var option = command.Options.First(o =>
            o.Aliases.Contains("--skip-critic")) as Option<bool>;

        var parser = new Parser(command);
        var result = parser.Parse("checkout --skip-critic");

        var value = result.GetValueForOption(option!);

        Assert.True(value);
    }

    [Fact]
    public void SkipCriticOption_WorksWithOtherOptions()
    {
        var command = new GenerateCommand();

        var skipOption = command.Options.First(o =>
            o.Aliases.Contains("--skip-critic")) as Option<bool>;

        var countOption = command.Options.First(o =>
            o.Aliases.Contains("--count") || o.Aliases.Contains("-n")) as Option<int?>;

        var parser = new Parser(command);
        var result = parser.Parse("checkout --skip-critic --count 10");

        Assert.True(result.GetValueForOption(skipOption!));
        Assert.Equal(10, result.GetValueForOption(countOption!));
    }

    [Fact]
    public void GenerateCommand_HasCorrectDescription()
    {
        var command = new GenerateCommand();

        Assert.Equal("Generate test cases from documentation using AI", command.Description);
    }

    [Fact]
    public void GenerateCommand_SuiteArgumentIsOptional()
    {
        var command = new GenerateCommand();

        var argument = command.Arguments.First();

        Assert.Equal(ArgumentArity.ZeroOrOne, argument.Arity);
    }

    [Fact]
    public void SkipCriticOption_HasDescription()
    {
        var command = new GenerateCommand();

        var option = command.Options.First(o =>
            o.Aliases.Contains("--skip-critic"));

        Assert.NotNull(option.Description);
        Assert.Contains("verification", option.Description?.ToLowerInvariant());
    }

    [Theory]
    [InlineData("checkout --skip-critic", true)]
    [InlineData("checkout", false)]
    [InlineData("--skip-critic checkout", true)]
    [InlineData("checkout -n 5 --skip-critic", true)]
    [InlineData("checkout --focus \"negative\" --skip-critic", true)]
    public void SkipCriticOption_ParsesCorrectly(string args, bool expectedSkipCritic)
    {
        var command = new GenerateCommand();

        var option = command.Options.First(o =>
            o.Aliases.Contains("--skip-critic")) as Option<bool>;

        var parser = new Parser(command);
        var result = parser.Parse(args);

        var value = result.GetValueForOption(option!);

        Assert.Equal(expectedSkipCritic, value);
    }
}
