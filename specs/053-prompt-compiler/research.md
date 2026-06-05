# Phase 0 — Research: Prompt-compiler + generation handoff inversion

All "unknowns" here are resolved against the codebase and the two grounding investigation docs (`docs/investigation/01-generation-seam.md`, `03-deterministic-core.md`). No external research required — this is a relocation of existing, grep-proven model-free code.

## D-1 — Where to relocate the prompt compiler

- **Decision**: New class `PromptCompiler` in a new namespace `Spectra.CLI.Generation` (folder `src/Spectra.CLI/Generation/`). `GenerationAgent.BuildFullPrompt` becomes a thin delegate.
- **Rationale**: Investigation `03` F-2 records that `BuildFullPrompt` is model-free but Copilot-coupled *by location* (it is `internal static` inside `CopilotGenerationAgent`). Moving it out of `Agent/Copilot/` removes that coupling so a non-Copilot caller (the new CLI command) can reach it. Delegating from the old method keeps one source of truth and keeps existing tests green.
- **Alternatives considered**: (a) Make `BuildFullPrompt` public in place — rejected, leaves it Copilot-coupled, fails the "standalone artifact" intent of FR-002. (b) Duplicate the logic — rejected, two sources of prompt truth drift.

## D-2 — How "refuse-to-emit" is signalled

- **Decision**: `PromptCompiler.Compile(...)` returns a `PromptCompileResult` — either `Success(string prompt)` or `Failure(string missingInput, string message)`. No exceptions for the expected "missing input" case; exceptions are reserved for programmer error.
- **Rationale**: FR-004 requires reporting *which* input failed in a form a skill/CLI can act on. A typed result with a named missing-input field maps cleanly to a deterministic non-zero exit code and a machine-readable JSON field. Mirrors the existing `CriteriaExtractionResult` typed-outcome pattern (`CriteriaExtractor.cs`), which the team already trusts.
- **Alternatives considered**: Throwing `ArgumentNullException` — rejected, harder for a skill to parse the specific input and conflates expected refusal with bugs.

## D-3 — Which inputs are "required" for the compiler

- **Decision**: Required = the user prompt/behaviors text, the requested count (> 0), and the criteria context. The profile format and the Testimize dataset are treated as **present-with-fallback** (profile format falls back to the embedded default via `ProfileFormatLoader`; Testimize is genuinely optional and collapses its template block when empty), so they do not trigger refusal.
- **Rationale**: The spec's worked example for refuse-to-emit is "criteriaContext is null (the Spec 045 class)". The criteria context is the input whose silent absence most degrades grounding, so it is the headline required input. Count must be positive to produce a meaningful prompt. The existing `BuildFullPrompt` already tolerates null profile/testimize by design (`GenerationAgent.cs:457`, `:463`), so promoting those to "required" would be a behavior change the spec does not ask for.
- **Open knob**: whether an empty-string criteria context counts as "missing" — resolved by the spec Assumptions ("present-but-empty == absent"). The compiler treats whitespace-only criteria as missing.

## D-4 — Fail-loud vs. the existing truncation salvage

- **Decision**: The ingestor does **not** call `TryRepairTruncatedArray`. A response whose JSON does not parse as a complete array is a loud `IngestResult.Failure` with code `MALFORMED_JSON` (or `TRUNCATED` when a `[` opened but the array never closed). Nothing is persisted.
- **Rationale**: FR-006 explicitly says truncation now triggers retry, not silent salvage. The salvage method (`GenerationAgent.cs:619`) was the old reliability crutch; with the retry living in skill choreography (FR-007), a truncated response must surface as a specific error the skill can re-prompt against.
- **Alternatives considered**: Keep salvage behind a flag — rejected, YAGNI and it re-introduces the silent-repair path the spec removes.

## D-5 — Validation at the boundary

- **Decision**: After parsing each element into a `TestCase` (reusing the relocated `ParseTestCase` logic), run the full batch through `Spectra.Core.Validation.TestValidator.ValidateAll`. Any `ValidationError` ⇒ the whole batch fails loud (batch atomicity, per spec Assumptions) and nothing persists. The `ValidationError` codes (e.g. `INVALID_ID`, `MISSING_PRIORITY`) are surfaced verbatim as the machine-readable error.
- **Rationale**: Reuses the model-free validator proven in `03` §2.3 — no new validation logic, and the error codes are already machine-readable. Batch-atomic persistence matches `TestPersistenceService`'s "write all then regenerate index" contract (a partial write would leave the index inconsistent).
- **Alternatives considered**: Per-test partial persistence — rejected by spec Assumptions (no partial persistence) and by the index-consistency invariant.

## D-6 — CLI command shape

- **Decision**: Two sibling subcommands under `ai`: `spectra ai compile-prompt` and `spectra ai ingest-tests`. `compile-prompt` reads its declared inputs (suite, focus, count, and resolves criteria/profile/testimize the same way the handler does), prints the prompt to stdout, writes nothing. `ingest-tests` reads agent content (from `--from <file>` or stdin), validates fail-loud, and persists on success; deterministic exit codes (0 success, non-zero on refuse/validation failure).
- **Rationale**: Constitution §IV (CLI-first, CI-friendly exit codes). Siblings under `ai` keep `generate` untouched for now (supports the staged scoping decision) and read naturally for the skill that will call them in 055.
- **Alternatives considered**: Subcommands of `generate` — rejected, `generate` is a leaf command with a default handler + positional arg; nesting under it is more confusing than a sibling.

## D-7 — Keeping the regression net green

- **Decision**: `GenerationAgent.BuildFullPrompt` keeps its exact signature and delegates to `PromptCompiler`; `ParseTestsFromResponse`/`ParseTestCase` logic is *copied* into the ingestor (not deleted from `GenerationAgent`) until the call-site-removal task. No edits to `Spectra.Core` or `TestPersistenceService` or their tests.
- **Rationale**: The spec's hard rule: any break in the `Spectra.Core`/persistence corpus is a regression to investigate, not a test to update. Leaving the old private parse helpers in place until the final rewire task keeps every existing CLI test compiling and green.
