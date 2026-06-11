using Microsoft.Extensions.Logging;
using Spectra.CLI.Agent;
using System.Text.Json;
using Spectra.CLI.Infrastructure;
using Spectra.CLI.Output;
using Spectra.CLI.Profile;
using Spectra.CLI.Source;
using Spectra.Core.Config;
using Spectra.Core.Models.Config;
using Spectre.Console;

namespace Spectra.CLI.Commands.Init;

/// <summary>
/// Handles the init command logic.
/// </summary>
public sealed class InitHandler
{
    private readonly ILogger<InitHandler> _logger;
    private readonly string _workingDirectory;
    private readonly bool _interactive;
    private readonly OutputFormat _outputFormat;

    private const string ConfigFileName = "spectra.config.json";
    private const string DeployWorkflowPath = ".github/workflows/deploy-dashboard.yml";
    private const string DocsDir = "docs";
    private const string TestsDir = "test-cases";
    private const string TemplatesDir = "templates";
    private const string BugReportTemplatePath = "templates/bug-report.md";

    public InitHandler(ILogger<InitHandler> logger, string? workingDirectory = null, bool interactive = false, OutputFormat outputFormat = OutputFormat.Human)
    {
        _logger = logger;
        _workingDirectory = workingDirectory ?? Directory.GetCurrentDirectory();
        _interactive = interactive;
        _outputFormat = outputFormat;
    }

    /// <summary>
    /// Initializes SPECTRA in the current directory.
    /// </summary>
    /// <param name="force">Overwrite existing configuration if true</param>
    /// <returns>Exit code</returns>
    public async Task<int> HandleAsync(bool force, bool skipSkills = false, CancellationToken ct = default)
    {
        try
        {
            _logger.LogInformation("Initializing SPECTRA in {Directory}", _workingDirectory);

            // Check for existing configuration
            var configPath = Path.Combine(_workingDirectory, ConfigFileName);
            if (File.Exists(configPath) && !force)
            {
                _logger.LogError("Configuration already exists at {Path}. Use --force to overwrite.", configPath);
                return ExitCodes.Error;
            }

            // Create directories
            await CreateDirectoriesAsync(ct);

            // Create configuration file
            await CreateConfigFileAsync(configPath, ct);

            if (!skipSkills)
            {
                // Create bundled SKILL files (incl. the execution skill under .claude/skills/)
                await CreateBundledSkillFilesAsync(force, ct);

                // Create offline usage guide (USAGE.md)
                await CreateUsageGuideAsync(force, ct);
            }

            // Create prompt templates
            await CreatePromptTemplatesAsync(force, ct);

            // Create default profile and customization guide
            await CreateDefaultProfileAndCustomizationAsync(force, ct);

            // Create dashboard deployment workflow
            await CreateDeployWorkflowAsync(ct);

            // Update .gitignore
            await UpdateGitIgnoreAsync(ct);

            // Create bug report template
            await CreateBugReportTemplateAsync(ct);

            // Build initial document index if docs exist
            await BuildInitialIndexAsync(configPath, ct);

            _logger.LogInformation("SPECTRA initialized successfully!");
            _logger.LogInformation("Created:");
            _logger.LogInformation("  - {ConfigPath}", ConfigFileName);
            _logger.LogInformation("  - {DocsDir}/", DocsDir);
            _logger.LogInformation("  - {TestsDir}/", TestsDir);
            _logger.LogInformation("  - .claude/skills/spectra-execution/SKILL.md");
            _logger.LogInformation("  - {WorkflowPath}", DeployWorkflowPath);
            _logger.LogInformation("");
            _logger.LogInformation("  - {TemplatePath}", BugReportTemplatePath);
            _logger.LogInformation("");
            _logger.LogInformation("Bug report template created at {Path}", BugReportTemplatePath);
            _logger.LogInformation("  Customize it or delete it — the execution agent adapts automatically.");
            _logger.LogInformation("");
            _logger.LogInformation("Dashboard auto-deployment workflow created.");
            _logger.LogInformation("See docs/deployment/cloudflare-pages-setup.md for setup instructions.");

            // Spec 069: the interactive AI-provider / model-preset / critic setup steps were removed
            // — SPECTRA no longer runs an in-process model, so init asks no AI-provider question.
            // Only the automation-directory step remains interactive.
            if (_interactive)
            {
                Console.WriteLine();
                await InteractiveAutomationDirsAsync(ct);
            }

            NextStepHints.Print("init", true,
                _interactive ? VerbosityLevel.Normal : VerbosityLevel.Quiet, outputFormat: _outputFormat);
            return ExitCodes.Success;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize SPECTRA");
            return ExitCodes.Error;
        }
    }

