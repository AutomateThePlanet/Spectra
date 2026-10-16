# Data Model: Docs and Guidance (Phase 6, Part 5)

No product data model changes. The entities below are test-side shapes used by the guards.

## RemovedConcept

| Field | Type | Notes |
|---|---|---|
| `Pattern` | regex (case-insensitive unless noted) | e.g. `spectra ai generate\b`, `spectra auth\b`, `ai\.providers`, `--extract-criteria`, `--skip-splitting`, `spectra-mcp`, `mcp__spectra__`, `MCP [Ss]erver` (SPECTRA's), `Copilot (SDK|Chat|Space)`, `generation_batch_size`, `ai\.defaultProvider`, `ai\.analysis\.max_prompt_tokens`, retired critic keys in a critic context |
| `RemovedIn` | string | `059`, `069`, `070`, `096`, `—` (never existed / wrong path) |
| `Replacement` | string | wording to use instead (research R1) |
| `AppliesTo` | flags | `Docs`, `SourceStrings`, `BundledContent` |

Validation: the list is the single source for every guard; a guard failure names the concept, the file and the line.

## AllowRule

- `HistoricalFiles`: explicit glob list (research R2).
- `RemovalMarkers`: `removed`, `no longer`, `there is no`, `is gone`, `are gone`, `retired`, `legacy`, `pre-v2`,
  `deprecated`.
- `LegitimateMcp`: `BELLATRIX`, `Nova`, `Azure DevOps` (FR-004).
- Scope: a paragraph = maximal run of non-blank lines; a list item, a table row and a fenced code block are each
  their own paragraph (a marker in one bullet never covers another, and a marker outside a fence does not cover a
  command inside it — commands in fences must be real).

## CommandLineCheck

Input: one line starting with `spectra `. Output: `Ok` or `Missing(kind: Command|Option, token, at)`.

- Command resolution: walk `RootCommand.Subcommands` by name/alias; `a|b` alternatives at a command position each
  resolved.
- Option resolution: token `-x`/`--x` (brackets, parentheses stripped; `|`-split) ∈ aliases of the resolved command's
  options ∪ every ancestor's options.
- Ignored: `<…>`, `{…}`, `…`, quoted strings, values, `# comment`.
- Mode `Parse`: `RootCommand.Parse(tokens).Errors` empty (worked example only; only lines starting with `spectra `).
- Documented-in-reference rule: a command counts as documented when its full path (`spectra <group> <name>`, or `<group> <name>` for a subcommand) appears in a heading or a code span/block of docs/cli-reference.md — not as a bare word.

## Doc-change guide shape

- Section: `## When documentation changes` in docs/usage.md; ends at the next `## `.
- Steps: an ordered list whose items start with the bold step names, in order: Docs changed, Criteria, Triage, Relink,
  REMOVED review, Group edits, Unchanged and pending, Re-verify, Acknowledge, Committed vs scratch.
- Worked example: one `### Worked example` subsection; its fenced `bash` lines starting with `spectra ` are
  `Parse`-checked; it shows abbreviated staged-file shapes (`updated-<id>.json`, a critic verdict).
- Skill/agent names: every `spectra-[a-z-]+` name in the section is a key of `SkillContent.All` or `AgentContent.All`.
- Refresh line: an edited `USAGE.md` / `CUSTOMIZATION.md` → delete it and run `spectra update-skills`; a prompt template →
  `spectra prompts reset <id>`.
- Committed-vs-scratch table: two columns (`Path`, `Committed?`); every scratch row ∈ `SpectraGitIgnore.Patterns`, and
  every pattern under `.spectra/` that a user meets in the update flow appears.

## CLAUDE.md shape

- Headings (required): `## Tech Stack`, `## Project Structure`, `## Commands`, `## Code Style`, `## Execution`,
  `## Active Technologies`, `## Recent Changes`.
- Project Structure: one fenced block; each line `<indent><Name>/ [<Name>/ …]  # purpose` (one or more folders per
  line, so the file fits in 150 lines); indent depth = nesting under the last folder of the line above; root = repo.
- Commands: one fenced `bash` block; every `spectra …` line structural-checked.
- Recent Changes: a table `| Spec | Summary |`, one row per spec (078–100), each summary ≤ 2 lines linking
  `specs/NNN-*/` and pointing to CHANGELOG.md.
- Length ≤ 150 lines.
