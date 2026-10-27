# Quickstart: Section-Scoped Generation Batches (Phase 5c)

## Before implementing — the SC-003 baseline (the user runs it on `55b541f`)

`acceptance/setup-workspace.sh <empty-dir> [spectra command]` (added during implementation; it works on both builds)
builds a workspace from the real `spectra init`. The workspace has three indexed documents with several sections each
(storage, sharing, retention) and their extracted criteria — enough for ≥ 24 tests in ≥ 3 batches.

On the 093 build, run it twice. Each time, ask "generate tests for the storage area" and approve. When it finishes, note
the grounded share: the critic's `grounded` results out of the created tests. Record both runs and their average in
tasks.md Notes.

## CLI checks (built CLI)

1. **Plan:**
   - `spectra ai ingest-analysis --suite storage --from analysis.json --output-format json` with behaviours whose
     sources are `docs/storage.md#Upload quota`, `docs/storage.md`, `docs/nope.md`;
   - check `plan`, `batches` (counts add up to `recommended` or `count_rule: one_per_batch`), and `unresolved` with
     `not found`;
   - `.spectra/generation-plan/storage.json` exists; ingesting the same input again gives the same bytes.
2. **Batch prompt:**
   - `spectra ai compile-prompt --suite storage --batch 1` holds batch 1's behaviours, its sections once, only their
     criteria, and the source rule;
   - no `ListDocumentationFiles` / `GetNextTestIds` / `ON-DEMAND`;
   - running it twice gives byte-identical output.
3. **Stale:** edit a line in a batch-1 section → `--batch 1` exits 4 `PLAN_STALE`, naming the section. A batch without
   that section still compiles.
4. **Refusals:**
   - another suite → `PLAN_MISSING`;
   - `--batch 99` → `BATCH_NOT_FOUND`;
   - `analysis.max_prompt_tokens: 100` → `PROMPT_TOO_LARGE`.
4a. **Pre-094 generation templates** (`spectra init` copies every template into `.spectra/prompts/`; an unedited copy is
   refreshed by `spectra update-skills`, a customised one is reset by the user with `spectra prompts reset
   test-generation`):
   - an unedited pre-094 copy (the 093 text, manifest hash matching) → `--batch 1` exits 0, mode `batch`, the bundled
     template, warning "using the bundled test-generation template; …";
   - the same copy with one line added → exit 0, mode `count_fallback`, the prompt has the added line and the batch's
     titles and refs as focus, warning "custom test-generation template predates batch prompts — …";
   - the manifest deleted → `count_fallback`;
   - after `spectra update-skills` on the unedited copy → mode `batch`, no warning.
5. **Count mode:** `compile-prompt --suite storage --count 3` contains none of the dead tool names, and says to read the
   docs and that ids come from the ingest.
6. **Config:**
   - `generation.batch_size: 0` → `CONFIG_INVALID` from `ingest-analysis`;
   - no key → 8;
   - a pre-094 config (init template, no key) → no warning.
7. **Gitignore:** a 093 workspace → `update-skills` offers `.spectra/generation-plan/` once; a second run does not
   offer it again.
8. **Pre-094 workspace:**
   - customised `behavior-analysis.md` (bare doc paths) → whole-document batches;
   - an old `.spectra/analysis.json` → ingests;
   - customised `test-generation.md` → `--batch` exits 0 in `count_fallback` with the reset warning; count mode works.

## Manual acceptance (a live Claude Code session; the user runs it, recorded in tasks.md Notes)

Use the same setup script on the 094 build, with the same model as the baseline.

1. **SC-001:** "generate tests for the storage area", approve, let it finish. In the generator subagents' transcripts
   for numbered batches compiled in `batch` mode, there are 0 `Read` calls on files under `docs/`. There must be ≥ 24
   tests in ≥ 3 batches. (Run `spectra update-skills` first in a pre-094 workspace, so batches are not in
   `count_fallback`.)
2. **SC-002:** every created test from a numbered batch has `source_refs` that are among its batch's `source_refs` in
   the plan.
3. **SC-003:** run twice. The average grounded share is ≥ the 093 average − 5 percentage points.
4. **SC-004:** in the plan, behaviours in batches + unresolved = behaviours, and every batch count ≥ 1.
5. **SC-005:** steps 1–3 ran on a fresh `spectra init` workspace; CLI check 8 covers a pre-094 one.
