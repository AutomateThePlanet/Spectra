using Microsoft.Extensions.Logging;
using Spectra.CLI.Agent;
using System.Text.Json;
using Spectra.CLI.Infrastructure;
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

    private const string ConfigFileName = "spectra.config.json";
    private const string SkillPath = ".github/skills/test-generation/SKILL.md";
    private const string ExecutionAgentPath = ".github/agents/spectra-execution.agent.md";
    private const string ExecutionSkillPath = ".github/skills/spectra-execution/SKILL.md";
    private const string VsCodeMcpPath = ".vscode/mcp.json";
    private const string DocsDir = "docs";
    private const string TestsDir = "tests";

    public InitHandler(ILogger<InitHandler> logger, string? workingDirectory = null, bool interactive = false)
    {
        _logger = logger;
        _workingDirectory = workingDirectory ?? Directory.GetCurrentDirectory();
        _interactive = interactive;
    }

    /// <summary>
    /// Initializes SPECTRA in the current directory.
    /// </summary>
    /// <param name="force">Overwrite existing configuration if true</param>
    /// <returns>Exit code</returns>
    public async Task<int> HandleAsync(bool force, CancellationToken ct = default)
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

            // Create skill file
            await CreateSkillFileAsync(ct);

            // Install execution agent files
            await InstallAgentFilesAsync(force, ct);

            // Create VS Code MCP configuration
            await CreateVsCodeMcpConfigAsync(ct);

            // Update .gitignore
            await UpdateGitIgnoreAsync(ct);

            // Build initial document index if docs exist
            await BuildInitialIndexAsync(configPath, ct);

            _logger.LogInformation("SPECTRA initialized successfully!");
            _logger.LogInformation("Created:");
            _logger.LogInformation("  - {ConfigPath}", ConfigFileName);
            _logger.LogInformation("  - {DocsDir}/", DocsDir);
            _logger.LogInformation("  - {TestsDir}/", TestsDir);
            _logger.LogInformation("  - {SkillPath}", SkillPath);
            _logger.LogInformation("  - {AgentPath}", ExecutionAgentPath);
            _logger.LogInformation("  - {SkillPath}", ExecutionSkillPath);
            _logger.LogInformation("  - {McpPath}", VsCodeMcpPath);

            // Interactive auth setup
            if (_interactive)
            {
                Console.WriteLine();
                await InteractiveAuthSetupAsync(ct);
            }

            return ExitCodes.Success;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize SPECTRA");
            return ExitCodes.Error;
        }
    }

    private async Task InteractiveAuthSetupAsync(CancellationToken ct)
    {
        AnsiConsole.MarkupLine("[bold]AI Provider Setup[/]");
        AnsiConsole.WriteLine();

        // Provider selection with arrow keys
        var providerChoices = new Dictionary<string, (string Provider, string? ApiKeyEnv, bool NeedsEndpoint)>
        {
            ["GitHub Models (GITHUB_TOKEN or gh CLI)"] = ("github-models", null, false),
            ["OpenAI (OPENAI_API_KEY)"] = ("openai", "OPENAI_API_KEY", false),
            ["Azure OpenAI (AZURE_OPENAI_API_KEY)"] = ("azure-openai", "AZURE_OPENAI_API_KEY", true),
            ["Anthropic (ANTHROPIC_API_KEY)"] = ("anthropic", "ANTHROPIC_API_KEY", false),
            ["Azure Anthropic (AZURE_ANTHROPIC_API_KEY)"] = ("azure-anthropic", "AZURE_ANTHROPIC_API_KEY", true),
            ["Skip for now"] = (null!, null, false)
        };

        var providerChoice = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("Which [cyan]AI provider[/] would you like to use?")
                .PageSize(10)
                .HighlightStyle(Style.Parse("cyan"))
                .AddChoices(providerChoices.Keys));

        var (provider, apiKeyEnv, needsEndpoint) = providerChoices[providerChoice];

        if (provider is null)
        {
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("[grey]Skipped. Run 'spectra auth' to check authentication status later.[/]");
            return;
        }

        AnsiConsole.WriteLine();

        // Azure providers need endpoint configuration
        string? baseUrl = null;
        if (needsEndpoint)
        {
            baseUrl = await PromptForAzureEndpointAsync(provider, ct);
            if (baseUrl is null)
            {
                AnsiConsole.MarkupLine("[grey]Skipped. You can configure the endpoint later in spectra.config.json.[/]");
                return;
            }
            AnsiConsole.WriteLine();
        }

        // Model selection - for Azure providers, prompt for deployment name
        string selectedModel;
        if (needsEndpoint)
        {
            // Azure providers need deployment name input
            AnsiConsole.MarkupLine("Enter your [cyan]deployment name[/] (the name you gave your model deployment in Azure):");
            selectedModel = AnsiConsole.Prompt(
                new TextPrompt<string>("Deployment name:")
                    .PromptStyle("green")
                    .Validate(name =>
                    {
                        if (string.IsNullOrWhiteSpace(name))
                            return ValidationResult.Error("[red]Deployment name is required[/]");
                        return ValidationResult.Success();
                    }));
        }
        else
        {
            // Standard providers - select from list
            var models = GetModelsForProvider(provider);
            var modelChoices = models.ToDictionary(m => $"{m.Name} [grey]({m.Id})[/]", m => m.Id);

            var modelChoice = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title($"Select a [cyan]model[/] for {provider}:")
                    .PageSize(20)
                    .HighlightStyle(Style.Parse("cyan"))
                    .AddChoices(modelChoices.Keys));

            selectedModel = modelChoices[modelChoice];
        }

        // Update config file with selected provider and model
        var configPath = Path.Combine(_workingDirectory, ConfigFileName);
        var configContent = ConfigLoader.GenerateConfig(provider, selectedModel, apiKeyEnv, baseUrl);
        await File.WriteAllTextAsync(configPath, configContent, ct);

        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine($"[green]Updated[/] {ConfigFileName} with [cyan]{provider}[/] / [cyan]{selectedModel}[/]");
        AnsiConsole.WriteLine();

        AnsiConsole.MarkupLine("Checking Copilot SDK authentication...");

        var authResult = await AgentFactory.GetAuthStatusAsync(ct);

        if (authResult.IsAuthenticated)
        {
            AnsiConsole.MarkupLine($"  [green][[OK]][/] Authenticated via {authResult.Source}");
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("[green]Configuration complete![/]");
        }
        else
        {
            AnsiConsole.MarkupLine($"  [yellow][[NOT CONFIGURED]][/]");
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("To authenticate:");
            foreach (var instruction in authResult.SetupInstructions)
            {
                if (!string.IsNullOrEmpty(instruction))
                {
                    AnsiConsole.MarkupLine($"  [grey]{Markup.Escape(instruction)}[/]");
                }
            }
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("Run [cyan]spectra auth[/] to verify authentication after setup.");
        }
    }

    private static async Task<string?> PromptForAzureEndpointAsync(string provider, CancellationToken ct)
    {
        var displayName = provider == "azure-openai" ? "Azure OpenAI" : "Azure Anthropic";
        var exampleUrl = provider == "azure-openai"
            ? "https://your-resource.openai.azure.com/"
            : "https://your-resource.inference.ai.azure.com/";

        AnsiConsole.MarkupLine($"[cyan]{displayName}[/] requires your Azure endpoint URL.");
        AnsiConsole.MarkupLine($"[grey]Example: {exampleUrl}[/]");
        AnsiConsole.WriteLine();

        var endpoint = AnsiConsole.Prompt(
            new TextPrompt<string>("Enter your [cyan]Azure endpoint URL[/]:")
                .PromptStyle("green")
                .AllowEmpty()
                .Validate(url =>
                {
                    if (string.IsNullOrWhiteSpace(url))
                        return ValidationResult.Success(); // Allow empty to skip

                    if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
                        return ValidationResult.Error("[red]Invalid URL format[/]");

                    if (uri.Scheme != "https")
                        return ValidationResult.Error("[red]URL must use HTTPS[/]");

                    return ValidationResult.Success();
                }));

        if (string.IsNullOrWhiteSpace(endpoint))
        {
            return null;
        }

        // Ensure trailing slash
        if (!endpoint.EndsWith('/'))
        {
            endpoint += '/';
        }

        AnsiConsole.MarkupLine($"[green]Endpoint configured:[/] {endpoint}");

        // Also prompt for the deployment name (model)
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[grey]For Azure deployments, you'll need your deployment name (not the model name).[/]");

        return endpoint;
    }

    private static List<(string Id, string Name)> GetModelsForProvider(string provider)
    {
        return provider switch
        {
            "github-models" => new List<(string, string)>
            {
                // GPT-5.x Models
                ("gpt-5.4", "GPT-5.4"),
                ("gpt-5.4-mini", "GPT-5.4 Mini (0.33x cost)"),
                ("gpt-5.3-codex", "GPT-5.3 Codex"),
                ("gpt-5.2", "GPT-5.2"),
                ("gpt-5.2-codex", "GPT-5.2 Codex"),
                ("gpt-5.1-codex-max", "GPT-5.1 Codex Max"),
                ("gpt-5.1-codex-mini", "GPT-5.1 Codex Mini (Preview)"),
                ("gpt-5-mini", "GPT-5 Mini"),
                // GPT-4.x Models
                ("gpt-4.1", "GPT-4.1"),
                ("gpt-4o", "GPT-4o (Recommended)"),
                // Gemini Models
                ("gemini-3-flash", "Gemini 3 Flash (Preview, 0.33x cost)"),
                ("gemini-3-pro", "Gemini 3 Pro (Preview)"),
                ("gemini-3.1-pro", "Gemini 3.1 Pro (Preview)"),
                // Grok Models
                ("grok-code-fast-1", "Grok Code Fast 1 (0.25x cost)"),
                // Other
                ("raptor-mini", "Raptor Mini (Preview)"),
            },
            "openai" => new List<(string, string)>
            {
                // GPT-5.x
                ("gpt-5.4", "GPT-5.4"),
                ("gpt-5.4-pro", "GPT-5.4 Pro"),
                ("gpt-5.4-mini", "GPT-5.4 Mini"),
                ("gpt-5.4-nano", "GPT-5.4 Nano"),
                ("gpt-5.2", "GPT-5.2"),
                // GPT-4.x
                ("gpt-4.1", "GPT-4.1"),
                ("gpt-4o", "GPT-4o (Recommended)"),
                ("gpt-4o-mini", "GPT-4o Mini"),
                ("gpt-4-turbo", "GPT-4 Turbo"),
                ("gpt-4-turbo-preview", "GPT-4 Turbo Preview"),
                ("gpt-4", "GPT-4"),
                // GPT-3.5
                ("gpt-3.5-turbo", "GPT-3.5 Turbo"),
                // Reasoning
                ("o1", "o1 (Reasoning)"),
                ("o1-mini", "o1 Mini"),
                ("o1-preview", "o1 Preview"),
                ("o3-mini", "o3 Mini"),
            },
            "anthropic" => new List<(string, string)>
            {
                // Claude 4.6
                ("claude-opus-4.6", "Claude Opus 4.6 (Most capable)"),
                ("claude-sonnet-4.6", "Claude Sonnet 4.6"),
                // Claude 4.5
                ("claude-haiku-4.5", "Claude Haiku 4.5"),
                // Claude 4
                ("claude-opus-4-20250514", "Claude Opus 4"),
                ("claude-sonnet-4-20250514", "Claude Sonnet 4 (Recommended)"),
                ("claude-haiku-4-20250514", "Claude Haiku 4"),
                // Claude 3.5
                ("claude-3-5-sonnet-20241022", "Claude 3.5 Sonnet"),
                ("claude-3-5-haiku-20241022", "Claude 3.5 Haiku"),
                // Claude 3
                ("claude-3-opus-20240229", "Claude 3 Opus"),
                ("claude-3-sonnet-20240229", "Claude 3 Sonnet"),
                ("claude-3-haiku-20240307", "Claude 3 Haiku"),
            },
            "azure-openai" => new List<(string, string)>
            {
                ("your-gpt4o-deployment", "GPT-4o (enter your deployment name)"),
                ("your-gpt4-deployment", "GPT-4 (enter your deployment name)"),
                ("your-gpt35-deployment", "GPT-3.5 Turbo (enter your deployment name)"),
            },
            "azure-anthropic" => new List<(string, string)>
            {
                ("your-claude-deployment", "Claude (enter your deployment name)"),
            },
            _ => new List<(string, string)>
            {
                ("gpt-4o", "GPT-4o")
            }
        };
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
            var index = await indexService.EnsureIndexAsync(_workingDirectory, config.Source, forceRebuild: true, ct);
            _logger.LogInformation("  - docs/_index.md ({Count} documents indexed)", index.TotalDocuments);
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
            Path.Combine(_workingDirectory, ".github", "skills", "test-generation"),
            Path.Combine(_workingDirectory, DocsDir, "requirements")
        };

        foreach (var dir in directories)
        {
            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
                _logger.LogDebug("Created directory: {Directory}", dir);
            }
        }

        // Create requirements template
        await CreateRequirementsTemplateAsync(ct);
    }

    private async Task CreateRequirementsTemplateAsync(CancellationToken ct)
    {
        var reqPath = Path.Combine(_workingDirectory, DocsDir, "requirements", "_requirements.yaml");
        if (File.Exists(reqPath))
        {
            return;
        }

        const string template = """
            # Requirements definition file for Spectra coverage analysis
            # Uncomment and customize the examples below
            #
            # requirements:
            #   - id: REQ-001
            #     title: "User can log in with valid credentials"
            #     source: docs/authentication.md
            #     priority: high
            #   - id: REQ-002
            #     title: "System rejects invalid passwords"
            #     source: docs/authentication.md
            #     priority: high
            """;

        await File.WriteAllTextAsync(reqPath, template, ct);
        _logger.LogDebug("Created requirements template: {Path}", reqPath);
    }

    private async Task CreateConfigFileAsync(string configPath, CancellationToken ct)
    {
        var templateContent = GetEmbeddedTemplate("spectra.config.json");
        await File.WriteAllTextAsync(configPath, templateContent, ct);
        _logger.LogDebug("Created configuration file: {Path}", configPath);
    }

    private async Task CreateSkillFileAsync(CancellationToken ct)
    {
        var skillPath = Path.Combine(_workingDirectory, SkillPath);
        var skillDir = Path.GetDirectoryName(skillPath)!;

        if (!Directory.Exists(skillDir))
        {
            Directory.CreateDirectory(skillDir);
        }

        var templateContent = GetEmbeddedTemplate("test-generation-skill.md");
        await File.WriteAllTextAsync(skillPath, templateContent, ct);
        _logger.LogDebug("Created skill file: {Path}", skillPath);
    }

    private async Task CreateVsCodeMcpConfigAsync(CancellationToken ct)
    {
        var mcpConfigPath = Path.Combine(_workingDirectory, VsCodeMcpPath);
        var vsCodeDir = Path.GetDirectoryName(mcpConfigPath)!;

        if (!Directory.Exists(vsCodeDir))
        {
            Directory.CreateDirectory(vsCodeDir);
        }

        // Don't overwrite existing MCP config
        if (File.Exists(mcpConfigPath))
        {
            _logger.LogDebug("VS Code MCP config already exists, skipping: {Path}", mcpConfigPath);
            return;
        }

        var mcpConfig = """
            {
              "servers": {
                "spectra": {
                  "command": "spectra-mcp",
                  "args": ["."]
                }
              }
            }
            """;

        await File.WriteAllTextAsync(mcpConfigPath, mcpConfig, ct);
        _logger.LogDebug("Created VS Code MCP config: {Path}", mcpConfigPath);
    }

    private async Task UpdateGitIgnoreAsync(CancellationToken ct)
    {
        var gitIgnorePath = Path.Combine(_workingDirectory, ".gitignore");
        const string spectraSection = "# SPECTRA";
        const string executionDir = ".execution/";
        const string lockPattern = ".spectra.lock";

        var patterns = new[] { executionDir, lockPattern };
        var newEntries = new List<string>();

        if (File.Exists(gitIgnorePath))
        {
            var content = await File.ReadAllTextAsync(gitIgnorePath, ct);

            // Check which patterns are missing
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
            sb.AppendLine(executionDir);
            sb.AppendLine(lockPattern);

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
            "test-generation-skill.md" => GetDefaultSkillContent(),
            _ => throw new FileNotFoundException($"Template not found: {templateName}")
        };
    }

    private static string GetDefaultSkillContent() => """
        # Test Generation Skill

        You are an AI assistant specialized in generating comprehensive manual test cases from documentation.

        ## Available Tools

        - `get_document_map`: List all documentation files
        - `load_source_document`: Read a specific document
        - `search_source_docs`: Search for relevant content
        - `read_test_index`: View existing tests in a suite
        - `get_next_test_ids`: Allocate sequential test IDs
        - `check_duplicates_batch`: Verify tests are unique
        - `batch_write_tests`: Submit generated tests
        """;

    private async Task InstallAgentFilesAsync(bool force, CancellationToken ct)
    {
        // Install execution agent prompt
        var agentPath = Path.Combine(_workingDirectory, ExecutionAgentPath);
        await InstallAgentFileAsync(agentPath, AgentResourceLoader.GetExecutionAgentPrompt, force, ct);

        // Install execution skill
        var skillPath = Path.Combine(_workingDirectory, ExecutionSkillPath);
        await InstallAgentFileAsync(skillPath, AgentResourceLoader.GetSkillContent, force, ct);
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
}
