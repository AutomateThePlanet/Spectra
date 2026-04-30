using System.CommandLine;
using Spectra.CLI.Options;

namespace Spectra.CLI.Commands.Docs;

/// <summary>
/// Lists every suite in the v2 documentation manifest with document count,
/// token estimate, and analysis-skip status (Spec 040 Phase 6).
/// </summary>
public sealed class DocsListSuitesCommand : Command
{
    public DocsListSuitesCommand()
        : base("list-suites", "List documentation suites in the v2 manifest")
    {
        this.SetHandler(async (context) =>
        {
            var outputFormat = context.ParseResult.GetValueForOption(GlobalOptions.OutputFormatOption);
            var handler = new DocsListSuitesHandler(outputFormat);
            context.ExitCode = await handler.ExecuteAsync(context.GetCancellationToken());
        });
    }
}
