using System.Text;
using Spectra.Core.Models;

namespace Spectra.CLI.Agent;

/// <summary>
/// Builds prompts for document-grounded test generation with semantic deduplication.
/// Uses a two-step approach: scenario discovery first, then test generation.
/// </summary>
public static class GroundedPromptBuilder
{
    /// <summary>
    /// Builds the system prompt that enforces document-grounded generation.
    /// </summary>
    public static string BuildSystemPrompt()
    {
        return """
            You are a test case generation expert that creates DOCUMENT-GROUNDED test cases.

            CRITICAL: Do NOT generate generic test patterns. Every test MUST trace to a specific
            behavior, rule, or requirement found in the provided documentation. If a behavior
            is not explicitly described in the docs, do not create a test for it.

            ## TWO-STEP APPROACH

            STEP 1 - SCENARIO DISCOVERY:
            Before generating any test, you must first identify SPECIFIC testable scenarios from
            the documentation. Each scenario must:
            - Reference the exact document section it came from
            - Be specific enough to create a focused test
            - Not overlap with existing tests (check the EXISTING TESTS section)

            STEP 2 - TEST GENERATION:
            For each unique scenario not already covered, generate one focused test case.

            ## OUTPUT FORMAT

            Return ONLY a JSON array of test cases. No markdown, no explanation, just valid JSON.

            Each test case must have this structure:
            {
              "id": "TC-XXX",
              "title": "Short descriptive title that captures the SPECIFIC scenario",
              "priority": "high|medium|low",
              "tags": ["tag1", "tag2"],
              "component": "component-name",
              "preconditions": "Prerequisites for the test",
              "steps": ["Step 1", "Step 2", "Step 3"],
              "expected_result": "What should happen - be SPECIFIC based on the documentation",
              "test_data": "Any test data needed (optional)",
              "source_refs": ["docs/file.md#Section-Name"],
              "scenario_from_doc": "Quote or paraphrase the specific doc content this test verifies",
              "estimated_duration": "5m"
            }

            ## GROUNDING REQUIREMENTS

            1. EVERY test MUST include a "scenario_from_doc" field with the specific behavior/rule
               being tested, quoted or paraphrased from the documentation.

            2. source_refs MUST include the document path AND section name (e.g., "docs/API.md#File-Upload")

            3. The expected_result MUST match what the documentation says should happen, not a
               generic "should work" or "should succeed".

            4. BAD example: "Verify login functionality" (too generic, not grounded)
               GOOD example: "Verify 401 response when JWT token is expired (per API_DOC.md#Authentication)"

            ## DEDUPLICATION

            Before generating each test, check if a semantically similar test already exists:
            - Compare titles for similar meaning (not just exact match)
            - Compare what the test is verifying
            - If an existing test covers the same scenario with similar steps, SKIP IT

            ## GUIDELINES

            - Generate unique test IDs in TC-XXX format (use numbers 100+)
            - Cover the SPECIFIC scenarios mentioned in the docs: exact error codes, limits,
              timeouts, validation rules, etc.
            - Steps should be clear and actionable
            - When asked for "full coverage", extract EVERY testable scenario from the docs

            ## Creating Test Cases from Undocumented Behavior

            When a user explicitly describes behavior that is NOT in the documentation and asks you
            to create a test for it, follow these steps:

            ### Step 1: Understand the Behavior
            Ask clarifying questions, but ONLY ask what is missing from the user's description.
            The questions you may need to ask cover:
            - Screen or module where the behavior occurs
            - Steps to trigger the behavior
            - Expected result
            - Preconditions or setup required
            - Priority (high/medium/low)
            - Which test suite this belongs to
            If the user's description is detailed enough to cover these aspects, do NOT ask questions.
            Skip straight to duplicate detection.

            ### Step 2: Check for Duplicates
            Before generating the test, search existing tests using the find_test_cases tool with
            keywords extracted from the user's behavior description.

            - If an EXACT duplicate is found (same behavior, same steps, same expected result):
              Offer three options:
              1. Update the existing test with any new details
              2. Create a new, separate test anyway
              3. Cancel and take no action
              Wait for the user to choose before proceeding.

            - If a SIMILAR (but not exact) test is found:
              Show the existing test to the user with an explanation of how it differs, then
              proceed with generating the new test unless the user says otherwise.

            - If NO matches are found:
              Proceed silently to test generation without mentioning the search.

            ### Step 3: Generate the Test Case
            Generate the test case in standard SPECTRA format, but with these specific field values:
            - source_refs: [] (empty array — there is no documentation source)
            - grounding.verdict: "manual"
            - grounding.source: "user-described"
            - grounding.created_by: the current user identity
            - grounding.note: a brief sentence of context about why this test was created from
              a user description rather than documentation

            ### Step 4: Show Draft for Review
            Present the complete draft test case to the user for review and confirmation.
            Do NOT write the test file until the user explicitly confirms the draft is acceptable.
            Wait for the user to approve, request changes, or cancel.

            ### Step 5: Save and Show Documentation Reminder
            After saving the test file, always display this reminder:
            "This test has no documentation source. Consider updating docs to include this behavior."

            ## When NOT to Use the Undocumented Behavior Flow

            - If the user says "generate tests from docs" → use normal document-grounded generation
            - If the user provides a document path or URL → use document-based generation
            - Only use the undocumented behavior flow when the user explicitly describes a behavior
              that is NOT covered in existing documentation and asks you to create a test for it
            """;
    }

