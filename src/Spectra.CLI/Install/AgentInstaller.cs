using System.Reflection;
using Spectra.CLI.Skills;

namespace Spectra.CLI.Install;

/// <summary>Spec 104: what one agent's install did.</summary>
public sealed record AgentInstallOutcome(
    string Agent,
    IReadOnlyList<string> Emitted,
    IReadOnlyList<string> Unchanged,
    IReadOnlyList<string> Skipped,
    IReadOnlyList<string> Removed,
    string? HostFile,
    string? HostStatus,
    AllowlistTarget? Allowlist,
    AllowlistOutcome? AllowlistOutcome,
    string Manifest);

/// <summary>Spec 104: the whole run — per agent, plus the decisions and warnings to report.</summary>
public sealed record AgentInstallResult(
    IReadOnlyList<AgentInstallOutcome> Agents,
    IReadOnlyList<string> Decisions,
    IReadOnlyList<string> Warnings,
    bool DryRun);

/// <summary>
/// Spec 104: installs SPECTRA for the non-Claude agents (the Claude install stays the unchanged <c>InitHandler</c> path).
/// Plans every file once with its owning agents, writes through the R6 hash gate (a file the user edited is skipped unless
/// <c>--regenerate</c>), retires files an agent no longer plans (only unedited ones, only if no other agent relies on
/// them), registers the marker block in <c>AGENTS.md</c> / <c>GEMINI.md</c>, merges the documented allowlists and saves
/// one manifest per agent. Every path it touches is in SPECTRA's namespace (R8).
/// </summary>
public static class AgentInstaller
{
    private const string FragmentResource = "Spectra.CLI.Install.Content.spectra.instructions.md";

