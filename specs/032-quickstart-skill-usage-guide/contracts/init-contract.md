# Contract: `spectra init` and `spectra update-skills` output for feature 032

This document captures the user-visible contract changes introduced by this feature. There are no new commands or flags — only new files produced by existing commands.

## `spectra init`

### New artifacts written

After a successful `spectra init` (without `--skip-skills`), the following NEW files MUST exist on disk:

| Path | Purpose |
|------|---------|
| `.github/skills/spectra-quickstart/SKILL.md` | Quickstart SKILL (workflow-oriented onboarding for Copilot Chat) |
| `USAGE.md` | Offline workflow reference (project-root markdown doc) |

### Manifest updates

The `.spectra/skills-manifest.json` file MUST contain SHA-256 hashes for both new files:

```json
{
  "version": "1.0",
  "files": {
    "...existing entries...": "...",
    ".github/skills/spectra-quickstart/SKILL.md": "<sha256-hex>",
    "USAGE.md": "<sha256-hex>"
  }
}
```

### `--skip-skills` behavior

When `spectra init --skip-skills` is invoked, NEITHER new file is created and NEITHER manifest entry is added. This matches the existing `--skip-skills` semantics for the other 11 SKILLs and for `CUSTOMIZATION.md`.

### `--force` behavior

When `spectra init --force` is invoked on a project that already has these files, both files are overwritten with the latest bundled content (matching existing `--force` semantics for `CUSTOMIZATION.md` and other bundled files).

## `spectra update-skills`

### Refresh behavior

For each of the two new manifest entries:

1. Compute the SHA-256 of the on-disk file.
2. Compare against the manifest hash.
3. **If equal** → file is unmodified by the user; overwrite with the current embedded content; update the manifest hash.
4. **If not equal** → file has been customized; skip; do not touch the manifest.
5. **If file missing** → write fresh from the embedded content; record hash.

This is identical to the existing `update-skills` behavior for `CUSTOMIZATION.md` and the other SKILLs.

### Output / reporting

The existing `update-skills` summary table MUST include rows for the two new files showing one of: `created`, `updated`, `skipped (customized)`, `up-to-date`.

## Quickstart SKILL frontmatter contract

The bundled `spectra-quickstart.md` file MUST begin with a YAML frontmatter block:

```yaml
---
name: SPECTRA Quickstart
description: Guided onboarding and workflow walkthroughs for SPECTRA via Copilot Chat.
---
```

The `name` and `description` fields are consumed by Copilot Chat to surface the SKILL to the user. The description MUST clearly identify it as an onboarding/walkthrough SKILL so the LLM picks it for "help me get started"-style intents.

## Quickstart SKILL content contract

The body of `spectra-quickstart.md` MUST contain (verifiable by tests):

1. The literal string `## Available tools`.
2. A workflow overview listing 11 distinct workflow names.
3. A `## Workflow N:` header for each of the 11 workflows (N = 1..11).
4. A `## Quick Troubleshooting` section.
5. A `## Example user requests that trigger this SKILL` section with at least 10 trigger phrases.

## USAGE.md content contract

The body of `USAGE.md` MUST contain (verifiable by tests):

1. The literal heading `# SPECTRA Usage Guide — VS Code Copilot Chat` (or similar — the existence of "SPECTRA Usage Guide" is sufficient).
2. A `## Prerequisites` section.
3. Sections covering all 11 workflows (verified by counting heading occurrences for the 11 workflow names: `Generating Test Cases`, `Extracting Acceptance Criteria`, `Importing Criteria`, `Coverage Analysis`, `Generating Dashboard`, `Validating Tests`, `Updating Tests`, `Executing Tests`, `Creating a Custom Profile`, `Indexing Documentation`, `Customizing SPECTRA`).
4. A `## Troubleshooting` section.
5. A "Complete Pipeline" or "Start to Finish" section describing the end-to-end flow.
6. NO references to in-chat tool names (`runInTerminal`, `awaitTerminal`, `readFile`, `browser/openBrowserPage`).

## Agent prompt contract

After this feature lands, both `spectra-generation.agent.md` and `spectra-execution.agent.md` MUST contain a textual reference to `spectra-quickstart` (verifiable by tests with a substring assertion).