    private async Task BuildInitialIndexAsync(string configPath, CancellationToken ct)
    {
        try
        {
            var json = await File.ReadAllTextAsync(configPath, ct);
            var config = JsonSerializer.Deserialize<SpectraConfig>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
            if (config is null) return;

            var docsPath = Path.Combine(_workingDirectory, config.Source.LocalDir.TrimEnd('/', '\\'));
            if (!Directory.Exists(docsPath)) return;

            var hasFiles = Directory.EnumerateFiles(docsPath, "*.md", SearchOption.AllDirectories).Any();
            if (!hasFiles) return;

            var indexService = new DocumentIndexService();
            var newLayout = await indexService.EnsureNewLayoutAsync(
                _workingDirectory, config.Source, config.Coverage, forceRebuild: true, suiteFilter: null, ct);
            _logger.LogInformation(
                "  - docs/_index/_manifest.yaml ({Count} documents indexed across {Suites} suite(s))",
                newLayout.Manifest.TotalDocuments,
                newLayout.Manifest.Groups.Count);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to build initial document index");
        }
    }

    private async Task CreateDirectoriesAsync(CancellationToken ct)
    {
        var directories = new[]
        {
            Path.Combine(_workingDirectory, DocsDir),
            Path.Combine(_workingDirectory, TestsDir),
            Path.Combine(_workingDirectory, DocsDir, "criteria"),
            Path.Combine(_workingDirectory, TemplatesDir)
        };

        foreach (var dir in directories)
        {
            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
                _logger.LogDebug("Created directory: {Directory}", dir);
            }
        }

