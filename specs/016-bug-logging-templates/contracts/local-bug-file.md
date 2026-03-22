# Local Bug File Contract

## File Location

```
reports/{run_id}/bugs/BUG-{test_id}.md
reports/{run_id}/bugs/attachments/{filename}
```

## File Naming

- Bug files: `BUG-{test_id}.md` (e.g., `BUG-TC-101.md`)
- Attachments: original filename preserved (e.g., `screenshot-TC-101-step3.png`)
- If multiple bugs for same test in same run: `BUG-{test_id}-{n}.md` (n = 2, 3, ...)

## File Content

Local bug files use the same format as template-generated reports.
If a template exists, the populated template is saved.
If no template, the agent-composed report is saved.

## Directory Creation

The `bugs/` and `bugs/attachments/` directories are created on first bug save within a run.
They sit alongside existing report files (`{run_id}.json`, `{run_id}.md`, `{run_id}.html`).
