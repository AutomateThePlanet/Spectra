using System.Text.Json.Serialization;

namespace Spectra.Core.Models.Config;

/// <summary>
/// Branding and theming configuration for the dashboard.
/// All fields are optional — null/omitted means "use default".
/// </summary>
public sealed class BrandingConfig
{
    /// <summary>
    /// Display name replacing "SPECTRA Dashboard" in header and page title.
    /// </summary>
    [JsonPropertyName("company_name")]
    public string? CompanyName { get; init; }

    /// <summary>
    /// Path to logo image file (PNG, SVG, JPG). Relative to config file location.
    /// </summary>
    [JsonPropertyName("logo")]
    public string? Logo { get; init; }

    /// <summary>
    /// Path to favicon file (ICO, PNG, SVG). Relative to config file location.
    /// </summary>
    [JsonPropertyName("favicon")]
    public string? Favicon { get; init; }

    /// <summary>
    /// Theme preset: "light" (default) or "dark".
    /// </summary>
    [JsonPropertyName("theme")]
    public string? Theme { get; init; }

    /// <summary>
    /// Custom color overrides applied on top of the theme.
    /// </summary>
    [JsonPropertyName("colors")]
    public ColorPaletteConfig? Colors { get; init; }

    /// <summary>
    /// Path to custom CSS file. Relative to config file location.
    /// </summary>
    [JsonPropertyName("custom_css")]
    public string? CustomCss { get; init; }
}
