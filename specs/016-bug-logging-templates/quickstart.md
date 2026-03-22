# Quickstart: 016 Bug Logging

## Setup

1. Run `spectra init` (or re-run for existing projects) to create:
   - `templates/bug-report.md` — default bug report template
   - `bug_tracking` section in `spectra.config.json`

2. (Optional) Connect a bug tracker MCP:
   - Azure DevOps MCP server
   - Jira MCP server
   - GitHub MCP server (built-in with Copilot)

## Usage

1. Start a test execution run as normal
2. When a test fails, the agent offers to log a bug
3. Review the pre-filled bug report
4. Confirm to submit to your tracker (or save locally)
5. Bug ID is recorded in test notes and frontmatter

## Configuration

Edit `spectra.config.json`:

```json
{
  "bug_tracking": {
    "provider": "auto",
    "template": "templates/bug-report.md",
    "default_severity": "medium",
    "auto_attach_screenshots": true,
    "auto_prompt_on_failure": true
  }
}
```

## Customization

- Edit `templates/bug-report.md` to match your team's format
- Add custom `{{variables}}` — unrecognized ones are left for manual fill
- Delete the template entirely — the agent composes reports directly
- Set `provider` to `"local"` to always save as Markdown files

## Code Changes Required

### New Files
- `src/Spectra.Core/Models/Config/BugTrackingConfig.cs`
- `src/Spectra.CLI/Templates/bug-report.md` (embedded resource)
- `src/Spectra.Core/BugReporting/BugReportTemplateEngine.cs`
- `src/Spectra.Core/BugReporting/BugReportContext.cs`
- `src/Spectra.Core/BugReporting/BugReportWriter.cs`
- `.github/skills/spectra-bug-logging/SKILL.md`

### Modified Files
- `src/Spectra.Core/Models/Config/SpectraConfig.cs` — add `BugTracking` property
- `src/Spectra.Core/Models/TestCaseFrontmatter.cs` — add `Bugs` field
- `src/Spectra.Core/Models/TestCase.cs` — add `Bugs` field
- `src/Spectra.Core/Models/TestIndexEntry.cs` — add `Bugs` field
- `src/Spectra.Core/Parsing/FrontmatterUpdater.cs` — add `UpdateBugs()` method
- `src/Spectra.CLI/Commands/Init/InitHandler.cs` — add template creation + config
- `src/Spectra.CLI/Templates/spectra.config.json` — add `bug_tracking` section
- `src/Spectra.MCP/Tools/TestExecution/AdvanceTestCaseTool.cs` — add bug context to response
- `.github/agents/spectra-execution.agent.md` — expand bug logging section
- `src/Spectra.Core/Index/DocumentIndexWriter.cs` — include `bugs` in index entries
