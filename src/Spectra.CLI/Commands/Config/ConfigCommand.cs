using System.CommandLine;
using Spectra.CLI.Infrastructure;
using Spectra.CLI.Options;

namespace Spectra.CLI.Commands.Config;

/// <summary>
/// Command to view or modify configuration.
/// </summary>
public sealed class ConfigCommand : Command
{
    public ConfigCommand() : base("config", "View or modify spectra configuration")
    {
        var keyArgument = new Argument<string?>(
            "key",
            () => null,
            "Configuration key (e.g., 'source.dir', 'ai.defaultProvider')");

        var valueArgument = new Argument<string?>(
            "value",
            () => null,
            "New value to set (if omitted, shows current value)");

        var rawOption = new Option<bool>(
            ["--raw", "-r"],
            "Show raw JSON output");

        AddArgument(keyArgument);
        AddArgument(valueArgument);
        AddOption(rawOption);

        this.SetHandler(async (context) =>
        {
            var key = context.ParseResult.GetValueForArgument(keyArgument);
            var value = context.ParseResult.GetValueForArgument(valueArgument);
            var showRaw = context.ParseResult.GetValueForOption(rawOption);
            var verbosity = context.ParseResult.GetValueForOption(GlobalOptions.VerbosityOption);

            var outputFormat = context.ParseResult.GetValueForOption(GlobalOptions.OutputFormatOption);

            var handler = new ConfigHandler(verbosity, outputFormat);
            context.ExitCode = await handler.ExecuteAsync(
                key,
                value,
                showRaw,
                context.GetCancellationToken());
        });

        // Automation directory subcommands
        var addAutoDirCmd = new Command("add-automation-dir", "Add an automation code directory for coverage analysis");
        var addPathArg = new Argument<string>("path", "Directory path to add");
        addAutoDirCmd.AddArgument(addPathArg);
        addAutoDirCmd.SetHandler(async (context) =>
        {
            var path = context.ParseResult.GetValueForArgument(addPathArg);
            var handler = new ConfigHandler();
            context.ExitCode = await handler.AddAutomationDirAsync(path, context.GetCancellationToken());
        });
        AddCommand(addAutoDirCmd);

        var removeAutoDirCmd = new Command("remove-automation-dir", "Remove an automation code directory");
        var removePathArg = new Argument<string>("path", "Directory path to remove");
        removeAutoDirCmd.AddArgument(removePathArg);
        removeAutoDirCmd.SetHandler(async (context) =>
        {
            var path = context.ParseResult.GetValueForArgument(removePathArg);
            var handler = new ConfigHandler();
            context.ExitCode = await handler.RemoveAutomationDirAsync(path, context.GetCancellationToken());
        });
        AddCommand(removeAutoDirCmd);

        var listAutoDirsCmd = new Command("list-automation-dirs", "List configured automation code directories");
        listAutoDirsCmd.SetHandler(async (context) =>
        {
            var handler = new ConfigHandler();
            context.ExitCode = await handler.ListAutomationDirsAsync(context.GetCancellationToken());
        });
        AddCommand(listAutoDirsCmd);
    }
}
