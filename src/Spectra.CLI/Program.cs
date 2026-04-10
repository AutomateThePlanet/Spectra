using System.CommandLine;
using Spectra.CLI.Commands;
using Spectra.CLI.Commands.Ai;
using Spectra.CLI.Commands.Auth;
using Spectra.CLI.Commands.Config;
using Spectra.CLI.Commands.Dashboard;
using Spectra.CLI.Commands.Docs;
using Spectra.CLI.Commands.Index;
using Spectra.CLI.Commands.Init;
using Spectra.CLI.Commands.List;
using Spectra.CLI.Commands.Show;
using Spectra.CLI.Commands.Prompts;
using Spectra.CLI.Commands.UpdateSkills;
using Spectra.CLI.Commands.Validate;
using Spectra.CLI.Options;

namespace Spectra.CLI;

/// <summary>
/// Entry point for the Spectra CLI.
/// </summary>
public class Program
{
    /// <summary>
    /// Main entry point.
    /// </summary>
    public static async Task<int> Main(string[] args)
    {
        var rootCommand = CreateRootCommand();
        return await rootCommand.InvokeAsync(args);
    }

    /// <summary>
    /// Creates the root command with all subcommands.
    /// </summary>
    public static RootCommand CreateRootCommand()
    {
        var rootCommand = new RootCommand("Spectra - AI-native test generation and execution framework");

        // Add global options
        GlobalOptions.AddTo(rootCommand);

        // Commands
        rootCommand.AddCommand(InitCommand.Create());
        rootCommand.AddCommand(new ValidateCommand());
        rootCommand.AddCommand(new IndexCommand());
        rootCommand.AddCommand(new DashboardCommand());
        rootCommand.AddCommand(new DocsCommand());
        rootCommand.AddCommand(AiCommand.Create());
        rootCommand.AddCommand(new ListCommand());
        rootCommand.AddCommand(new ShowCommand());
        rootCommand.AddCommand(new ConfigCommand());
        rootCommand.AddCommand(new InitProfileCommand());
        rootCommand.AddCommand(new ProfileCommand());
        rootCommand.AddCommand(new AuthCommand());
        rootCommand.AddCommand(new UpdateSkillsCommand());
        rootCommand.AddCommand(new PromptsCommand());

        return rootCommand;
    }
}
