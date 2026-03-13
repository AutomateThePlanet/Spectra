using System.CommandLine;
using Spectra.CLI.Infrastructure;
using Spectra.CLI.Options;

namespace Spectra.CLI.Commands.Validate;

/// <summary>
/// Command to validate test files against schema rules.
/// </summary>
public sealed class ValidateCommand : Command
{
    public ValidateCommand() : base("validate", "Validate test files against schema rules")
    {
        var suiteOption = new Option<string?>(
            ["--suite", "-s"],
            "Validate only the specified suite");

        AddOption(suiteOption);

        this.SetHandler(async (context) =>
        {
            var suite = context.ParseResult.GetValueForOption(suiteOption);
            var verbosity = context.ParseResult.GetValueForOption(GlobalOptions.VerbosityOption);

            var handler = new ValidateHandler(verbosity);
            context.ExitCode = await handler.ExecuteAsync(suite, context.GetCancellationToken());
        });
    }
}
