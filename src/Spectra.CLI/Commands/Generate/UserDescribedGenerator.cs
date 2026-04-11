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
    /// Builds the AI prompt used to format a user's description into a test case.
    /// Public/static so unit tests can verify prompt structure without invoking AI.
    /// </summary>
    public static string BuildPrompt(
        string description,
        string? context,
        string suite,
        IReadOnlyCollection<string> existingIds,
        string? documentContext = null,
        string? criteriaContext = null)
    {
        var idList = string.Join(", ", existingIds);
        var contextLine = string.IsNullOrEmpty(context) ? "" : $"\nAdditional context: {context}";

        var prompt = $"""
            Create a single manual test case for the '{suite}' feature based on this tester's description.

            **The user's description is the source of truth.** Use any reference material below ONLY to align terminology, navigation paths, and known acceptance criteria — never to override or contradict the description.

            Description: {description}{contextLine}

            Requirements:
            - Create a unique ID in format TC-XXX (do not duplicate: {idList})
            - Include clear steps and expected results
            - Set priority based on the described behavior's criticality
            - This is a user-described test — include relevant tags

            Provide: id, title, priority, steps, expected_result, tags, component (if inferable)
            """;

        if (!string.IsNullOrWhiteSpace(documentContext))
        {
            prompt += $"""


                ## Reference Documentation (for formatting context only)

                The following documentation describes the product area related to this test.
                Use it to align your test steps with actual product behavior, terminology,
                and navigation paths. Do NOT verify the user's description against these docs —
                the user's description is the source of truth.

                {documentContext}
                """;
        }

        if (!string.IsNullOrWhiteSpace(criteriaContext))
        {
            prompt += $"""


                ## Related Acceptance Criteria

                The following acceptance criteria are defined for this area.
                If the user's test maps to any of these criteria, include the matching
                criteria IDs in the test's `criteria` frontmatter field.

                {criteriaContext}
                """;
        }

        return prompt;
    }

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
        CancellationToken ct = default,
        string? documentContext = null,
        string? criteriaContext = null,
        IReadOnlyList<string>? sourceRefPaths = null)
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

        var prompt = BuildPrompt(description, context, suite, existingIds, documentContext, criteriaContext);

        var result = await agent.GenerateTestsAsync(
            prompt,
            [],
            [],
            1,
            criteriaContext: null,
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
            SourceRefs = sourceRefPaths is { Count: > 0 } ? sourceRefPaths : test.SourceRefs,
            RelatedWorkItems = test.RelatedWorkItems,
            Custom = test.Custom,
            Steps = test.Steps,
            ExpectedResult = test.ExpectedResult,
            TestData = test.TestData,
            Criteria = test.Criteria,
            FilePath = test.FilePath,
            Grounding = new GroundingMetadata
            {
                Verdict = VerificationVerdict.Manual,
                Score = 1.0,
                Generator = config.Ai.Providers?.FirstOrDefault(p => p.Enabled)?.Model ?? agent.ProviderName,
                Critic = "user-described",
                VerifiedAt = DateTimeOffset.UtcNow
            }
        };
    }
}
