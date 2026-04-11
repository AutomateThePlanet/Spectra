# Phase 0 Research: Progress Bars

No NEEDS CLARIFICATION markers in spec; no new dependencies. Three tactical decisions captured during code exploration.

## Decision 1: Reuse `ProgressReporter` Spectre wrapper

**Decision**: Call the existing `ProgressReporter.ProgressAsync(description, total, action)` wrapper at `src/Spectra.CLI/Output/ProgressReporter.cs:64–91` rather than calling `AnsiConsole.Progress()` directly inside `GenerateHandler`/`UpdateHandler`.

**Rationale**: The wrapper already centralizes the JSON-mode suppression check (`if (_outputFormat == OutputFormat.Json) return;` at lines 25, 46, 69, 98, 107, 116, 125, 143). Both handlers already hold a `_progress` field of type `ProgressReporter`. Adding direct Spectre calls would duplicate suppression logic and risk drift.

**Alternatives considered**:

- Inline `AnsiConsole.Progress()` blocks per handler — rejected: duplicates suppression checks, splits Spectre usage across files.
- Introduce a new `ProgressBarRunner` abstraction — rejected: YAGNI; one wrapper already exists and serves the same purpose.

## Decision 2: Suppress on non-interactive stdout (no TTY)

**Decision**: Extend the existing JSON-mode suppression in `ProgressReporter` to also suppress when `AnsiConsole.Profile.Capabilities.Interactive == false` (redirected stdout, CI, piped invocation).

**Rationale**: Even at default verbosity, ANSI escape sequences corrupt redirected output. Spectre exposes the capability flag directly — no extra dependency, no probing.

**Alternatives considered**:

- Always render — rejected: corrupts piped output, fails SC-003 in piped scenarios.
- Add a `--no-progress` flag — rejected: YAGNI; capability detection is automatic and correct.

## Decision 3: Update progress bar tracks proposals, not chunks

**Decision**: In `UpdateHandler`, the progress bar advances per proposal in the apply-changes loop (`UpdateHandler.cs:579–631`), not per "chunk" (which doesn't exist in code).

**Rationale**: Exploration revealed that `UpdateHandler` does a single batch classification (`classifier.ClassifyBatch` at line 269) followed by a per-proposal apply loop. There is no chunk concept. The spec text used "chunks" generically; the natural progress unit is the proposal.

**Alternatives considered**:

- Introduce real chunking — rejected: out of scope, would change classification semantics for no benefit.
- Bar covers classification phase only — rejected: classification is a single opaque AI call; nothing to advance.

## Open Questions

None. Ready for Phase 1.
