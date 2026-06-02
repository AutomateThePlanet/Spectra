# Feature Specification: Resilient Criteria Extraction

**Feature Branch**: `047-resilient-criteria-extraction`
**Created**: 2026-05-28
**Status**: Draft
**Input**: User description: "Make acceptance-criteria extraction resilient at scale by fixing two confirmed defects. (1) Cache poisoning: parse failures and empty-response transport errors are indistinguishable from a legitimate empty result, so the document is hashed and skipped on every future non-`--force` run. Change extraction to return a typed result carrying a status, record the hash ONLY on a genuine extraction result (including a real empty result), and on a parse/transport failure leave the cache untouched and retry up to 2 times with short backoff. (2) The `docs index` path applies a single 60-second deadline for the ENTIRE corpus, which silently aborts extraction on large projects; replace it with a per-document deadline matching the `ai analyze --extract-criteria` path. Out of scope: content truncation/token-budget on the extraction path, and merging the two extractor implementations — both deferred to a future spec."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Inconclusive extractions are retried, never silently cached (Priority: P1)

A QA engineer runs acceptance-criteria extraction across a large documentation set. For a handful of documents the AI returns a malformed or empty reply (network hiccup, truncated output, model glitch). Today, those documents get the same cache treatment as a legitimate "this document has no testable criteria" outcome: the document hash is recorded and the document is permanently skipped on subsequent non-`--force` runs. After this change, the system distinguishes genuine extraction results from transport/parse failures, retries inconclusive results a small bounded number of times, and only caches the hash when it has a real answer in hand. Documents that remain inconclusive after retries are surfaced as failures and re-attempted automatically on the next run.

**Why this priority**: This is the bug that silently strips coverage data from large projects and is invisible to the user — they end up with under-counted criteria forever unless they remember the `--force` escape hatch. Fixing this is the core value of the spec; everything else is supporting.

**Independent Test**: Drive the extractor with a stubbed AI response that simulates each failure mode (unparseable text, deserialize-null, empty response, thrown exception). Verify that (a) no cache entry is written, (b) the document appears in the failed-documents list, and (c) a follow-up run without `--force` re-invokes the AI for that document. Verify the inverse for a real empty result (a valid empty list): cache IS written and the next run skips it.

**Acceptance Scenarios**:

1. **Given** a document the AI returns unparseable text for, **When** extraction runs (no `--force`), **Then** no cache hash is recorded for that document, the document is listed under failed documents, and a subsequent run re-invokes the AI for it.
2. **Given** a document the AI returns a valid empty criteria list for, **When** extraction runs, **Then** the cache hash IS recorded with `criteria_count: 0`, and a subsequent run without `--force` skips the document.
3. **Given** a document whose first extraction attempt returns a parse failure but whose second attempt returns a valid criteria list, **When** extraction runs, **Then** the AI is called twice for that document, the valid result is cached, and the document is reported as successful.
4. **Given** a document whose every attempt returns a parse failure, **When** extraction runs, **Then** the AI is called exactly the configured maximum number of attempts (2), no cache hash is recorded, and the document is reported as failed.
5. **Given** a thrown exception or per-document timeout during extraction, **When** extraction runs, **Then** behaviour matches today: no hash recorded, document re-attempted next run, and no additional retries are triggered (exceptions/timeouts are not subject to the new retry policy).

---

### User Story 2 - Large-corpus `docs index` completes extraction instead of aborting at 60s (Priority: P1)

A QA engineer runs `spectra docs index` on a project with dozens of documentation files. Today, a single 60-second deadline wraps the entire extraction phase across all documents; on any reasonably large corpus this almost always fires, the extraction is aborted wholesale, and the only signal is one easy-to-miss warning line. After this change, timeouts are scoped to each individual document. The corpus continues processing even if a single document is slow, and only the truly slow documents are reported as timed out.

**Why this priority**: This is the most likely cause of the original "big project → no criteria" report and is independent of Defect 1. Without this fix, the typed-result/retry work from Story 1 is still bypassed entirely on the `docs index` entry point because the run never gets that far.

**Independent Test**: Run `docs index` against a corpus large enough that the cumulative extraction time exceeds 60 seconds, with each individual document completing well within its per-document timeout. Verify that all documents are extracted (none aborted by the old corpus-level deadline). Separately, simulate one slow document among many fast ones and verify only the slow document is reported as timed out.

**Acceptance Scenarios**:

