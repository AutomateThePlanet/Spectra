# Feature Specification: Criteria Extraction Re-homing + Extractor Unification

**Feature Branch**: `054-criteria-extraction-rehoming`
**Created**: 2026-06-05
**Status**: Draft
**Input**: User description: "Spec 053 — Criteria extraction re-homing + extractor unification. Move criteria extraction off the model call into the interactive agent, keep the typed-result boundary, and unify the two divergent extractors onto one contract."

> **Series note**: This is migration spec **2 of 6** (the 052–057 series). The conceptual numbering in the source material calls this "Spec 053"; in the repository it lives in directory `054-` because of the established one-step offset (conceptual spec *N* → directory *N+1*). It reuses the prompt-compile → validate → choreography-retry pattern introduced by the prompt-compiler work that precedes it in the series.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Extract criteria without a model call inside the CLI (Priority: P1)

A developer points the tool at a documentation file (or corpus) to extract acceptance criteria. The CLI deterministically compiles an extraction prompt from the document content and emits it. The interactive agent performs the extractive turn in its own context. The CLI then ingests the agent-produced content, classifies it into a typed outcome (`Extracted | EmptyResponse | ParseFailure`), and — when the outcome is genuinely extractable — caches it. At no point does the CLI itself open a session and call a model to do the extraction.

**Why this priority**: This is the core inversion the spec exists to deliver. It removes the in-process model dependency from the extraction code paths, making extraction deterministic and token-free to test, and aligning extraction with the handoff pattern the rest of the migration series adopts. Without it, none of the other stories have a foundation.

**Independent Test**: Run the extraction prompt-compilation step on a document with known criteria text and assert the compiled prompt is byte-identical for identical input and contains the document content; then feed a representative agent response to the classification step and assert the outcome is `Extracted` with the parsed criteria. Both halves are verifiable with no model/provider configured.

**Acceptance Scenarios**:

1. **Given** a document with extractable acceptance criteria, **When** extraction runs, **Then** the agent returns criteria, the CLI classifies the outcome as `Extracted`, and the result is written to the content-hash cache.
2. **Given** identical document input on two separate invocations, **When** the prompt is compiled both times, **Then** the two compiled prompts are byte-identical (no timestamps, GUIDs, or unordered content).
3. **Given** an extraction request, **When** the CLI runs it end to end, **Then** no model/provider session is opened by the CLI extraction code itself to produce the criteria.

---

### User Story 2 - Empty or whitespace documents short-circuit with no model turn (Priority: P1)

A developer runs extraction against a document that is empty or contains only whitespace. The CLI recognizes there is genuinely nothing to extract and returns an `Extracted` outcome with an empty criteria list — without compiling a prompt for, or handing off to, the agent. This "nothing to extract" result is treated as a legitimate, cacheable outcome.

**Why this priority**: This guards a common real-world case (placeholder or stub docs) and preserves an existing optimization. Losing it would either waste an agent turn on empty input or, worse, misclassify "empty source" as a failure and trigger pointless retries. It is P1 because the spec explicitly requires the short-circuit be preserved and it is cheap to verify.

**Independent Test**: Call the extraction entry point with an empty string and with a whitespace-only string; assert each returns outcome `Extracted` with zero criteria and that no prompt was compiled and no handoff was requested.

**Acceptance Scenarios**:

1. **Given** a whitespace-only or empty document, **When** extraction runs, **Then** the CLI returns `Extracted` with an empty criteria list and performs no agent handoff.
2. **Given** that short-circuit result, **When** caching is evaluated, **Then** the result is treated as cacheable (genuine "nothing to extract").

---

### User Story 3 - Malformed agent responses become typed failures, never exceptions (Priority: P1)

A developer's extraction run receives an agent response that is malformed, empty, or otherwise unparseable. The CLI classifies it into a typed `ParseFailure` or `EmptyResponse` outcome rather than throwing. The bad result is **not** written to the cache, and the surrounding choreography retries up to the configured limit; if every attempt remains non-extractable, the developer sees the document reported as inconclusive/failed (not a crash) and it is left to be retried on a future run.

