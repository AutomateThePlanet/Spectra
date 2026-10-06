using System.CommandLine;
using Spectra.CLI.Infrastructure;
using Spectra.CLI.Options;

namespace Spectra.CLI.Commands.Docs;

/// <summary>
/// Spec 086 (FR-019): <c>spectra docs topology [--output-format json]</c> — which git repositories (including
/// submodules) own the docs tests reference, and which referenced docs are in none. Diagnostic and read-only.
/// </summary>
public sealed class DocsTopologyCommand : Command
{
    public DocsTopologyCommand()
        : base("topology", "List the git repositories that own referenced docs (read-only diagnostic)")
    {
        this.SetHandler(async context =>
        {
            var json = context.ParseResult.GetValueForOption(GlobalOptions.OutputFormatOption) == OutputFormat.Json;
            context.ExitCode = await DocsTopologyHandler.RunAsync(Directory.GetCurrentDirectory(), json, context.GetCancellationToken());
        });
    }
}
