using Spectra.CLI.Skills;

namespace Spectra.CLI.Tests.Skills;

/// <summary>
/// US5 (spec 065): the <c>spectra-execute</c> SKILL is bundled, registered, and encodes the
/// human-in-the-loop guardrails (present → wait for verdict → advance; never fabricate; explicit
/// verdict only), driving the <c>spectra run</c> CLI rather than MCP tools.
/// </summary>
public class ExecuteSkillTests
{
    [Fact]
    public void ExecuteSkill_IsBundledAndRegistered()
    {
        Assert.True(SkillContent.All.ContainsKey("spectra-execute"));
        Assert.False(string.IsNullOrWhiteSpace(SkillContent.Execute));
        Assert.Contains("name: spectra-execute", SkillContent.Execute);
    }

    [Fact]
    public void ExecuteSkill_DrivesSpectraRunCli()
    {
        var content = SkillContent.Execute;
        Assert.Contains("spectra run start", content);
        Assert.Contains("spectra run advance", content);
        Assert.Contains("spectra run finalize", content);
    }

    [Fact]
    public void ExecuteSkill_EnforcesGuardrails()
    {
        var content = SkillContent.Execute;
        // Wait-for-verdict + no-auto-advance + no-fabrication.
        Assert.Contains("WAIT", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("auto-advance", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("fabricate", content, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ExecutionAgent_DrivesCli_NotMcpToolsByDefault()
    {
        var content = AgentContent.ExecutionAgent;
        Assert.Contains("spectra run", content);
        // The default loop no longer instructs raw mcp__spectra__* tool calls in the workflow steps.
        Assert.Contains("spectra run list-active", content);
    }
}
