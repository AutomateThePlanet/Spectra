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
        // Add subcommands. Spec 059 retired the in-process `ai generate` command — generation
        // now runs entirely on the compile/ingest seam below, driven by the spectra-generate skill.
        AddCommand(new UpdateCommand());
        AddCommand(new AnalyzeCommand());

        // Spec 053: inverted-handoff CLI surface (compile prompt → agent generates → ingest).
        AddCommand(new CompilePromptCommand());
        AddCommand(new IngestTestsCommand());

        // Spec 054: model-free criteria-extraction surface (compile extraction prompt → agent
        // extracts → ingest criteria).
        AddCommand(new CompileExtractionPromptCommand());
        AddCommand(new IngestCriteriaCommand());

        // Spec 055: model-free critic surface (compile critic prompt → context:fork subagent
        // verifies → ingest verdict, fail-loud on damage).
        AddCommand(new CompileCriticPromptCommand());
        AddCommand(new IngestVerdictCommand());

        // Spec 059: model-free analyze-first surface (compile analysis prompt → agent identifies
        // behaviors in-session → ingest analysis recommendation, fail-loud on damage).
        AddCommand(new CompileAnalysisPromptCommand());
        AddCommand(new IngestAnalysisCommand());
    }

    /// <summary>
    /// Creates a new AiCommand instance.
    /// </summary>
    public static AiCommand Create() => new();
}
