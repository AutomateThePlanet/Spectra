# Phase 0 Research: Test Hardening & Documentation Audit (047–051)

All NEEDS CLARIFICATION items from Technical Context are resolved below.

## R1 — Where do the cross-spec and named-regression tests live?

**Decision**: Create a new test project `tests/Spectra.Integration.Tests/` (net8.0, xUnit 2.9.3) referencing `Spectra.CLI`, `Spectra.MCP`, and `Spectra.Core`, and add it to `Spectra.slnx`. Grant it `InternalsVisibleTo` from both `Spectra.CLI` and `Spectra.MCP`. Host `EndToEndScenarios.cs` (Part A) and `OriginalBugRegression.cs` (Part B) there. The scale test (Part C) stays in `tests/Spectra.CLI.Tests/Extraction/ScaleTests.cs`.

**Rationale**: The headline cross-spec workflows (`FromDescriptionHighPriority_RunsViaFilter_EndToEnd`, `IndexDeployed_AfterFromDescription_FindTestCasesReturnsIt`) exercise the CLI generation/persistence path **and** the MCP `start_execution_run` / `find_test_cases` tools in one flow. No existing test project references both assemblies: `Spectra.CLI.Tests` → CLI only, `Spectra.MCP.Tests` → MCP only. The seam between CLI and MCP is exactly what this spec exists to cover, so a project that references both is the natural and only clean home. Keeping `EndToEndScenarios` and `OriginalBugRegression` each as one coherent class (as the source description specifies) also requires a single project that sees both assemblies' internals.

**Alternatives considered**:
- *Add a `Spectra.CLI` ProjectReference to `Spectra.MCP.Tests`* — rejected: pollutes the MCP unit-test assembly with a CLI dependency and inverts the layering; the from-description flow is a CLI concern that does not belong under MCP tests.
- *Split each cross-spec test across two assemblies* — rejected: impossible to assert a single continuous user journey across an assembly boundary, and it fragments the `EndToEndScenarios` class the spec asks for.

**Constitution note**: Principle V (YAGNI) and the governance rule "new projects require explicit justification" — justified in `plan.md` Complexity Tracking. This is the only new project; it adds no production code.

## R2 — Running generation in CI without a live Copilot provider

**Decision**: Inject a deterministic fake `IAgentRuntime` (returns a fixed `GenerationResult` and records the `criteriaContext` it received) through the existing `agentFactory` seam on `UserDescribedGenerator.GenerateAsync(...)`. Cross-spec from-description tests compose the *real* production services in sequence — `UserDescribedGenerator.GenerateAsync(agentFactory: fake)` → `TestPersistenceService.PersistAsync(...)` → read the produced `_index.json` from disk → real `StartExecutionRunTool` / `FindTestCasesTool`.

**Rationale**: `UserDescribedGenerator.GenerateAsync` already exposes `agentFactory: Func<SpectraConfig,string,string,Action<string>?,CancellationToken,Task<AgentCreateResult>>?` (default builds a real Copilot agent). A fake returning `AgentCreateResult.Succeeded(fakeRuntime)` keeps the test hermetic and offline. Composing the same services `GenerateHandler.ExecuteFromDescriptionAsync` composes (verified in `GenerateHandler.cs:1826-1874`) exercises 049 (persist + index regen), 050 (criteria forwarding — assert the fake received `criteriaContext` and the resulting `TestCase.Criteria` is populated), and feeds 051 (the index the MCP tool reads).

**Known limitation (documented, out of scope to fix)**: `GenerateHandler.ExecuteFromDescriptionAsync` is `private` and constructs `UserDescribedGenerator` without exposing an agent seam at the *command* level, so a literal `InvokeAsync(["ai","generate",...])` from-description run cannot be made hermetic today. A command-level agent-injection seam would be a production change and is therefore **out of scope** (FR-028). The chosen approach drives the identical production services one level below the command, which satisfies FR-008 ("exercise 2+ specs against fixture data, assert observable outcomes"). This gap is recorded in the doc-audit report as a follow-up candidate.

**Alternatives considered**:
- *Shell out to the real CLI with a live provider* — rejected: non-hermetic, network-dependent, non-deterministic, cannot run on a fresh CI checkout (violates FR-029 / SC-008).
- *Add a command-level agent seam* — rejected: production change, violates "no new features."

## R3 — Batch generation path (A2 / B1) hermetic seam

