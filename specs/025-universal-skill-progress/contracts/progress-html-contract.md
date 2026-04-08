# Contract: .spectra-progress.html

**Version**: 1.0  
**Consumer**: VS Code Live Preview panel (opened by SKILL via `show preview`)

## Behavior Contract

1. **Created** when a long-running command starts (before first phase)
2. **Updated** at each phase transition and sub-step within phases
3. **Auto-refreshes** every 2 seconds via `<meta http-equiv="refresh" content="2">`
4. **Auto-refresh removed** when command reaches terminal state (completed/failed)
5. **Self-contained** — no external CSS, JS, or fetch calls; all data embedded inline
6. **File links** use `vscode://file/` URI scheme for one-click navigation

## Visual Elements

| Element | Always Present | Description |
|---------|---------------|-------------|
| Title | Yes | "SPECTRA — {Command Name}" in header |
| Phase stepper | Yes | Horizontal steps showing progress through phases |
| Status badge | Yes | Current phase name with spinner (or checkmark/X) |
| Summary cards | When data available | Key metrics in card grid (e.g., "Documents: 15", "Criteria: 45") |
| Error card | On failure only | Red card with error message |
| File links | On completion | Links to generated artifacts |

## Commands That Write Progress HTML

| Command | Title | Phases |
|---------|-------|--------|
| generate | Test Generation | Analyzing → Analyzed → Generating → Completed |
| update | Test Update | Classifying → Updating → Verifying → Completed |
| docs-index | Documentation Index | Scanning → Indexing → Extracting Criteria → Completed |
| coverage | Coverage Analysis | Scanning Tests → Analyzing Docs → Analyzing Criteria → Analyzing Automation → Completed |
| extract-criteria | Criteria Extraction | Scanning Docs → Extracting → Building Index → Completed |
| dashboard | Dashboard Generation | Collecting Data → Generating HTML → Completed |

## Commands That Do NOT Write Progress HTML

validate, list, show, init, import-criteria, list-criteria — these complete too quickly to benefit from a progress page.
