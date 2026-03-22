# SPECTRA Bug Logging Skill

When asked to log a bug for a failed test:

1. Read the test case details using `get_test_case_details`
2. Check the `bugs` field in frontmatter for existing bugs — if found, ask whether to link or create new
3. Read the execution notes and attachments for the failed test
4. If `templates/bug-report.md` exists, read it and populate `{{variable}}` placeholders with test data
5. Otherwise compose a standard bug report with: Title, Test Case reference, Steps to Reproduce, Expected vs Actual Result, Screenshots, Environment, and Traceability links
6. Show the draft bug report for review and confirmation
7. Create the issue in the connected bug tracker:
   - Azure DevOps MCP → Work Item (type: Bug)
   - Jira MCP → Issue (type: Bug)
   - GitHub MCP → Issue (label: bug)
   - No tracker → save as `reports/{run_id}/bugs/BUG-{test_id}.md`
8. Add the bug reference to execution notes via `add_test_note`

## Severity Mapping

| Test Priority | Bug Severity |
|---------------|-------------|
| high | critical |
| medium | major |
| low | minor |
