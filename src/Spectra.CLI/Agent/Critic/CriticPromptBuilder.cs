using System.Text;
using System.Text.Json;
using Spectra.CLI.Prompts;
using Spectra.Core.Models;

namespace Spectra.CLI.Agent.Critic;

/// <summary>
/// Builds prompts for critic model verification.
/// </summary>
public sealed class CriticPromptBuilder
{
    private const int MaxDocumentChars = 8000;
    private const int MaxDocuments = 5;

    private PromptTemplateLoader? _templateLoader;

    /// <summary>
    /// Sets the template loader for customizable prompts.
    /// </summary>
    public void SetTemplateLoader(PromptTemplateLoader? loader) => _templateLoader = loader;

    /// <summary>
    /// Builds the system prompt for the critic model.
    /// </summary>
    public string BuildSystemPrompt()
    {
        if (_templateLoader is not null)
        {
            var template = _templateLoader.LoadTemplate("critic-verification");
            var values = new Dictionary<string, string>
            {
                ["test_case"] = "",
                ["source_document"] = "",
                ["acceptance_criteria"] = ""
            };
            return PromptTemplateLoader.Resolve(template, values);
        }

        return """
            You are a test case verification expert. Your job is to verify that test cases are grounded in source documentation.

            For each test case, analyze every claim (preconditions, steps, expected results) and determine if it can be traced to the provided documentation.

            Output format: Return ONLY a JSON object with this structure:
            {
              "verdict": "grounded" | "partial" | "hallucinated",
              "score": 0.0-1.0,
              "findings": [
                {
                  "element": "Step 1" | "Expected Result" | "Precondition",
                  "claim": "The specific claim being checked",
                  "status": "grounded" | "unverified" | "hallucinated",
                  "evidence": "Quote from documentation (if grounded)" | null,
                  "reason": "Why unverified or hallucinated (if not grounded)" | null
                }
              ]
            }

            Verdict rules:
            - "grounded": ALL claims can be traced to documentation
            - "partial": SOME claims are verified, but others cannot be confirmed
            - "hallucinated": The test contains invented behaviors or contradicts documentation

            Be strict but fair:
            - Generic UI actions (click, navigate, enter text) don't need documentation
            - Specific behaviors, values, or business rules MUST be in documentation
            - If documentation is vague, mark as "unverified" not "hallucinated"
            - "hallucinated" is reserved for clear inventions or contradictions
            """;
    }

    /// <summary>
    /// Builds the user prompt with test case and documentation context.
    /// </summary>
    public string BuildUserPrompt(TestCase test, IReadOnlyList<SourceDocument> documents)
    {
        var sb = new StringBuilder();

        sb.AppendLine("## Test Case to Verify");
        sb.AppendLine();
        sb.AppendLine($"**ID**: {test.Id}");
        sb.AppendLine($"**Title**: {test.Title}");
        sb.AppendLine();

        if (!string.IsNullOrWhiteSpace(test.Preconditions))
        {
            sb.AppendLine("**Preconditions**:");
            sb.AppendLine(test.Preconditions);
            sb.AppendLine();
        }

        sb.AppendLine("**Steps**:");
        for (var i = 0; i < test.Steps.Count; i++)
        {
            sb.AppendLine($"{i + 1}. {test.Steps[i]}");
        }
        sb.AppendLine();

        sb.AppendLine("**Expected Result**:");
        sb.AppendLine(test.ExpectedResult);
        sb.AppendLine();

        if (!string.IsNullOrWhiteSpace(test.TestData))
        {
            sb.AppendLine("**Test Data**:");
            sb.AppendLine(test.TestData);
            sb.AppendLine();
        }

        sb.AppendLine("---");
        sb.AppendLine();
        sb.AppendLine("## Source Documentation");
        sb.AppendLine();

        var docsToInclude = SelectRelevantDocuments(documents, test);

        if (docsToInclude.Count == 0)
        {
            sb.AppendLine("*No relevant documentation provided.*");
        }
        else
        {
            foreach (var doc in docsToInclude)
            {
                sb.AppendLine($"### {doc.Title ?? doc.Path}");
                sb.AppendLine($"**Path**: {doc.Path}");
                sb.AppendLine();

                var content = TruncateContent(doc.Content, MaxDocumentChars);
                sb.AppendLine(content);
                sb.AppendLine();
            }
        }

        sb.AppendLine("---");
        sb.AppendLine();
        sb.AppendLine("Verify this test case against the documentation. Return JSON only:");

        return sb.ToString();
    }

    /// <summary>
    /// Selects the most relevant documents for a test case.
    /// </summary>
    private static List<SourceDocument> SelectRelevantDocuments(
        IReadOnlyList<SourceDocument> documents,
        TestCase test)
    {
        // Prioritize documents referenced in source_refs
        var referenced = new List<SourceDocument>();
        var other = new List<SourceDocument>();

        foreach (var doc in documents)
        {
            if (test.SourceRefs.Any(r =>
                doc.Path.Contains(r, StringComparison.OrdinalIgnoreCase) ||
                r.Contains(doc.Path, StringComparison.OrdinalIgnoreCase)))
            {
                referenced.Add(doc);
            }
            else
            {
                other.Add(doc);
            }
        }

        // Take referenced docs first, then fill with others
        var result = referenced.Take(MaxDocuments).ToList();
        var remaining = MaxDocuments - result.Count;

        if (remaining > 0)
        {
            result.AddRange(other.Take(remaining));
        }

        return result;
    }

    /// <summary>
    /// Truncates content to fit within token limits.
    /// </summary>
    private static string TruncateContent(string content, int maxChars)
    {
        if (content.Length <= maxChars)
            return content;

        // Try to truncate at a paragraph boundary
        var truncated = content[..maxChars];
        var lastNewline = truncated.LastIndexOf("\n\n", StringComparison.Ordinal);

        if (lastNewline > maxChars / 2)
        {
            truncated = truncated[..lastNewline];
        }

        return truncated + "\n\n[... content truncated ...]";
    }
}
