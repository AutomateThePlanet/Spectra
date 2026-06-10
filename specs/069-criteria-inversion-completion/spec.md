# Feature Specification: Criteria-extraction inversion — completion + Copilot SDK removal

**Feature Branch**: `069-criteria-inversion-completion`
**Created**: 2026-06-09
**Status**: Draft
**Input**: Complete the criteria-extraction inversion and remove the GitHub Copilot SDK entirely. Wire
the existing model-free seam (`compile-extraction-prompt` / `ingest-criteria`) into the
`spectra-criteria` skill; converge `docs index` onto the `.criteria.yaml` artifact and retire the
`RequirementsExtractor` → `_requirements.yaml` path; drop import compound-splitting; delete
`Agent/Copilot/`; and strip `ai.providers` + `ai.critic` from the config schema, generation, template,
validation, display, and the interactive init steps. Grounded by `INVESTIGATION-criteria-inversion.md`
(2026-06-09).

## Context & Motivation

`ai.providers[0]` is the **last live consumer of the GitHub Copilot SDK** — read at exactly three
runtime sites (`DocsIndexHandler.cs:253`, `AnalyzeHandler.cs:436`, `AnalyzeHandler.cs:956`) to build an
in-process extractor against `CopilotService.CreateGenerationSessionAsync` → `session.SendAndWaitAsync`
(`CriteriaExtractor.cs:67-88`, `RequirementsExtractor.cs:63-77`). `ai.critic` is already dead
(`CriticFactory.cs:108-117` returns the retirement message; `CriticModelResolver.Resolve` has zero
callers) and the five generation classes were removed in Spec 059. Eliminating these three sites — and
rescuing the model-free helpers they sit beside — makes `ai.providers` removable and `Agent/Copilot/`
deletable, completing the v2 migration off metered in-process inference with **no transitional
half-state**.

The inverted seam is **already built (Spec 054) but unwired**: `compile-extraction-prompt` +
`ingest-criteria` are registered (`AiCommand.cs:26-27`); the `spectra-criteria` skill still calls the
old in-process path (`spectra-criteria.md:17`).

**Two-path root cause** (why scope includes `docs index`):

| Path | Extractor | Model type | Output artifact | Seam |
|---|---|---|---|---|
| `analyze --extract-criteria` | `CriteriaExtractor` | `AcceptanceCriterion` | `docs/criteria/*.criteria.yaml` + `_criteria_index.yaml` | **BUILT** (unwired) |
| `docs index` | `RequirementsExtractor` | `RequirementDefinition` | `docs/requirements/_requirements.yaml` | **NONE** (inline/blocking) |

`docs index` runs the pre-023 requirements model inline; a short-lived non-interactive process cannot
make a model call after inversion, so removing inline extraction is forced. **Confirmed decisions:**
(1) `docs index` converges onto the criteria seam and becomes index-only; `RequirementsExtractor` is
retired. (2) Import compound-splitting is dropped.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Criteria extraction runs without the metered SDK (Priority: P1)

A SPECTRA user extracts acceptance criteria from their documentation. Instead of the CLI spawning an
in-process Copilot SDK model call, the `spectra-criteria` skill drives a per-document loop: compile a
deterministic prompt, perform the extraction as an in-session model turn (reading the doc with file
tools), then ingest the result through the fail-loud boundary. The produced `.criteria.yaml` and
`_criteria_index.yaml` are identical to what the old path produced.

**Why this priority**: This is the core inversion — it removes the only live metered code path and is
the prerequisite for deleting the SDK. Without it, nothing else can land.

**Independent Test**: Run the criteria-extract skill on a multi-doc corpus; confirm the CLI makes no
model/SDK call, each changed doc goes through compile → in-session turn → ingest, and the output files
are byte-identical to a pre-inversion run.

**Acceptance Scenarios**:

1. **Given** a workspace with documentation and no prior criteria, **When** the criteria-extract skill
   runs, **Then** for each document it invokes `compile-extraction-prompt` → in-session extraction →
   `ingest-criteria`, and `docs/criteria/*.criteria.yaml` + `_criteria_index.yaml` are written.
2. **Given** an extraction turn that returns empty or unparseable content, **When** it is ingested,
   **Then** nothing is persisted and the index is left byte-unchanged (fail-loud, anti-cache-poisoning).
3. **Given** a re-extraction of an unchanged document, **When** the skill runs, **Then** that document
   is skipped **without** a model turn, and existing criterion IDs are preserved.

