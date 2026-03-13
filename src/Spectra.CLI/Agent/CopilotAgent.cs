using Spectra.Core.Models;
using Spectra.Core.Models.Config;

namespace Spectra.CLI.Agent;

/// <summary>
/// GitHub Copilot SDK agent implementation.
/// This is a placeholder that will be integrated with the actual Copilot SDK.
/// </summary>
public sealed class CopilotAgent : IAgentRuntime
{
    private readonly AiConfig _config;

    public string ProviderName => "github-copilot";

    public CopilotAgent(AiConfig? config = null)
    {
        _config = config ?? new AiConfig { Providers = [] };
    }

    public async Task<bool> IsAvailableAsync(CancellationToken ct = default)
    {
        // TODO: Check if Copilot SDK is available
        // For now, return true to allow local development
        await Task.CompletedTask;
        return true;
    }

    public async Task<GenerationResult> GenerateTestsAsync(
        string prompt,
        DocumentMap documentMap,
        IReadOnlyList<TestCase> existingTests,
        CancellationToken ct = default)
    {
        // TODO: Implement actual Copilot SDK integration
        // This is a stub that returns an empty result

        await Task.Delay(100, ct); // Simulate API call

        // For now, return an error indicating that the SDK is not configured
        return new GenerationResult
        {
            Tests = [],
            Errors =
            [
                "GitHub Copilot SDK integration not yet implemented. " +
                "Configure an alternative provider or implement the Copilot SDK integration."
            ]
        };
    }
}
