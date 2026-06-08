# Quickstart: Targeted test updates (inverted update seam)

This walks the new seam end-to-end. It runs inside the Claude Code interactive session (no in-process model, no Copilot SDK).

## Prerequisites

- A Spectra workspace (`spectra.config.json`, `test-cases/<suite>/…`).
- A source doc that changed, so at least one test classifies as **OUTDATED**.

## The loop (what the `spectra-update` skill drives)

### 1. Select what needs updating (reused classifier)

```
spectra ai update --suite checkout --diff --no-interaction --output-format json --verbosity quiet
```

Read the classification result; collect the ids classified **OUTDATED**. `TestClassifier` is the selector — it tells you *which* tests are stale, not how to fix them.

### 2. Per OUTDATED test — compile an update prompt (deterministic)

```
spectra ai compile-update-prompt --suite checkout --test-id TC-104
```

- Exit `0` → the update prompt is on stdout (original test + changed source/criteria + "edit, don't regenerate; preserve id/structure/manual fields").
- Exit `4` → missing input (no such test, or no changed source/criteria) — skip.

### 3. Edit in-session

Read the prompt; edit **only** what the doc change requires. Keep the id, the structure, and any manual verdict/notes. Write the whole edited test as a JSON array (one element) to `.spectra/updated.json`.

### 4. Ingest (fail-loud, invariant-protected)

```
spectra ai ingest-update checkout --test-id TC-104 --from .spectra/updated.json
```

- Exit `0` → persisted; **id unchanged**, `_index.json` regenerated.
- Exit `5` → content invalid **or** `DRIFT_DETECTED` (an out-of-scope field changed). Read the error, re-edit, retry (bounded: max 2 attempts).
- Exit `6` → schema invalid. Read the error, re-edit, retry.

On any non-zero exit, **nothing was persisted** — the original test and indexes are untouched.

## Verifying the guarantees

| Guarantee | How to check |
|-----------|--------------|
| Edited, not regenerated; id preserved (US1) | `git diff test-cases/checkout/TC-104.md` shows targeted edits; the id frontmatter is unchanged. |
| Manual fields preserved (US2) | Start from a test whose grounding verdict is `manual`; after ingest the verdict/note are still there even if your edited JSON omitted them. |
| Drift surfaced, not persisted (US3) | Put an out-of-scope change (e.g. flip `priority`) into the edited JSON; ingest exits `5` `DRIFT_DETECTED` and writes nothing. |
| Bounded fail-loud retry (US4) | Feed malformed JSON; ingest exits `5/6` with a specific error; the skill retries ≤2× then stops. |
| UP_TO_DATE untouched (US1.3) | Tests not in the OUTDATED set are never compiled/ingested — `git status` shows them unchanged. |
| Index parity (FR-008) | After a successful ingest, `spectra validate` passes (Index Currency gate). |

## Where it lives

- Commands: `spectra ai compile-update-prompt`, `spectra ai ingest-update` (registered in `AiCommand`).
- Compiler/ingestor: `src/Spectra.CLI/Generation/UpdatePromptCompiler.cs`, `UpdatedTestIngestor.cs`.
- Persist path: `src/Spectra.CLI/IO/TestPersistenceService.cs` (reused).
- Selector: `src/Spectra.Core/Update/TestClassifier.cs` (reused).
- Skill: `src/Spectra.CLI/Skills/Content/Skills/spectra-update.md` (rewritten to drive this loop).
- Template: `prompts/test-update.md` (new).
