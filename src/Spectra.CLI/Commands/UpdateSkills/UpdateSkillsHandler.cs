using Spectra.CLI.Infrastructure;
using Spectra.CLI.Output;
using Spectra.CLI.Profile;
using Spectra.CLI.Prompts;
using Spectra.CLI.Skills;
using Spectra.Core.Config;
using Spectra.Core.Models.Config;

namespace Spectra.CLI.Commands.UpdateSkills;

/// <summary>
/// Handles the update-skills command: updates unmodified files, skips user-modified ones.
/// </summary>
public sealed class UpdateSkillsHandler
{
    private readonly VerbosityLevel _verbosity;
    private readonly OutputFormat _outputFormat;
    private readonly ProgressReporter _progress;

    public UpdateSkillsHandler(
        VerbosityLevel verbosity = VerbosityLevel.Normal,
        OutputFormat outputFormat = OutputFormat.Human,
        Spectre.Console.IAnsiConsole? console = null)
    {
        _verbosity = verbosity;
        _outputFormat = outputFormat;
        _progress = new ProgressReporter(console, outputFormat);
    }

    public async Task<int> ExecuteAsync(CancellationToken ct = default)
    {
        var currentDir = Directory.GetCurrentDirectory();
        var manifestStore = new SkillsManifestStore(currentDir);
        var manifest = await manifestStore.LoadAsync(ct);

        if (manifest.Files.Count == 0)
        {
            _progress.Error("No SKILL files found. Run 'spectra init' first.");
            return ExitCodes.Error;
        }

        var updated = new List<string>();
        var unchanged = new List<string>();
        var skipped = new List<string>();
        var skippedNoRecord = new List<(string Path, string Advice)>();
        const string DeleteAdvice = "delete it to get the new version";

        // Spec 056: authoring skills update under .claude/skills/; agents are role-routed
        // (generation → main-session skill, critic → .claude/agents/, execution → unchanged .github/).
        foreach (var (name, content) in SkillContent.All)
        {
            var skillPath = SkillInstallLayout.SkillPath(currentDir, name);
            await ProcessFileAsync(currentDir, skillPath, content, DeleteAdvice, manifest, updated, unchanged, skipped, skippedNoRecord, ct);
        }

        // Process agent files. Spec 097 (FR-022, FR-022a): the critic is rendered with ai.critic.model; the config is
        // read only for that value, so an invalid one never fails the command.
        var criticModel = await CriticModelAsync(currentDir, ct);
        foreach (var (name, content) in AgentContent.Rendered(criticModel))
        {
            var agentPath = SkillInstallLayout.AgentPath(currentDir, name);
            await ProcessFileAsync(currentDir, agentPath, content, DeleteAdvice, manifest, updated, unchanged, skipped, skippedNoRecord, ct);
        }

        // Process prompt templates
        foreach (var templateId in BuiltInTemplates.AllTemplateIds)
        {
            var content = BuiltInTemplates.GetRawContent(templateId);
            if (content is null) continue;

            var relativePath = Path.Combine(".spectra", "prompts", $"{templateId}.md");
            var templatePath = Path.Combine(currentDir, relativePath);
            await ProcessFileAsync(currentDir, templatePath, content, $"run spectra prompts reset {templateId} or {DeleteAdvice}",
                manifest, updated, unchanged, skipped, skippedNoRecord, ct);
        }

        // Process default profile (profiles/_default.yaml)
        var defaultProfilePath = Path.Combine(currentDir, "profiles", "_default.yaml");
        await ProcessFileAsync(
            currentDir,
            defaultProfilePath,
            ProfileFormatLoader.LoadEmbeddedDefaultYaml(),
            DeleteAdvice,
            manifest, updated, unchanged, skipped, skippedNoRecord, ct);

        // Process customization guide (CUSTOMIZATION.md at project root)
        var customizationPath = Path.Combine(currentDir, "CUSTOMIZATION.md");
        await ProcessFileAsync(
            currentDir,
            customizationPath,
            ProfileFormatLoader.LoadEmbeddedCustomizationGuide(),
            DeleteAdvice,
            manifest, updated, unchanged, skipped, skippedNoRecord, ct);

        // Spec 089 (F6) / Spec 090 (FR-D1): existing workspaces get new gitignore patterns here — only ones never offered
        // before, never by creating a .gitignore. Spec 096 (FR-009): the record is the .gitignore's marker lines; the
        // old per-clone record in the manifest is migrated into them once and then no longer kept.
        var offer = await Init.SpectraGitIgnore.OfferNewAsync(currentDir, manifest.GitIgnoreOffered, ct);
        if (!offer.MissingFile)
        {
            manifest.GitIgnoreOffered = null;
        }

        // Save updated manifest
        await manifestStore.SaveAsync(manifest, ct);

        // Report results
        if (updated.Count > 0)
        {
            _progress.Success("Updated:");
            foreach (var f in updated)
                _progress.Info($"  {Path.GetRelativePath(currentDir, f)}");
        }

        if (unchanged.Count > 0 && _verbosity >= VerbosityLevel.Normal)
        {
            _progress.Info("Unchanged:");
            foreach (var f in unchanged)
                _progress.Info($"  {Path.GetRelativePath(currentDir, f)}");
        }

        // Spec 090 (FR-D1): name the patterns added, or the ones to add by hand when there is no .gitignore.
        if (offer.Added.Count > 0)
        {
            _progress.Success($"Updated: .gitignore (added: {string.Join(", ", offer.Added)})");
        }
        else if (offer.MissingFile && offer.Candidates.Count > 0)
        {
            _progress.Warning($"no .gitignore; add: {string.Join(", ", offer.Candidates)}");
        }

        if (skipped.Count > 0)
        {
            _progress.Warning("Skipped (customized by user):");
            foreach (var f in skipped)
                _progress.Warning($"  {Path.GetRelativePath(currentDir, f)}");
        }

        // Spec 096 (FR-002): a file with no record that differs from the bundled content may have been edited — never
        // overwritten.
        foreach (var (path, advice) in skippedNoRecord)
        {
            _progress.Warning($"  {Path.GetRelativePath(currentDir, path)}: skipped (no record — it may have been edited); {advice}");
        }

        // Spec 096 (analyze M1): foreign manifest keys that match no install path, as one summary line.
        if (manifest.DroppedForeignKeys > 0)
        {
            _progress.Warning($"{manifest.DroppedForeignKeys} manifest entries from another location were dropped (was the workspace moved?)");
        }

        return ExitCodes.Success;
    }

