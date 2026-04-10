using System.Text.Json;
using Spectra.CLI.Agent.Testimize;
using Spectra.Core.Models.Config;

namespace Spectra.CLI.Tests.Agent.Testimize;

/// <summary>
/// Spec 038 US3: graceful-degradation paths in TestimizeMcpClient.
/// Every failure mode must collapse to "return false / return null", never throw.
/// </summary>
public class TestimizeMcpClientGracefulTests
{
    [Fact]
    public async Task CallToolAsync_NeverStarted_ReturnsNullWithoutThrowing()
    {
        await using var client = new TestimizeMcpClient();
        var paramsElement = JsonSerializer.SerializeToElement(new { });

        var result = await client.CallToolAsync("any_tool", paramsElement);

        Assert.Null(result);
    }

    [Fact]
    public async Task StartAsync_BogusCommand_ReturnsFalseAndDisposeIsClean()
    {
        var client = new TestimizeMcpClient();
        var config = new TestimizeMcpConfig
        {
            Command = "this-tool-does-not-exist-spectra-038-test",
            Args = ["--mcp"]
        };

        var started = await client.StartAsync(config);
        Assert.False(started);

        // Even after a failed start, dispose must be safe
        await client.DisposeAsync();
    }

    [Fact]
    public async Task StartAsync_ProcessExitsImmediately_HealthCheckFalse()
    {
        var client = new TestimizeMcpClient();

        // Use `cmd /c exit 1` (Windows) or `false` (Unix). Either way the
        // process exits with non-zero. Whether StartAsync catches that within
        // its 150ms grace window or not, IsHealthyAsync MUST return false
        // shortly after — the contract is "graceful degradation", not "always
        // detect race conditions perfectly".
        var config = OperatingSystem.IsWindows()
            ? new TestimizeMcpConfig { Command = "cmd", Args = ["/c", "exit", "1"] }
            : new TestimizeMcpConfig { Command = "false", Args = [] };

        await client.StartAsync(config);
        // Give the dying process a moment to actually exit.
        await Task.Delay(300);

        var healthy = await client.IsHealthyAsync();
        Assert.False(healthy);

        await client.DisposeAsync();
    }

    [Fact]
    public async Task IsHealthyAsync_StartedButCrashed_ReturnsFalse()
    {
        // Start with a process that will be dead by the time we probe.
        var client = new TestimizeMcpClient();
        var config = OperatingSystem.IsWindows()
            ? new TestimizeMcpConfig { Command = "cmd", Args = ["/c", "exit", "0"] }
            : new TestimizeMcpConfig { Command = "true", Args = [] };

        await client.StartAsync(config);
        // Whether StartAsync returned true or false, the probe must be safe
        var healthy = await client.IsHealthyAsync();
        Assert.False(healthy);

        await client.DisposeAsync();
    }
}
