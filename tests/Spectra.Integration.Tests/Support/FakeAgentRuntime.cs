using Spectra.CLI.Agent;
using Spectra.Core.Models;
using Spectra.Core.Models.Config;
using Spectra.Core.Models.Testimize;

namespace Spectra.Integration.Tests.Support;

/// <summary>
/// Spec 052: deterministic, offline <see cref="IAgentRuntime"/> for hermetic
/// cross-spec tests. Records the <c>criteriaContext</c> it receives (proving
/// the Spec 050 forwarding) and returns a caller-supplied test case. Plugged in
/// via the <c>agentFactory</c> seam on
/// <see cref="Spectra.CLI.Commands.Generate.UserDescribedGenerator.GenerateAsync"/>.
/// </summary>
public sealed class FakeAgentRuntime : IAgentRuntime
{
    private readonly Func<TestCase> _testFactory;

    public FakeAgentRuntime(Func<TestCase> testFactory) => _testFactory = testFactory;

    /// <summary>The criteria context passed to the most recent generation call.</summary>
    public string? LastCriteriaContext { get; private set; }

    /// <summary>True when the last generation call received a non-empty criteria block.</summary>
    public bool ReceivedCriteria => !string.IsNullOrWhiteSpace(LastCriteriaContext);

    public int CallCount { get; private set; }

    public string ProviderName => "fake";

    public Task<bool> IsAvailableAsync(CancellationToken ct = default) => Task.FromResult(true);

    public Task<GenerationResult> GenerateTestsAsync(
        string prompt,
        IReadOnlyList<SourceDocument> documents,
        IReadOnlyList<TestCase> existingTests,
        int requestedCount,
        string? criteriaContext = null,
        TestimizeDataset? testimizeData = null,
        CancellationToken ct = default)
    {
        CallCount++;
        LastCriteriaContext = criteriaContext;
        return Task.FromResult(new GenerationResult { Tests = new[] { _testFactory() } });
    }

    /// <summary>Matches the <c>agentFactory</c> delegate shape so it can be passed directly.</summary>
    public Task<AgentCreateResult> CreateAsync(
        SpectraConfig config,
        string basePath,
        string testsPath,
        Action<string>? onStatus,
        CancellationToken ct)
        => Task.FromResult(AgentCreateResult.Succeeded(this));
}
