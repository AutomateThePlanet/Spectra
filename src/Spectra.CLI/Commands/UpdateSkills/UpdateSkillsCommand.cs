using System.CommandLine;
using Spectra.CLI.Infrastructure;
using Spectra.CLI.Options;

namespace Spectra.CLI.Commands.UpdateSkills;

/// <summary>
/// Command to update bundled SKILL files to the latest version.
/// </summary>
public sealed class UpdateSkillsCommand : Command
{
    public UpdateSkillsCommand() : base("update-skills", "Update bundled SKILL and agent files to the latest version")
    {
        this.SetHandler(async (context) =>
        {
            var verbosity = context.ParseResult.GetValueForOption(GlobalOptions.VerbosityOption);
            var outputFormat = context.ParseResult.GetValueForOption(GlobalOptions.OutputFormatOption);

            var handler = new UpdateSkillsHandler(verbosity, outputFormat);
            context.ExitCode = await handler.ExecuteAsync(context.GetCancellationToken());
        });
    }
}
