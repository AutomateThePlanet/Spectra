using System.CommandLine;
using Spectra.CLI.Commands.Generate;
using Spectra.CLI.Commands.Index;
using Spectra.CLI.Commands.Init;
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
        rootCommand.AddCommand(new GenerateCommand());
        // rootCommand.AddCommand(ListCommand.Create());
        // rootCommand.AddCommand(ShowCommand.Create());
        // rootCommand.AddCommand(ConfigCommand.Create());
        // rootCommand.AddCommand(AiCommand.Create());

        return rootCommand;
    }
}