**Why this priority**: This is the failure-semantics contract the whole migration relies on. A throwing extractor aborts batch runs and (historically) could poison the cache. Keeping failures typed, non-cached, and retryable is what makes the system resilient. P1 because both extractor paths must converge on exactly this behavior.

**Independent Test**: Feed the classification step a malformed response and an empty response; assert it returns `ParseFailure` / `EmptyResponse` respectively, never throws, and reports the result as non-cacheable. Drive the retry choreography with a stub that returns a non-cacheable outcome and assert it retries to the limit and never writes cache.

**Acceptance Scenarios**:

1. **Given** a malformed agent response, **When** the CLI classifies it, **Then** it returns a typed `ParseFailure` (it does not throw) and the result is non-cacheable.
2. **Given** an empty agent response, **When** the CLI classifies it, **Then** it returns a typed `EmptyResponse` (it does not throw) and the result is non-cacheable.
3. **Given** a non-cacheable outcome, **When** the choreography evaluates retry, **Then** it retries up to the limit and stops; a cacheable (`Extracted`) outcome is never retried.
4. **Given** any non-cacheable outcome at any attempt, **When** caching is evaluated, **Then** nothing is written to the content-hash cache for that document.

---

### User Story 4 - `docs index` uses the same typed contract as `--extract-criteria` (Priority: P2)

A developer runs `docs index`, which extracts requirements/criteria across the documentation corpus. After this change, that path consumes the **same** typed extraction contract as `ai analyze --extract-criteria`: it returns a typed outcome and no longer throws on empty input or timeout. A single slow, empty, or malformed document is reported as a failed/inconclusive document and the corpus run continues, rather than the whole command aborting on an exception.

**Why this priority**: Unifying the legacy extractor removes a second, divergent failure contract and a fragile throwing path. It is P2 (not P1) because it builds on the typed boundary established in Stories 1–3, but it is still core to the spec's stated goal of "one contract after migration, not two."

**Independent Test**: Drive the unified extractor with empty input and with a simulated timeout; assert each returns a typed outcome (no exception). Run a `docs index` over a small corpus that includes one empty and one good document; assert the good document yields criteria, the empty one is recorded as a typed non-extractable outcome, and the command completes without throwing.

**Acceptance Scenarios**:

1. **Given** an empty document on the `docs index` path, **When** extraction runs, **Then** it returns a typed outcome (no throw).
2. **Given** a document that times out on the `docs index` path, **When** extraction runs, **Then** it returns a typed non-cacheable outcome (no throw) and the document is surfaced in the failed/inconclusive count.
3. **Given** a corpus with a mix of good and non-extractable documents, **When** `docs index` runs, **Then** good documents produce cached criteria and the run completes, surfacing failed documents via the existing failed-document reporting.

---

### Edge Cases

