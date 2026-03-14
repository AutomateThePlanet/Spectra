namespace Spectra.CLI.Agent.Skills;

/// <summary>
/// Loads skill definitions from SKILL.md files.
/// </summary>
public sealed class SkillLoader
{
    private const string DefaultSkillPath = ".github/skills/test-generation/SKILL.md";
    private const string FallbackSkillPath = "SKILL.md";

    /// <summary>
    /// Loads the test generation skill definition.
    /// </summary>
    public async Task<SkillDefinition> LoadAsync(
        string basePath,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(basePath);

        // Try default path first
        var skillPath = Path.Combine(basePath, DefaultSkillPath);

        if (!File.Exists(skillPath))
        {
            // Try fallback path
            skillPath = Path.Combine(basePath, FallbackSkillPath);
        }

        if (!File.Exists(skillPath))
        {
            return SkillDefinition.Default;
        }

        var content = await File.ReadAllTextAsync(skillPath, ct);
        return Parse(content, skillPath);
    }

    /// <summary>
    /// Loads a skill from a specific path.
    /// </summary>
    public async Task<SkillDefinition> LoadFromPathAsync(
        string skillPath,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(skillPath);

        if (!File.Exists(skillPath))
        {
            throw new FileNotFoundException($"Skill file not found: {skillPath}", skillPath);
        }

        var content = await File.ReadAllTextAsync(skillPath, ct);
        return Parse(content, skillPath);
    }

    private static SkillDefinition Parse(string content, string filePath)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return SkillDefinition.Default;
        }

        var lines = content.Split('\n');
        string? name = null;
        string? description = null;
        var systemMessage = new List<string>();
        var inSystemMessage = false;

        foreach (var line in lines)
        {
            var trimmed = line.Trim();

            // Extract name from first H1
            if (name is null && trimmed.StartsWith("# "))
            {
                name = trimmed[2..].Trim();
                continue;
            }

            // Extract description from first paragraph after title
            if (name is not null && description is null && !string.IsNullOrWhiteSpace(trimmed) && !trimmed.StartsWith('#'))
            {
                description = trimmed;
                continue;
            }

            // Look for system message section
            if (trimmed.StartsWith("## System") || trimmed.StartsWith("## Instructions"))
            {
                inSystemMessage = true;
                continue;
            }

            // Another H2 ends system message section
            if (inSystemMessage && trimmed.StartsWith("## "))
            {
                inSystemMessage = false;
                continue;
            }

            if (inSystemMessage)
            {
                systemMessage.Add(line);
            }
        }

        // If no explicit system message section, use entire content after title
        var finalSystemMessage = systemMessage.Count > 0
            ? string.Join('\n', systemMessage).Trim()
            : content;

        return new SkillDefinition
        {
            Name = name ?? "Test Generation",
            Description = description ?? "Generates test cases from documentation",
            SystemMessage = finalSystemMessage,
            FilePath = filePath
        };
    }
}

/// <summary>
/// Represents a loaded skill definition.
/// </summary>
public sealed record SkillDefinition
{
    public required string Name { get; init; }
    public required string Description { get; init; }
    public required string SystemMessage { get; init; }
    public string? FilePath { get; init; }

    /// <summary>
    /// Default skill definition when no SKILL.md is found.
    /// </summary>
    public static SkillDefinition Default => new()
    {
        Name = "Test Generation",
        Description = "Generates test cases from documentation",
        SystemMessage = """
            You are an expert QA engineer specializing in manual test case generation.

            Your task is to analyze documentation and generate comprehensive manual test cases.

            For each test case, provide:
            - A unique ID (format: TC-XXX)
            - A descriptive title
            - Clear, numbered steps
            - Expected results
            - Appropriate priority (high, medium, low)
            - Relevant tags

            Focus on:
            - Happy path scenarios
            - Edge cases and boundary conditions
            - Error handling scenarios
            - Integration points

            Avoid:
            - Duplicating existing tests
            - Overly complex test cases
            - Implementation details
            """
    };
}
