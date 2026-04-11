# Feature Specification: Parallel Critic Verification & Error Log

**Feature Branch**: `042-parallel-critic-error-log`
**Created**: 2026-04-11
**Status**: Draft
**Input**: Spec 043 — parallel critic verification + dedicated error log + rate limit visibility in run summary.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Faster Critic Verification on Large Suites (Priority: P1)

A QA lead runs `spectra ai generate checkout` and waits ~46 minutes for 238 tests. Roughly half of that time (~23 min) is the critic verifying tests one at a time. Each call is independent, so parallelizing them should cut the run dramatically. The user wants a knob to set how many critic calls run concurrently — defaulting to today's behavior (1) so nothing breaks for existing setups, but raising to 5 or 10 should make the critic phase 5–10× faster.

**Why this priority**: Critic time is the dominant cost on big runs. A single config knob unlocks a 4–5× speedup with no behavior change for existing users.

**Independent Test**: Set `ai.critic.max_concurrent: 5` in `spectra.config.json`, run `spectra ai generate <suite>` on a suite with ≥20 tests, observe the critic phase completes in ~1/5 the time. Final test files and verdicts must be identical to a sequential run.

**Acceptance Scenarios**:

1. **Given** `max_concurrent: 1` (default), **When** generate runs, **Then** critic calls execute sequentially exactly as today.
2. **Given** `max_concurrent: 5` and 20 tests, **When** the critic phase runs, **Then** at most 5 verifications are in flight at once and total elapsed ≈ (20/5) × per-call time.
3. **Given** parallel verification completes out of order, **When** results are written to test files and the index, **Then** order matches the original input list.
4. **Given** one critic call throws mid-batch, **When** other calls are still running, **Then** they continue, the failed test gets an `Unverified` verdict, and the error is recorded.

---

### User Story 2 - Dedicated Error Log for Failed Calls (Priority: P1)

When a run fails or produces unexpected verdicts, the user wants to know *why*. Today the debug log shows hundreds of OK lines and only a one-line summary on failure — exception details, HTTP response bodies, and rate-limit headers are lost. The user wants a separate `.spectra-errors.log` that is empty on healthy runs and contains full exception context on failures, so a single `cat .spectra-errors.log` answers "did anything go wrong?".

**Why this priority**: Without error context, debugging timeouts, rate limits, and parse failures requires re-running with extra logging. A persistent error log makes incidents diagnosable from the existing run.

**Independent Test**: Configure a failing critic provider (bad API key), run generate, verify `.spectra-errors.log` is created with full exception type, message, stack, and (when available) response body.

**Acceptance Scenarios**:

1. **Given** `debug.enabled: true` and a healthy run, **When** generate completes, **Then** `.spectra-errors.log` is not created or modified.
2. **Given** a critic call returns HTTP 429, **When** the error is caught, **Then** `.spectra-errors.log` records test id, exception type, message, response body, retry-after value, and stack; the debug log gets a short `CRITIC ERROR ... see=.spectra-errors.log` cross-reference.
3. **Given** a generation batch times out, **When** the timeout fires, **Then** the error log captures the batch number, configured timeout, elapsed time, and stack.
4. **Given** `debug.enabled: false`, **When** an error occurs, **Then** no error log file is created (consistent with debug log behavior).

---

### User Story 3 - Rate Limit & Error Counts in Run Summary (Priority: P2)

After a run finishes, the user sees the existing Run Summary panel with token counts and elapsed time. They want two new fields — `Errors` and `Rate limits` — plus the active `Critic concurrency` setting. If rate limits occurred, a hint suggests reducing `max_concurrent`. The same counts appear in the `RUN TOTAL` line of the debug log so CI can grep for `rate_limits=` or `errors=`.

**Why this priority**: Manual tuning of `max_concurrent` needs visibility into whether the chosen value is causing throttling. Without surfacing the count, users can't tune intelligently.

**Independent Test**: Force rate limits (e.g., set `max_concurrent: 20` against a low-quota provider), run generate, verify the Run Summary shows `Rate limits: N` with the tuning hint and the debug log's `RUN TOTAL` line includes `rate_limits=N errors=N`.

**Acceptance Scenarios**:

1. **Given** a clean run, **When** the summary renders, **Then** `Errors 0` and `Rate limits 0` appear (no hint).
2. **Given** 3 rate-limited calls during a run, **When** the summary renders, **Then** `Rate limits 3 (consider reducing ai.critic.max_concurrent)` appears.
3. **Given** any run, **When** the `RUN TOTAL` line is emitted, **Then** it ends with `rate_limits=<n> errors=<n>`.

---

### Edge Cases