- **Empty vs. failed ambiguity**: An empty *source document* is `Extracted, []` (cacheable); an empty *agent response* to a non-empty source is `EmptyResponse` (non-cacheable, retryable). These two must not be conflated — the short-circuit decision is made on the source before any handoff.
- **Missing required input at compile time**: If the prompt compiler is asked to emit with required input absent, it must refuse to emit rather than produce a degenerate prompt (mirroring the established compiler-refusal behavior).
- **Retry exhaustion**: When every attempt for a document returns a non-cacheable outcome, the document is reported as inconclusive/failed and left for a future run; the cache for that document remains untouched.
- **Cache-hit path**: A document whose content hash already has a cached `Extracted` result skips both prompt compilation and handoff (existing cache behavior is unchanged).
- **Timeout on the unified path**: A timed-out document on `docs index` must become a typed non-cacheable outcome (not a thrown `TimeoutException`), counted among failed documents, without aborting the corpus.
- **Whitespace-only response**: An agent response that is only whitespace is treated as `EmptyResponse`, not `Extracted, []`.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The CLI's criteria-extraction code paths MUST NOT call any model/provider to perform extraction. The in-process model calls in both the typed extractor and the legacy extractor MUST be removed from the extraction flow.
- **FR-002**: Extraction prompt compilation MUST follow the established prompt-compiler pattern: deterministic (byte-identical output for identical input), testable without any model/provider or token budget, and it MUST refuse to emit when a required input is missing rather than producing a degenerate prompt.
- **FR-003**: The CLI MUST ingest agent-extracted content through the existing typed boundary, producing a `CriteriaExtractionResult` with outcome `Extracted | EmptyResponse | ParseFailure`. The empty-source short-circuit — returning `Extracted` with an empty criteria list, with no prompt compilation and no agent handoff — MUST be preserved.
- **FR-004**: The legacy requirements/criteria extractor used by `docs index` MUST be unified onto the typed-outcome contract: it MUST return a typed result and MUST NOT throw on empty input or on timeout. The `docs index` command MUST consume this unified contract.
- **FR-005**: The extraction retry MUST run as skill/agent choreography (not as an in-process model-call retry), while preserving the existing semantics: retry ONLY on non-cacheable outcomes (`ParseFailure` / `EmptyResponse`), stop at the configured attempt limit, and never write a non-cacheable result to the cache.
- **FR-006**: The content-hash cache contract MUST be unchanged: a result is cacheable if and only if its outcome is `Extracted`; only `Extracted` results are written to the cache. No bad result may poison the cache.
- **FR-007**: After this change there MUST be exactly one "extract criteria from a document" failure-semantics contract. The previously divergent throwing legacy path MUST no longer exist as a second contract.
- **FR-008**: Both `ai analyze --extract-criteria` and `docs index` MUST share the unified extraction contract end to end (compile → handoff → classify → retry → cache), differing only in their per-command orchestration, not in their extraction failure semantics.

### Reused Verbatim *(must not be modified)*

- **The typed result type** `CriteriaExtractionResult` and its `ExtractionOutcome` enum (`Extracted | EmptyResponse | ParseFailure`), including the `IsCacheable => Outcome == Extracted` rule.
- **The classification logic** `ClassifyResponse` (the pure function that maps a response to an outcome).
- **The content-hash cache** and its gating on cacheability.
- **`Spectra.Core` requirements parsing.** All of the above remain model-free and are the boundary this spec builds on.

### Key Entities

- **Extraction prompt**: The deterministic, compiled instruction set derived from a document's content, handed to the interactive agent. Carries no model/provider configuration; identical input yields identical text.
- **`CriteriaExtractionResult`**: The typed outcome of ingesting an agent response — one of `Extracted` (with criteria, cacheable), `EmptyResponse` (non-cacheable), or `ParseFailure` (non-cacheable).
- **Acceptance criterion**: A single extracted criterion parsed from agent output (the payload of an `Extracted` result), persisted via the existing criteria index/files.
- **Content-hash cache entry**: A per-document cached `Extracted` result keyed by document content hash; written only for cacheable outcomes.
- **Unified extractor contract**: The single "extract criteria from a document" failure-semantics interface that both the `--extract-criteria` path and the `docs index` path consume after migration.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Zero model/provider calls originate from either CLI extraction code path during extraction — verifiable by running both `ai analyze --extract-criteria` and `docs index` with no provider configured and confirming prompt compilation and response classification complete without attempting a session.
- **SC-002**: Exactly one extraction failure-semantics contract exists after the change: the legacy extractor returns a typed outcome in 100% of empty-input and timeout cases and throws in 0% of them.
- **SC-003**: The empty-source short-circuit produces `Extracted` with an empty criteria list and triggers zero prompt compilations and zero agent handoffs, in 100% of empty/whitespace-source cases.
- **SC-004**: The cache is never poisoned: across runs that include malformed and empty agent responses, the number of non-cacheable outcomes written to the content-hash cache is 0.
- **SC-005**: Retry choreography preserves original semantics: non-cacheable outcomes retry up to the configured limit (and no further), and cacheable outcomes are retried 0 times.
- **SC-006**: The protected regression net is unchanged and green: all `Spectra.Core` parsing/requirements tests and all `CriteriaExtractionResult` / `ClassifyResponse` classification tests pass without modification.
- **SC-007**: A `docs index` run over a mixed corpus (good + empty + slow/timeout documents) completes without throwing, produces criteria for the good documents, and reports the non-extractable documents via the existing failed-document count/list.

