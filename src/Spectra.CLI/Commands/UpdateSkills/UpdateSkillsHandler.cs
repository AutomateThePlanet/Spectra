using Spectra.CLI.Infrastructure;
using Spectra.CLI.Output;
using Spectra.CLI.Prompts;
using Spectra.CLI.Skills;

namespace Spectra.CLI.Commands.UpdateSkills;

/// <summary>
/// Handles the update-skills command: updates unmodified files, skips user-modified ones.
/// </summary>
public sealed class UpdateSkillsHandler
{
    private readonly VerbosityLevel _verbosity;
    private readonly OutputFormat _outputFormat;
    private readonly ProgressReporter _progress;

    public UpdateSkillsHandler(VerbosityLevel verbosity = VerbosityLevel.Normal, OutputFormat outputFormat = OutputFormat.Human)
    {
        _verbosity = verbosity;
        _outputFormat = outputFormat;
        _progress = new ProgressReporter(outputFormat: outputFormat);
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

        // Process SKILL files
        foreach (var (name, content) in SkillContent.All)
        {
            var skillPath = Path.Combine(currentDir, ".github", "skills", name, "SKILL.md");
            await ProcessFileAsync(skillPath, content, manifest, updated, unchanged, skipped, ct);
        }

        // Process agent files
        foreach (var (name, content) in AgentContent.All)
        {
            var agentPath = Path.Combine(currentDir, ".github", "agents", name);
            await ProcessFileAsync(agentPath, content, manifest, updated, unchanged, skipped, ct);
        }

        // Process prompt templates
        foreach (var templateId in BuiltInTemplates.AllTemplateIds)
        {
            var content = BuiltInTemplates.GetRawContent(templateId);
            if (content is null) continue;

            var relativePath = Path.Combine(".spectra", "prompts", $"{templateId}.md");
            var templatePath = Path.Combine(currentDir, relativePath);
            await ProcessFileAsync(templatePath, content, manifest, updated, unchanged, skipped, ct);
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

        if (skipped.Count > 0)
        {
            _progress.Warning("Skipped (customized by user):");
            foreach (var f in skipped)
                _progress.Warning($"  {Path.GetRelativePath(currentDir, f)}");
        }

        return ExitCodes.Success;
    }

    private async Task ProcessFileAsync(
        string filePath,
        string bundledContent,
        SkillsManifest manifest,
        List<string> updated,
        List<string> unchanged,
        List<string> skipped,
        CancellationToken ct)
    {
        var bundledHash = FileHasher.ComputeHash(bundledContent);

        if (!File.Exists(filePath))
        {
            // File missing — recreate
            var dir = Path.GetDirectoryName(filePath)!;
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            await File.WriteAllTextAsync(filePath, bundledContent, ct);
            manifest.Files[filePath] = bundledHash;
            updated.Add(filePath);
            return;
        }

        // Check if file was modified by user
        var currentHash = await FileHasher.ComputeFileHashAsync(filePath, ct);
        var expectedHash = manifest.Files.GetValueOrDefault(filePath);

        if (expectedHash is not null && currentHash != expectedHash)
        {
            // User modified the file — skip
            skipped.Add(filePath);
            return;
        }

        // Check if bundled content has changed
        if (currentHash == bundledHash)
        {
            unchanged.Add(filePath);
            return;
        }

        // Unmodified by user, new bundled content — update
        await File.WriteAllTextAsync(filePath, bundledContent, ct);
        manifest.Files[filePath] = bundledHash;
        updated.Add(filePath);
    }
}
