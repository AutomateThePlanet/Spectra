# Phase 1 Data Model: Test Hardening & Documentation Audit (047–051)

This feature adds **no new production data structures**. It exercises existing ones and introduces a few test-only support types plus documentation deliverables. Entities below are the testing/documentation artifacts the spec produces, and the existing production types they assert against.

## Test-only support types (new, in `tests/`)

### FakeAgentRuntime
- **Purpose**: Deterministic `IAgentRuntime` for hermetic generation tests.
- **Implements**: `Spectra.CLI.Agent.IAgentRuntime`.
- **Behavior**:
  - `GenerateTestsAsync(prompt, docs, existing, count, criteriaContext, testimizeData, ct)` — records `criteriaContext` (exposed as `LastCriteriaContext`) and returns a fixed `GenerationResult` whose `Tests[0]` carries a populated `Criteria` field (configurable per test).
  - `IsAvailableAsync` → `true`. `ProviderName` → `"fake"`.
- **Wiring**: supplied via the `agentFactory` delegate of `UserDescribedGenerator.GenerateAsync` as `AgentCreateResult.Succeeded(fake)`.

### NoOpDelayProvider
- **Purpose**: `IExtractionDelayProvider` that records requested delays without sleeping (reused/mirrored from existing `AnalyzeHandlerRetryTests`).
- **Members**: `List<TimeSpan> Calls`; `DelayAsync` appends and returns `Task.CompletedTask`.

### SyntheticCorpusFactory (scale test helper)
- **Purpose**: Build N `DocumentEntry` items with realistic content size and a configurable per-document `extractPerDoc` latency/outcome map (fast-success / slow-timeout / throw-then-succeed).
- **Outputs**: `IReadOnlyList<DocumentEntry>` plus a `Func<DocumentEntry,CancellationToken,Task<IReadOnlyList<RequirementDefinition>>>` extract delegate.

### OnDiskIndexLoader (cross-spec helper)
- **Purpose**: `Func<string, IEnumerable<TestIndexEntry>>` that reads the real `test-cases/{suite}/_index.json` and maps to `TestIndexEntry`, for feeding the real MCP tools.

## Existing production types under test (no change)

| Type / member | Source file | Asserted property |
|---------------|-------------|-------------------|
| `CriteriaExtractionResult` (`Outcome`, `IsCacheable`) | `Agent/Copilot/CriteriaExtractionResult.cs` | parse/empty outcomes are non-cacheable; extracted is cacheable |
| `IExtractionDelayProvider` | `Agent/Copilot/IExtractionDelayProvider.cs` | retry backoff invoked without wall-clock sleep |
| `DocsIndexHandler.ExtractCriteriaLoopAsync` + `PerDocumentDeadline` | `Commands/Docs/DocsIndexHandler.cs` | per-document deadline; partial success; no corpus-wide abort |
| `AnalyzeHandler.ExtractWithRetryAsync` | `Commands/AnalyzeHandler.cs` | retry of non-cacheable outcome; cache gated on `IsCacheable` |
| `ComputeCriteriaWarning` | `Commands/Docs/DocsIndexHandler.cs` | warning when indexed>0 and criteria==0 |
| `BuildNoCriteriaNote` | `Commands/Generate/GenerateHandler.cs` | note when suiteMatchedCount==0 |
| `CriteriaContextResult.SuiteMatchedCount` / `LoadCriteriaContextAsync` | `Commands/Generate/GenerateHandler.cs` | match count drives the note |
| `CriteriaSource.Outcome` (default `"extracted"`) | `Models/Coverage/CriteriaSource.cs` | legacy entries deserialize as `extracted` |
| `TestPersistenceService.PersistAsync` | `IO/TestPersistenceService.cs` | from-description test written + index regenerated |
| `UserDescribedGenerator.GenerateAsync` (`agentFactory`, `criteriaContext`) | `Commands/Generate/UserDescribedGenerator.cs` | criteria forwarded to agent; result carries `Criteria` |
| `StartExecutionRunTool` (`priorities`/`tags`/`components` + legacy `filters`) | `Tools/RunManagement/StartExecutionRunTool.cs` | canonical filter shape filters; legacy honored |
| `McpProtocol.DeserializeParams<T>` (`UnmappedMemberHandling.Disallow`, `SuggestFilterField`) | `Server/McpProtocol.cs` | misshapen field → actionable error, not silent drop |
| `FindTestCasesTool` | `Tools/Data/...` | from-description test discoverable post-persist |
| `MetadataIndex` / `TestIndexEntry` | `Spectra.Core` | index shape parity for from-description test |

## Documentation deliverables (new files)

### Doc-Audit Report — `docs/specs/052-doc-audit-report.md`
- **Shape**: one table row per audited file with columns `File | Disposition | Notes`. Disposition ∈ {confirmed-current, updated, superseded}. Every "updated" row maps to a file actually modified in this change set.

### SKILL Transcripts — `docs/specs/052-skill-transcripts.md`
- **Shape**: one section per SKILL/agent file in scope; each holds a realistic user prompt and the representative rendered output demonstrating post-051 behavior, plus a one-line coherence verdict.

### Consolidated CHANGELOG entry — `CHANGELOG.md`
- **Shape**: one versioned entry, three subsections (Fixed / Added / Changed), each bullet attributed `(#0NN)`.

### PROJECT-KNOWLEDGE additions — `PROJECT-KNOWLEDGE.md`
- **Shape**: a Spec 052 row in the spec table + a "silent-failure pattern" learning entry (lenient deserialization, returning a value for a required field, `catch { return []; }`).

## Validation rules (derived from FRs)

- Each named regression test's xUnit `DisplayName` MUST equal the user-facing symptom phrasing (FR-014).
- Cross-spec tests MUST assert observable outputs (counts, ids, populated fields, warnings/notes), never private state (FR-008).
- The no-criteria note MUST be present in the result object even when console printing is suppressed at quiet verbosity (FR-007) — assert against the result, not stdout.
- The scale test MUST carry `[Trait("Category","Scale")]` (FR-018).
- Every doc/SKILL file in scope MUST appear once in the audit report with a disposition (FR-019, FR-023); "updated" ⇒ actually modified (FR-022).