## Assumptions

- **Retry limit**: The choreography preserves the existing attempt limit (historically two attempts total — one retry — for non-cacheable outcomes). The exact value is inherited from current configuration, not redefined by this spec.
- **CLI surface shape**: Extraction adopts the same compile/ingest surface shape established by the preceding prompt-compiler work in this series (a deterministic prompt-compilation surface plus a classification/ingest surface), rather than introducing a novel interaction model. Exact command names follow the series' existing naming conventions.
- **Series ordering dependency**: This spec assumes the prompt-compiler/handoff pattern from the preceding spec is in place and "fixed" (i.e., the model-call inversion, not just the CLI compile surface, is available to mirror). If only the compile surface exists, closing that gap is a prerequisite to this spec, not part of its scope.
- **Two extractor implementations may remain distinct types**: Unification is of the *failure-semantics contract* (typed outcome, no throwing), not necessarily a merge into a single class. The Spec 047 decision to keep two extractor implementations for unrelated reasons is **not** re-opened; only the contract converges.
- **No new persisted data model**: The criteria index, criteria files, and cache key/format are unchanged.

## Out of Scope

- The generation handoff itself (the preceding spec in the series) — this spec reuses its pattern but does not re-implement it.
- The Critic path (a later spec in the series).
- The generation/extraction **skills** that invoke these CLI surfaces (a later spec) — only the CLI-side contract changes here.
- Provider/runtime retirement (a later spec).
- Re-opening the Spec 047 decision to keep extractors as separate implementations for reasons unrelated to failure semantics.
- Any change to the cache key/format, the criteria index schema, or `Spectra.Core` parsing.

## Dependencies

- **Preceding series spec (prompt-compiler / generation handoff)**: This spec reuses its prompt-compile → validate → choreography-retry contract. Work should begin once that pattern is fixed (the model-call inversion available to mirror, not just the standalone compile command).

## Documentation Impact

- **Factually wrong (must fix)**: Criteria-extraction documentation and `docs index` documentation that describes provider/model behavior performed inside the CLI, or that describes the throwing legacy path, becomes incorrect and must be corrected.
- **Stale (update)**: The criteria workflow page must be updated to reflect the compile → agent-handoff → classify → retry choreography.

## Tests

- **Rewrite (covers deleted/dead behavior)**: Tests asserting in-process model calls in either extractor; tests asserting the legacy extractor throws on empty input or on timeout.
- **Do not touch (regression net)**: All `Spectra.Core` parsing/requirements tests; all `CriteriaExtractionResult` / `ClassifyResponse` classification tests. If one of these breaks, it signals a regression to investigate, not a test to update.
- **Net-new**:
  - Unified-extractor typed-result tests: the legacy extractor returns a typed outcome (no throw) on empty input and on timeout; `docs index` consumes the typed outcome (FR-004, FR-007, FR-008).
  - Extraction-choreography retry tests: a non-cacheable outcome retries and stops at the limit; a cacheable outcome is never retried; the cache is never poisoned (FR-005, FR-006).
  - Empty-source short-circuit test: empty/whitespace source returns `Extracted, []` with no prompt compilation and no agent handoff (FR-003).
  - Deterministic compile test: identical document input yields byte-identical compiled prompts and the compiler refuses to emit on missing required input (FR-002).
