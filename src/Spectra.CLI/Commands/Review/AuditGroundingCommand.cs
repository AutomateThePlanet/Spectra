using System.CommandLine;
using Spectra.CLI.Infrastructure;
using Spectra.CLI.Options;

namespace Spectra.CLI.Commands.Review;

/// <summary>
/// Spec 072 FR2: Audit grounding state for a suite — resume oracle + inspection surface.
/// </summary>
public sealed class AuditGroundingCommand : Command
{
    public AuditGroundingCommand() : base("audit-grounding", "Report per-test grounding state for a suite")
    {
        var suiteOption = new Option<string>(
            ["--suite", "-s"],
            "Suite name to audit")
        { IsRequired = true };

        AddOption(suiteOption);

        this.SetHandler(async (context) =>
        {
            var suite = context.ParseResult.GetValueForOption(suiteOption)!;
            var outputFormat = context.ParseResult.GetValueForOption(GlobalOptions.OutputFormatOption);
            var isJson = outputFormat == OutputFormat.Json;
            var ct = context.GetCancellationToken();

            var currentDir = Directory.GetCurrentDirectory();
            var testsDir = await AuditGroundingHandler.ResolveTestsDirAsync(currentDir, ct);
            var handler = new AuditGroundingHandler(currentDir, testsDir);

            context.ExitCode = await handler.RunAsync(suite, isJson, ct);
        });
    }
}
