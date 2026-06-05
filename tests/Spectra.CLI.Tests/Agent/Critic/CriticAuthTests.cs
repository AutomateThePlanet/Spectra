using Spectra.CLI.Agent.Critic;
using Spectra.Core.Models;
using Spectra.Core.Models.Config;

namespace Spectra.CLI.Tests.Agent.Critic;

/// <summary>
/// Tests for CriticFactory and CriticCreateResult after Spec 058 retired the in-process critic.
/// <see cref="CriticFactory.TryCreate"/> now always reports the in-process critic unavailable —
/// verification of record runs as the spectra-critic subagent.
/// </summary>
public class CriticAuthTests
{
    [Fact]
    public void TryCreate_EnabledConfig_ReportsSubagent()
    {
        // Spec 058: the in-process critic is retired; the factory never constructs one.
        var config = new CriticConfig { Enabled = true };

        var result = CriticFactory.TryCreate(config);

        Assert.False(result.Success);
        Assert.Null(result.Critic);
        Assert.Equal("subagent", result.ProviderName);
        Assert.Contains("subagent", result.ErrorMessage ?? "", StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TryCreate_NullConfig_ReportsSubagent()
    {
        var result = CriticFactory.TryCreate(null);

        Assert.False(result.Success);
        Assert.Equal("subagent", result.ProviderName);
    }

    [Fact]
    public void CriticCreateResult_Succeeded_HasCorrectProperties()
    {
        var mockCritic = new MockCritic();
        var result = CriticCreateResult.Succeeded(mockCritic, "test-provider");

        Assert.True(result.Success);
        Assert.NotNull(result.Critic);
        Assert.Equal("test-provider", result.ProviderName);
        Assert.Null(result.ErrorMessage);
    }

    [Fact]
    public void CriticCreateResult_Failed_HasCorrectProperties()
    {
        var result = CriticCreateResult.Failed("test-provider", "Test error", "Help 1", "Help 2");

        Assert.False(result.Success);
        Assert.Null(result.Critic);
        Assert.Equal("test-provider", result.ProviderName);
        Assert.Equal("Test error", result.ErrorMessage);
        Assert.Equal(2, result.HelpInstructions.Count);
    }

    [Fact]
    public void CriticCreateResult_Failed_EmptyHelp_HasEmptyList()
    {
        var result = CriticCreateResult.Failed("provider", "Error");

        Assert.False(result.Success);
        Assert.Empty(result.HelpInstructions);
    }

    [Fact]
    public async Task MockCritic_IsAvailable_ReturnsTrue()
    {
        var critic = new MockCritic();
        Assert.True(await critic.IsAvailableAsync(CancellationToken.None));
    }

    [Fact]
    public void MockCritic_ModelName_ReturnsExpected()
    {
        var critic = new MockCritic();
        Assert.Equal("mock-model", critic.ModelName);
    }

    private sealed class MockCritic : ICriticRuntime
    {
        public string ModelName => "mock-model";

        public Task<bool> IsAvailableAsync(CancellationToken ct) => Task.FromResult(true);

        public Task<Spectra.Core.Models.Grounding.VerificationResult> VerifyTestAsync(
            Spectra.Core.Models.TestCase test,
            IReadOnlyList<SourceDocument> sources,
            CancellationToken ct) =>
            Task.FromResult(new Spectra.Core.Models.Grounding.VerificationResult
            {
                Verdict = Spectra.Core.Models.Grounding.VerificationVerdict.Grounded,
                Score = 1.0,
                CriticModel = ModelName,
                Findings = []
            });
    }
}
