using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Spectra.Core.Models.Config;

namespace Spectra.CLI.Dashboard;

/// <summary>
/// Injects branding configuration into dashboard HTML templates.
/// </summary>
public sealed class BrandingInjector
{
    private const string DefaultCompanyName = "SPECTRA Dashboard";
    private const long MaxLogoSizeBytes = 5 * 1024 * 1024; // 5 MB

    private static readonly Regex CssColorPattern = new(
        @"^(#([0-9a-fA-F]{3}){1,2}|rgb\(\s*\d+\s*,\s*\d+\s*,\s*\d+\s*\)|rgba\(\s*\d+\s*,\s*\d+\s*,\s*\d+\s*,\s*[\d.]+\s*\)|hsl\(\s*\d+\s*,\s*\d+%?\s*,\s*\d+%?\s*\)|hsla\(\s*\d+\s*,\s*\d+%?\s*,\s*\d+%?\s*,\s*[\d.]+\s*\)|[a-zA-Z]+)$",
        RegexOptions.Compiled);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        WriteIndented = false
    };

    private readonly List<string> _warnings = [];

    /// <summary>
    /// Warnings generated during branding injection.
    /// </summary>
    public IReadOnlyList<string> Warnings => _warnings;

    /// <summary>
    /// Injects branding configuration into the HTML template.
    /// </summary>
    /// <param name="html">The HTML template with placeholders.</param>
    /// <param name="config">Branding configuration, or null for defaults.</param>
    /// <param name="configDir">Directory containing spectra.config.json for path resolution.</param>
    /// <returns>HTML with branding applied.</returns>
    public string InjectBranding(string html, BrandingConfig? config, string configDir)
    {
        _warnings.Clear();

        var companyName = config?.CompanyName ?? DefaultCompanyName;
        var theme = ValidateTheme(config?.Theme);

        // Replace company name in header and title
        html = html.Replace("{{COMPANY_NAME}}", companyName);
        html = ReplaceTitle(html, companyName);

        // Inject favicon
        html = html.Replace("{{FAVICON_LINK}}", BuildFaviconLink(config?.Favicon, configDir));

        // Inject logo
        html = html.Replace("{{LOGO_IMG}}", BuildLogoImg(config?.Logo, companyName, configDir));

        // Build branding styles (theme + custom colors)
        html = html.Replace("{{BRANDING_STYLES}}", BuildBrandingStyles(theme, config?.Colors));

        // Inject custom CSS link
        html = html.Replace("{{CUSTOM_CSS_LINK}}", BuildCustomCssLink(config?.CustomCss, configDir));

        // Embed branding config JSON for client-side access
        html = html.Replace("{{BRANDING_CONFIG}}", BuildBrandingConfigScript(config));

        return html;
    }

    /// <summary>
    /// Resolves a file path relative to the config directory.
    /// Returns null if the file does not exist.
    /// </summary>
    public string? ResolveAssetPath(string? relativePath, string configDir)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
            return null;

        var resolved = Path.IsPathRooted(relativePath)
            ? relativePath
            : Path.GetFullPath(Path.Combine(configDir, relativePath));

        if (!File.Exists(resolved))
        {
            _warnings.Add($"Asset file not found: {relativePath} (resolved to {resolved})");
            return null;
        }

        return resolved;
    }

    /// <summary>
    /// Copies branding assets (logo, favicon, custom CSS) to the output directory.
    /// </summary>
    public void CopyAssets(BrandingConfig? config, string configDir, string outputDir)
    {
        if (config is null) return;

        CopyAsset(config.Logo, configDir, outputDir, "logo");
        CopyAsset(config.Favicon, configDir, outputDir, "favicon");
        CopyAsset(config.CustomCss, configDir, outputDir, "custom", ".css");
    }

    private void CopyAsset(string? sourcePath, string configDir, string outputDir, string baseName, string? forceExtension = null)
    {
        if (string.IsNullOrWhiteSpace(sourcePath)) return;

        var resolved = ResolveAssetPath(sourcePath, configDir);
        if (resolved is null) return;

        var fileInfo = new FileInfo(resolved);
        if (baseName == "logo" && fileInfo.Length > MaxLogoSizeBytes)
        {
            _warnings.Add($"Logo file is {fileInfo.Length / (1024 * 1024.0):F1} MB — large assets may affect dashboard load time");
        }

        var extension = forceExtension ?? Path.GetExtension(resolved);
        var destName = baseName + extension;
        var destPath = Path.Combine(outputDir, destName);

        File.Copy(resolved, destPath, overwrite: true);
    }

    private string ValidateTheme(string? theme)
    {
        if (string.IsNullOrWhiteSpace(theme))
            return "light";

        if (theme.Equals("light", StringComparison.OrdinalIgnoreCase))
            return "light";

        if (theme.Equals("dark", StringComparison.OrdinalIgnoreCase))
            return "dark";

        _warnings.Add($"Invalid theme value \"{theme}\" — defaulting to \"light\"");
        return "light";
    }

    private static string ReplaceTitle(string html, string companyName)
    {
        // Replace <title>SPECTRA Dashboard</title> with the company name
        return Regex.Replace(
            html,
            @"<title>[^<]*</title>",
            $"<title>{companyName}</title>");
    }

    private string BuildFaviconLink(string? faviconPath, string configDir)
    {
        if (string.IsNullOrWhiteSpace(faviconPath))
            return "";

        var resolved = ResolveAssetPath(faviconPath, configDir);
        if (resolved is null)
            return "";

        var ext = Path.GetExtension(resolved);
        return $"    <link rel=\"icon\" href=\"favicon{ext}\">";
    }

    private string BuildLogoImg(string? logoPath, string companyName, string configDir)
    {
        if (string.IsNullOrWhiteSpace(logoPath))
            return "";

        var resolved = ResolveAssetPath(logoPath, configDir);
        if (resolved is null)
            return "";

        var ext = Path.GetExtension(resolved);
        var alt = System.Net.WebUtility.HtmlEncode(companyName);
        return $"<img src=\"logo{ext}\" alt=\"{alt}\" class=\"header-logo\">";
    }

    private string BuildBrandingStyles(string theme, ColorPaletteConfig? colors)
    {
        var sb = new StringBuilder();

        // Dark theme overrides
        if (theme == "dark")
        {
            sb.AppendLine("    <style>");
            sb.AppendLine("    :root {");
            sb.AppendLine("        --primary-color: #1e3a5f;");
            sb.AppendLine("        --primary-light: #2563eb;");
            sb.AppendLine("        --accent-color: #3b82f6;");
            sb.AppendLine("        --text-color: #e2e8f0;");
            sb.AppendLine("        --text-muted: #94a3b8;");
            sb.AppendLine("        --bg-color: #0f172a;");
            sb.AppendLine("        --card-bg: #1e293b;");
            sb.AppendLine("        --border-color: #334155;");
            sb.AppendLine("        --color-success: #22c55e;");
            sb.AppendLine("        --color-warning: #f59e0b;");
            sb.AppendLine("        --color-danger: #ef4444;");
            sb.AppendLine("        --shadow-sm: 0 1px 2px rgba(0, 0, 0, 0.2);");
            sb.AppendLine("        --shadow-md: 0 4px 6px rgba(0, 0, 0, 0.3);");
            sb.AppendLine("        --shadow-lg: 0 10px 15px rgba(0, 0, 0, 0.4);");
            sb.AppendLine("    }");
            sb.AppendLine("    </style>");
        }

        // Custom color overrides (applied after theme)
        if (colors is not null && HasAnyColor(colors))
        {
            sb.AppendLine("    <style>");
            sb.AppendLine("    :root {");

            AppendColorVar(sb, "--primary-color", colors.Primary);
            AppendColorVar(sb, "--primary-light", colors.Primary); // primary-light follows primary
            AppendColorVar(sb, "--accent-color", colors.Accent);
            AppendColorVar(sb, "--bg-color", colors.Background);
            AppendColorVar(sb, "--text-color", colors.Text);
            AppendColorVar(sb, "--card-bg", colors.Surface);
            AppendColorVar(sb, "--border-color", colors.Border);

            sb.AppendLine("    }");
            sb.AppendLine("    </style>");
        }

        return sb.ToString().TrimEnd();
    }

    private void AppendColorVar(StringBuilder sb, string varName, string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return;

        if (!CssColorPattern.IsMatch(value))
        {
            _warnings.Add($"Invalid color value for \"{varName}\": \"{value}\" — using theme default");
            return;
        }

        sb.AppendLine($"        {varName}: {value};");
    }

    private static bool HasAnyColor(ColorPaletteConfig colors) =>
        colors.Primary is not null ||
        colors.Accent is not null ||
        colors.Background is not null ||
        colors.Text is not null ||
        colors.Surface is not null ||
        colors.Border is not null;

    private string BuildCustomCssLink(string? customCssPath, string configDir)
    {
        if (string.IsNullOrWhiteSpace(customCssPath))
            return "";

        var resolved = ResolveAssetPath(customCssPath, configDir);
        if (resolved is null)
            return "";

        return "    <link rel=\"stylesheet\" href=\"custom.css\">";
    }

    private static string BuildBrandingConfigScript(BrandingConfig? config)
    {
        if (config is null)
            return "";

        var json = JsonSerializer.Serialize(config, JsonOptions);
        return $"    <script id=\"branding-config\" type=\"application/json\">{json}</script>";
    }
}
