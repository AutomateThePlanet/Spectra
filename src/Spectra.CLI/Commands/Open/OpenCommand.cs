using System.CommandLine;
using Spectra.CLI.Options;

namespace Spectra.CLI.Commands.Open;

/// <summary>
/// Opens a local file or URL in the OS default application (browser, viewer, etc.).
/// Cross-platform: delegates to UseShellExecute so Windows, macOS, and Linux each
/// use their default handler.
/// </summary>
public sealed class OpenCommand : Command
{
    public OpenCommand() : base("open", "Open a file or URL in the default application")
    {
        var pathArgument = new Argument<string>(
            "path",
            "File path or URL to open");

        AddArgument(pathArgument);

        this.SetHandler(async context =>
        {
            var path = context.ParseResult.GetValueForArgument(pathArgument);
            var outputFormat = context.ParseResult.GetValueForOption(GlobalOptions.OutputFormatOption);

            var handler = new OpenHandler(outputFormat);
            context.ExitCode = await handler.ExecuteAsync(path);
        });
    }
}
