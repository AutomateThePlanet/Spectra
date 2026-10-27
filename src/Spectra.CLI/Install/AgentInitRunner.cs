using Spectra.CLI.Commands.Init;
using Spectra.CLI.Infrastructure;

namespace Spectra.CLI.Install;

/// <summary>
/// Spec 104: <c>spectra init --agent &lt;list&gt;</c>. On a fresh workspace (or with <c>--force</c>) it runs the unchanged
/// workspace setup first — with the Claude files only when <c>claude</c> is in the list — then installs every other agent.
/// On an initialised workspace it installs only the other agents and leaves the config and the Claude files alone (Claude
/// is refreshed by <c>spectra update-skills</c>). With <c>--dry-run</c> it writes nothing and lists the plan.
/// </summary>
public static class AgentInitRunner
{
    private const string Tag = "[spectra init]";

    public static async Task<int> RunAsync(
        InitHandler handler, string root, string agentValue, bool force, bool skipSkills, bool regenerate, bool dryRun,
        TextWriter output, TextWriter notices, CancellationToken ct)
    {
        IReadOnlyList<string> agents;
        try
        {
            agents = AgentSelection.Parse(agentValue, root, notices);
        }
        catch (AgentSelectionException ex)
        {
            notices.WriteLine($"{Tag} error: {ex.Message}");
            return ExitCodes.ValidationError;
        }

        var claudeSelected = agents.Contains("claude");
        var fullInit = force || !File.Exists(Path.Combine(root, "spectra.config.json"));
        var claudePresent = (claudeSelected && fullInit && !skipSkills) || AgentSelection.ClaudeInstalled(root);

        if (dryRun)
        {
            output.WriteLine($"{Tag} dry run — nothing is written. Agents: {string.Join(", ", agents)}.");
            output.WriteLine(fullInit
                ? $"{Tag} workspace: the standard setup (spectra.config.json, docs/, test-cases/, .spectra/prompts/, profiles/, USAGE.md, CUSTOMIZATION.md, templates/, .gitignore entries, the dashboard workflow)."
                : $"{Tag} workspace: already initialised — spectra.config.json is left as it is.");
        }
        else if (fullInit)
        {
            var exit = await handler.HandleAsync(force, skipSkills, ct, installClaude: claudeSelected);
            if (exit != ExitCodes.Success)
            {
                return exit;
            }
        }

        if (claudeSelected)
        {
            output.WriteLine(fullInit
                ? $"{Tag} claude: {(dryRun ? "would install" : "installed")} the standard Claude Code files (.claude/skills, .claude/agents, .claude/settings.json)."
                : $"{Tag} claude: already initialised — refresh it with `spectra update-skills`.");
        }

        if (skipSkills)
        {
            output.WriteLine($"{Tag} --skip-skills: no agent files installed.");
            return ExitCodes.Success;
        }

        if (agents.All(a => a == "claude"))
        {
            return ExitCodes.Success;
        }

        var result = AgentInstaller.Install(root, agents, claudePresent, regenerate, dryRun);
        Report(result, output);
        return ExitCodes.Success;
    }

    /// <summary>Human-readable report: per agent counts (every file in a dry run), host blocks, allowlists, decisions, warnings.</summary>
    public static void Report(AgentInstallResult result, TextWriter output)
    {
        foreach (var agent in result.Agents)
        {
            var target = AgentSelection.Get(agent.Agent);
            var verb = result.DryRun ? "would write" : "written";
            output.WriteLine(
                $"{Tag} {agent.Agent} ({target.DisplayName}): {agent.Emitted.Count} {verb}, {agent.Unchanged.Count} unchanged, "
                + $"{agent.Skipped.Count} skipped (edited by you; --regenerate overwrites), {agent.Removed.Count} {(result.DryRun ? "would be removed" : "removed")} "
                + $"— manifest {agent.Manifest}");
            output.WriteLine($"  instructions: {target.FragmentPath}");
            if (agent.HostFile is not null)
            {
                output.WriteLine($"  {agent.HostFile}: SPECTRA block {agent.HostStatus}");
            }

            if (agent.Allowlist is not null && agent.AllowlistOutcome is not null)
            {
                output.WriteLine(agent.AllowlistOutcome.Status == "skipped"
                    ? $"  allowlist: skipped — {agent.AllowlistOutcome.Reason}"
                    : $"  allowlist: {agent.Allowlist.Description} ({agent.AllowlistOutcome.Status})");
            }

            if (result.DryRun)
            {
                foreach (var path in agent.Emitted) output.WriteLine($"    write    {path}");
                foreach (var path in agent.Unchanged) output.WriteLine($"    same     {path}");
                foreach (var path in agent.Skipped) output.WriteLine($"    skip     {path}");
                foreach (var path in agent.Removed) output.WriteLine($"    remove   {path}");
            }

            foreach (var path in agent.Skipped.Where(_ => !result.DryRun))
            {
                output.WriteLine($"  skipped (edited by you): {path}");
            }
        }

        foreach (var decision in result.Decisions)
        {
            output.WriteLine($"{Tag} decision: {decision}");
        }

        foreach (var warning in result.Warnings)
        {
            output.WriteLine($"{Tag} warning: {warning}");
        }
    }
}
