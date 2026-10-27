using Spectra.CLI.IO;
using Spectra.Core.Models;
using Spectra.Core.Parsing;

namespace Spectra.CLI.Tests.TestFixtures;

/// <summary>
/// A test-case file with every frontmatter key the parser reads, two unknown keys, a comment, an unknown
/// body section and a step sub-bullet (Spec 078 lossless-write fixture).
/// </summary>
public static class FullFieldTestCase
{
    public const string Id = "TC-100";

    /// <summary>The fixture with LF line endings, whatever the checkout's line-ending setting.</summary>
    public static string Markdown => RawMarkdown.Replace("\r\n", "\n");

    private const string RawMarkdown = """
        ---
        # hand-written header comment
        id: TC-100
        priority: high
        tags:
          - smoke
          - checkout
        component: checkout
        owner: qa-team
        description: "Verifies payment: card declined path"
        environment:
          - staging
          - chrome
        depends_on: TC-099
        source_refs:
          - docs/checkout.md#Payment
        scenario_from_doc: "Card is declined by the gateway"
        estimated_duration: 1h30m15s
        criteria:
          - AC-CHECKOUT-001
          - AC-CHECKOUT-002
        requirements:
          - REQ-12
        related_work_items:
          - AB#1234
        bugs:
          - BUG-77
        x_ticket: {id: 42, system: jira}
        automated_by:
          - tests/e2e/checkout.spec.ts
        custom:
          team: payments
          count: '5'
          enabled: true
          nested:
            level: deep
            items:
              - one
              - two
          notes: |
            first line
            second line
        status: orphaned
        orphaned_reason: "Source documentation no longer exists"
        orphaned_date: 2026-01-15
        grounding:
          verdict: partial
          score: 0.873
          generator: claude-sonnet
          critic: claude-haiku
          verified_at: 2026-02-03T04:05:06.789Z
          unverified_claims:
            - "Gateway returns code 51"
          flagged_for_review: true
          repair_attempts: 1
          repaired: true
          condensed_findings:
            - element: "step 2"
              reason: "code not documented"
          source: manual-review
          created_by: vik
          note: "Checked against gateway spec"
        ---

        # Card declined shows an error

        Some intro text that no parser field captures.

        ## Preconditions

        User is logged in with a saved card.

        ## Steps

        1. Open the checkout page
        2. Pay with the declined test card
           - use card 4000 0000 0000 0002
        3. Observe the error banner

        ## Notes

        Hand-written notes the parser ignores.

        ## Expected Result

        An error banner says the card was declined.

        ## Test Data

        Card: 4000 0000 0000 0002

        """;

    /// <summary>The same content with CRLF line endings.</summary>
    public static string CrlfMarkdown => Markdown.Replace("\r\n", "\n").Replace("\n", "\r\n");

    /// <summary>
    /// The fixture as SPECTRA itself writes it: keys in the fixed order, no unknown keys, no unknown
    /// sections, no lifecycle fields.
    /// </summary>
    public static string CanonicalMarkdown
    {
        get
        {
            var parsed = Parse(Markdown);
            return new TestFileWriter().FormatTestCase(parsed with
            {
                Layout = null,
                Status = null,
                OrphanedReason = null,
                OrphanedDate = null
            });
        }
    }

    public static TestCase Parse(string markdown, string filePath = "TC-100.md")
    {
        var result = new TestCaseParser().Parse(markdown, filePath);
        Assert.True(result.IsSuccess, string.Join("; ", result.Errors.Select(e => e.Message)));
        return result.Value;
    }
}