1. **Given** a corpus whose total extraction time exceeds 60 seconds but where every individual document completes within the per-document timeout, **When** `docs index` runs, **Then** every document is extracted and none are reported as timed out.
2. **Given** a corpus where one document exceeds the per-document timeout and the rest complete normally, **When** `docs index` runs, **Then** only the slow document is reported as timed out and the others are extracted normally.
3. **Given** any document that times out during `docs index`, **When** the run completes, **Then** the user sees the same "run `spectra ai analyze --extract-criteria` separately" guidance message scoped to that specific document, not to the whole run.

---

### User Story 3 - Failed-document visibility in reports and exit codes (Priority: P2)

When extraction declines to cache a result, the user needs to see it. Inconclusive documents (parse failure, empty response, per-document timeout) appear in the existing `documents_failed` count and `failed_documents` list in the structured output, and the process exit code reflects the failure consistent with today's failure-reporting behaviour. CI pipelines that consume these signals continue to work without changes.

**Why this priority**: The fix is only useful if the user can see what was deferred. Without surfacing the failures, "silently skipped" becomes "silently retried forever," which is also bad. This piggy-backs on the existing reporting mechanism, so it is a small but essential complement to Stories 1 and 2.

**Independent Test**: Run extraction against a corpus including documents that will produce each non-cacheable outcome (parse failure after retries, empty response after retries, per-document timeout). Verify the structured output's `documents_failed` count, `failed_documents` list contents, and the process exit code match today's failure-reporting conventions.

**Acceptance Scenarios**:

1. **Given** N documents that yield non-cacheable outcomes after retries, **When** extraction completes, **Then** `documents_failed` equals N and `failed_documents` lists each of their paths.
2. **Given** at least one non-cacheable document in a run, **When** extraction completes, **Then** the process exit code matches the existing convention for partial-failure runs.

---

### Edge Cases

- **Document with empty/whitespace input content** (file exists but is empty): treated as a genuine "nothing to extract" result. Cache hash IS recorded so the system does not re-read the empty file on every run; criteria count is 0.
- **AI returns valid JSON that deserializes to an empty list**: real empty result. Cache hash IS recorded; criteria count is 0; next run skips the document.
- **Transient failure followed by success within the retry budget**: cached normally; counted as a success, not a failure.
- **Persistent parse failure exhausting the retry budget**: no cache hash; document appears in failed list; next run re-attempts from scratch.
- **A thrown exception bubbling out of the extractor** (e.g. I/O error, cancellation): not subject to the new bounded-retry policy; falls through to today's "continue without caching" behaviour. The same applies to per-document timeouts.
- **`--force` flag**: continues to bypass the cache entirely and forces re-extraction; unchanged.
- **A single document that hangs forever on the `docs index` path**: bounded by its own per-document timeout, does not block the rest of the corpus.
- **Hash-compute failure for the source document**: unchanged from today — document is skipped without cache write and re-attempted next run.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The criteria extractor MUST distinguish three outcome classes for any single-document extraction: a genuine extraction result (including a real empty list), an empty-response transport-class failure, and a parse-class failure (no parseable structure, null deserialization, or any exception raised while parsing the response).
- **FR-002**: The system MUST record the document's cache hash ONLY when the extraction outcome is a genuine extraction result. A genuine empty result (valid response that legitimately contained no criteria) IS cacheable; an empty AI response or a parse failure is NOT cacheable.
- **FR-003**: When the input content for a document is empty or whitespace, the system MUST treat it as a genuine extraction result with zero criteria (cacheable), so an empty source file is not re-read on every subsequent run.
- **FR-004**: When an exception is raised inside the response parser, the system MUST log the exception's reason and return a parse-class failure outcome. The system MUST NOT silently swallow the exception and return an empty list.
- **FR-005**: The system MUST retry a non-cacheable outcome (empty response or parse failure) up to a bounded maximum of 2 attempts per document with a short backoff between attempts. The system MUST NOT retry genuine extraction results (success) and MUST NOT subject thrown exceptions or per-document timeouts to this retry policy.
- **FR-006**: A document that yields a non-cacheable outcome after the retry budget is exhausted MUST be re-attempted on the next non-`--force` run (i.e. no hash is written, so the cache-skip gate does not fire).
- **FR-007**: The `docs index` extraction phase MUST apply a per-document timeout consistent with the `ai analyze --extract-criteria` path (2 minutes per document) and MUST NOT apply a single deadline across the whole corpus.
- **FR-008**: When a single document times out during `docs index`, the remaining documents in the corpus MUST continue to be processed normally.
- **FR-009**: The user-facing guidance to "run `spectra ai analyze --extract-criteria` separately" MUST be scoped to the specific document(s) that timed out, not raised as a corpus-wide warning.
- **FR-010**: Documents with non-cacheable outcomes (after retries) and documents that timed out MUST be reported via the existing `documents_failed` counter and `failed_documents` collection in the structured output, and MUST be reflected in the process exit code per the existing failure-reporting convention.
- **FR-011**: The `--force` flag MUST continue to bypass the cache and force re-extraction, unchanged by this work.
- **FR-012**: No changes shall be made in this work to the criteria file writer, criteria index writer, criteria source model, or the file hasher. (Empty-file-on-disk distinguishability and other writer-side concerns are deferred to a future spec.)
- **FR-013**: No content truncation or token-budget enforcement shall be added to the extraction path in this work. (Deferred to a future spec.)
- **FR-014**: The two extractor implementations (`docs index` path and `ai analyze` path) shall not be merged in this work. (Deferred to a future spec.) Only the corpus-level deadline behaviour on the `docs index` side is changed.

