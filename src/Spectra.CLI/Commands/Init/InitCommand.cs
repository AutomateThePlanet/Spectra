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

        var interactiveOption = new Option<bool?>(
            aliases: ["--interactive", "-i"],
            description: "Enable interactive setup (default: auto-detect terminal)");

        var noInteractiveOption = new Option<bool>(
            name: "--no-interactive",
            getDefaultValue: () => false,
            description: "Disable interactive setup");

        var command = new Command("init", "Initialize SPECTRA in this repository")
        {
            forceOption,
            interactiveOption,
            noInteractiveOption
        };

        command.SetHandler(async (InvocationContext context) =>
        {
            var force = context.ParseResult.GetValueForOption(forceOption);
            var interactive = context.ParseResult.GetValueForOption(interactiveOption);
            var noInteractive = context.ParseResult.GetValueForOption(noInteractiveOption);
            var verbosity = context.ParseResult.GetValueForOption(GlobalOptions.VerbosityOption);

            // Determine interactive mode
            bool isInteractive;
            if (noInteractive)
            {
                isInteractive = false;
            }
            else if (interactive.HasValue)
            {
                isInteractive = interactive.Value;
            }
            else
            {
                // Auto-detect: interactive if stdin/stdout are connected to a terminal
                isInteractive = !Console.IsInputRedirected && !Console.IsOutputRedirected;
            }

            var logger = LoggingSetup.CreateLogger<InitHandler>(verbosity);
            var handler = new InitHandler(logger, interactive: isInteractive);

            context.ExitCode = await handler.HandleAsync(force, context.GetCancellationToken());
        });

        return command;
    }
}
