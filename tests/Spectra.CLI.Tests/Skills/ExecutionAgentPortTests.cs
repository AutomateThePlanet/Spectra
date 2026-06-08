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
    public void ExecutionAgent_DrivesSpectraRunCli()
    {
        // Spec 065: the agent's default loop drives the `spectra run` CLI (one tool, no MCP config),
        // not raw MCP tool calls. MCP remains an optional networked path mentioned in prose.
        var agent = Agent();
        Assert.Contains("spectra run start", agent);
        Assert.Contains("spectra run show", agent);
        Assert.Contains("spectra run advance", agent);
        Assert.Contains("spectra run finalize", agent);
        // Screenshot capture via the CLI (local host).
        Assert.Contains("spectra run screenshot-clipboard", agent);
        Assert.Contains("spectra run screenshot", agent);
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
        // Spec 065: FAIL/BLOCKED/SKIP ask first and wait; BLOCKED uses `advance --status blocked`, not skip.
        Assert.Contains("ask BEFORE running the command", agent);
        Assert.Contains("BLOCKED uses `advance --status blocked`", agent);
    }

    [Fact]
    public void ExecutionAgent_UsesPlainText_NotDialogTools()
    {
        // Spec 065: the dialog/popup ban is stated as plain-text-only.
        var agent = Agent();
        Assert.Contains("plain text", agent);
        Assert.Contains("dialog/popup", agent);
    }
}
