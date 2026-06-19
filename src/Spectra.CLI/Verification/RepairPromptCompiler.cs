using System.Text;
using Spectra.Core.Models;
using Spectra.Core.Models.Grounding;

namespace Spectra.CLI.Verification;

/// <summary>
/// Deterministic, model-free repair-prompt compiler (Spec 071 FR4). Emits a plain-text repair
/// prompt injecting the original test artifact, the critic's non-grounded findings, and the
/// relevant source docs. Mirrors CriticPromptCompiler in structure; never calls a model.
/// </summary>
public static class RepairPromptCompiler
{
    private const int MaxDocsPerRepair = 5;
    private const int MaxDocChars = 8000;

    /// <summary>
    /// Compiles a plain-text repair prompt for a partial test.
    /// Refuses (returns null + reason) if the test has no id/title, or if findings are empty.
    /// </summary>
    public static (string? Prompt, string? RefusalReason) Compile(
        TestCase test,
        IReadOnlyList<CriticFinding> nonGroundedFindings,
        IReadOnlyList<SourceDocument> sourceDocs)
    {
        if (string.IsNullOrWhiteSpace(test.Id) || string.IsNullOrWhiteSpace(test.Title))
            return (null, "Test artifact is missing id or title.");

        if (nonGroundedFindings.Count == 0)
            return (null, "No non-grounded findings to repair — the critic marked all claims as grounded.");

        var sb = new StringBuilder();
        sb.AppendLine("# SPECTRA Test Repair");
        sb.AppendLine();
        sb.AppendLine("You are correcting a generated test that the critic found partially ungrounded.");
        sb.AppendLine($"Your task: rewrite ONLY the elements listed in the critic findings below.");
        sb.AppendLine($"Preserve the test id ({test.Id}), priority, component, tags, and any elements the critic found grounded.");
        sb.AppendLine("Return a JSON array containing the ONE corrected test, using the same schema as the generation output.");
        sb.AppendLine();

        // Test artifact
        sb.AppendLine($"## Test to repair: {test.Id}");
        sb.AppendLine();
        sb.AppendLine($"**Title**: {test.Title}");

        if (!string.IsNullOrWhiteSpace(test.Preconditions))
        {
            sb.AppendLine($"**Preconditions**:");
            sb.AppendLine(test.Preconditions);
        }

        sb.AppendLine("**Steps**:");
        for (var i = 0; i < test.Steps.Count; i++)
            sb.AppendLine($"{i + 1}. {test.Steps[i]}");

        sb.AppendLine($"**Expected Result**: {test.ExpectedResult}");

        if (!string.IsNullOrWhiteSpace(test.TestData))
        {
            sb.AppendLine($"**Test Data**: {test.TestData}");
        }

        sb.AppendLine();
        sb.AppendLine("---");
        sb.AppendLine();

        // Critic findings
        sb.AppendLine("## Critic findings (elements that could not be grounded)");
        sb.AppendLine();
        sb.AppendLine("The critic identified these specific elements as unverified against the documentation:");
        sb.AppendLine();
        for (var i = 0; i < nonGroundedFindings.Count; i++)
        {
            var f = nonGroundedFindings[i];
            var statusLabel = f.Status == FindingStatus.Hallucinated ? "HALLUCINATED" : "UNVERIFIED";
            sb.AppendLine($"{i + 1}. **{f.Element}** [{statusLabel}]");
            sb.AppendLine($"   Claim: \"{f.Claim}\"");
            if (!string.IsNullOrWhiteSpace(f.Reason))
                sb.AppendLine($"   Reason: {f.Reason}");
        }

        sb.AppendLine();
        sb.AppendLine("---");
        sb.AppendLine();

        // Source documentation
        if (sourceDocs.Count > 0)
        {
            sb.AppendLine("## Source documentation");
            sb.AppendLine();
            foreach (var doc in sourceDocs.Take(MaxDocsPerRepair))
            {
                sb.AppendLine($"### {doc.Title}");
                sb.AppendLine($"**Path**: {doc.Path}");
                sb.AppendLine();
                var content = doc.Content.Length > MaxDocChars
                    ? doc.Content[..MaxDocChars] + $"\n... [truncated at {MaxDocChars} chars]"
                    : doc.Content;
                sb.AppendLine(content);
                sb.AppendLine();
            }
            sb.AppendLine("---");
            sb.AppendLine();
        }
        else
        {
            sb.AppendLine("## Source documentation");
            sb.AppendLine();
            sb.AppendLine("_No source documents available for this test. Correct the identified elements based on your knowledge of the test domain, keeping changes minimal and targeted._");
            sb.AppendLine();
            sb.AppendLine("---");
            sb.AppendLine();
        }

        // Instructions
        sb.AppendLine("## Instructions");
        sb.AppendLine();
        sb.AppendLine($"1. Rewrite ONLY the elements listed under \"Critic findings\" — do NOT change other elements.");
        sb.AppendLine($"2. For each ungrounded element, find the actual correct value in the source documentation above.");
        sb.AppendLine($"3. Preserve: id ({test.Id}), priority, component, tags, title (unless wrong), and all grounded steps/elements.");
        sb.AppendLine($"4. Do NOT invent new values — only use values traceable to the documentation.");
        sb.AppendLine($"5. Return a JSON array containing the ONE corrected test, using the same schema as `spectra ai ingest-tests` expects.");

        return (sb.ToString(), null);
    }
}
