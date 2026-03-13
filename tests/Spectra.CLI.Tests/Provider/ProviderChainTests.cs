using Spectra.CLI.Agent;
using Spectra.CLI.Provider;
using Spectra.Core.Models;
using Spectra.Core.Models.Config;

namespace Spectra.CLI.Tests.Provider;

public class ProviderChainTests
{
    private static ProviderConfig CreateProvider(string name, int priority = 1, bool enabled = true)
    {
        return new ProviderConfig
        {
            Name = name,
            Model = "test-model",
            Priority = priority,
            Enabled = enabled
        };
    }

    private class MockAgentRuntime : IAgentRuntime
    {
        public string ProviderName { get; }
        public bool IsAvailable { get; set; } = true;
        public Exception? ThrowOnGenerate { get; set; }

        public MockAgentRuntime(string name)
        {
            ProviderName = name;
        }

        public Task<bool> IsAvailableAsync(CancellationToken ct = default)
        {
            return Task.FromResult(IsAvailable);
        }

        public Task<GenerationResult> GenerateTestsAsync(
            string prompt,
            DocumentMap documentMap,
            IReadOnlyList<TestCase> existingTests,
            CancellationToken ct = default)
        {
            if (ThrowOnGenerate is not null)
            {
                throw ThrowOnGenerate;
            }

            return Task.FromResult(new GenerationResult
            {
                Tests = []
            });
        }
    }

    [Fact]
    public async Task ExecuteAsync_NoProviders_ReturnsFailure()
    {
        var chain = new ProviderChain([]);

        var result = await chain.ExecuteAsync<int>((agent, ct) => Task.FromResult(42));

        Assert.False(result.IsSuccess);
        Assert.Contains("No providers configured", result.Error);
    }

    [Fact]
    public async Task ExecuteAsync_FirstProviderSucceeds_ReturnsSuccess()
    {
        var providers = new[] { CreateProvider("provider1") };
        var mockAgent = new MockAgentRuntime("provider1");

        var chain = new ProviderChain(providers, _ => mockAgent);

        var result = await chain.ExecuteAsync<int>((agent, ct) => Task.FromResult(42));

        Assert.True(result.IsSuccess);
        Assert.Equal(42, result.Value);
        Assert.Equal("provider1", result.UsedProvider);
    }

    [Fact]
    public async Task ExecuteAsync_FirstProviderUnavailable_FallsBackToSecond()
    {
        var providers = new[]
        {
            CreateProvider("provider1", priority: 1),
            CreateProvider("provider2", priority: 2)
        };

        var agents = new Dictionary<string, MockAgentRuntime>
        {
            ["provider1"] = new("provider1") { IsAvailable = false },
            ["provider2"] = new("provider2") { IsAvailable = true }
        };

        var chain = new ProviderChain(providers, config => agents[config.Name]);

        var result = await chain.ExecuteAsync<int>((agent, ct) => Task.FromResult(42));

        Assert.True(result.IsSuccess);
        Assert.Equal("provider2", result.UsedProvider);
        Assert.Equal(2, result.Attempts.Count);
    }

    [Fact]
    public async Task ExecuteAsync_FirstProviderThrowsRecoverableError_FallsBackToSecond()
    {
        var providers = new[]
        {
            CreateProvider("provider1", priority: 1),
            CreateProvider("provider2", priority: 2)
        };

        var agents = new Dictionary<string, MockAgentRuntime>
        {
            ["provider1"] = new("provider1") { ThrowOnGenerate = new Exception("Rate limit exceeded") },
            ["provider2"] = new("provider2")
        };

        var chain = new ProviderChain(providers, config => agents[config.Name]);

        // Operation must call agent method to trigger the exception
        var result = await chain.ExecuteAsync<GenerationResult>(async (agent, ct) =>
            await agent.GenerateTestsAsync("test", new DocumentMap { Documents = [], TotalSizeKb = 0 }, [], ct));

        Assert.True(result.IsSuccess);
        Assert.Equal("provider2", result.UsedProvider);
    }

    [Fact]
    public async Task ExecuteAsync_FirstProviderThrowsUnrecoverableError_ReturnsFailure()
    {
        var providers = new[]
        {
            CreateProvider("provider1", priority: 1),
            CreateProvider("provider2", priority: 2)
        };

        // Non-recoverable errors include things like syntax errors or invalid requests
        var agents = new Dictionary<string, MockAgentRuntime>
        {
            ["provider1"] = new("provider1") { ThrowOnGenerate = new ArgumentException("Invalid argument") },
            ["provider2"] = new("provider2")
        };

        var chain = new ProviderChain(providers, config => agents[config.Name]);

        // Operation must call agent method to trigger the exception
        var result = await chain.ExecuteAsync<GenerationResult>(async (agent, ct) =>
            await agent.GenerateTestsAsync("test", new DocumentMap { Documents = [], TotalSizeKb = 0 }, [], ct));

        Assert.False(result.IsSuccess);
        Assert.Contains("Unrecoverable", result.Error);
    }

