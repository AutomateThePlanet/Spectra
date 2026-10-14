using System.CommandLine;
using Spectra.CLI.Commands.Generate;
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

        // Spec 097 (FR-005, FR-006): filters for the listed entries; the summary still covers the whole suite.
        var actionOption = new Option<string[]>("--action",
            "Only entries needing this action: critic, ground, repair, review, none (repeatable or comma-separated)")
        { AllowMultipleArgumentsPerToken = false };
        var idsOption = new Option<string?>("--ids", "Only these tests (comma-separated); ids not in the suite are reported");

        AddOption(suiteOption);
        AddOption(actionOption);
        AddOption(idsOption);

        this.SetHandler(async (context) =>
        {
            var suite = context.ParseResult.GetValueForOption(suiteOption)!;
            var actions = (context.ParseResult.GetValueForOption(actionOption) ?? [])
                .SelectMany(a => a.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
                .Select(a => a.ToLowerInvariant())
                .ToList();
            if (actions.FirstOrDefault(a => !AuditGroundingHandler.Actions.Contains(a)) is { } unknown)
            {
                Console.Error.WriteLine($"--action must be one of {string.Join(", ", AuditGroundingHandler.Actions)} (got '{unknown}').");
                context.ExitCode = 1;
                return;
            }

            var idsValue = context.ParseResult.GetValueForOption(idsOption);
            var ids = IdList.IsGiven(idsValue) ? IdList.Parse(idsValue) : null;
            var outputFormat = context.ParseResult.GetValueForOption(GlobalOptions.OutputFormatOption);
            var isJson = outputFormat == OutputFormat.Json;
            var ct = context.GetCancellationToken();

            var currentDir = Directory.GetCurrentDirectory();
            var testsDir = await AuditGroundingHandler.ResolveTestsDirAsync(currentDir, ct);
            var handler = new AuditGroundingHandler(currentDir, testsDir);

            context.ExitCode = await handler.RunAsync(suite, isJson, ct, actions.Count > 0 ? actions.ToHashSet() : null, ids);
        });
    }
}
