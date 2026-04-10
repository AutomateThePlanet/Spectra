using System.CommandLine;
using Spectra.CLI.Infrastructure;
using Spectra.CLI.Options;

namespace Spectra.CLI.Commands.Prompts;

/// <summary>
/// Command to view and manage prompt templates that control AI reasoning.
/// </summary>
public sealed class PromptsCommand : Command
{
    public PromptsCommand() : base("prompts", "View and manage prompt templates for AI operations")
    {
        AddCommand(CreateListCommand());
        AddCommand(CreateShowCommand());
        AddCommand(CreateResetCommand());
        AddCommand(CreateValidateCommand());
    }

    private static Command CreateListCommand()
    {
        var cmd = new Command("list", "List all prompt templates with their customization status");

        cmd.SetHandler(async context =>
        {
            var outputFormat = context.ParseResult.GetValueForOption(GlobalOptions.OutputFormatOption);
            var handler = new PromptsListHandler(outputFormat);
            context.ExitCode = await handler.ExecuteAsync(context.GetCancellationToken());
        });

        return cmd;
    }

    private static Command CreateShowCommand()
    {
        var templateIdArg = new Argument<string>("template-id", "Template to show (e.g., behavior-analysis)");
        var rawOption = new Option<bool>("--raw", "Show template with unresolved placeholders");

        var cmd = new Command("show", "Display a template's content");
        cmd.AddArgument(templateIdArg);
        cmd.AddOption(rawOption);

        cmd.SetHandler(async context =>
        {
            var templateId = context.ParseResult.GetValueForArgument(templateIdArg);
            var raw = context.ParseResult.GetValueForOption(rawOption);
            var handler = new PromptsShowHandler();
            context.ExitCode = await handler.ExecuteAsync(templateId, raw, context.GetCancellationToken());
        });

        return cmd;
    }

    private static Command CreateResetCommand()
    {
        var templateIdArg = new Argument<string?>("template-id", () => null, "Template to reset (or use --all)");
        var allOption = new Option<bool>("--all", "Reset all templates to defaults");

        var cmd = new Command("reset", "Reset a template to its built-in default");
        cmd.AddArgument(templateIdArg);
        cmd.AddOption(allOption);

        cmd.SetHandler(async context =>
        {
            var templateId = context.ParseResult.GetValueForArgument(templateIdArg);
            var all = context.ParseResult.GetValueForOption(allOption);
            var handler = new PromptsResetHandler();
            context.ExitCode = await handler.ExecuteAsync(templateId, all, context.GetCancellationToken());
        });

        return cmd;
    }

    private static Command CreateValidateCommand()
    {
        var templateIdArg = new Argument<string>("template-id", "Template to validate");

        var cmd = new Command("validate", "Validate a template for syntax errors and unknown placeholders");
        cmd.AddArgument(templateIdArg);

        cmd.SetHandler(async context =>
        {
            var templateId = context.ParseResult.GetValueForArgument(templateIdArg);
            var outputFormat = context.ParseResult.GetValueForOption(GlobalOptions.OutputFormatOption);
            var handler = new PromptsValidateHandler(outputFormat);
            context.ExitCode = await handler.ExecuteAsync(templateId, context.GetCancellationToken());
        });

        return cmd;
    }
}
