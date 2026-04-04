using Spectra.CLI.Agent;
using Spectra.CLI.Agent.Copilot;
using Spectra.Core.Models;
using Spectra.Core.Models.Config;
using Spectra.Core.Models.Grounding;

namespace Spectra.CLI.Commands.Generate;

/// <summary>
/// Generates a structured test case from a user's plain-language description.
/// </summary>
public sealed class UserDescribedGenerator
{
    /// <summary>
    /// Creates a test case from a description, with grounding.verdict = manual.
    /// </summary>
    public async Task<TestCase?> GenerateAsync(
        string description,
        string? context,
        string suite,
        IReadOnlyCollection<string> existingIds,
        SpectraConfig config,
        string currentDir,
        string testsPath,
        Action<string>? onStatus,
        CancellationToken ct = default)
    {
        var createResult = await AgentFactory.CreateAgentAsync(
            config,
            currentDir,
            testsPath,
            status => onStatus?.Invoke(status),
            ct);

        if (!createResult.Success)
            return null;

        var agent = createResult.Agent!;

        if (!await agent.IsAvailableAsync(ct))
            return null;

        var idList = string.Join(", ", existingIds);
        var contextLine = string.IsNullOrEmpty(context) ? "" : $"\nAdditional context: {context}";

        var prompt = $"""
            Create a single manual test case for the '{suite}' feature based on this tester's description:

            Description: {description}{contextLine}

            Requirements:
            - Create a unique ID in format TC-XXX (do not duplicate: {idList})
            - Include clear steps and expected results
            - Set priority based on the described behavior's criticality
            - This is a user-described test — include relevant tags

            Provide: id, title, priority, steps, expected_result, tags, component (if inferable)
            """;

        var result = await agent.GenerateTestsAsync(
            prompt,
            [],
            [],
            1,
            ct);

        if (!result.IsSuccess || result.Tests.Count == 0)
            return null;

        var test = result.Tests[0];

        // Apply manual grounding metadata
        return new TestCase
        {
            Id = test.Id,
            Title = test.Title,
            Priority = test.Priority,
            Tags = test.Tags,
            Component = test.Component,
            Preconditions = test.Preconditions,
            Environment = test.Environment,
            EstimatedDuration = test.EstimatedDuration,
            DependsOn = test.DependsOn,
            SourceRefs = [],
            RelatedWorkItems = test.RelatedWorkItems,
            Custom = test.Custom,
            Steps = test.Steps,
            ExpectedResult = test.ExpectedResult,
            TestData = test.TestData,
            FilePath = test.FilePath,
            Grounding = new GroundingMetadata
            {
                Verdict = VerificationVerdict.Manual,
                Score = 1.0,
                Generator = agent.ProviderName,
                Critic = "user-described",
                VerifiedAt = DateTimeOffset.UtcNow
            }
        };
    }
}