- **`max_concurrent` ≤ 0**: clamp to 1 (sequential).
- **`max_concurrent` > 20**: clamp to 20 with a warning. Values > 10 emit a warning at startup about rate limit risk.
- **All tests fail in parallel**: every test gets an `Unverified` verdict; run still produces output.
- **Error log file is locked / unwritable**: log a single warning to stderr, continue the run (best-effort).
- **Concurrent writers to error log**: serialized via lock.
- **Mixed success/failure in a parallel batch**: results preserve original order; failed entries carry `Unverified` verdict with the exception message.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST support a configurable maximum number of concurrent critic verification calls via `ai.critic.max_concurrent` (integer, default 1).
- **FR-002**: System MUST clamp `max_concurrent` to the inclusive range [1, 20]; ≤0 clamped silently, >20 clamped with stderr warning, >10 emits a rate-limit-risk warning at run start.
- **FR-003**: When `max_concurrent` is 1, the critic MUST execute calls sequentially with behavior identical to the current implementation (backward compatibility).
- **FR-004**: When `max_concurrent` is N (N>1), the critic MUST allow up to N verification calls in flight concurrently using a semaphore-based throttle.
- **FR-005**: Verified test results MUST be returned to the caller in the same order as the input test list, regardless of completion order.
- **FR-006**: A failure in one critic call MUST NOT abort other in-flight or pending calls; failed tests MUST receive an `Unverified` verdict carrying the exception message.
- **FR-007**: System MUST write a dedicated error log file (default `.spectra-errors.log`) whenever `debug.enabled: true` AND at least one error occurs during the run.
- **FR-008**: The error log path MUST be configurable via `debug.error_log_file` (default `.spectra-errors.log`) and MUST follow the same `debug.mode` (append/overwrite) semantics as the debug log.
- **FR-009**: When no errors occur during a run, the error log file MUST NOT be created or modified.
- **FR-010**: Each error log entry MUST include: ISO-8601 UTC timestamp, phase tag (critic/generate/analyze/update/criteria), context (test id, batch number, etc.), exception type, message, full stack trace, and (when available) HTTP response body truncated to 500 characters and `Retry-After` header value.
- **FR-011**: System MUST capture errors at all of the following sites: `BehaviorAnalyzer`, `CopilotGenerationAgent`, `CopilotCritic`, `UpdateHandler`, `CriteriaExtractor`, and `GenerateHandler.VerifyTestsAsync`.
- **FR-012**: When an error is captured to the error log, the corresponding debug log line MUST include `see=<error_log_file>` so a reader can locate the full context.
- **FR-013**: System MUST detect HTTP 429 / rate-limit failures distinctly from generic exceptions and increment a `rate_limits` counter visible in the run summary.
- **FR-014**: System MUST track a total `errors` counter across all phases and surface it in the run summary.
- **FR-015**: The `RUN TOTAL` debug log line MUST include `rate_limits=<n>` and `errors=<n>` suffixes.
- **FR-016**: The terminal Run Summary panel MUST display `Errors`, `Rate limits`, and `Critic concurrency` fields. When `Rate limits > 0`, a hint `(consider reducing ai.critic.max_concurrent)` MUST appear next to the count.
- **FR-017**: Error log writes MUST be thread-safe (multiple async tasks may write concurrently without corruption).
- **FR-018**: If the error log file cannot be written (permission denied, disk full), the run MUST continue and emit a single stderr warning; error logging failures MUST NOT abort the run.

### Key Entities

- **CriticConfig.MaxConcurrent**: New integer property, default 1, JSON name `max_concurrent`. Clamped [1, 20].
- **DebugConfig.ErrorLogFile**: New string property, default `.spectra-errors.log`, JSON name `error_log_file`.
- **ErrorLogger**: New service. Static-style (matching DebugLogger). Methods to write phase-tagged entries with full exception context. Thread-safe via lock. No-op when disabled.
- **RunErrorTracker**: Counts `errors` and `rate_limits` per run. Consumed by `RunSummaryDebugFormatter` and `RunSummaryPresenter`.
- **VerificationResult.Unverified**: Existing fallback verdict, reused for parallel-critic failures.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: With `max_concurrent: 5` on a 200-test suite, the critic phase completes in ≤25% of the elapsed time of the same run with `max_concurrent: 1`.
- **SC-002**: With `max_concurrent: 1`, the generated test files, indexes, and verdicts are identical to the pre-spec-043 implementation for the same input.
- **SC-003**: For a run that produces zero errors, no `.spectra-errors.log` file is created.
- **SC-004**: For a run with N errors, `.spectra-errors.log` contains exactly N entries, each with timestamp, phase tag, exception type, message, and stack trace.
- **SC-005**: A user investigating a failed run can identify the root cause (rate limit vs. timeout vs. parse error) by reading only `.spectra-errors.log`, without re-running with extra flags.
- **SC-006**: The Run Summary panel displays `Critic concurrency`, `Errors`, and `Rate limits` on every generate/update run.
- **SC-007**: When rate limits are detected, the user receives an actionable hint pointing at `ai.critic.max_concurrent` in the same run's output.

## Out of Scope

- Parallel generation batches (batches share allocated ID ranges).
- Pipelining (overlapping generation batch N+1 with verification of batch N).
- Auto-throttling concurrency in response to rate limits (manual tuning only in v1).
- Parallel update chunks (ordering dependencies kept sequential).
