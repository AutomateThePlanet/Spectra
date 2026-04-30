using System.CommandLine;
using Spectra.CLI.Options;

namespace Spectra.CLI.Commands.Suite;

/// <summary>
/// Spec 040: parent command for suite-level operations.
/// Subcommands: <c>list</c>, <c>rename</c>, <c>delete</c>.
/// </summary>
public sealed class SuiteCommand : Command
{
    public SuiteCommand() : base("suite", "Manage test suites (list, rename, delete)")
    {
        AddCommand(BuildListCommand());
        AddCommand(BuildRenameCommand());
        AddCommand(BuildDeleteCommand());
    }

    private static Command BuildListCommand()
    {
        var cmd = new Command("list", "List all suites with their test counts");
        cmd.SetHandler(async context =>
        {
            var verbosity = context.ParseResult.GetValueForOption(GlobalOptions.VerbosityOption);
            var outputFormat = context.ParseResult.GetValueForOption(GlobalOptions.OutputFormatOption);
            var handler = new SuiteListHandler(verbosity, outputFormat);
            context.ExitCode = await handler.ExecuteAsync(context.GetCancellationToken());
        });
        return cmd;
    }

    private static Command BuildRenameCommand()
    {
        var cmd = new Command("rename", "Rename a test suite (updates references in selections and config)");
        var oldArg = new Argument<string>("old", "Existing suite name");
        var newArg = new Argument<string>("new", "New suite name (must match ^[a-z0-9][a-z0-9_-]*$)");
        var dryRun = new Option<bool>("--dry-run", "Preview without modifying any files");
        var force = new Option<bool>("--force", "Skip the interactive confirmation");
        cmd.AddArgument(oldArg);
        cmd.AddArgument(newArg);
        cmd.AddOption(dryRun);
        cmd.AddOption(force);

        cmd.SetHandler(async context =>
        {
            var oldName = context.ParseResult.GetValueForArgument(oldArg);
            var newName = context.ParseResult.GetValueForArgument(newArg);
            var preview = context.ParseResult.GetValueForOption(dryRun);
            var f = context.ParseResult.GetValueForOption(force);
            var noInteraction = context.ParseResult.GetValueForOption(GlobalOptions.NoInteractionOption);
            var verbosity = context.ParseResult.GetValueForOption(GlobalOptions.VerbosityOption);
            var outputFormat = context.ParseResult.GetValueForOption(GlobalOptions.OutputFormatOption);

            var handler = new SuiteRenameHandler(verbosity, outputFormat);
            context.ExitCode = await handler.ExecuteAsync(oldName, newName, preview, f, noInteraction, context.GetCancellationToken());
        });
        return cmd;
    }

    private static Command BuildDeleteCommand()
    {
        var cmd = new Command("delete", "Delete a test suite directory and all its tests");
        var nameArg = new Argument<string>("name", "Suite name to delete");
        var dryRun = new Option<bool>("--dry-run", "Preview without modifying any files");
        var force = new Option<bool>("--force", "Skip the interactive confirmation; override automation and dependency guards");
        cmd.AddArgument(nameArg);
        cmd.AddOption(dryRun);
        cmd.AddOption(force);

        cmd.SetHandler(async context =>
        {
            var name = context.ParseResult.GetValueForArgument(nameArg);
            var preview = context.ParseResult.GetValueForOption(dryRun);
            var f = context.ParseResult.GetValueForOption(force);
            var noInteraction = context.ParseResult.GetValueForOption(GlobalOptions.NoInteractionOption);
            var verbosity = context.ParseResult.GetValueForOption(GlobalOptions.VerbosityOption);
            var outputFormat = context.ParseResult.GetValueForOption(GlobalOptions.OutputFormatOption);

            var handler = new SuiteDeleteHandler(verbosity, outputFormat);
            context.ExitCode = await handler.ExecuteAsync(name, preview, f, noInteraction, context.GetCancellationToken());
        });
        return cmd;
    }
}
