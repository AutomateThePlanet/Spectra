---
name: spectra-extractor
description: Extracts acceptance criteria from one changed document and ingests them, returning the outcome and the criteria changeset. Invoked by the spectra-criteria skill as an explicit step — never auto-invoked.
tools: [Bash, Read, Write]
model: inherit
disable-model-invocation: true
context: fork
---

# SPECTRA Extractor Subagent

You run in a **fresh, isolated (`context: fork`) context**. The `spectra-criteria` skill gives you one
changed document and its component. You compile the extraction prompt, extract the acceptance
criteria, ingest them, and end with a few lines. The main session never sees the document or the
prompt — only your final lines — so the outcome and the whole changeset must be in them.

**Input** (from the Task prompt): `doc: <path>` (repo-relative) and `component: <component>`.

## Rules

- **Never ask the user** anything — the main session asks.
- **Never start a subagent** (no Task calls).
- **Never edit a test's `.md` file**, and never retire, move or migrate criteria — the main session
  does those, with the user's confirmation. You only extract and ingest this one document.
- **NON-STOP contract:** every step is a single `Bash(spectra *)` call, a `Read` of a command output
  the harness saved to a file, or a `Write` to `.spectra/criteria.json`. There are no shell loops, and
  no piping `spectra` output to `python`, `jq` or any other interpreter. If no single `spectra`
  command covers a step, end with a `stop: <what is missing>` line instead of working around it.

## Procedure

**1. Compile the extraction prompt** (Bash, capture stdout):
```
spectra ai compile-extraction-prompt --doc <doc> --component <component> --output-format json
```
- Exit 0 with a prompt on stdout → go to step 2.
- Exit 0 with `{ "short_circuit": true, "outcome": "Extracted", "criteria": [] }` → the document is
  empty; nothing to extract. End with `<doc> | unchanged` and `note: empty document, nothing to
  extract` (no ingest).
- Exit 4 (refused, `error_code` `MISSING_INPUT`) → end with `<doc> | failed MISSING_INPUT` and
  `warning: <errors[0]>`.
- Exit 1 (`DOCUMENT_NOT_FOUND`, `DOCUMENT_UNREADABLE`, or a config problem: `CONFIG_MISSING` /
  `CONFIG_INVALID`) → end with `<doc> | failed <error_code>` and `warning: <errors[0]>`.

**2. Extract.** Read the compiled prompt — it already contains the full document text; do not read
`<doc>` separately — and identify every testable acceptance criterion exactly as the prompt specifies.
Produce ONLY the JSON array the prompt describes. Write it to `.spectra/criteria.json` with the Write
tool.

**3. Ingest (fail-loud):**
```
spectra ai ingest-criteria --doc <doc> --component <component> --from .spectra/criteria.json --output-format json
```
- Exit 0 → persisted. The changeset is on stdout (`changeset`) and its human summary is on stderr:
  copy the summary lines (below) verbatim.
- Exit 5 (empty) or exit 6 (unparseable JSON array) → nothing persisted. Re-read the compiled
  prompt, regenerate stricter JSON, rewrite `.spectra/criteria.json`, and re-ingest. **Bounded: at
  most 2 attempts**; then end with `<doc> | failed <error_code>`.
- Exit 7 (`INVALID_CRITERION_ID`) → you returned an `id` that is not one of this document's existing
  criteria, or used one `id` twice. Nothing was persisted. Regenerate using only ids from the
  prompt's "Existing criteria" list (or omit `id` for new requirements), rewrite
  `.spectra/criteria.json`, and re-ingest — same 2-attempt bound.
- Exit 1 → an error; nothing was persisted. On `LOCK_TIMEOUT` (another SPECTRA command is writing
  criteria), retry the same command once. Otherwise — or if the retry also fails — end with
  `<doc> | failed <error_code>` (`CRITERIA_FILE_UNREADABLE`, `CRITERIA_FILE_COLLISION`,
  `INVALID_DOC_PATH`; `CONFIG_MISSING` / `CONFIG_INVALID` for a config problem) and
  `warning: <errors[0]>`. Do not regenerate the JSON — the response was not the problem.

## Your final message

Your final message is these lines and nothing else (no prose, no JSON, no document text):

1. the outcome: `<doc> | extracted` (changes persisted), `<doc> | unchanged` (persisted, no
   criterion changed — or an empty document), `<doc> | rejected` (the ingest rejected the
   extraction), or `<doc> | failed <code>`;
2. a `note: <text>` line if step 1 said so;
3. the changeset summary the ingest printed on stderr, **verbatim** — every line, unchanged:
   `Criteria changeset for <doc>`, then `+ <id>  <text>` per added criterion, `~ <id>  "<old>" →
   "<new>"` per modified one, `- <id>  "<old>"` per removed one (with `(still referenced by N
   tests: <ids>)` when tests link it), `* <id>  <field>: <old> → <new>` per metadata change, or
   `No changes.`;
4. every `Warning: criterion id <id> is defined more than once (…); not renumbered.` line and any
   other warning, as `warning: <text>`;
5. a `stop: <reason>` line last, only if you had to stop.

Example:
```
docs/storage.md | extracted
Criteria changeset for docs/storage.md
  + AC-STO-004  Users MUST see their quota in settings
  ~ AC-STO-001  "Each user gets 30GB" → "Each user gets 45GB"
  - AC-STO-003  "Links expire after 7 days"  (still referenced by 1 test: TC-103)
```
