namespace Spectra.CLI.Tests.TestFixtures;

/// <summary>Spec 079: seeds a pre-079 (base-name) criteria layout into a <see cref="CriteriaTestWorkspace"/>.</summary>
public static class OldLayoutCriteria
{
    /// <summary>
    /// <c>docs/checkout.md</c> (source_doc typed with a backslash) and <c>docs/login.md</c>, each with its own
    /// base-name file; plus an import entry that must stay untouched.
    /// </summary>
    public static void Seed(CriteriaTestWorkspace ws)
    {
        ws.WriteDoc("docs/checkout.md", "# Checkout\n");
        ws.WriteDoc("docs/login.md", "# Login\n");
        ws.WriteCriteriaFile("checkout.criteria.yaml", """
            criteria:
            - id: AC-CHECKOUT-001
              text: System MUST validate the card
              source_doc: docs\checkout.md
            - id: AC-CHECKOUT-007
              text: System MUST retry once
              source_doc: docs\checkout.md
            """);
        ws.WriteCriteriaFile("login.criteria.yaml", """
            criteria:
            - id: AC-LOGIN-002
              text: Users MUST log in
              source_doc: docs/login.md
            """);
        ws.WriteCriteriaFile("imported/jira.criteria.yaml", """
            criteria:
            - id: AC-JIRA-001
              text: Imported requirement
            """);
        ws.WriteIndexYaml("""
            version: 1
            total_criteria: 4
            sources:
            - file: checkout.criteria.yaml
              source_doc: docs\checkout.md
              source_type: document
              doc_hash: aaa
              criteria_count: 2
            - file: login.criteria.yaml
              source_doc: docs/login.md
              source_type: document
              doc_hash: bbb
              criteria_count: 1
            - file: imported/jira.criteria.yaml
              source_doc: jira.csv
              source_type: import
              criteria_count: 1
            """);
    }

    /// <summary>Two docs that already share one base-name file (a past collision).</summary>
    public static void SeedCollision(CriteriaTestWorkspace ws)
    {
        ws.WriteDoc("docs/a/login.md", "# Login A\n");
        ws.WriteDoc("docs/b/login.md", "# Login B\n");
        ws.WriteCriteriaFile("login.criteria.yaml", """
            criteria:
            - id: AC-LOGIN-001
              text: B wrote last
              source_doc: docs/b/login.md
            """);
        ws.WriteIndexYaml("""
            version: 1
            sources:
            - file: login.criteria.yaml
              source_doc: docs/a/login.md
              source_type: document
              criteria_count: 1
            - file: login.criteria.yaml
              source_doc: docs/b/login.md
              source_type: document
              criteria_count: 1
            """);
    }
}