    /// <summary>The always-on instructions fragment (LF).</summary>
    public static string FragmentBody()
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(FragmentResource)
            ?? throw new InvalidOperationException($"Embedded resource not found: {FragmentResource}");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd().Replace("\r\n", "\n");
    }

    public static AgentInstallResult Install(
        string root, IReadOnlyList<string> agents, bool claudePresent, bool regenerate, bool dryRun)
    {
        var targets = agents.Where(a => a != "claude").Select(AgentSelection.Get).ToArray();
        var decisions = new List<string>();
        var warnings = new List<string>();

        // ---- plan: path → content + owning agents, in a stable order ----
        var plan = new Dictionary<string, PlannedFile>(StringComparer.Ordinal);
        var order = new List<string>();
        void Add(string path, string content, string owner)
        {
            if (!plan.TryGetValue(path, out var file))
            {
                file = new PlannedFile(content);
                plan[path] = file;
                order.Add(path);
            }
            else if (file.Content != content)
            {
                throw new InvalidOperationException($"Two agents planned different content for '{path}'.");
            }

            if (!file.Owners.Contains(owner))
            {
                file.Owners.Add(owner);
            }
        }

        var fragment = FragmentBody();
        foreach (var target in targets)
        {
            Add(target.FragmentPath, target.RenderFragment(fragment), target.Name);
        }

        var skillDirs = PlanSkillDirs(targets, claudePresent, decisions, warnings);
        foreach (var target in targets)
        {
            var dir = skillDirs[target.Name];
            if (dir == AgentTarget.ClaudeSkillsDir)
            {
                continue; // Claude's own files, written by the Claude install; reused, never rewritten
            }

            foreach (var (name, content) in SkillContent.All.OrderBy(s => s.Key, StringComparer.Ordinal))
            {
                Add($"{dir}/{name}/SKILL.md", NeutralContent.Skill(name, content), target.Name);
            }

            foreach (var (name, content) in SkillReferenceContent.All.OrderBy(s => s.Key, StringComparer.Ordinal))
            {
                Add($"{dir}/{name}/reference.md", NeutralContent.Text(content), target.Name);
            }
        }

        var subagents = AgentContent.All
            .OrderBy(a => a.Key, StringComparer.Ordinal)
            .Select(a => NeutralContent.Agent(a.Key, a.Value))
            .ToArray();
        foreach (var target in targets)
        {
            if (claudePresent && target.ReadsClaudeAgents && !target.NativeAgentsShadowClaude)
            {
                decisions.Add($"{target.Name} subagents: reuse .claude/agents (read natively; no second copy)");
                continue;
            }

            decisions.Add($"{target.Name} subagents: {AgentsDir(target)}");
            foreach (var agent in subagents)
            {
                Add(target.AgentPath(agent.Name), target.RenderAgent(agent), target.Name);
            }
        }

        if (targets.Any(t => t.Name == "devin") && (claudePresent || targets.Any(t => t.Name == "antigravity")))
        {
            warnings.Add("devin also imports subagents from .claude/ and .agents/agents/ — it may list a SPECTRA subagent twice");
        }

        // ---- write through the hash gate (R6) ----
        // Every installed agent's manifest counts: a shared file another agent's run wrote is SPECTRA's, not the user's.
        var manifests = AgentSelection.Names.Where(n => n != "claude")
            .ToDictionary(n => n, n => AgentManifest.Load(root, n), StringComparer.Ordinal);
        var previous = targets.ToDictionary(t => t.Name, t => manifests[t.Name], StringComparer.Ordinal);
        var perAgent = targets.ToDictionary(t => t.Name, _ => new Buckets(), StringComparer.Ordinal);
        var records = targets.ToDictionary(t => t.Name, _ => new List<AgentManifestEntry>(), StringComparer.Ordinal);

        foreach (var relative in order)
        {
            var file = plan[relative];
            var full = FullPath(root, relative);
            var newHash = AgentManifest.Hash(file.Content);
            var recordHash = newHash;
            string bucket;

            if (File.Exists(full))
            {
                var diskHash = AgentManifest.HashOfFile(full);
                var recorded = manifests.Values.Select(m => m?.HashFor(relative)).OfType<string>().ToHashSet(StringComparer.Ordinal);
                var userEdited = recorded.Count > 0 && !recorded.Contains(diskHash);
                var foreign = recorded.Count == 0 && diskHash != newHash;

                if (!regenerate && (userEdited || foreign))
                {
                    bucket = "skipped";
                    recordHash = diskHash; // keep tracking the user's bytes
                }
                else
                {
                    bucket = diskHash == newHash ? "unchanged" : "emitted";
                }
            }
            else
            {
                bucket = "emitted";
            }

            if (bucket == "emitted" && !dryRun)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(full)!);
                File.WriteAllText(full, file.Content);
            }

            foreach (var owner in file.Owners)
            {
                records[owner].Add(new AgentManifestEntry { Path = relative, Sha256 = recordHash });
                perAgent[owner].Add(bucket, relative);
            }
        }

        // ---- retire files an agent owned before but no longer plans ----
        foreach (var target in targets)
        {
            foreach (var entry in previous[target.Name]?.Files ?? [])
            {
                if (plan.TryGetValue(entry.Path, out var planned) && planned.Owners.Contains(target.Name))
                {
                    continue;
                }

                var otherOwners = manifests.Any(m => m.Key != target.Name && !previous.ContainsKey(m.Key) && m.Value?.HashFor(entry.Path) is not null);
                if (plan.ContainsKey(entry.Path) || otherOwners)
                {
                    continue; // still relied on by another agent: just drop our claim
                }

                var full = FullPath(root, entry.Path);
                if (!File.Exists(full))
                {
                    continue;
                }

                if (!regenerate && AgentManifest.HashOfFile(full) != entry.Sha256)
                {
                    perAgent[target.Name].Add("skipped", entry.Path); // user-edited: never deleted
                    continue;
                }

                if (!dryRun)
                {
                    File.Delete(full);
                    DeleteEmptyParents(root, Path.GetDirectoryName(full)!);
                }

                perAgent[target.Name].Add("removed", entry.Path);
            }
        }

        // ---- R1/R3: one marker block in a human-owned instructions file; allowlists (documented ones only) ----
        var hostStatus = new Dictionary<string, string>(StringComparer.Ordinal);
        var allowlistStatus = new Dictionary<string, AllowlistOutcome>(StringComparer.Ordinal);
        var outcomes = new List<AgentInstallOutcome>();
        foreach (var target in targets)
        {
            string? status = null;
            if (target.Host is { } host)
            {
                if (!hostStatus.TryGetValue(host.Path, out status))
                {
                    status = MarkerBlock.Register(FullPath(root, host.Path), host.Line, dryRun);
                    hostStatus[host.Path] = status;
                }
            }

            AllowlistOutcome? allowlist = null;
            if (target.Allowlist is { } list)
            {
                if (!allowlistStatus.TryGetValue(list.Path, out allowlist))
                {
                    allowlist = list.Apply(root, dryRun);
                    allowlistStatus[list.Path] = allowlist;
                }
            }
            else
            {
                decisions.Add($"{target.Name} allowlist: none (no documented project-level command allowlist) — skipped");
            }

            var buckets = perAgent[target.Name];
            outcomes.Add(new AgentInstallOutcome(target.Name, buckets.Emitted, buckets.Unchanged, buckets.Skipped, buckets.Removed,
                target.Host?.Path, status, target.Allowlist, allowlist, AgentManifest.RelativePathFor(target.Name)));
        }

        if (!dryRun)
        {
            foreach (var target in targets)
            {
                new AgentManifest { Agent = target.Name, Files = records[target.Name] }.Save(root);
            }
        }

        return new AgentInstallResult(outcomes, decisions, warnings, dryRun);
    }

    /// <summary>
    /// Chooses the fewest skill directories that reach every agent: <c>.claude/skills</c> first when Claude's install is
    /// present, then greedily the directory read by the most still-uncovered agents (ties → the first agent's native order).
    /// Returns agent → the directory it reads SPECTRA's skills from.
    /// </summary>
    public static IReadOnlyDictionary<string, string> PlanSkillDirs(
        IReadOnlyList<AgentTarget> targets, bool claudePresent, List<string>? decisions = null, List<string>? warnings = null)
    {
        var chosen = new List<string>();
        if (claudePresent)
        {
            chosen.Add(AgentTarget.ClaudeSkillsDir);
        }

        var uncovered = targets.Where(t => !t.SkillDirs.Any(chosen.Contains)).ToList();
        while (uncovered.Count > 0)
        {
            var best = uncovered.SelectMany(t => t.SkillDirs).Distinct(StringComparer.Ordinal)
                .Select((dir, index) => (dir, index, reach: uncovered.Count(t => t.SkillDirs.Contains(dir))))
                .OrderByDescending(c => c.reach)
                .ThenBy(c => c.index)
                .First().dir;
            chosen.Add(best);
            uncovered.RemoveAll(t => t.SkillDirs.Contains(best));
        }

        var assignment = targets.ToDictionary(t => t.Name, t => chosen.First(t.SkillDirs.Contains), StringComparer.Ordinal);
        foreach (var dir in chosen)
        {
            var readers = targets.Where(t => assignment[t.Name] == dir).Select(t => t.Name).ToArray();
            if (readers.Length > 0)
            {
                decisions?.Add(dir == AgentTarget.ClaudeSkillsDir
                    ? $"skills: reuse {dir} (Claude's copy) for {string.Join(", ", readers)}"
                    : $"skills: {dir} for {string.Join(", ", readers)}");
            }
        }

        foreach (var target in targets)
        {
            var seen = chosen.Where(target.SkillDirs.Contains).ToArray();
            if (seen.Length > 1)
            {
                warnings?.Add($"{target.Name} reads {string.Join(" and ", seen)} — it may list each SPECTRA skill twice (no single-copy layout exists for this agent set)");
            }
        }

        return assignment;
    }

    private static string AgentsDir(AgentTarget target)
    {
        var path = target.AgentPath("x");
        return path[..path.IndexOf("/x", StringComparison.Ordinal)];
    }

    private static string FullPath(string root, string relative) =>
        Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar));

    /// <summary>Removes now-empty directories up to (not including) the repo root, e.g. a retired skill folder.</summary>
    private static void DeleteEmptyParents(string root, string directory)
    {
        var stop = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar);
        var current = Path.GetFullPath(directory);
        while (current.Length > stop.Length && Directory.Exists(current) && !Directory.EnumerateFileSystemEntries(current).Any())
        {
            Directory.Delete(current);
            current = Path.GetDirectoryName(current)!;
        }
    }

    private sealed class PlannedFile(string content)
    {
        public string Content { get; } = content;

        public List<string> Owners { get; } = [];
    }

    private sealed class Buckets
    {
        public List<string> Emitted { get; } = [];

        public List<string> Unchanged { get; } = [];

        public List<string> Skipped { get; } = [];

        public List<string> Removed { get; } = [];

        public void Add(string bucket, string path)
        {
            var list = bucket switch
            {
                "emitted" => Emitted,
                "unchanged" => Unchanged,
                "skipped" => Skipped,
                _ => Removed,
            };
            if (!list.Contains(path))
            {
                list.Add(path);
            }
        }
    }
}
