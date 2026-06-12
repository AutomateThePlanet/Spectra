using System.CommandLine;
using Spectra.CLI.Infrastructure;
using Spectra.CLI.Options;
using Spectra.CLI.Output;
using Spectra.CLI.Progress;
using Spectra.CLI.Results;

namespace Spectra.CLI.Commands.Ai;

/// <summary>
/// Writes the throwaway seam progress poller page to .spectra/seam-progress.html.
/// Called by seam skills (spectra-generate, spectra-criteria, spectra-update) at the start
/// of each multi-step in-session loop. The agent then writes .spectra/progress.json between
/// steps; the page polls it and renders a live phase checklist.
///
/// THROWAWAY: superseded when the execution console (Spec 066/067) generalises to authoring.
/// </summary>
public sealed class InitSeamProgressCommand : Command
{
    public InitSeamProgressCommand() : base("init-seam-progress",
        "Write .spectra/seam-progress.html — the throwaway progress poller for seam skills")
    {
        this.SetHandler(async context =>
        {
            var outputFormat = context.ParseResult.GetValueForOption(GlobalOptions.OutputFormatOption);

            var outputPath = Path.Combine(Directory.GetCurrentDirectory(), ".spectra", "seam-progress.html");
            try
            {
                await SeamProgressPageWriter.WriteAsync(outputPath);

                var result = new OpenResult
                {
                    Command = "init-seam-progress",
                    Status = "completed",
                    Path = Path.GetRelativePath(Directory.GetCurrentDirectory(), outputPath),
                    Opened = false
                };

                if (outputFormat == OutputFormat.Json)
                    JsonResultWriter.Write(result);
                else
                    Console.WriteLine($"Written: {result.Path}");

                context.ExitCode = ExitCodes.Success;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error writing seam progress page: {ex.Message}");
                context.ExitCode = ExitCodes.Error;
            }
        });
    }
}
