using Spectra.CLI.Skills;

namespace Spectra.CLI.Tests.Skills;

/// <summary>
/// Spec 056 (FR-003 / FR-004) — the ported generation skill mandates the critic subagent as an
/// explicit, non-skippable step and drives the bounded compile→generate→ingest→regenerate loop over
/// the model-free CLI verbs. These are static contract checks on the skill text (the basis for the
/// mandatory-critic guarantee), mirroring the preceding spec's CriticSubagentSkillTests style.
/// </summary>
public sealed class GenerationSkillContractTests
{
    private static string Generate() => SkillContent.Generate;

    [Fact]
    public void GenerationSkill_InvokesCriticSubagent_AsMandatoryStep()
    {
        var skill = Generate();
        Assert.Contains("spectra-critic", skill);
        Assert.Contains("MANDATORY", skill);
        // The critic step is explicit and not auto-invoked.
        Assert.Contains("never auto-invoked", skill);
    }

    [Fact]
    public void GenerationSkill_CriticStep_IsNotSkippable()
    {
        var skill = Generate();
        // The mandated flow states verification is never skipped, and generation is run with
        // --skip-critic specifically because the subagent is the critic of record.
        Assert.Contains("never skipped", skill);
        Assert.Contains("--skip-critic", skill);
        Assert.Contains("critic of record", skill);
    }

    [Fact]
    public void GenerationSkill_DrivesFailLoudBoundedLoop()
    {
        var skill = Generate();
        // Hands the verdict to the deterministic boundary and reacts to its fail-loud exit codes.
        Assert.Contains("spectra ai ingest-verdict", skill);
        Assert.Contains("compile-critic-prompt", skill);
        Assert.Contains("fail loud", skill);
        // Bounded retry on the specific error; stop at the limit.
        Assert.Contains("retry limit", skill);
        Assert.Contains("Regenerate", skill);
    }

    [Fact]
    public void GenerationSkill_GatesOnVerdict_DropOnHallucinated()
    {
        var skill = Generate();
        Assert.Contains("hallucinated", skill);
        Assert.Contains("spectra delete", skill); // a dropped (hallucinated) test is removed
    }

    [Fact]
    public void GenerationSkill_ResolvedToolsInclude_Task()
    {
        // The skill needs the Task tool to invoke the context:fork critic subagent.
        Assert.Contains("Task", Generate());
    }

    [Fact]
    public void GenerationAgent_References_MandatoryCriticStep()
    {
        var agent = AgentContent.GenerationAgent;
        Assert.Contains("spectra-critic", agent);
        Assert.Contains("mandatory", agent, StringComparison.OrdinalIgnoreCase);
    }
}
