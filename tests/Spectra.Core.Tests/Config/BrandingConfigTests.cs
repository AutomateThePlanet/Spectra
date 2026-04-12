using System.Text.Json;
using Spectra.Core.Models.Config;

namespace Spectra.Core.Tests.Config;

public class BrandingConfigTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    [Fact]
    public void Deserialize_FullConfig_AllFieldsPopulated()
    {
        var json = """
        {
            "company_name": "Acme Corp",
            "logo": "assets/logo.svg",
            "favicon": "assets/favicon.ico",
            "theme": "dark",
            "colors": {
                "primary": "#0d47a1",
                "accent": "#1565c0",
                "background": "#121212",
                "text": "#ffffff",
                "surface": "#1e1e1e",
                "border": "#333333"
            },
            "custom_css": "assets/custom.css"
        }
        """;

        var config = JsonSerializer.Deserialize<BrandingConfig>(json, JsonOptions);

        Assert.NotNull(config);
        Assert.Equal("Acme Corp", config.CompanyName);
        Assert.Equal("assets/logo.svg", config.Logo);
        Assert.Equal("assets/favicon.ico", config.Favicon);
        Assert.Equal("dark", config.Theme);
        Assert.NotNull(config.Colors);
        Assert.Equal("#0d47a1", config.Colors.Primary);
        Assert.Equal("#1565c0", config.Colors.Accent);
        Assert.Equal("#121212", config.Colors.Background);
        Assert.Equal("#ffffff", config.Colors.Text);
        Assert.Equal("#1e1e1e", config.Colors.Surface);
        Assert.Equal("#333333", config.Colors.Border);
        Assert.Equal("assets/custom.css", config.CustomCss);
    }

    [Fact]
    public void Deserialize_EmptyObject_AllFieldsNull()
    {
        var json = "{}";

        var config = JsonSerializer.Deserialize<BrandingConfig>(json, JsonOptions);

        Assert.NotNull(config);
        Assert.Null(config.CompanyName);
        Assert.Null(config.Logo);
        Assert.Null(config.Favicon);
        Assert.Null(config.Theme);
        Assert.Null(config.Colors);
        Assert.Null(config.CustomCss);
    }

    [Fact]
    public void Deserialize_PartialConfig_OnlySetFieldsPopulated()
    {
        var json = """
        {
            "company_name": "Acme Corp",
            "theme": "light"
        }
        """;

        var config = JsonSerializer.Deserialize<BrandingConfig>(json, JsonOptions);

        Assert.NotNull(config);
        Assert.Equal("Acme Corp", config.CompanyName);
        Assert.Null(config.Logo);
        Assert.Equal("light", config.Theme);
        Assert.Null(config.Colors);
    }

    [Fact]
    public void Roundtrip_Serialization_PreservesValues()
    {
        var config = new BrandingConfig
        {
            CompanyName = "Test Corp",
            Theme = "dark",
            Colors = new ColorPaletteConfig
            {
                Primary = "#ff0000",
                Accent = "#00ff00"
            }
        };

        var json = JsonSerializer.Serialize(config, JsonOptions);
        var deserialized = JsonSerializer.Deserialize<BrandingConfig>(json, JsonOptions);

        Assert.NotNull(deserialized);
        Assert.Equal("Test Corp", deserialized.CompanyName);
        Assert.Equal("dark", deserialized.Theme);
        Assert.Equal("#ff0000", deserialized.Colors?.Primary);
        Assert.Equal("#00ff00", deserialized.Colors?.Accent);
    }

    [Fact]
    public void DashboardConfig_BackwardCompatibility_NullBrandingByDefault()
    {
        var json = """
        {
            "output_dir": "./site",
            "title": "My Dashboard"
        }
        """;

        var config = JsonSerializer.Deserialize<DashboardConfig>(json, JsonOptions);

        Assert.NotNull(config);
        Assert.Equal("./site", config.OutputDir);
        Assert.Equal("My Dashboard", config.Title);
        Assert.Null(config.Branding);
    }

    [Fact]
    public void DashboardConfig_WithBranding_Deserializes()
    {
        var json = """
        {
            "output_dir": "./site",
            "branding": {
                "company_name": "Acme Corp",
                "theme": "dark"
            }
        }
        """;

        var config = JsonSerializer.Deserialize<DashboardConfig>(json, JsonOptions);

        Assert.NotNull(config);
        Assert.NotNull(config.Branding);
        Assert.Equal("Acme Corp", config.Branding.CompanyName);
        Assert.Equal("dark", config.Branding.Theme);
    }

    [Fact]
    public void SpectraConfig_WithBranding_DeserializesFromFullConfig()
    {
        var json = """
        {
            "source": { "mode": "local", "local_dir": "docs/" },
            "tests": { "dir": "test-cases/" },
            "ai": { "providers": [] },
            "dashboard": {
                "branding": {
                    "company_name": "Acme Corp"
                }
            }
        }
        """;

        var config = JsonSerializer.Deserialize<SpectraConfig>(json, JsonOptions);

        Assert.NotNull(config);
        Assert.NotNull(config.Dashboard.Branding);
        Assert.Equal("Acme Corp", config.Dashboard.Branding.CompanyName);
    }

    [Fact]
    public void ColorPaletteConfig_PartialColors_OnlySetFieldsPopulated()
    {
        var json = """
        {
            "primary": "#0d47a1"
        }
        """;

        var config = JsonSerializer.Deserialize<ColorPaletteConfig>(json, JsonOptions);

        Assert.NotNull(config);
        Assert.Equal("#0d47a1", config.Primary);
        Assert.Null(config.Accent);
        Assert.Null(config.Background);
        Assert.Null(config.Text);
        Assert.Null(config.Surface);
        Assert.Null(config.Border);
    }
}
