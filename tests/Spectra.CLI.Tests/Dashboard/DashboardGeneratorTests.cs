using Spectra.CLI.Dashboard;
using Spectra.Core.Models.Dashboard;

namespace Spectra.CLI.Tests.Dashboard;

/// <summary>
/// Unit tests for DashboardGenerator.
/// </summary>
public class DashboardGeneratorTests : IDisposable
{
    private readonly string _outputDir;

    public DashboardGeneratorTests()
    {
        _outputDir = Path.Combine(Path.GetTempPath(), $"spectra-dashboard-test-{Guid.NewGuid():N}");
    }

    public void Dispose()
    {
        if (Directory.Exists(_outputDir))
        {
            Directory.Delete(_outputDir, recursive: true);
        }
    }

    [Fact]
    public async Task GenerateAsync_CreatesOutputDirectory()
    {
        var generator = new DashboardGenerator();
        var data = CreateMinimalDashboardData();

        await generator.GenerateAsync(data, _outputDir);

        Assert.True(Directory.Exists(_outputDir));
    }

    [Fact]
    public async Task GenerateAsync_CreatesIndexHtml()
    {
        var generator = new DashboardGenerator();
        var data = CreateMinimalDashboardData();

        await generator.GenerateAsync(data, _outputDir);

        var indexPath = Path.Combine(_outputDir, "index.html");
        Assert.True(File.Exists(indexPath));
    }

    [Fact]
    public async Task GenerateAsync_CreatesStylesDirectory()
    {
        var generator = new DashboardGenerator();
        var data = CreateMinimalDashboardData();

        await generator.GenerateAsync(data, _outputDir);

        var stylesPath = Path.Combine(_outputDir, "styles");
        Assert.True(Directory.Exists(stylesPath));
        Assert.True(File.Exists(Path.Combine(stylesPath, "main.css")));
    }

    [Fact]
    public async Task GenerateAsync_CreatesScriptsDirectory()
    {
        var generator = new DashboardGenerator();
        var data = CreateMinimalDashboardData();

        await generator.GenerateAsync(data, _outputDir);

        var scriptsPath = Path.Combine(_outputDir, "scripts");
        Assert.True(Directory.Exists(scriptsPath));
        Assert.True(File.Exists(Path.Combine(scriptsPath, "app.js")));
    }

    [Fact]
    public async Task GenerateAsync_EmbedsJsonData()
    {
        var generator = new DashboardGenerator();
        var data = CreateMinimalDashboardData();

        await generator.GenerateAsync(data, _outputDir);

        var indexPath = Path.Combine(_outputDir, "index.html");
        var html = await File.ReadAllTextAsync(indexPath);

        Assert.Contains("dashboard-data", html);
        Assert.Contains("application/json", html);
        Assert.Contains("\"version\":", html);
        Assert.Contains("\"repository\":", html);
    }

    [Fact]
    public async Task GenerateAsync_EmbedsSuiteData()
    {
        var generator = new DashboardGenerator();
        var data = new DashboardData
        {
            GeneratedAt = DateTime.UtcNow,
            Repository = "test-repo",
            Suites =
            [
                new SuiteStats
                {
                    Name = "checkout",
                    TestCount = 5,
                    AutomationCoverage = 80.5m,
                    ByPriority = new Dictionary<string, int> { ["high"] = 3, ["medium"] = 2 },
                    ByComponent = new Dictionary<string, int>(),
                    Tags = ["smoke", "regression"]
                }
            ]
        };

        await generator.GenerateAsync(data, _outputDir);

        var indexPath = Path.Combine(_outputDir, "index.html");
        var html = await File.ReadAllTextAsync(indexPath);

        Assert.Contains("checkout", html);
    }

