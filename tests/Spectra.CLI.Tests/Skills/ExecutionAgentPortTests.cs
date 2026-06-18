using Spectra.CLI.Skills;

namespace Spectra.CLI.Tests.Skills;

/// <summary>
/// Spec 067 — the execution agent is rewritten from loop-driver to **orchestrator + on-call**: it
/// selects tests, starts the run, launches <c>spectra run console</c>, hands over the URL, and answers
/// on-call questions by reading <c>spectra run status</c> (SQLite) + source docs. It no longer presents
/// tests or collects verdicts in chat. Spec 057's de-Copilot'd doc-lookup contract is preserved; the
/// verdict discipline is restated as a console guarantee (FR-002/FR-003/FR-004/FR-006). Static contract
/// checks on the bundled agent content.
/// </summary>
public sealed class ExecutionAgentPortTests
{
    // skill-pair-merge: execution agent content merged into spectra-execute skill.
    private static string Agent() => SkillContent.Execute;

    // ---------- preserved from spec 057 (no Copilot-isms, native doc lookup) ----------

    [Theory]
    [InlineData("model: GPT-4o")]
    [InlineData("disable-model-invocation")]
    [InlineData("get_copilot_space")]
    [InlineData("list_copilot_spaces")]
    [InlineData("copilot_space")]
    [InlineData("github/")]
    [InlineData("runInTerminal")]
    [InlineData("awaitTerminal")]
    [InlineData("show preview")]
    public void ExecutionAgent_HasNoCopilotIsm(string forbidden)
    {
        Assert.DoesNotContain(forbidden, Agent(), StringComparison.Ordinal);
    }

    [Fact]
    public void ExecutionAgent_DocLookup_IsNativeFileRead()
    {
        var agent = Agent();
        Assert.Contains("source_refs", agent);
        Assert.Contains("Read", agent);
        Assert.Contains("Documentation lookup", agent);
        Assert.DoesNotContain("Copilot Spaces", agent);
    }

    // ---------- orchestrate-not-drive (FR-002 / FR-003 / SC-001) ----------

    [Fact]
    public void ExecutionAgent_Orchestrates_StartLaunchConsole()
    {
        var agent = Agent();
        Assert.Contains("spectra run start", agent);
        Assert.Contains("spectra run console", agent);
        Assert.Contains("spectra run finalize", agent);
    }

    [Fact]
    public void ExecutionAgent_LaunchesConsole_HandsOverUrl()
    {
        // FR-003: after start, launch the console and hand the tester the local URL.
        var agent = Agent();
        Assert.Contains("spectra run console", agent);
        Assert.Contains("http://127.0.0.1", agent);
    }

    [Fact]
    public void ExecutionAgent_DoesNotDriveChatLoop()
    {
        // FR-001/FR-002: the per-test presentation + result-collection loop is gone (console owns it).
        var agent = Agent();
        Assert.DoesNotContain("Result Collection", agent);
        Assert.DoesNotContain("ask BEFORE running the command", agent);
        Assert.DoesNotContain("Result? (pass/fail/blocked/skip)", agent);
    }

    // ---------- on-call state from the database (FR-006 / SC-004) ----------

    [Fact]
    public void ExecutionAgent_OnCall_ReadsStatusFromDb()
    {
        var agent = Agent();
        Assert.Contains("spectra run status", agent);
        Assert.Contains("never from the console page", agent);
    }

    // ---------- guardrail discipline as a console guarantee (FR-004 / SC-003) ----------

    [Fact]
    public void ExecutionAgent_StatesConsoleGuarantee()
    {
        var agent = Agent();
        Assert.Contains("console", agent, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("verdict", agent, StringComparison.OrdinalIgnoreCase);
        // The agent's only residual verdict rule: never record one in chat.
        Assert.Contains("never record a verdict in chat", agent, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ExecutionAgent_NeverFabricatesNotes()
    {
        Assert.Contains("NEVER fabricate", Agent());
    }

    [Fact]
    public void ExecutionAgent_UsesPlainText_NotDialogTools()
    {
        var agent = Agent();
        Assert.Contains("plain text", agent, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("dialog/popup", agent);
    }

    // ---------- lifecycle retained (FR-002 / US4) ----------

    [Fact]
    public void ExecutionAgent_RetainsLifecycleControls()
    {
        var agent = Agent();
        Assert.Contains("spectra run finalize", agent);
        Assert.Contains("resume", agent, StringComparison.OrdinalIgnoreCase);
    }
}
