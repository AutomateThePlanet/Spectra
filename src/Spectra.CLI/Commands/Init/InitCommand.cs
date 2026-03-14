using System.CommandLine;
using System.CommandLine.Invocation;
using Microsoft.Extensions.Logging;
using Spectra.CLI.Infrastructure;
using Spectra.CLI.Options;

namespace Spectra.CLI.Commands.Init;

/// <summary>
/// The init command for initializing SPECTRA in a repository.
/// </summary>
public static class InitCommand
{
    /// <summary>
    /// Creates the init command.
    /// </summary>
    public static Command Create()
    {
        var forceOption = new Option<bool>(
            name: "--force",
            getDefaultValue: () => false,
            description: "Overwrite existing configuration");

        var command = new Command("init", "Initialize SPECTRA in this repository")
        {
            forceOption
        };

        command.SetHandler(async (InvocationContext context) =>
        {
            var force = context.ParseResult.GetValueForOption(forceOption);
            var verbosity = context.ParseResult.GetValueForOption(GlobalOptions.VerbosityOption);

            var logger = LoggingSetup.CreateLogger<InitHandler>(verbosity);
            var handler = new InitHandler(logger);

            context.ExitCode = await handler.HandleAsync(force, context.GetCancellationToken());
        });

        return command;
    }
}
