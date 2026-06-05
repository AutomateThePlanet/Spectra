using Spectra.CLI.Skills;

namespace Spectra.CLI.Tests.Skills;

/// <summary>
/// Spec 057 — the single canonical execution agent is de-Copilot'd (no GPT-4o pin, no Copilot Spaces),
/// resolves doc-lookup via native file reads, and preserves the human-verdict pause. Static contract
/// checks on the bundled agent content (the basis for SC-002/SC-003/SC-006).
/// </summary>
public sealed class ExecutionAgentPortTests
{
    private static string Agent() => AgentContent.ExecutionAgent;

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
        // The Copilot Spaces section is replaced by a read-the-source-docs section.
        Assert.Contains("source_refs", agent);
        Assert.Contains("Read", agent);
        Assert.Contains("Documentation lookup", agent);
        Assert.DoesNotContain("Copilot Spaces", agent);
    }

    [Fact]
    public void ExecutionAgent_StillReferencesMcpTools_Unchanged()
    {
        var agent = Agent();
        Assert.Contains("start_execution_run", agent);
        Assert.Contains("get_test_case_details", agent);
        Assert.Contains("advance_test_case", agent);
        Assert.Contains("finalize_execution_run", agent);
        // Screenshot capture stays on the existing path-based tools (FR-006).
        Assert.Contains("save_clipboard_screenshot", agent);
        Assert.Contains("save_screenshot", agent);
    }

    // ---------- verdict-pause guardrails (FR-004 / SC-006) ----------

    [Fact]
    public void ExecutionAgent_NeverFabricatesNotes()
    {
        var agent = Agent();
        Assert.Contains("NEVER fabricate", agent);
        Assert.Contains("Never invent notes", agent);
    }

    [Fact]
    public void ExecutionAgent_AsksBeforeRecording_NonPassOutcomes()
    {
        var agent = Agent();
        // FAIL/BLOCKED/SKIP ask first and wait; BLOCKED uses advance_test_case, not skip.
        Assert.Contains("ask BEFORE calling the tool", agent);
        Assert.Contains("BLOCKED uses `advance_test_case`", agent);
    }

    [Fact]
    public void ExecutionAgent_UsesPlainText_NotDialogTools()
    {
        // The askQuestion/askForConfirmation ban becomes "use plain text".
        var agent = Agent();
        Assert.Contains("plain text", agent);
        Assert.Contains("askQuestion", agent); // named in the ban line
    }
}
