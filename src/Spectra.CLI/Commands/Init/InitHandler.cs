using Microsoft.Extensions.Logging;
using Spectra.CLI.Agent;
using Spectra.CLI.Infrastructure;
using Spectra.Core.Config;

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

            // Update .gitignore
            await UpdateGitIgnoreAsync(ct);

            _logger.LogInformation("SPECTRA initialized successfully!");
            _logger.LogInformation("Created:");
            _logger.LogInformation("  - {ConfigPath}", ConfigFileName);
            _logger.LogInformation("  - {DocsDir}/", DocsDir);
            _logger.LogInformation("  - {TestsDir}/", TestsDir);
            _logger.LogInformation("  - {SkillPath}", SkillPath);

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
        Console.WriteLine("AI Provider Setup");
        Console.WriteLine(new string('-', 40));
        Console.WriteLine();
        Console.WriteLine("Which AI provider would you like to use?");
        Console.WriteLine("  1. GitHub Models (uses GITHUB_TOKEN or gh CLI)");
        Console.WriteLine("  2. OpenAI (requires OPENAI_API_KEY)");
        Console.WriteLine("  3. Anthropic (requires ANTHROPIC_API_KEY)");
        Console.WriteLine("  4. Skip for now");
        Console.Write("> ");

        var input = Console.ReadLine()?.Trim();

        if (string.IsNullOrEmpty(input) || input == "4")
        {
            Console.WriteLine();
            Console.WriteLine("Skipped. Run 'spectra auth' to check authentication status later.");
            return;
        }

        var provider = input switch
        {
            "1" => "github-models",
            "2" => "openai",
            "3" => "anthropic",
            _ => null
        };

        if (provider is null)
        {
            Console.WriteLine();
            Console.WriteLine("Invalid selection. Run 'spectra auth' to check authentication status later.");
            return;
        }

        Console.WriteLine();
        Console.WriteLine($"Checking {provider} authentication...");

        var authResult = await AgentFactory.GetAuthStatusAsync(provider, null, ct);

        if (authResult.IsAuthenticated)
        {
            Console.WriteLine($"  [OK] Authenticated via {authResult.Source}");
            Console.WriteLine();
            Console.WriteLine("Configuration complete!");
        }
        else
        {
            Console.WriteLine($"  [NOT CONFIGURED]");
            Console.WriteLine();
            Console.WriteLine("To authenticate:");
            foreach (var instruction in authResult.SetupInstructions)
            {
                if (!string.IsNullOrEmpty(instruction))
                {
                    Console.WriteLine($"  {instruction}");
                }
            }
            Console.WriteLine();
            Console.WriteLine("Run 'spectra auth' to verify authentication after setup.");
        }
    }

    private async Task CreateDirectoriesAsync(CancellationToken ct)
    {
        var directories = new[]
        {
            Path.Combine(_workingDirectory, DocsDir),
            Path.Combine(_workingDirectory, TestsDir),
            Path.Combine(_workingDirectory, ".github", "skills", "test-generation")
        };

        foreach (var dir in directories)
        {
            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
                _logger.LogDebug("Created directory: {Directory}", dir);
            }
        }
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

    private async Task UpdateGitIgnoreAsync(CancellationToken ct)
    {
        var gitIgnorePath = Path.Combine(_workingDirectory, ".gitignore");
        const string lockPattern = ".spectra.lock";

        if (File.Exists(gitIgnorePath))
        {
            var content = await File.ReadAllTextAsync(gitIgnorePath, ct);
            if (!content.Contains(lockPattern))
            {
                var newContent = content.TrimEnd() + Environment.NewLine + Environment.NewLine +
                    "# SPECTRA lock files" + Environment.NewLine +
                    lockPattern + Environment.NewLine;
                await File.WriteAllTextAsync(gitIgnorePath, newContent, ct);
                _logger.LogDebug("Updated .gitignore with lock pattern");
            }
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
}