**Decision**: For `BatchGeneration_FromExtractedCriteria_PopulatesCriteriaField` and the "extract-criteria on generation" regression, drive the criteria pipeline through the internal seams the batch flow uses — `GenerateHandler.LoadCriteriaContextAsync(...)` (returns `CriteriaContextResult` with `SuiteMatchedCount`) and a fake `IAgentRuntime.GenerateTestsAsync` that asserts the `criteriaContext` argument is non-null and returns tests whose `Criteria` field reflects the supplied IDs. The batch handler calls `AgentFactory.CreateAgentAsync` directly (no handler-level seam), so these tests assert the *contract* at the `IAgentRuntime`/`LoadCriteriaContextAsync` boundary rather than via `InvokeAsync`.

**Rationale**: This is the same accessible boundary 050/048 are implemented at; it proves the criteria reach the agent and land on the test, which is the user-observable outcome. Requires internal access (hence `InternalsVisibleTo` in R1).

**Alternatives considered**: Driving the full batch command hermetically — rejected for the same reason as R2 (no command-level agent seam; fixing it is out of scope).

## R4 — Scale test & "no silent skip after partial failure" (Part C, A3, B5)

**Decision**: Drive `DocsIndexHandler.ExtractCriteriaLoopAsync(documents, existing, extractPerDoc, perDocDeadline, onSlowDoc, onDocFailure, ct)` directly (it is `internal static`). Generate N synthetic `DocumentEntry` (default 30) with realistic content size. Inject an `extractPerDoc` delegate that delays per call and, for designated documents, exceeds `perDocDeadline` (timeout) or throws (failure) then succeeds on retry. Assert: (a) at least some documents are extracted (no whole-corpus abort), (b) only the designated slow/failed documents appear in the timed-out / failed lists, (c) elapsed time is consistent with **per-document** deadlines, not a single shared corpus budget. Tag `[Trait("Category","Scale")]`.

For cache-poisoning (B5 / `ParseFailure_DoesNotPoisonCache`): drive `AnalyzeHandler.ExtractWithRetryAsync(extractAttempt, maxAttempts, backoff, delayProvider, ct)` with a `NoOpDelayProvider` (records but does not sleep) and a stub returning `ParseFailure` then `Extracted`; assert `result.IsCacheable` is false on the parse-failure outcome and true after success, mirroring the existing `AnalyzeHandlerRetryTests` pattern but with the symptom display name.

**Rationale**: `ExtractCriteriaLoopAsync` takes `perDocDeadline` as a parameter and uses `Task.WhenAny(extractTask, Task.Delay(perDocDeadline, ct))` per document (verified `DocsIndexHandler.cs:386-410`) — exactly the post-047 per-document semantics. A single shared corpus budget (the pre-047 bug) would make a fast document fail once an earlier slow document consumed the budget; the mixed fast/slow assertion makes that regression observable.

**Latency tuning**: To keep the full CI pass fast, the injected per-call latency and `perDocDeadline` are small constants (e.g. deadline ≈ 300 ms, slow doc ≈ 600 ms) chosen so the per-document property is provable while 30 documents complete in a few seconds. The literal "3-second" figure from the source description is the *intent* (simulate provider latency); the test exposes the latency/deadline/N as constants so it can be dialed up. `[Trait("Category","Scale")]` lets fast-feedback runs exclude it via `--filter "Category!=Scale"` while full CI runs everything.

**Alternatives considered**: A real 30×3s = 90s test — rejected as needlessly slow for CI; the per-document property does not require literal 3 s latency to be proven.

## R5 — The three "misshapen filter" requests (A5 / `FilterSilentDrop_NoLongerOccurs`)

**Decision**: Reproduce the exact `PRIORITY-FILTER-INVESTIGATION.md` Q4 Path C cases against the real `StartExecutionRunTool` over a fixture suite:

| # | Request shape | Post-051 expected outcome |
|---|---------------|---------------------------|
| 1 | `{ suite, priority: "high" }` (top-level singular) | Actionable error (strict deserialization), suggests `priorities` (array). Does **not** enqueue whole suite. |
| 2 | `{ suite, priorities: ["high"] }` (find_test_cases shape) | Filters correctly — canonical shape, only high-priority tests enqueued. |
| 3 | `{ suite, filters: { priorities: ["high"] } }` (nested but plural) | Actionable error — `priorities` is not valid inside the legacy nested `filters` object; suggests the top-level shape. Does **not** enqueue whole suite. |

