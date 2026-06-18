using System.CommandLine;
using System.Diagnostics;
using Spectra.CLI.Infrastructure;
using Spectra.CLI.Options;
using Spectra.CLI.Output;
using Spectra.CLI.Progress;
using Spectra.CLI.Results;

namespace Spectra.CLI.Commands.Ai;

/// <summary>
/// Writes the throwaway seam progress poller page to .spectra/seam-progress.html and opens it
/// in the OS default browser (suppress with --no-open for CI/headless runs).
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
        var noOpenOption = new Option<bool>("--no-open",
            description: "Skip auto-opening the page in the browser (use in CI/headless)");
        this.AddOption(noOpenOption);

        this.SetHandler(async context =>
        {
            var outputFormat = context.ParseResult.GetValueForOption(GlobalOptions.OutputFormatOption);
            var noOpen = context.ParseResult.GetValueForOption(noOpenOption);

            var outputPath = Path.Combine(Directory.GetCurrentDirectory(), ".spectra", "seam-progress.html");
            try
            {
                await SeamProgressPageWriter.WriteAsync(outputPath);

                bool opened = false;
                if (!noOpen)
                {
                    try
                    {
                        // Reuses the same cross-platform opener pattern as OpenHandler:
                        // UseShellExecute=true delegates to the OS default browser on all platforms.
                        Process.Start(new ProcessStartInfo(outputPath) { UseShellExecute = true });
                        opened = true;
                    }
                    catch
                    {
                        // Non-fatal: page was written; user can open manually.
                    }
                }

                var result = new OpenResult
                {
                    Command = "init-seam-progress",
                    Status = "completed",
                    Path = Path.GetRelativePath(Directory.GetCurrentDirectory(), outputPath),
                    Opened = opened
                };

                if (outputFormat == OutputFormat.Json)
                    JsonResultWriter.Write(result);
                else
                    Console.WriteLine(opened
                        ? $"Written and opened: {result.Path}"
                        : $"Written: {result.Path}");

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
