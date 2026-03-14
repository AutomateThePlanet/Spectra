using Spectra.Core.Models.Config;

namespace Spectra.CLI.Provider;

/// <summary>
/// Resolves AI providers from configuration.
/// </summary>
public sealed class ProviderResolver
{
    private readonly AiConfig _config;

    public ProviderResolver(AiConfig config)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
    }

    /// <summary>
    /// Gets the default provider based on priority and availability.
    /// </summary>
    public ProviderConfig? GetDefaultProvider()
    {
        return _config.Providers
            .Where(p => p.Enabled)
            .OrderBy(p => p.Priority)
            .FirstOrDefault();
    }

    /// <summary>
    /// Gets a specific provider by name.
    /// </summary>
    public ProviderConfig? GetProvider(string name)
    {
        return _config.Providers
            .FirstOrDefault(p => p.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Gets all enabled providers ordered by priority.
    /// </summary>
    public IReadOnlyList<ProviderConfig> GetEnabledProviders()
    {
        return _config.Providers
            .Where(p => p.Enabled)
            .OrderBy(p => p.Priority)
            .ToList();
    }

    /// <summary>
    /// Gets providers suitable for fallback.
    /// </summary>
    public IReadOnlyList<ProviderConfig> GetFallbackProviders(string excludeName)
    {
        return _config.Providers
            .Where(p => p.Enabled && !p.Name.Equals(excludeName, StringComparison.OrdinalIgnoreCase))
            .OrderBy(p => p.Priority)
            .ToList();
    }
}
