# Bug Report Template Contract

## Template Location

Default: `templates/bug-report.md` (relative to repo root)
Configurable via: `bug_tracking.template` in `spectra.config.json`

## Variable Substitution Rules

1. Variables use `{{variable_name}}` syntax (double curly braces)
2. Known variables are replaced with values from `BugReportContext`
3. Unknown/custom variables are left as-is for manual fill
4. Empty values render as empty string (not the placeholder)
5. List values (attachments, source_refs, requirements) render as Markdown lists

## Supported Variables

| Variable | Required | Type | Example Value |
|----------|----------|------|---------------|
| `{{title}}` | auto | string | `"Bug: Login timeout - Step 3 fails"` |
| `{{test_id}}` | auto | string | `"TC-101"` |
| `{{test_title}}` | auto | string | `"Login with valid credentials"` |
| `{{suite_name}}` | auto | string | `"authentication"` |
| `{{environment}}` | auto | string | `"staging"` |
| `{{severity}}` | auto | string | `"major"` |
| `{{run_id}}` | auto | string | `"a1b2c3d4-..."` |
| `{{failed_steps}}` | auto | string | Numbered step list |
| `{{expected_result}}` | auto | string | From test case |
| `{{attachments}}` | auto | string | Markdown image links |
| `{{source_refs}}` | auto | string | Comma-separated list |
| `{{requirements}}` | auto | string | Comma-separated list |
| `{{component}}` | auto | string | From frontmatter |

## Template States

| State | Behavior |
|-------|----------|
| Exists (default) | Agent reads, substitutes variables, shows preview |
| Customized | Agent substitutes known variables, leaves custom ones |
| Deleted | Agent composes report from BugReportContext directly |
| Path set to null | Same as deleted — template disabled |

## Default Template Content

See `templates/bug-report.md` created by `spectra init`.
