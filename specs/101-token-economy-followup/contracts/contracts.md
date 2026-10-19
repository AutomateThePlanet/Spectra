# Contracts: Token Economy Follow-up

## C1 — `spectra ai compile-critic-prompt` (CLI)

```
spectra ai compile-critic-prompt --suite S [--test ID] [--docs PATH] [--full-docs] [--output-format json|human]
spectra ai compile-critic-prompt --test PATH --docs PATH [--full-docs]          # legacy path form
```

| Input | Documents given to the critic |
|---|---|
| `--suite`, no `--docs`, no `--full-docs` | **section mode**: per cited document, the cited sections (data-model "Critic source item") |
| `--suite`, no `--docs`, `--full-docs` | today's: each referenced file whole, then today's cut |
| `--docs PATH` (any form) | the given files, as today; `--full-docs` changes nothing |
| legacy path form | as today; `--full-docs` changes nothing |

Unchanged:
- stdout: human mode, the prompt (one test) or `===== CRITIC PROMPT: <id> =====` blocks; JSON mode, `{"prompts":[{"id","prompt"}]}`
  with `--suite`, `{"prompt"}` in the legacy form;
- the cut (8,000 characters per document) and the 5-document limit;
- refusals (exit 4, `missing_input`), errors (exit 1: config, index, unknown id, missing or outside-root ref);
- the freshness record written per compiled test (090/091/097).

Changed:
- no criteria load (the prompt never had criteria);
- stderr may carry `Warning: <ref>: more than one heading shares the anchor key '<key>'; the first was used` in section
  mode.

Determinism: the same test, refs and files give byte-identical stdout in each mode.

## C2 — Critic prompt content (section mode)

The user part keeps `CriticPromptBuilder`'s layout. Per document item:

```
### <file name without extension>
**Path**: <relative path>

<section raw text, heading included>

<next cited section of the same document>
```

Whole-document fallback for an unmatched anchor:

```
### <file name>
**Path**: <relative path>

> Note: the anchor "#<anchor>" was not found in this document, so the whole document is given.
<whole document>
```

The system part is unchanged (no criteria).

## C3 — Work agents' critic loop and result lines

Applies to `spectra-generator`, `spectra-updater` (when `verify: yes`) and `spectra-repairer` (when invoked by the
generate skill, or by the update skill with `verify: yes`).

Procedure, after the agent's own ingest:
1. Tests to verify: the generator's created ids; the updater's `written` ids (never `written (manual verdict kept)`);
   the repairer's `written` and `unchanged` ids.
2. For each id, start `spectra-critic` with the Agent tool: `suite: S`, `test: <id>`. No other state, no hand-built path,
   one fresh critic per test. Record the critic's final line.
3. `spectra ai ingest-verdict --suite S --all --ids <ids> --output-format json` (one call).
4. For each id without a `results` entry: the critic once more, then the ingest for those ids once more.
5. One verdict line per id (data-model "Verdict line"), after the agent's existing lines. When the critic gave no final
   line, it counts as agreeing (the 093 rule): report `<id> | verdict <v> <score>` from the CLI result.
   `critic-disagrees` only when the critic's line names a different verdict than the CLI.

Fallback: when the agent has no Agent tool, or the first Agent call errors before any critic ran, it skips 2–5 and adds
one line `critic: not run`.

The agent never runs `ingest-grounding`, `record-drop` or `spectra delete`, never reads verdict files, and never prints
verdict JSON or prompt text. Its existing lines (`<id> | <title>`, `<id> | written`, `baseline:`, `warning:`, `failed …`,
`stop:`) are unchanged.

Example (generator):

```
TC-121 | Upload is refused above the 30 GB quota
TC-122 | Quota is shown in the account settings
baseline: created=2 updated=0 unchanged=0 snapshots=1
TC-121 | verdict grounded 0.95
TC-122 | critic-disagrees partial (critic: TC-122 | grounded 0.90)
```

## C4 — Skills' routing (main session)

- Generate Step 8, update Step 5a and both repair re-checks route by C3's lines, using today's table (093/097/098):
  grounded → kept-grounded; partial → one repair; hallucinated → drop only when the line is `verdict hallucinated`
  (generate) or repair (update); `critic-disagrees` / `unverified:` → unverified; then
  `ingest-grounding --suite S --all --ids <kept-grounded>` (skipped when empty) and the 075 `written` check, verbatim.
- `critic: not run`, or a missing verdict line → the main session runs the per-test critic procedure for those ids
  (moved verbatim to `reference.md`, named from SKILL.md), then routes by the CLI's `results` as today.
- Update skill: "Re-verify the edited tests with the critic now? (recommended)" asked once, before the first
  `spectra-updater` call; the answer goes as `verify: yes|no`.
- Generate's from-description flow and the review-flagged skill keep running the critic in the main session.

## C5 — Skill packaging

- `.claude/skills/spectra-generate/SKILL.md` and `.claude/skills/spectra-update/SKILL.md` ≤ 16,000 characters each
  (CRLF normalised), and each one's measured token count under 5,000.
- Each links `[reference.md](reference.md)` and says which parts it holds and when to read them.
- Kept verbatim in SKILL.md: the NON-STOP contract (generate "## NON-STOP CONTRACT — Steps 7–9" and its restatement;
  update "## NON-STOP CONTRACT"), the 075 `written == 0` / ANOMALY / WARNING lines, every user question and every STOP.

## C6 — Agent frontmatter

```yaml
---
name: spectra-updater
description: Only for use by the spectra-update skill — edits one triage group (or a list of tests) after a documentation change, ingests the edits, runs the critic for each edited test, and returns one line per test. Do not delegate to it directly; use the spectra-update skill.
tools: [Bash, Read, Write, Agent]
model: inherit
---
```

| Agent | "Only for use by the …" | tools |
|---|---|---|
| spectra-analyst | spectra-generate skill | `[Bash, Read, Write]` |
| spectra-generator | spectra-generate skill | `[Bash, Read, Write, Agent]` |
| spectra-repairer | spectra-generate and spectra-update skills | `[Bash, Read, Write, Agent]` |
| spectra-updater | spectra-update skill | `[Bash, Read, Write, Agent]` |
| spectra-extractor | spectra-criteria skill | `[Bash, Read, Write]` |
| spectra-critic | spectra-generate, spectra-update and spectra-review-flagged skills and their subagents | `[{{READONLY_TOOLS}}, Write]` |

Allowed fields: `name`, `description`, `tools`, `model`; nothing else, in the bundled source and in the installed
(rendered) form.