    [Fact]
    public async Task GenerateAsync_EmbedsTestData()
    {
        var generator = new DashboardGenerator();
        var data = new DashboardData
        {
            GeneratedAt = DateTime.UtcNow,
            Repository = "test-repo",
            Tests =
            [
                new TestEntry
                {
                    Id = "TC-001",
                    Suite = "checkout",
                    Title = "Verify checkout flow",
                    File = "TC-001.md",
                    Priority = "high",
                    Tags = ["smoke"],
                    HasAutomation = true
                }
            ]
        };

        await generator.GenerateAsync(data, _outputDir);

        var indexPath = Path.Combine(_outputDir, "index.html");
        var html = await File.ReadAllTextAsync(indexPath);

        Assert.Contains("TC-001", html);
        Assert.Contains("Verify checkout flow", html);
    }

    [Fact]
    public async Task GenerateAsync_HtmlHasNavigationButtons()
    {
        var generator = new DashboardGenerator();
        var data = CreateMinimalDashboardData();

        await generator.GenerateAsync(data, _outputDir);

        var indexPath = Path.Combine(_outputDir, "index.html");
        var html = await File.ReadAllTextAsync(indexPath);

        Assert.Contains("data-view=\"suites\"", html);
        Assert.Contains("data-view=\"tests\"", html);
        Assert.Contains("data-view=\"runs\"", html);
        Assert.Contains("data-view=\"coverage\"", html);
    }

    [Fact]
    public async Task GenerateAsync_HtmlHasFilterControls()
    {
        var generator = new DashboardGenerator();
        var data = CreateMinimalDashboardData();

        await generator.GenerateAsync(data, _outputDir);

        var indexPath = Path.Combine(_outputDir, "index.html");
        var html = await File.ReadAllTextAsync(indexPath);

        Assert.Contains("filter-priority", html);
        Assert.Contains("filter-component", html);
        Assert.Contains("filter-search", html);
    }

    [Fact]
    public async Task GenerateAsync_CssHasCardStyles()
    {
        var generator = new DashboardGenerator();
        var data = CreateMinimalDashboardData();

        await generator.GenerateAsync(data, _outputDir);

        var cssPath = Path.Combine(_outputDir, "styles", "main.css");
        var css = await File.ReadAllTextAsync(cssPath);

        Assert.Contains(".card", css);
        Assert.Contains(".card-grid", css);
        Assert.Contains(".badge", css);
    }

    [Fact]
    public async Task GenerateAsync_JsHasRenderFunctions()
    {
        var generator = new DashboardGenerator();
        var data = CreateMinimalDashboardData();

        await generator.GenerateAsync(data, _outputDir);

        var jsPath = Path.Combine(_outputDir, "scripts", "app.js");
        var js = await File.ReadAllTextAsync(jsPath);

        Assert.Contains("renderSuites", js);
        Assert.Contains("renderTests", js);
        Assert.Contains("renderRuns", js);
        Assert.Contains("renderCoverage", js);
    }