        // Create acceptance criteria template
        await CreateAcceptanceCriteriaTemplateAsync(ct);
    }

    private async Task CreateAcceptanceCriteriaTemplateAsync(CancellationToken ct)
    {
        var criteriaIndexPath = Path.Combine(_workingDirectory, DocsDir, "criteria", "_criteria_index.yaml");
        if (!File.Exists(criteriaIndexPath))
        {
            const string indexTemplate = """
                # Auto-generated by SPECTRA. Run 'spectra ai analyze --extract-criteria' to populate.
                version: 1
                total_criteria: 0
                sources: []
                """;

            await File.WriteAllTextAsync(criteriaIndexPath, indexTemplate, ct);
            _logger.LogDebug("Created acceptance criteria index: {Path}", criteriaIndexPath);
        }

        var samplePath = Path.Combine(_workingDirectory, DocsDir, "criteria", "sample.criteria.yaml");
        if (!File.Exists(samplePath))
        {
            const string sampleTemplate = """
                # Example acceptance criteria file. Delete this file after reviewing.
                #
                # You can:
                #   1. Run 'spectra ai analyze --extract-criteria' to auto-extract from your docs
                #   2. Import from Jira/ADO: 'spectra ai analyze --import-criteria ./export.csv'
                #   3. Write criteria manually in this format
                #
                criteria:
                  - id: AC-SAMPLE-001
                    text: "User MUST be able to log in with email and password"
                    rfc2119: MUST
                    component: auth
                    priority: high
                    tags: [authentication, login]

                  - id: AC-SAMPLE-002
                    text: "System SHOULD display password strength indicator"
                    rfc2119: SHOULD
                    component: auth
                    priority: medium
                    tags: [authentication, ux]
                """;

            await File.WriteAllTextAsync(samplePath, sampleTemplate, ct);
            _logger.LogDebug("Created sample acceptance criteria file: {Path}", samplePath);
        }
    }

    private async Task CreateBugReportTemplateAsync(CancellationToken ct)
    {
        var templatePath = Path.Combine(_workingDirectory, BugReportTemplatePath);
        if (File.Exists(templatePath))
        {
            return;
        }

        var content = GetEmbeddedTemplate("bug-report.md");
        await File.WriteAllTextAsync(templatePath, content, ct);
        _logger.LogDebug("Created bug report template: {Path}", templatePath);
    }

    private async Task CreateConfigFileAsync(string configPath, CancellationToken ct)
    {
        var templateContent = GetEmbeddedTemplate("spectra.config.json");
        await File.WriteAllTextAsync(configPath, templateContent, ct);
        _logger.LogDebug("Created configuration file: {Path}", configPath);
    }

    private async Task CreateBundledSkillFilesAsync(bool force, CancellationToken ct)
    {
        var manifest = new Skills.SkillsManifest();
        var manifestStore = new Skills.SkillsManifestStore(_workingDirectory);

        // Spec 056: the authoring set installs as Claude Code skills under .claude/skills/; the
        // generation agent becomes a main-session skill and the critic a .claude/agents/ subagent.
        // The execution agent keeps its .github/ install (ported by the next spec).
        foreach (var (name, content) in Skills.SkillContent.All)
        {
            var skillPath = Skills.SkillInstallLayout.SkillPath(_workingDirectory, name);
            await InstallAgentFileAsync(skillPath, () => content, force, ct);
            manifest.Files[skillPath] = Infrastructure.FileHasher.ComputeHash(content);
        }

        // Create bundled agent files (role-routed by SkillInstallLayout)
        foreach (var (name, content) in Skills.AgentContent.All)
        {
            var agentPath = Skills.SkillInstallLayout.AgentPath(_workingDirectory, name);
            await InstallAgentFileAsync(agentPath, () => content, force, ct);
            manifest.Files[agentPath] = Infrastructure.FileHasher.ComputeHash(content);
        }

        await manifestStore.SaveAsync(manifest, ct);
        _logger.LogDebug("Created skills manifest with {Count} entries", manifest.Files.Count);
    }

    private async Task CreateUsageGuideAsync(bool force, CancellationToken ct)
    {
        var manifestStore = new Skills.SkillsManifestStore(_workingDirectory);
        var manifest = await manifestStore.LoadAsync(ct);

        var usagePath = Path.Combine(_workingDirectory, "USAGE.md");
        var usageContent = ProfileFormatLoader.LoadEmbeddedUsageGuide();
        const string usageRelative = "USAGE.md";

        if (!File.Exists(usagePath) || force)
        {
            await File.WriteAllTextAsync(usagePath, usageContent, ct);
            _logger.LogInformation("Created usage guide: {Path}", usageRelative);
        }
        else
        {
            _logger.LogDebug("Usage guide already exists, skipping: {Path}", usagePath);
        }
        manifest.Files[usageRelative] = Infrastructure.FileHasher.ComputeHash(usageContent);

        await manifestStore.SaveAsync(manifest, ct);
    }

    private async Task CreatePromptTemplatesAsync(bool force, CancellationToken ct)
    {
        var promptsDir = Path.Combine(_workingDirectory, ".spectra", "prompts");
        Directory.CreateDirectory(promptsDir);

        // Load existing manifest to add template hashes
        var manifestStore = new Skills.SkillsManifestStore(_workingDirectory);
        var manifest = await manifestStore.LoadAsync(ct);

        foreach (var templateId in CLI.Prompts.BuiltInTemplates.AllTemplateIds)
        {
            var content = CLI.Prompts.BuiltInTemplates.GetRawContent(templateId);
            if (content is null) continue;

            var filePath = Path.Combine(promptsDir, $"{templateId}.md");
            if (File.Exists(filePath) && !force)
            {
                _logger.LogDebug("Prompt template already exists, skipping: {Path}", filePath);
                continue;
            }

            await File.WriteAllTextAsync(filePath, content, ct);
            var relativePath = Path.Combine(".spectra", "prompts", $"{templateId}.md");
            manifest.Files[relativePath] = Infrastructure.FileHasher.ComputeHash(content);
            _logger.LogInformation("Created prompt template: {Path}", relativePath);
        }

        await manifestStore.SaveAsync(manifest, ct);
    }

    private async Task CreateDefaultProfileAndCustomizationAsync(bool force, CancellationToken ct)
    {
        var manifestStore = new Skills.SkillsManifestStore(_workingDirectory);
        var manifest = await manifestStore.LoadAsync(ct);

        // profiles/_default.yaml
        var profilesDir = Path.Combine(_workingDirectory, "profiles");
        Directory.CreateDirectory(profilesDir);
        var profilePath = Path.Combine(profilesDir, "_default.yaml");
        var profileContent = ProfileFormatLoader.LoadEmbeddedDefaultYaml();
        var profileRelative = Path.Combine("profiles", "_default.yaml");

        if (!File.Exists(profilePath) || force)
        {
            await File.WriteAllTextAsync(profilePath, profileContent, ct);
            _logger.LogInformation("Created default profile: {Path}", profileRelative);
        }
        else
        {
            _logger.LogDebug("Default profile already exists, skipping: {Path}", profilePath);
        }
        manifest.Files[profileRelative] = Infrastructure.FileHasher.ComputeHash(profileContent);

        // CUSTOMIZATION.md (project root)
        var customizationPath = Path.Combine(_workingDirectory, "CUSTOMIZATION.md");
        var customizationContent = ProfileFormatLoader.LoadEmbeddedCustomizationGuide();
        const string customizationRelative = "CUSTOMIZATION.md";

        if (!File.Exists(customizationPath) || force)
        {
            await File.WriteAllTextAsync(customizationPath, customizationContent, ct);
            _logger.LogInformation("Created customization guide: {Path}", customizationRelative);
        }
        else
        {
            _logger.LogDebug("Customization guide already exists, skipping: {Path}", customizationPath);
        }
        manifest.Files[customizationRelative] = Infrastructure.FileHasher.ComputeHash(customizationContent);

        await manifestStore.SaveAsync(manifest, ct);
    }

    private async Task UpdateGitIgnoreAsync(CancellationToken ct)
    {
        var gitIgnorePath = Path.Combine(_workingDirectory, ".gitignore");
        const string spectraSection = "# SPECTRA";

        // Spec 040 lifecycle: PID/sentinel/lock/HWM are workspace-local
        // derived state; never committed. Per FR-010, FR-020.
        var patterns = new[]
        {
            ".execution/*.db",
            ".execution/*.db-*",
            ".spectra.lock",
            ".spectra/.pid",
            ".spectra/.cancel",
            ".spectra/id-allocator.lock",
            ".spectra/id-allocator.json"
        };
        var newEntries = new List<string>();

        if (File.Exists(gitIgnorePath))
        {
            var content = await File.ReadAllTextAsync(gitIgnorePath, ct);

            // Check which patterns are missing (idempotent)
            foreach (var pattern in patterns)
            {
                if (!content.Contains(pattern))
                {
                    newEntries.Add(pattern);
                }
            }

            if (newEntries.Count > 0)
            {
                var needsHeader = !content.Contains(spectraSection);
                var sb = new System.Text.StringBuilder(content.TrimEnd());
                sb.AppendLine();
                sb.AppendLine();

                if (needsHeader)
                {
                    sb.AppendLine(spectraSection);
                }

                foreach (var entry in newEntries)
                {
                    sb.AppendLine(entry);
                }

                await File.WriteAllTextAsync(gitIgnorePath, sb.ToString(), ct);
                _logger.LogDebug("Updated .gitignore with SPECTRA patterns");
            }
        }
        else
        {
            // Create new .gitignore
            var sb = new System.Text.StringBuilder();
            sb.AppendLine(spectraSection);
            foreach (var pattern in patterns)
            {
                sb.AppendLine(pattern);
            }

            await File.WriteAllTextAsync(gitIgnorePath, sb.ToString(), ct);
            _logger.LogDebug("Created .gitignore with SPECTRA patterns");
        }
    }

    private static string GetEmbeddedTemplate(string templateName)
    {
        var assembly = typeof(InitHandler).Assembly;
        var resourceName = $"Spectra.CLI.Templates.{templateName}";

        using var stream = assembly.GetManifestResourceStream(resourceName);
        if (stream is not null)
        {
            using var reader = new StreamReader(stream);
            return reader.ReadToEnd();
        }

        // Fallback: read from file system during development
        var templatePath = Path.Combine(
            Path.GetDirectoryName(assembly.Location)!,
            "..", "..", "..", "..", "src", "Spectra.CLI", "Templates", templateName);

        if (File.Exists(templatePath))
        {
            return File.ReadAllText(templatePath);
        }

        // Final fallback: use default content
        return templateName switch
        {
            "spectra.config.json" => ConfigLoader.GenerateDefaultConfig(),
            "bug-report.md" => GetDefaultBugReportTemplate(),
            _ => throw new FileNotFoundException($"Template not found: {templateName}")
        };
    }

    private static string GetDefaultBugReportTemplate() => """
        ## {{title}}

        **Test Case:** {{test_id}} - {{test_title}}
        **Suite:** {{suite_name}}
        **Environment:** {{environment}}
        **Severity:** {{severity}}
        **Execution Run:** {{run_id}}

        ### Steps to Reproduce

        {{failed_steps}}

        ### Expected Result

        {{expected_result}}

        ### Actual Result

        [Describe what actually happened]

        ### Screenshots

        {{attachments}}

        ### Additional Context

        [Any relevant details — browser, OS, test data used, error messages]

        ### Traceability

        - **Source Documentation:** {{source_refs}}
        - **Requirements:** {{requirements}}
        - **Component:** {{component}}
        """;

    private async Task CreateDeployWorkflowAsync(CancellationToken ct)
    {
        var workflowPath = Path.Combine(_workingDirectory, DeployWorkflowPath);
        if (File.Exists(workflowPath))
        {
            _logger.LogDebug("Deploy workflow already exists, skipping: {Path}", workflowPath);
            return;
        }

        var directory = Path.GetDirectoryName(workflowPath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var content = GetEmbeddedTemplate("deploy-dashboard.yml");
        await File.WriteAllTextAsync(workflowPath, content, ct);
        _logger.LogDebug("Created deploy workflow: {Path}", workflowPath);
    }

    private async Task InstallAgentFileAsync(string targetPath, Func<string> contentProvider, bool force, CancellationToken ct)
    {
        var directory = Path.GetDirectoryName(targetPath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        if (File.Exists(targetPath) && !force)
        {
            _logger.LogDebug("Agent file exists, skipping: {Path}", targetPath);
            return;
        }

        var content = contentProvider();
        await File.WriteAllTextAsync(targetPath, content, ct);
        _logger.LogDebug("Installed agent file: {Path}", targetPath);
    }

    private async Task InteractiveAutomationDirsAsync(CancellationToken ct)
    {
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[bold]Automation Directory Setup[/]");
        AnsiConsole.MarkupLine("[grey]Tell SPECTRA where your automated test code lives (for coverage analysis).[/]");
        AnsiConsole.MarkupLine("[grey]Examples: ../tests, src/Tests, e2e/[/]");
        AnsiConsole.WriteLine();

        var input = AnsiConsole.Prompt(
            new TextPrompt<string>("Enter paths [grey](comma-separated, or press Enter for defaults)[/]:")
                .AllowEmpty()
                .PromptStyle("green"));

        if (string.IsNullOrWhiteSpace(input))
        {
            AnsiConsole.MarkupLine("[grey]Using defaults (tests, test, spec, specs, e2e)[/]");
            return;
        }

        var dirs = input.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(d => !string.IsNullOrWhiteSpace(d))
            .Distinct()
            .ToList();

        if (dirs.Count == 0)
        {
            AnsiConsole.MarkupLine("[grey]Using defaults (tests, test, spec, specs, e2e)[/]");
            return;
        }

        // Update config file with the automation dirs
        var configPath = Path.Combine(_workingDirectory, ConfigFileName);
        if (File.Exists(configPath))
        {
            var json = await File.ReadAllTextAsync(configPath, ct);
            var root = System.Text.Json.Nodes.JsonNode.Parse(json);
            if (root is not null)
            {
                root["coverage"] ??= new System.Text.Json.Nodes.JsonObject();
                var dirsArray = new System.Text.Json.Nodes.JsonArray();
                foreach (var d in dirs) dirsArray.Add(d);
                root["coverage"]!["automation_dirs"] = dirsArray;

                var options = new JsonSerializerOptions { WriteIndented = true };
                await File.WriteAllTextAsync(configPath, root.ToJsonString(options), ct);
            }
        }

        AnsiConsole.MarkupLine($"[green]Added {dirs.Count} automation director{(dirs.Count == 1 ? "y" : "ies")} to spectra.config.json[/]");
    }
}
