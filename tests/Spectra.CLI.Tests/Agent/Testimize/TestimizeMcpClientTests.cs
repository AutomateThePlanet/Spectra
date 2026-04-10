using Spectra.CLI.Agent.Testimize;
using Spectra.Core.Models.Config;

namespace Spectra.CLI.Tests.Agent.Testimize;

/// <summary>
/// Spec 038: TestimizeMcpClient must never throw — every failure mode collapses
/// to "return false / return null" so callers can fall back gracefully.
/// </summary>
public class TestimizeMcpClientTests
{
    [Fact]
    public async Task StartAsync_NotInstalled_ReturnsFalseWithoutThrowing()
    {
        await using var client = new TestimizeMcpClient();
        var config = new TestimizeMcpConfig
        {
            Command = "definitely-not-a-real-command-spectra-038",
            Args = ["--mcp"]
        };

        var started = await client.StartAsync(config);

        Assert.False(started);
    }

    [Fact]
    public async Task IsHealthyAsync_NeverStarted_ReturnsFalse()
    {
        await using var client = new TestimizeMcpClient();
        Assert.False(await client.IsHealthyAsync());
    }

    [Fact]
    public async Task DisposeAsync_NeverStarted_DoesNotThrow()
    {
        var client = new TestimizeMcpClient();
        await client.DisposeAsync();
        // No exception
    }

    [Fact]
    public async Task DisposeAsync_CalledTwice_DoesNotThrow()
    {
        var client = new TestimizeMcpClient();
        await client.DisposeAsync();
        await client.DisposeAsync();
        // No exception
    }
}