    [Fact]
    public async Task GenerateAsync_ThrowsOnNullData()
    {
        var generator = new DashboardGenerator();

        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            generator.GenerateAsync(null!, _outputDir));
    }

    [Fact]
    public async Task GenerateAsync_ThrowsOnEmptyOutputPath()
    {
        var generator = new DashboardGenerator();
        var data = CreateMinimalDashboardData();

        await Assert.ThrowsAsync<ArgumentException>(() =>
            generator.GenerateAsync(data, ""));
    }

    [Fact]
    public async Task GenerateAsync_WithCustomTemplate_UsesCustomTemplate()
    {
        // Create custom template directory
        var templateDir = Path.Combine(Path.GetTempPath(), $"spectra-template-{Guid.NewGuid():N}");
        Directory.CreateDirectory(templateDir);
        try
        {
            var customHtml = """
                <!DOCTYPE html>
                <html>
                <head><title>Custom Dashboard</title></head>
                <body>
                    <h1>Custom Template</h1>
                    <script id="dashboard-data" type="application/json">
                    {{DASHBOARD_DATA}}
                    </script>
                </body>
                </html>
                """;
            await File.WriteAllTextAsync(Path.Combine(templateDir, "index.html"), customHtml);

            var generator = new DashboardGenerator(Path.Combine(templateDir, "index.html"));
            var data = CreateMinimalDashboardData();

            await generator.GenerateAsync(data, _outputDir);

            var indexPath = Path.Combine(_outputDir, "index.html");
            var html = await File.ReadAllTextAsync(indexPath);

            Assert.Contains("Custom Template", html);
        }
        finally
        {
            Directory.Delete(templateDir, recursive: true);
        }
    }

    [Fact]
    public async Task GenerateAsync_OverwritesExistingOutput()
    {
        Directory.CreateDirectory(_outputDir);
        await File.WriteAllTextAsync(Path.Combine(_outputDir, "index.html"), "old content");

        var generator = new DashboardGenerator();
        var data = CreateMinimalDashboardData();

        await generator.GenerateAsync(data, _outputDir);

        var html = await File.ReadAllTextAsync(Path.Combine(_outputDir, "index.html"));
        Assert.DoesNotContain("old content", html);
        Assert.Contains("SPECTRA Dashboard", html);
    }

    [Fact]
    public async Task GenerateAsync_CopiesFunctionsDirectory_WhenPresent()
    {
        // Create a template directory with functions/
        var templateDir = Path.Combine(Path.GetTempPath(), $"spectra-template-{Guid.NewGuid():N}");
        Directory.CreateDirectory(templateDir);
        try
        {
            // Set up minimal template with styles, scripts, and functions
            var stylesDir = Path.Combine(templateDir, "styles");
            var scriptsDir = Path.Combine(templateDir, "scripts");
            var functionsDir = Path.Combine(templateDir, "functions", "auth");
            Directory.CreateDirectory(stylesDir);
            Directory.CreateDirectory(scriptsDir);
            Directory.CreateDirectory(functionsDir);

            await File.WriteAllTextAsync(Path.Combine(templateDir, "index.html"),
                "<html><head><title>{{COMPANY_NAME}}</title>{{FAVICON_LINK}}{{CUSTOM_CSS_LINK}}{{BRANDING_STYLES}}</head><body>{{LOGO_IMG}}{{BRANDING_CONFIG}}<script id=\"dashboard-data\" type=\"application/json\">{{DASHBOARD_DATA}}</script></body></html>");
            await File.WriteAllTextAsync(Path.Combine(stylesDir, "main.css"), "body {}");
            await File.WriteAllTextAsync(Path.Combine(scriptsDir, "app.js"), "// app");
            await File.WriteAllTextAsync(Path.Combine(templateDir, "functions", "_middleware.js"), "export async function onRequest() {}");
            await File.WriteAllTextAsync(Path.Combine(functionsDir, "callback.js"), "export async function onRequest() {}");
            await File.WriteAllTextAsync(Path.Combine(templateDir, "access-denied.html"), "<html>Access Denied</html>");

            var generator = new DashboardGenerator(Path.Combine(templateDir, "index.html"));
            var data = CreateMinimalDashboardData();

            await generator.GenerateAsync(data, _outputDir);

            // Verify functions directory was copied
            Assert.True(Directory.Exists(Path.Combine(_outputDir, "functions")));
            Assert.True(File.Exists(Path.Combine(_outputDir, "functions", "_middleware.js")));
            Assert.True(File.Exists(Path.Combine(_outputDir, "functions", "auth", "callback.js")));
            Assert.True(File.Exists(Path.Combine(_outputDir, "access-denied.html")));
        }
        finally
        {
            Directory.Delete(templateDir, recursive: true);
        }
    }

    private static DashboardData CreateMinimalDashboardData()
    {
        return new DashboardData
        {
            GeneratedAt = DateTime.UtcNow,
            Repository = "test-repo"
        };
    }
}