    /// <summary>
    /// Spec 097 (FR-022a): the critic's model. No config → the default; a valid config → <c>ai.critic.model</c> or the
    /// default; a config invalid for any reason → one warning, and the model line of the installed critic agent is kept
    /// (the default when the agent isn't installed or has no model line).
    /// </summary>
    private async Task<string> CriticModelAsync(string currentDir, CancellationToken ct)
    {
        var loaded = await ConfigLoader.LoadForReadAsync(currentDir, ConfigSections.None, ct);
        if (loaded.IsSuccess)
        {
            return loaded.Config!.Ai.EffectiveCriticModel;
        }

        var firstError = loaded.Error?.Split('\n')[0].Trim() ?? "";
        if (firstError.StartsWith(ConfigLoader.InvalidConfigPrefix, StringComparison.Ordinal))
        {
            firstError = firstError[ConfigLoader.InvalidConfigPrefix.Length..];
        }

        _progress.Warning($"spectra.config.json is invalid ({loaded.ErrorCode}: {firstError}); the critic agent's model was not changed");
        return InstalledCriticModel(currentDir) ?? CriticConfig.DefaultModel;
    }

    /// <summary>The <c>model:</c> value in the installed critic agent's frontmatter, or null.</summary>
    private static string? InstalledCriticModel(string currentDir)
    {
        var path = SkillInstallLayout.AgentPath(currentDir, "spectra-critic.agent.md");
        if (!File.Exists(path))
        {
            return null;
        }

        var match = System.Text.RegularExpressions.Regex.Match(
            File.ReadAllText(path), @"\A---\r?\n(?:.*\r?\n)*?model:[ \t]*(?<model>\S+)[ \t]*\r?\n");
        return match.Success ? match.Groups["model"].Value : null;
    }

    /// <summary>
    /// Spec 096 (FR-001–FR-003): one installed file. Missing → written and recorded. No record → recorded when it equals
    /// the bundled content, otherwise skipped ("no record — it may have been edited") and never overwritten. A record
    /// that no candidate hash matches → skipped as customized. Matched → updated when the bundled content changed.
    /// </summary>
    private static async Task ProcessFileAsync(
        string root,
        string filePath,
        string bundledContent,
        string advice,
        SkillsManifest manifest,
        List<string> updated,
        List<string> unchanged,
        List<string> skipped,
        List<(string Path, string Advice)> skippedNoRecord,
        CancellationToken ct)
    {
        var key = ManifestKey.For(root, filePath);
        var bundledHash = FileHasher.ComputeHash(bundledContent);

        if (!File.Exists(filePath))
        {
            var dir = Path.GetDirectoryName(filePath)!;
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            await File.WriteAllTextAsync(filePath, bundledContent, ct);
            manifest.Record(key, bundledHash);
            updated.Add(filePath);
            return;
        }

        var currentHash = await FileHasher.ComputeFileHashAsync(filePath, ct);
        if (!manifest.IsRecorded(key))
        {
            if (currentHash == bundledHash)
            {
                manifest.Record(key, currentHash);
                unchanged.Add(filePath);
            }
            else
            {
                skippedNoRecord.Add((filePath, advice));
            }

            return;
        }

        if (!manifest.MatchesRecord(key, currentHash))
        {
            skipped.Add(filePath);
            return;
        }

        if (currentHash == bundledHash)
        {
            manifest.Record(key, currentHash);
            unchanged.Add(filePath);
            return;
        }

        await File.WriteAllTextAsync(filePath, bundledContent, ct);
        manifest.Record(key, bundledHash);
        updated.Add(filePath);
    }
}
