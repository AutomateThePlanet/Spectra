using System.CommandLine;

namespace Spectra.CLI.Commands.Docs;

/// <summary>
/// Parent command for documentation management.
/// </summary>
public sealed class DocsCommand : Command
{
    public DocsCommand() : base("docs", "Documentation management commands")
    {
        AddCommand(new DocsIndexCommand());
    }
}
