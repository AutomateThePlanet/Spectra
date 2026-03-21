using System.Text.Json.Serialization;

namespace Spectra.Core.Models.Config;

/// <summary>
/// Custom color overrides for dashboard branding.
/// Any null field inherits from the selected theme's defaults.
/// </summary>
public sealed class ColorPaletteConfig
{
    /// <summary>Primary brand color (header background, active nav).</summary>
    [JsonPropertyName("primary")]
    public string? Primary { get; init; }

    /// <summary>Accent color (links, interactive elements).</summary>
    [JsonPropertyName("accent")]
    public string? Accent { get; init; }

    /// <summary>Page background color.</summary>
    [JsonPropertyName("background")]
    public string? Background { get; init; }

    /// <summary>Primary text color.</summary>
    [JsonPropertyName("text")]
    public string? Text { get; init; }

    /// <summary>Card/panel background color.</summary>
    [JsonPropertyName("surface")]
    public string? Surface { get; init; }

    /// <summary>Border color for cards, dividers, inputs.</summary>
    [JsonPropertyName("border")]
    public string? Border { get; init; }
}
