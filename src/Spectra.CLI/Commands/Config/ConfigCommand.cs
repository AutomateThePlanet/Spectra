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

            var handler = new ConfigHandler(verbosity);
            context.ExitCode = await handler.ExecuteAsync(
                key,
                value,
                showRaw,
                context.GetCancellationToken());
        });
    }
}