### User Story 2 - No AI-provider configuration or prompts anywhere (Priority: P2)

A user runs `spectra init` in a new repo. They are **never** asked which AI provider to use. The
generated `spectra.config.json` contains no `ai.providers` or `ai.critic` block. The GitHub Copilot SDK
and all provider plumbing are gone from the product.

**Why this priority**: This is the user-visible cleanup that motivated the work and the proof the SDK
is provably gone. It depends on US1 (the inverted path must exist before the SDK can be deleted).

**Independent Test**: Run `spectra init`; confirm no AI-provider question appears and the generated
config has no `ai.providers`/`ai.critic`. Grep confirms `Agent/Copilot/` is deleted and no
`config.Ai.Providers` read survives outside removed validation/display.

**Acceptance Scenarios**:

1. **Given** a fresh repo, **When** `spectra init` runs interactively, **Then** no "AI Provider Setup",
   model-preset, or critic-setup prompt is shown.
2. **Given** a completed `spectra init`, **When** the generated `spectra.config.json` is inspected,
   **Then** it has no `ai.providers` and no `ai.critic`.
3. **Given** the built product, **When** the source tree is searched, **Then** `Agent/Copilot/` does not
   exist and no file references the `Spectra.CLI.Agent.Copilot` namespace.

### User Story 3 - `docs index` is fast and index-only (Priority: P3)

A user runs `spectra docs index`. It builds the documentation index only and returns promptly, making no
model call. Acceptance-criteria extraction is a separate, explicit, skill-driven step — not a hidden
inline cost of indexing. There is exactly one criteria artifact (`.criteria.yaml`), not a parallel
`_requirements.yaml`.

**Why this priority**: Convergence + perf cleanup; valuable but depends on US1 (the canonical artifact
and the changed-docs primitive must exist first).

**Independent Test**: Run `docs index` on the demo corpus; confirm it emits only the index, makes no
model call, and no `docs/requirements/_requirements.yaml` is produced or consumed.

**Acceptance Scenarios**:

1. **Given** a workspace, **When** `spectra docs index` runs, **Then** it builds the doc index and makes
   no model/SDK call.
2. **Given** the converged design, **When** criteria are produced, **Then** they exist only as
   `AcceptanceCriterion`/`.criteria.yaml`; `RequirementsExtractor`, `RequirementDefinition`, and
   `_requirements.yaml` no longer exist.

### Edge Cases

- **Empty / whitespace document**: `compile-extraction-prompt` short-circuits to `Extracted, []` with no
  prompt emitted and no model turn; ingest persists an empty result without poisoning the cache.
- **Extraction turn truncated/garbled**: ingest fails loud (distinct empty vs parse-failure outcomes),
  persists nothing, leaves the index byte-unchanged.
- **Mixed changed/unchanged corpus**: only changed docs (by SHA-256) get a model turn; unchanged docs
  are skipped deterministically.
- **Import file with compound bullet criteria**: with splitting dropped, the compound criterion is
  imported verbatim as a single deterministic record (no model call); behavior is documented.
- **Existing workspace whose `spectra.config.json` still has `ai.providers`/`ai.critic`**: extra keys are
  ignored (forward-compatible deserialization); no failure, no requirement to migrate.

## Requirements *(mandatory)*

### Functional Requirements

**Rescue (MUST precede any deletion)**

- **FR-001**: The system MUST relocate the model-free members currently inside `Agent/Copilot/` —
  `CriteriaExtractor.ClassifyResponse`, `NormalizePriority` + `NormalizeTechniqueHint`
  (`CriteriaExtractor.cs:190-262`), the `ExtractionOutcome` enum, and the `CriteriaExtractionResult` /
  `RequirementsExtractionResult` records — into a model-free namespace (e.g. `Spectra.CLI.Criteria`),
  repointing the two existing consumers `CriteriaIngestor.cs:38` and `IngestCriteriaCommand.cs:3` (drop
  their `using Spectra.CLI.Agent.Copilot;`).
- **FR-002**: After FR-001, no file outside `Agent/Copilot/` MUST reference the
  `Spectra.CLI.Agent.Copilot` namespace.

**Skill rewiring**

- **FR-003**: The `spectra-criteria` extraction flow MUST drive a per-document loop mirroring the
  generation seam (Specs 053/059): per changed doc → `compile-extraction-prompt --doc <path>
  [--component <c>]` → in-session model turn → `ingest-criteria --doc <path> [--component <c>] --from
  stdin`. The loop MUST live in the **skill**, not the CLI.
