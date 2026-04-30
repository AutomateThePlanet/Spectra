using System.CommandLine;

namespace Spectra.CLI.Commands.Docs;

/// <summary>
/// Prints the contents of one suite's index file (Spec 040 Phase 6).
/// </summary>
public sealed class DocsShowSuiteCommand : Command
{
    public DocsShowSuiteCommand()
        : base("show-suite", "Print a suite's index file from the v2 manifest")
    {
        var suiteIdArg = new Argument<string>(
            "suite-id",
            "The suite identifier as listed by 'spectra docs list-suites'");
        AddArgument(suiteIdArg);

        this.SetHandler(async (context) =>
        {
            var suiteId = context.ParseResult.GetValueForArgument(suiteIdArg);
            var handler = new DocsShowSuiteHandler();
            context.ExitCode = await handler.ExecuteAsync(suiteId, context.GetCancellationToken());
        });
    }
}
