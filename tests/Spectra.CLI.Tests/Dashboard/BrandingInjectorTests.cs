using Spectra.CLI.Dashboard;
using Spectra.Core.Models.Config;

namespace Spectra.CLI.Tests.Dashboard;

public class BrandingInjectorTests : IDisposable
{
    private readonly string _tempDir;
    private readonly BrandingInjector _injector;

    private const string MinimalTemplate = """
        <!DOCTYPE html>
        <html>
        <head>
            <title>{{COMPANY_NAME}}</title>
            {{FAVICON_LINK}}
            <link rel="stylesheet" href="styles/main.css">
            {{CUSTOM_CSS_LINK}}
            {{BRANDING_STYLES}}
        </head>
        <body>
            {{LOGO_IMG}}<h1>{{COMPANY_NAME}}</h1>
            {{BRANDING_CONFIG}}
        </body>
        </html>
        """;

    public BrandingInjectorTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"branding-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
        _injector = new BrandingInjector();
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    [Fact]
    public void InjectBranding_NullConfig_UsesDefaults()
    {
        var result = _injector.InjectBranding(MinimalTemplate, null, _tempDir);

        Assert.Contains("<title>SPECTRA Dashboard</title>", result);
        Assert.Contains("<h1>SPECTRA Dashboard</h1>", result);
        Assert.DoesNotContain("{{", result); // All placeholders replaced
    }

    [Fact]
    public void InjectBranding_CompanyName_ReplacesHeaderAndTitle()
    {
        var config = new BrandingConfig { CompanyName = "Acme Corp" };

        var result = _injector.InjectBranding(MinimalTemplate, config, _tempDir);

        Assert.Contains("<title>Acme Corp</title>", result);
        Assert.Contains("<h1>Acme Corp</h1>", result);
    }

    [Fact]
    public void InjectBranding_Logo_AddsImgTag()
    {
        var logoPath = Path.Combine(_tempDir, "logo.svg");
        File.WriteAllText(logoPath, "<svg></svg>");

        var config = new BrandingConfig { Logo = "logo.svg" };

        var result = _injector.InjectBranding(MinimalTemplate, config, _tempDir);

        Assert.Contains("<img src=\"logo.svg\"", result);
        Assert.Contains("class=\"header-logo\"", result);
    }

    [Fact]
    public void InjectBranding_MissingLogo_WarnsAndSkips()
    {
        var config = new BrandingConfig { Logo = "nonexistent.svg" };

        var result = _injector.InjectBranding(MinimalTemplate, config, _tempDir);

        Assert.DoesNotContain("<img", result);
        Assert.Contains("not found", _injector.Warnings[0]);
    }

    [Fact]
    public void InjectBranding_Favicon_AddsLinkTag()
    {
        var faviconPath = Path.Combine(_tempDir, "favicon.ico");
        File.WriteAllBytes(faviconPath, new byte[] { 0 });

        var config = new BrandingConfig { Favicon = "favicon.ico" };

        var result = _injector.InjectBranding(MinimalTemplate, config, _tempDir);

        Assert.Contains("<link rel=\"icon\" href=\"favicon.ico\">", result);
    }

    [Fact]
    public void InjectBranding_CustomColors_GeneratesCssVariables()
    {
        var config = new BrandingConfig
        {
            Colors = new ColorPaletteConfig
            {
                Primary = "#0d47a1",
                Accent = "#1565c0"
            }
        };

        var result = _injector.InjectBranding(MinimalTemplate, config, _tempDir);

        Assert.Contains("--primary-color: #0d47a1;", result);
        Assert.Contains("--accent-color: #1565c0;", result);
    }

    [Fact]
    public void InjectBranding_InvalidColor_WarnsAndSkips()
    {
        var config = new BrandingConfig
        {
            Colors = new ColorPaletteConfig { Primary = "not-a-color!" }
        };

        var result = _injector.InjectBranding(MinimalTemplate, config, _tempDir);

        // The invalid color should NOT appear in any CSS :root block
        Assert.DoesNotContain("--primary-color: not-a-color!", result);
        Assert.NotEmpty(_injector.Warnings);
        Assert.Contains("Invalid color value", _injector.Warnings[0]);
    }

    [Fact]
    public void InjectBranding_DarkTheme_InjectsThemeVariables()
    {
        var config = new BrandingConfig { Theme = "dark" };

        var result = _injector.InjectBranding(MinimalTemplate, config, _tempDir);

        Assert.Contains("--bg-color: #0f172a;", result);
        Assert.Contains("--text-color: #e2e8f0;", result);
        Assert.Contains("--card-bg: #1e293b;", result);
    }

    [Fact]
    public void InjectBranding_DarkThemeWithColors_ColorsOverrideTheme()
    {
        var config = new BrandingConfig
        {
            Theme = "dark",
            Colors = new ColorPaletteConfig { Primary = "#ff0000" }
        };

        var result = _injector.InjectBranding(MinimalTemplate, config, _tempDir);

        // Dark theme base variables
        Assert.Contains("--bg-color: #0f172a;", result);
        // Custom color override
        Assert.Contains("--primary-color: #ff0000;", result);
    }

