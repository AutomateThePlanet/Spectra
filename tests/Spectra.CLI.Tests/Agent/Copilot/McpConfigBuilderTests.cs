using GitHub.Copilot.SDK;
using Spectra.CLI.Agent.Copilot;
using Spectra.Core.Models.Config;

namespace Spectra.CLI.Tests.Agent.Copilot;

/// <summary>
/// v1.46.0: translation from <see cref="TestimizeConfig"/> to the Copilot
/// SDK's <see cref="McpLocalServerConfig"/>.
/// </summary>
public class McpConfigBuilderTests
{
    [Fact]
    public void BuildTestimizeServer_NullConfig_ReturnsNull()
    {
        Assert.Null(McpConfigBuilder.BuildTestimizeServer(null));
    }

    [Fact]
    public void BuildTestimizeServer_Disabled_ReturnsNull()
    {
        var cfg = new TestimizeConfig { Enabled = false };
        Assert.Null(McpConfigBuilder.BuildTestimizeServer(cfg));
    }

    [Fact]
    public void BuildTestimizeServer_Enabled_ReturnsLocalConfig()
    {
        var cfg = new TestimizeConfig
        {
            Enabled = true,
            Mcp = new TestimizeMcpConfig
            {
                Command = "testimize-mcp",
                Args = new[] { "--mcp" }
            }
        };

        var result = McpConfigBuilder.BuildTestimizeServer(cfg);

        Assert.NotNull(result);
        Assert.Equal("local", result!.Type);
        Assert.Equal("testimize-mcp", result.Command);
        Assert.Equal(new List<string> { "--mcp" }, result.Args);
    }

    [Fact]
    public void BuildTestimizeServer_Enabled_SetsToolsToWildcard()
    {
        var cfg = new TestimizeConfig
        {
            Enabled = true,
            Mcp = new TestimizeMcpConfig
            {
                Command = "testimize-mcp",
                Args = new[] { "--mcp" }
            }
        };

        var result = McpConfigBuilder.BuildTestimizeServer(cfg);

        Assert.NotNull(result);
        Assert.Equal(new List<string> { "*" }, result!.Tools);
    }

    [Fact]
    public void BuildTestimizeServer_Enabled_Sets30sTimeout()
    {
        var cfg = new TestimizeConfig
        {
            Enabled = true,
            Mcp = new TestimizeMcpConfig
            {
                Command = "testimize-mcp",
                Args = new[] { "--mcp" }
            }
        };

        var result = McpConfigBuilder.BuildTestimizeServer(cfg);

        Assert.NotNull(result);
        Assert.Equal(30_000, result!.Timeout);
    }

    [Fact]
    public void BuildTestimizeServer_CustomCommand_PassesThrough()
    {
        var cfg = new TestimizeConfig
        {
            Enabled = true,
            Mcp = new TestimizeMcpConfig
            {
                Command = @"C:\custom\path\testimize-mcp.exe",
                Args = new[] { "--mcp", "--verbose" }
            }
        };

        var result = McpConfigBuilder.BuildTestimizeServer(cfg);

        Assert.NotNull(result);
        Assert.Equal(@"C:\custom\path\testimize-mcp.exe", result!.Command);
        Assert.Equal(new List<string> { "--mcp", "--verbose" }, result.Args);
    }

    [Fact]
    public void BuildTestimizeServer_NullArgs_ReturnsEmptyArgsList()
    {
        var cfg = new TestimizeConfig
        {
            Enabled = true,
            Mcp = new TestimizeMcpConfig
            {
                Command = "testimize-mcp",
                Args = null!
            }
        };

        var result = McpConfigBuilder.BuildTestimizeServer(cfg);

        Assert.NotNull(result);
        Assert.NotNull(result!.Args);
        Assert.Empty(result.Args);
    }
}