    [Fact]
    public async Task ExecuteAsync_AllProvidersFail_ReturnsFailure()
    {
        var providers = new[]
        {
            CreateProvider("provider1", priority: 1),
            CreateProvider("provider2", priority: 2)
        };

        var agents = new Dictionary<string, MockAgentRuntime>
        {
            ["provider1"] = new("provider1") { IsAvailable = false },
            ["provider2"] = new("provider2") { IsAvailable = false }
        };

        var chain = new ProviderChain(providers, config => agents[config.Name]);

        var result = await chain.ExecuteAsync<int>((agent, ct) => Task.FromResult(42));

        Assert.False(result.IsSuccess);
        Assert.Contains("All providers failed", result.Error);
        Assert.Equal(2, result.Attempts.Count);
    }

    [Fact]
    public async Task ExecuteAsync_RaisesFallbackEvent()
    {
        var providers = new[]
        {
            CreateProvider("provider1", priority: 1),
            CreateProvider("provider2", priority: 2)
        };

        var agents = new Dictionary<string, MockAgentRuntime>
        {
            ["provider1"] = new("provider1") { ThrowOnGenerate = new Exception("Service unavailable") },
            ["provider2"] = new("provider2")
        };

        var chain = new ProviderChain(providers, config => agents[config.Name]);

        string? fallbackFrom = null;
        string? fallbackTo = null;
        chain.OnFallback += (from, to, reason) =>
        {
            fallbackFrom = from;
            fallbackTo = to;
        };

        // Operation must call agent method to trigger the exception
        var result = await chain.ExecuteAsync<GenerationResult>(async (agent, ct) =>
            await agent.GenerateTestsAsync("test", new DocumentMap { Documents = [], TotalSizeKb = 0 }, [], ct));

        Assert.True(result.IsSuccess);
        Assert.Equal("provider1", fallbackFrom);
        Assert.Equal("provider2", fallbackTo);
    }

    [Fact]
    public async Task ExecuteAsync_RecordsAttemptsCorrectly()
    {
        var providers = new[]
        {
            CreateProvider("provider1", priority: 1),
            CreateProvider("provider2", priority: 2)
        };

        var agents = new Dictionary<string, MockAgentRuntime>
        {
            ["provider1"] = new("provider1") { IsAvailable = false },
            ["provider2"] = new("provider2")
        };

        var chain = new ProviderChain(providers, config => agents[config.Name]);

        var result = await chain.ExecuteAsync<int>((agent, ct) => Task.FromResult(42));

        Assert.Equal(2, result.Attempts.Count);

        var attempt1 = result.Attempts[0];
        Assert.Equal("provider1", attempt1.ProviderName);
        Assert.False(attempt1.Success);
        Assert.NotNull(attempt1.Error);

        var attempt2 = result.Attempts[1];
        Assert.Equal("provider2", attempt2.ProviderName);
        Assert.True(attempt2.Success);
        Assert.Null(attempt2.Error);
    }

    [Fact]
    public async Task ExecuteAsync_CancellationToken_ThrowsOperationCanceled()
    {
        var providers = new[] { CreateProvider("provider1") };
        var mockAgent = new MockAgentRuntime("provider1");
        var chain = new ProviderChain(providers, _ => mockAgent);

        var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            chain.ExecuteAsync<int>((agent, ct) => Task.FromResult(42), cts.Token));
    }

    [Fact]
    public void ChainResult_Success_HasCorrectProperties()
    {
        var result = ChainResult<int>.Success(42, "provider1", []);

        Assert.True(result.IsSuccess);
        Assert.Equal(42, result.Value);
        Assert.Equal("provider1", result.UsedProvider);
        Assert.Null(result.Error);
    }

    [Fact]
    public void ChainResult_Failure_HasCorrectProperties()
    {
        var result = ChainResult<int>.Failure("Test error");

        Assert.False(result.IsSuccess);
        Assert.Equal(default, result.Value);
        Assert.Null(result.UsedProvider);
        Assert.Equal("Test error", result.Error);
    }
}
