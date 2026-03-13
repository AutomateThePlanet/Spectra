using System.CommandLine;
using Spectra.CLI.Commands.Analyze;
using Spectra.CLI.Commands.Generate;
using Spectra.CLI.Commands.Update;

namespace Spectra.CLI.Commands.Ai;

/// <summary>
/// Parent command that groups all AI-powered subcommands.
/// </summary>
public sealed class AiCommand : Command
{
    public AiCommand() : base("ai", "AI-powered test generation and analysis commands")
    {
        // Add subcommands
        AddCommand(new GenerateCommand());
        AddCommand(new UpdateCommand());
        AddCommand(new AnalyzeCommand());
    }

    /// <summary>
    /// Creates a new AiCommand instance.
    /// </summary>
    public static AiCommand Create() => new();
}
