using Spectra.Core.Models.Config;

namespace Spectra.CLI.Prompts;

/// <summary>
/// Provides the default 6 behavior categories used when config has no analysis.categories.
/// </summary>
public static class DefaultCategories
{
    public static readonly IReadOnlyList<CategoryDefinition> All =
    [
        new() { Id = "happy_path", Description = "Normal successful user flows" },
        new() { Id = "negative", Description = "Error handling, invalid inputs, failure scenarios" },
        new() { Id = "edge_case", Description = "Boundary conditions, unusual combinations, limits" },
        new() { Id = "boundary", Description = "Values at exact limits: file size = 10MB, password length = 8, count = 0 and max" },
        new() { Id = "error_handling", Description = "System returns appropriate errors, logs failures, and recovers gracefully" },
        new() { Id = "security", Description = "Permission checks, access control, authentication, input sanitization" }
    ];
}