### Key Entities *(include if feature involves data)*

- **Extraction Outcome**: One of three states classifying what happened when the system asked the AI to extract criteria from a single document — `Extracted` (a valid response, possibly empty), `EmptyResponse` (the AI returned nothing usable), or `ParseFailure` (the AI returned something that could not be interpreted as a criteria list).
- **Extraction Result**: The pair of (outcome, criteria list) returned for a single document. The criteria list is empty for any non-`Extracted` outcome. Only `Extracted` results are eligible to write a cache hash.
- **Per-Document Cache Entry**: The persisted record (document path, content hash, criteria count, criteria file) that lets the system skip unchanged documents on subsequent runs. Created only for cacheable outcomes; unchanged in shape by this work.
- **Failed-Document Report**: The existing list of document paths whose extraction did not produce a cacheable result, surfaced alongside the existing `documents_failed` counter and contributing to the process exit code.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: On a corpus where some fraction of documents produce a parse-class or empty-response outcome on a given run, 100% of those documents are re-attempted on the next non-`--force` run (no permanent silent skipping).
- **SC-002**: On a corpus whose cumulative extraction time exceeds 60 seconds, 100% of documents that individually complete within the per-document timeout produce a cached result on a single `docs index` run, instead of 0% under the previous corpus-level deadline.
- **SC-003**: For any document that yields a non-cacheable outcome, the maximum number of AI calls per run is bounded at exactly 2 (the retry budget); no document can drive more than 2 AI calls in a single run via this code path.
- **SC-004**: The count of documents reported in `documents_failed` matches the count of documents for which no cache hash was written in the same run, with no silent gap between "skipped" and "reported."
- **SC-005**: Genuine empty-criteria documents (real `[]` from the AI, or empty source file) are cached on the first successful run and produce zero AI calls on subsequent non-`--force` runs.
- **SC-006**: Every parse-class failure path emits a log entry identifying the reason (instead of being silently swallowed), so the user can diagnose why a document was deferred without re-running with extra diagnostics.

## Assumptions

- The existing 2-minute per-document timeout used by the `ai analyze --extract-criteria` path is the right value to mirror on the `docs index` path. If profiling later shows it is too long or too short, that's a separate tuning concern, not a scope question for this spec.
- "Short backoff" between retries is on the order of 1.5–3 seconds. Exact values are an implementation detail; the bound that matters is the 2-attempt cap.
- The existing failure-reporting plumbing (`documents_failed`, `failed_documents`, exit code) is the right channel to surface non-cacheable outcomes; no new reporting surface is introduced.
- Logging of the swallowed parse-exception goes through the existing extractor logger; no new logging infrastructure is introduced.
- The `--force` cache-bypass flag remains the documented escape hatch and does not need additional flags to disambiguate "force one document" vs "force all" in this spec.
- Per the included file audit, no model or writer changes are needed to implement the required behaviour; only the extractor, the caller (analyze handler), and the docs-index handler change.

## Out of Scope

- **Content truncation / token-budget enforcement on the extraction path.** Capping content blindly risks dropping criteria from the tail of a document silently and needs its own design (sliding window? per-section? configurable cap?). Deferred to a future spec.
- **Merging the two extractor implementations** (`docs index` path and `ai analyze --extract-criteria` path) into one. A refactor with regression surface across two commands; should not ride along with a bug fix. Deferred to a future spec.
- **Empty-file-on-disk distinguishability** and other criteria writer/index/source model changes. Deferred to a future spec (047 in the original problem note).
- **Rate-limit retry policy** for the AI call itself. Today's bare log-and-skip on rate-limit is unchanged here; would be a separate resilience improvement.
