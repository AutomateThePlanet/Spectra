# Queued feature 049 — Targeted update instructions

> **Investigation-only.** No production code, specs, configs, or skills were modified.
> Every claim about current behavior is cited `file:line`. Hypotheses are marked `INFERRED`
> with what would confirm them.

## Two preliminaries

- **No draft file exists** for this feature. Searches for "targeted update" / "update instructions"
  and a `queued/`/`backlog/`/`draft/` directory returned nothing. Intent below is reconstructed from
  the one-line summary: *"instructions for updating existing tests (not full regeneration)."*
- **Numbering collision.** The repo already ships an *implemented, unrelated* spec
  `specs/049-from-description-index-parity` (v1.52.6). The number `049` here is only a working label.

## Reconstructed intent

When documentation changes, update the *affected parts* of existing test cases (preserving id,
structure, manual notes, untouched fields) instead of regenerating each test from scratch — i.e.
give the model targeted, surgical update instructions.

## 1. Does the problem still exist? — **Yes, and it is sharper than before**

There is currently **no model-driven update at all**. The update flow is purely deterministic local
classification plus mechanical file rewrites:

- The handler states it outright: *"Update currently makes no AI calls"*
  (`src/Spectra.CLI/Commands/Update/UpdateHandler.cs:234-237`) and again
  *"update flow currently performs no AI calls (classification is purely local heuristics in
  TestClassifier)"* with `TokenUsage = null`
  (`UpdateHandler.cs:537-539`).
- Classification is heuristic: `TestClassifier.ClassifyBatch` over source contents + criteria
  (`UpdateHandler.cs:298-301`), surfacing UP_TO_DATE / OUTDATED / ORPHANED / REDUNDANT.
- "Applying changes" writes a `ProposedTest` (produced deterministically by
  `BatchProposeUpdatesTool`, `UpdateHandler.cs:333-342`), marks orphaned tests with a status, or
  deletes — no content is intelligently rewritten against the new docs
  (`UpdateHandler.cs:634-702`, esp. the `result.ToUpdate` write loop at `:644-651`).

So the "rewrite to match current documentation" promised by the update skill
(`src/Spectra.CLI/Skills/Content/Skills/spectra-update.md:9-12` and `:31`) overstates what the
model-free flow can do — that skill text is **stale** relative to the post-migration reality. The
feature's gap — surgical, doc-aware updates — is therefore not just unmet but currently impossible
through any path.

## 2. Where is the seam now?

The original feature implicitly assumed an in-process model could be handed an existing test plus
changed docs and told to edit it. Post-migration that assumption is doubly broken:

- The CLI is model-free; generation was **inverted** onto a compile → in-session-generate → ingest
  seam (Spec 053/059). The inverted surface is visible in the `ai` command tree
  (`src/Spectra.CLI/Commands/Ai/AiCommand.cs:20-35`).
- But **update was not inverted.** The seam exposes `compile-prompt`, `compile-analysis-prompt`,
  `compile-critic-prompt`, and the matching `ingest-tests` / `ingest-criteria` / `ingest-verdict` /
  `ingest-analysis` — and **no `compile-update-prompt` / `ingest-update`** (exhaustive grep of the
  `Commands/Generate/` compile+ingest commands; the update handler has no such call).

So delivering targeted updates now means **building a new inverted seam for update**, mirroring
053/059:

- a deterministic `compile-update-prompt` that emits, per OUTDATED test, the existing artifact + the
  changed source/criteria + explicit "edit, don't regenerate; preserve id/structure/manual fields"
  instructions (a new template alongside the existing `test-generation` template);
- in-session generation of the edited test;
- an `ingest-update` fail-loud boundary that validates and persists via the existing
  `TestPersistenceService` (write-file + regenerate-index, the same boundary generation uses);
- `spectra-update` skill choreography to drive the loop, replacing today's "the command rewrites for
  you" framing.

The deterministic classifier (`TestClassifier`, `Spectra.Core/Update/`) survives unchanged as the
*selector* of which tests need updating — it is the input to the new seam, not the mechanism of the
rewrite.

## 3. Verdict — **SURVIVES-WITH-REWRITE**

Still wanted — arguably more than before, since the update path now has no intelligence at all — but
the mechanism is fundamentally different. The original "give the in-process updater targeted
instructions" cannot be implemented; instead the update flow must be inverted onto a new
compile→generate→ingest seam, with the targeted-update instructions living in a new prompt template +
the `spectra-update` skill. The deterministic classification half stays; the rewrite half is net-new
choreography.

## 4. Dependencies / risk

- **Regression net:** the new ingest boundary must persist through `TestPersistenceService`
  (the single write+index entry point); the selector reuses `Spectra.Core/Update/TestClassifier`.
  Both are load-bearing — changes here ripple into discovery/index parity.
- **Provider/SDK retirement:** the new seam runs in the interactive session (no in-process model, no
  Copilot SDK), exactly like generation post-059. Therefore **not blocked** by the still-pending
  provider/SDK retirement.
- **Stale-skill cleanup:** any spec here should also correct `spectra-update.md:9-12,31`, which still
  claims the command "rewrites affected test cases" — false under the model-free flow.
- **Independent** of the other three queued features (distinct seam), though it is the largest of the
  four because it requires standing up a new compile/ingest pair rather than extending an existing
  one.
