using System.CommandLine;
using Spectra.CLI.Options;

namespace Spectra.CLI.Commands.Testimize;

/// <summary>
/// Spec 038: parent command for the optional Testimize integration.
/// Currently exposes one subcommand: `check`.
/// </summary>
public sealed class TestimizeCommand : Command
{
    public TestimizeCommand() : base("testimize", "Optional Testimize integration commands")
    {
        AddCommand(new TestimizeCheckCommand());
    }
}

/// <summary>
/// `spectra testimize check` — health check / status report.
/// </summary>
public sealed class TestimizeCheckCommand : Command
{
    public TestimizeCheckCommand() : base("check", "Report Testimize integration status (enabled, installed, healthy, mode, strategy)")
    {
        this.SetHandler(async (context) =>
        {
            var outputFormat = context.ParseResult.GetValueForOption(GlobalOptions.OutputFormatOption);
            var handler = new TestimizeCheckHandler(outputFormat);
            context.ExitCode = await handler.ExecuteAsync(context.GetCancellationToken());
        });
    }
}
