using Spectra.CLI.Skills;

namespace Spectra.CLI.Tests.Skills;

/// <summary>
/// Spec 067: the <c>spectra-execute</c> SKILL is bundled and registered, and encodes the
/// **orchestrate-not-drive** model — select → start → launch <c>spectra run console</c> → on-call. The
/// per-test verdict loop moved to the console; the SKILL no longer presents tests or collects verdicts in
/// chat. Verdict discipline is stated as a console guarantee (FR-001/FR-003/FR-004/FR-006).
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
    public void ExecuteSkill_Orchestrates_StartLaunchConsole()
    {
        // FR-001/FR-003: select → start → launch the console → finalize. No `advance` loop in chat.
        var content = SkillContent.Execute;
        Assert.Contains("spectra run start", content);
        Assert.Contains("spectra run console", content);
        Assert.Contains("spectra run finalize", content);
    }

    [Fact]
    public void ExecuteSkill_DoesNotDriveChatLoop()
    {
        // FR-001: the show→wait→record loop and the verdict-mapping table are gone — the console owns them.
        var content = SkillContent.Execute;
        Assert.DoesNotContain("Result? (pass/fail/blocked/skip)", content);
        Assert.DoesNotContain("spectra run advance --status pass", content);
    }

    [Fact]
    public void ExecuteSkill_StatesConsoleGuarantee_AndNoVerdictsInChat()
    {
        // FR-004: discipline relocated to the console, not deleted; the agent never records in chat.
        var content = SkillContent.Execute;
        Assert.Contains("console", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("verdict", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("fabricate", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("never record a verdict in chat", content, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ExecuteSkill_OnCall_ReadsStatusFromDb()
    {
        // FR-006: on-call state comes from `spectra run status` (SQLite), not the console page.
        Assert.Contains("spectra run status", SkillContent.Execute);
    }

    [Fact]
    public void ExecutionAgent_DrivesCli_NotMcpToolsByDefault()
    {
        var content = AgentContent.ExecutionAgent;
        Assert.Contains("spectra run", content);
        // The default flow no longer instructs raw mcp__spectra__* tool calls in the workflow steps.
        Assert.Contains("spectra run list-active", content);
    }

    [Fact]
    public void ExecutionAgent_HasNoSpectraMcpExecutionPath()
    {
        // Spec 070 (FR-008/SC-003): the optional "drive execution over the SPECTRA MCP server" fallback
        // is removed, and no mcp__spectra__* execution tool is referenced — execution is CLI-only.
        var content = AgentContent.ExecutionAgent;
        Assert.DoesNotContain("SPECTRA MCP server", content);
        Assert.DoesNotContain("mcp__spectra__", content);
        // The SEPARATE bug-logging MCP (Azure DevOps) is a different component and is preserved.
        Assert.Contains("Azure DevOps MCP", content);
    }

    [Fact]
    public void ExecuteSkill_HasNoSpectraMcpExecutionPath()
    {
        // Spec 070 (FR-007/SC-003): the skill describes execution only via `spectra run`.
        Assert.DoesNotContain("mcp__spectra__", SkillContent.Execute);
    }
}