- **FR-004**: Extraction MUST run as a main-session model turn — no extraction subagent / `context: fork`.

**Incremental-skip preservation**

- **FR-005**: The system MUST provide a model-free command that lists documents changed since the last
  index (SHA-256 compare via `FileHasher.ComputeFileHashAsync` against `CriteriaSource.doc_hash`), so the
  skill skips unchanged docs **without a model turn**.

**docs-index convergence**

- **FR-006**: `spectra docs index` MUST build the documentation index only; the inline extraction block
  (`DocsIndexHandler.cs:194-200`) MUST be removed. `--skip-criteria` MUST become a no-op or be removed.
- **FR-007**: `RequirementsExtractor`, `RequirementDefinition`, and the `docs/requirements/_requirements.yaml`
  artifact MUST be retired. Criteria extraction MUST be exclusively the skill-driven `.criteria.yaml` path.

**Import**

- **FR-008**: Import compound-splitting MUST be dropped — `CriteriaExtractor.SplitAndNormalizeAsync`
  (`:109-151`) and `BuildSplitPrompt` removed; import (`AnalyzeHandler.cs:956/959`) becomes a
  deterministic pass-through (the former `--skip-splitting` behavior, now the only behavior). Whether the
  `--skip-splitting` flag is removed or kept as an accepted no-op is the implementer's call and MUST be
  documented.

**SDK + config removal (only after FR-001..FR-008)**

- **FR-009**: `Agent/Copilot/` MUST be deleted entirely (`CopilotService`, `ProviderMapping`,
  `CriteriaExtractor`, `RequirementsExtractor`).
- **FR-010**: `ai.providers` and `ai.critic` MUST be removed from: the config model,
  `ConfigLoader.GenerateConfig`, the embedded `Templates/spectra.config.json`, validation
  (`ConfigLoader.cs:233,240` must not require providers), and display (`ConfigHandler.cs:203,206` must not
  print them).
- **FR-011**: All three interactive AI init steps MUST be removed from `InitHandler` —
  `InteractiveAuthSetupAsync` (`:160/:162`), `InteractiveModelPresetAsync` (Spec 041), and
  `InteractiveCriticSetupAsync`. `spectra init` MUST ask no AI-provider question.

**Preserved invariants (the seam contract — MUST stay byte-compatible)**

- **FR-012**: The outcome gate MUST be preserved — only `ExtractionOutcome.Extracted` persists;
  `EmptyResponse`/`ParseFailure` write nothing and leave the index byte-unchanged
  (`CriteriaIngestor.cs:80-83`).
- **FR-013**: The ID scheme MUST be preserved — `AC-{COMPONENT}-{NNN}`, reused by exact text match against
  the existing per-doc file (`:101-128`); never re-numbered on re-extraction.
- **FR-014**: Serialization MUST stay byte-compatible — per-doc `docs/criteria/{component}.criteria.yaml`
  (`CriteriaFileWriter`, atomic temp+rename, `OmitNull|OmitDefaults`) and `docs/criteria/_criteria_index.yaml`
  (`CriteriaIndexWriter`, recomputes `total_criteria` on every write), with the `AcceptanceCriterion`
  field shape unchanged.
- **FR-015**: The seam CLI signatures MUST stay unchanged — `compile-extraction-prompt` (prompt→stdout,
  exit 0 / 4 refused / 1 error, empty-source short-circuit) and `ingest-criteria`
  (exit 0 / 1 error / 5 empty / 6 parse).
- **FR-016**: RFC 2119 normalization MUST stay where it is — the prompt instruction in model-free
  `ExtractionPromptCompiler.cs:97-100`; the post-processing normalizers rescued by FR-001.

**Regression net (inviolable)**

- **FR-017**: `Spectra.Core` and `TestPersistenceService` tests MUST NOT be modified. A failure there is
  cause to investigate, not edit. All rescued helpers live in `Spectra.CLI`; `Spectra.Core`
  (`CriteriaMerger`, `CriteriaFileWriter`, `CriteriaIndexWriter`, `AcceptanceCriterion`) is untouched.
- **FR-018**: Existing `CriteriaIngestor` / seam tests MUST still pass after the rescue relocation
  (namespace change only).