    /// <summary>
    /// Builds the user prompt with full document content and existing tests for semantic dedup.
    /// </summary>
    public static string BuildUserPrompt(
        string userRequest,
        IReadOnlyList<SourceDocument> documents,
        IReadOnlyList<TestCase> existingTests,
        int requestedCount)
    {
        var sb = new StringBuilder();

        sb.AppendLine("# TEST GENERATION REQUEST");
        sb.AppendLine();
        sb.AppendLine(userRequest);
        sb.AppendLine();
        sb.AppendLine($"Generate exactly {requestedCount} new test cases that are NOT duplicates of existing tests.");
        sb.AppendLine();

        // Add existing tests with full details for semantic comparison
        if (existingTests.Count > 0)
        {
            sb.AppendLine("# EXISTING TESTS (do NOT duplicate these - check semantically, not just by title)");
            sb.AppendLine();

            foreach (var test in existingTests)
            {
                sb.AppendLine($"## {test.Id}: {test.Title}");
                if (test.Steps.Count > 0)
                {
                    sb.AppendLine("Steps:");
                    foreach (var step in test.Steps)
                    {
                        sb.AppendLine($"  - {step}");
                    }
                }
                if (!string.IsNullOrWhiteSpace(test.ExpectedResult))
                {
                    sb.AppendLine($"Expected: {test.ExpectedResult}");
                }
                sb.AppendLine();
            }
        }

        // Add full document content
        sb.AppendLine("# SOURCE DOCUMENTATION");
        sb.AppendLine();
        sb.AppendLine("Read the following documentation carefully. Extract SPECIFIC testable scenarios");
        sb.AppendLine("from each document. Only generate tests for behaviors explicitly described here.");
        sb.AppendLine();

        foreach (var doc in documents)
        {
            sb.AppendLine($"## Document: {doc.Path}");
            if (doc.Sections.Count > 0)
            {
                sb.AppendLine($"Sections: {string.Join(", ", doc.Sections)}");
            }
            sb.AppendLine();
            sb.AppendLine("```");
            sb.AppendLine(doc.Content);
            sb.AppendLine("```");
            sb.AppendLine();
        }

        sb.AppendLine("# INSTRUCTIONS");
        sb.AppendLine();
        sb.AppendLine("1. Read each document and identify specific testable scenarios");
        sb.AppendLine("2. For each scenario, check if it's already covered by existing tests");
        sb.AppendLine("3. Generate tests ONLY for scenarios not already covered");
        sb.AppendLine("4. Include the exact document section in source_refs");
        sb.AppendLine("5. Include scenario_from_doc with the specific doc content being tested");
        sb.AppendLine();
        sb.AppendLine("Generate the test cases as a JSON array:");

        return sb.ToString();
    }

    /// <summary>
    /// Builds a deduplication report for existing tests.
    /// </summary>
    public static string BuildExistingTestsSummary(IReadOnlyList<TestCase> existingTests)
    {
        if (existingTests.Count == 0)
        {
            return "No existing tests.";
        }

        var sb = new StringBuilder();
        sb.AppendLine($"Found {existingTests.Count} existing tests:");

        // Group by semantic similarity for easier comparison
        var byComponent = existingTests
            .GroupBy(t => t.Component ?? "general")
            .OrderBy(g => g.Key);

        foreach (var group in byComponent)
        {
            sb.AppendLine($"\n## {group.Key}:");
            foreach (var test in group)
            {
                sb.AppendLine($"  - {test.Id}: {test.Title}");
            }
        }

        return sb.ToString();
    }
}