    [Fact]
    public void InjectBranding_InvalidTheme_WarnsAndDefaultsToLight()
    {
        var config = new BrandingConfig { Theme = "neon" };

        var result = _injector.InjectBranding(MinimalTemplate, config, _tempDir);

        // Should NOT contain dark theme variables
        Assert.DoesNotContain("--bg-color: #0f172a;", result);
        Assert.Contains("Invalid theme", _injector.Warnings[0]);
    }

    [Fact]
    public void InjectBranding_CustomCss_AddsLinkTag()
    {
        var cssPath = Path.Combine(_tempDir, "custom.css");
        File.WriteAllText(cssPath, ".header { background: red; }");

        var config = new BrandingConfig { CustomCss = "custom.css" };

        var result = _injector.InjectBranding(MinimalTemplate, config, _tempDir);

        Assert.Contains("<link rel=\"stylesheet\" href=\"custom.css\">", result);
    }

    [Fact]
    public void InjectBranding_MissingCustomCss_WarnsAndSkips()
    {
        var config = new BrandingConfig { CustomCss = "missing.css" };

        var result = _injector.InjectBranding(MinimalTemplate, config, _tempDir);

        Assert.DoesNotContain("custom.css", result);
        Assert.Contains("not found", _injector.Warnings[0]);
    }

    [Fact]
    public void InjectBranding_FullConfig_AllElementsPresent()
    {
        // Create asset files
        File.WriteAllText(Path.Combine(_tempDir, "logo.png"), "fake-png");
        File.WriteAllBytes(Path.Combine(_tempDir, "favicon.ico"), new byte[] { 0 });
        File.WriteAllText(Path.Combine(_tempDir, "style.css"), "body {}");

        var config = new BrandingConfig
        {
            CompanyName = "Acme Corp",
            Logo = "logo.png",
            Favicon = "favicon.ico",
            Theme = "dark",
            Colors = new ColorPaletteConfig { Primary = "#0d47a1" },
            CustomCss = "style.css"
        };

        var result = _injector.InjectBranding(MinimalTemplate, config, _tempDir);

        Assert.Contains("<title>Acme Corp</title>", result);
        Assert.Contains("logo.png", result);
        Assert.Contains("favicon.ico", result);
        Assert.Contains("--bg-color: #0f172a;", result); // dark theme
        Assert.Contains("--primary-color: #0d47a1;", result); // custom color
        Assert.Contains("href=\"custom.css\"", result);
        Assert.DoesNotContain("{{", result);
    }

    [Fact]
    public void InjectBranding_EmbedsBrandingConfigJson()
    {
        var config = new BrandingConfig
        {
            CompanyName = "Test",
            Theme = "dark"
        };

        var result = _injector.InjectBranding(MinimalTemplate, config, _tempDir);

        Assert.Contains("branding-config", result);
        Assert.Contains("application/json", result);
    }

    [Fact]
    public void CopyAssets_CopiesLogoAndFavicon()
    {
        var outputDir = Path.Combine(_tempDir, "output");
        Directory.CreateDirectory(outputDir);

        File.WriteAllText(Path.Combine(_tempDir, "logo.svg"), "<svg></svg>");
        File.WriteAllBytes(Path.Combine(_tempDir, "favicon.ico"), new byte[] { 0, 1, 2 });

        var config = new BrandingConfig
        {
            Logo = "logo.svg",
            Favicon = "favicon.ico"
        };

        _injector.CopyAssets(config, _tempDir, outputDir);

        Assert.True(File.Exists(Path.Combine(outputDir, "logo.svg")));
        Assert.True(File.Exists(Path.Combine(outputDir, "favicon.ico")));
    }

    [Fact]
    public void CopyAssets_NullConfig_DoesNothing()
    {
        var outputDir = Path.Combine(_tempDir, "output");
        Directory.CreateDirectory(outputDir);

        _injector.CopyAssets(null, _tempDir, outputDir);

        // Only the directory itself should exist
        Assert.Empty(Directory.GetFiles(outputDir));
    }

    [Fact]
    public void ResolveAssetPath_AbsolutePath_UsedAsIs()
    {
        var absPath = Path.Combine(_tempDir, "logo.svg");
        File.WriteAllText(absPath, "<svg></svg>");

        var result = _injector.ResolveAssetPath(absPath, "/other/dir");

        Assert.Equal(absPath, result);
    }

    [Fact]
    public void ResolveAssetPath_RelativePath_ResolvedFromConfigDir()
    {
        File.WriteAllText(Path.Combine(_tempDir, "logo.svg"), "<svg></svg>");

        var result = _injector.ResolveAssetPath("logo.svg", _tempDir);

        Assert.NotNull(result);
        Assert.True(File.Exists(result));
    }

    [Fact]
    public void InjectBranding_LightTheme_NoThemeOverrides()
    {
        var config = new BrandingConfig { Theme = "light" };

        var result = _injector.InjectBranding(MinimalTemplate, config, _tempDir);

        // Light theme should NOT inject dark variables
        Assert.DoesNotContain("--bg-color: #0f172a;", result);
        Assert.Empty(_injector.Warnings);
    }
}
