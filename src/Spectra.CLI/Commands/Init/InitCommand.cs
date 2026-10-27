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

        var skipSkillsOption = new Option<bool>(
            name: "--skip-skills",
            getDefaultValue: () => false,
            description: "Skip creation of SKILL and agent files");

        // Spec 104: install for other AI agents too. Omitted → Claude Code, exactly as before.
        var agentOption = new Option<string?>(
            name: "--agent",
            description: "AI agents to install SPECTRA for, comma-separated: " + string.Join(", ", Install.AgentSelection.Names)
                + " (aliases: windsurf → devin, agy → antigravity), or auto to detect. Default: claude");

        var regenerateOption = new Option<bool>(
            name: "--regenerate",
            getDefaultValue: () => false,
            description: "With --agent: overwrite agent files you edited since SPECTRA wrote them");

        var command = new Command("init", "Initialize SPECTRA in this repository")
        {
            forceOption,
            interactiveOption,
            noInteractiveOption,
            skipSkillsOption,
            agentOption,
            regenerateOption
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

            var outputFormat = context.ParseResult.GetValueForOption(GlobalOptions.OutputFormatOption);
            var skipSkills = context.ParseResult.GetValueForOption(skipSkillsOption);

            var logger = LoggingSetup.CreateLogger<InitHandler>(verbosity);
            var handler = new InitHandler(logger, interactive: isInteractive, outputFormat: outputFormat);

            var agent = context.ParseResult.GetValueForOption(agentOption);
            if (agent is null)
            {
                context.ExitCode = await handler.HandleAsync(force, skipSkills, context.GetCancellationToken());
                // Human mode only: JSON-mode output (scripts, the 099 golden) stays exactly as before.
                if (context.ExitCode == ExitCodes.Success && outputFormat != OutputFormat.Json)
                {
                    Console.Error.WriteLine(
                        $"[spectra init] Installed for Claude Code. Other agents: spectra init --agent <{Install.AgentSelection.OthersList}> (comma-separated).");
                }

                return;
            }

            var output = outputFormat == OutputFormat.Json ? Console.Error : Console.Out;
            context.ExitCode = await Install.AgentInitRunner.RunAsync(
                handler, Directory.GetCurrentDirectory(), agent, force, skipSkills,
                context.ParseResult.GetValueForOption(regenerateOption),
                context.ParseResult.GetValueForOption(GlobalOptions.DryRunOption),
                output, Console.Error, context.GetCancellationToken());
        });

        return command;
    }
}