- **FR-019** (scoped carve-out from FR-017, authoritative): The regression net protects **behavior**, not
  the retired config contract. To achieve FR-010/SC-007, the implementation MAY edit **only** the Core
  tests that assert provider/critic *presence* — explicitly `ConfigLoaderTests.GenerateDefaultConfig_ReturnsValidJson`
  (provider part), the `ConfigLoaderTests.Load_With*` provider pins (incl. removing the `MISSING_PROVIDERS`
  case), `ProviderRetirementTests`, and the `CriticConfig` presence tests. Each such edit MUST document
  which retired assertion it removes and why. **No vestigial/dummy `ai.providers` block** may be emitted —
  full removal from the generated config, validation, and init. Any failure of a Core test that is **not**
  about provider/critic presence is a real regression: investigate, do **not** edit. Behavioral Core tests
  (`CriteriaMerger`, the `*Writer`s incl. `RequirementsWriterTests`, `TestPersistenceService`, criteria,
  grounding, parsing) stay 100% in force.

### Key Entities

- **AcceptanceCriterion** — the single canonical criteria record (`id`, `text`, `rfc2119`, `source`,
  `source_type`, `source_doc`, `source_section`, `component`, `priority`, `tags`, `linked_test_ids`,
  `technique_hint`); persisted to per-doc `.criteria.yaml`.
- **CriteriaIndex / CriteriaSource** — the roll-up `_criteria_index.yaml` (`version`, `total_criteria`,
  `sources[]`); each source carries `file`, `source_doc`, `source_type`, `doc_hash` (incremental-skip
  key), `criteria_count`, timestamps, and `outcome` (`"extracted"` only ever persisted).
- **ExtractionOutcome** — `Extracted` | `EmptyResponse` | `ParseFailure`; gates persistence.
- **RequirementDefinition / `_requirements.yaml`** — the retired pre-023 model (removed by this feature).

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A repository-wide search for reads of the AI-provider configuration returns **zero**
  results outside the (removed) validation/display sites.
- **SC-002**: The `Agent/Copilot/` code area no longer exists, and no source references its namespace.
- **SC-003**: A clean release build succeeds and the full test suite passes, with `Spectra.Core` and
  `TestPersistenceService` tests **unmodified**.
- **SC-004**: `spectra docs index` completes making **zero** model/SDK calls and produces only the
  documentation index (no `_requirements.yaml`).
- **SC-005**: For the criteria-extract skill on the demo corpus, every changed document is processed via
  compile → in-session turn → ingest, and **unchanged documents are skipped with zero model turns**.
- **SC-006**: On the demo corpus, the produced `.criteria.yaml` / `_criteria_index.yaml` are
  **byte-identical** to a pre-inversion run (diff-clean).
- **SC-007**: `spectra init` presents **no** AI-provider question, and the generated `spectra.config.json`
  contains no `ai.providers` and no `ai.critic`.

## Assumptions

- A future additive sub-seam may reintroduce compound-splitting if real imports demand it; for now
  splitting is dropped, not relocated.
- Existing workspaces whose config still carries `ai.providers`/`ai.critic` keep working — the keys are
  ignored on load (no migration required, forward-compatible deserialization).
- The per-document loop maps 1:1 onto today's handler-owned loop
  (`AnalyzeHandler.RunExtractCriteriaAsync` foreach `:484`, `DocsIndexHandler.ExtractCriteriaLoopAsync`
  `~:386`), so moving it into skill orchestration is a relocation, not a redesign.
- The `--skip-splitting` flag disposition (removed vs accepted no-op) is left to the implementer and will
  be documented in the PR.

## Out of Scope

- Re-homing import compound-splitting onto a sub-seam (dropped, not relocated).
- Any change to generation (053/059), the critic subagent (055), or the MCP execution engine.
- Coverage/index lexical matching.
- Multi-client breadth or new provider abstractions.

## Dependencies & Phase Order (causal — do not reorder)

1. FR-001, FR-002 — rescue helpers (nothing deleted yet).
2. FR-003, FR-004, FR-005 — rewire skill + changed-docs command; criteria path runs inverted end-to-end.
3. FR-006, FR-007 — docs-index index-only; retire requirements path.
4. FR-008 — drop import splitting.
5. **Gate**: SC-004, SC-005, SC-006 pass (SDK still present but unused).
6. FR-009, FR-010, FR-011 — delete `Agent/Copilot/`, strip config schema + init steps.
7. **Gate**: SC-001, SC-002, SC-003, SC-007 pass.