Assert each is either a correct filtered run (#2) or a structured `INVALID_PARAMS`/deserialization error whose message names the offending field and suggests the fix (#1, #3); assert none returns a run whose `test_count` equals the full suite size.

**Rationale**: Verified against `McpProtocol.cs` (`UnmappedMemberHandling = Disallow`, `DeserializeParams<T>` → `SuggestFilterField`) and `StartExecutionRunTool.NormalizeFilters`. Path C cases live at `docs/specs/PRIORITY-FILTER-INVESTIGATION.md:166-169`.

**Alternatives considered**: Inventing new malformed shapes — rejected; the spec mandates the *documented* Path C cases so the test traces directly to the investigation.

## R6 — MCP tools reading the real on-disk index (A1, A4)

**Decision**: Build the `Func<string, IEnumerable<TestIndexEntry>>` index loader the tools require by reading the actual `test-cases/{suite}/_index.json` the generation flow produced (via the existing `Spectra.Core` index reader / `MetadataIndex` deserialization). This makes 049's index parity observable: the MCP tool sees the from-description test only because it was registered.

**Rationale**: `StartExecutionRunTool` and `FindTestCasesTool` take the index loader as a constructor delegate (verified `StartExecutionRunTool.cs:57`). Pointing it at the real file (not an in-memory stub) is what proves 049 end to end.

**Alternatives considered**: In-memory `TestIndexEntry` stub (as unit tests use) — rejected for the cross-spec tests because it would bypass the index-registration behavior 049 is about; the whole point is to read what was written to disk.

## R7 — Documentation & SKILL audit scope

**Decision**: Audit scope is the on-disk docs under `docs/` (at minimum `usage.md`, `coverage.md`, `test-format.md`, `cli-reference.md`, `generic-mcp.md` (a.k.a. the generic MCP doc), `skills-integration.md`, `getting-started.md`) plus `PROJECT-KNOWLEDGE.md` and `CHANGELOG.md`, **and** the SKILL/agent content under `src/Spectra.CLI/Skills/Content/Skills/*.md` and `src/Spectra.CLI/Skills/Content/Agents/*.agent.md`. The `.github/skills/` path named in the source description **does not exist** in this repo; the real SKILL surface is `Skills/Content/`. Additional doc files are discovered by grep, not assumed. Deliverables `052-doc-audit-report.md` and `052-skill-transcripts.md` are written under `docs/specs/`.

**Rationale**: Verified there is no `.github/skills/` directory; `Skills/Content/Skills/` holds the 15 SKILL `.md` files (including `spectra-generate.md`, `spectra-docs.md`, `spectra-coverage.md`, `spectra-criteria.md`) and `Skills/Content/Agents/` holds `spectra-execution.agent.md` and `spectra-generation.agent.md`. The execution SKILL surface is the agent file `spectra-execution.agent.md` (there is no `spectra-execution.md`).

**SKILL "transcripts"**: Captured as representative rendered prompt→output examples demonstrating post-051 behavior (default-on extraction, indexed from-description tests carrying criteria, single filter shape, actionable filter errors), clearly labeled as representative renderings rather than live session captures (live interactive capture is not reproducible in CI). This satisfies the evidence intent of FR-024 honestly.

**Alternatives considered**: Treating `.github/skills/` as authoritative — rejected; it does not exist.

## R8 — Consolidated CHANGELOG entry & version

**Decision**: Add a single consolidated `CHANGELOG.md` entry titled for the 047–051 reliability block, organized Fixed / Added / Changed, user-facing, each line attributed to its originating spec. The per-spec lines already present remain in history; the consolidated entry is the one ship-readiness summary the spec asks for. Use the next patch version after 1.52.5 (i.e. `1.52.6`) since this spec ships no functional change — confirmed against current `CHANGELOG.md` / `Directory.Build.props` during implementation.

**Rationale**: Acceptance criterion 6 requires exactly one consolidated entry; semantic-version PATCH fits a tests-and-docs-only release (Principle: no features added). `PROJECT-KNOWLEDGE.md` gains a Spec 052 row plus a learning entry naming the silent-failure pattern (lenient deserialization; returning a value for a required field; `catch { return []; }` swallowing).

**Alternatives considered**: A `1.53.0` minor — rejected; no new capability ships here.
