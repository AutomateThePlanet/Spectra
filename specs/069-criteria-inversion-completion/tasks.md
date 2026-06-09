---
description: "Task list — Criteria-extraction inversion completion + Copilot SDK removal"
---

# Tasks: Criteria-extraction inversion — completion + Copilot SDK removal

**Input**: `/specs/069-criteria-inversion-completion/` (plan, spec, research, data-model, contracts, quickstart)
**Tests**: Included (spec defines acceptance scenarios + an inviolable regression net).
**Ordering**: The spec's phase order (§8) is **causal — do not reorder**. Two hard gates separate the
inverted-but-SDK-present state from the SDK-removed state.

## Path conventions

Single-project CLI: `src/Spectra.CLI/`, `src/Spectra.Core/`, `tests/Spectra.CLI.Tests/`,
`tests/Spectra.Core.Tests/` (the latter edited ONLY per FR-019).

---

## Phase A — Rescue helpers (FR-001/002) · US1 foundation · nothing deleted yet

- [ ] T001 [US1] Create `src/Spectra.CLI/Extraction/CriteriaResponseClassifier.cs` — move
  `CriteriaExtractor.ClassifyResponse` + `NormalizePriority` + `NormalizeTechniqueHint` verbatim into a
  model-free static class in namespace `Spectra.CLI.Extraction`.
- [ ] T002 [US1] Relocate `ExtractionOutcome` (enum) + `CriteriaExtractionResult` /
  `RequirementsExtractionResult` (records) from `src/Spectra.CLI/Agent/Copilot/CriteriaExtractionResult.cs`
  into `src/Spectra.CLI/Extraction/` (namespace `Spectra.CLI.Extraction`).
- [ ] T003 [US1] Repoint consumers: `CriteriaIngestor.cs` (drop `using Spectra.CLI.Agent.Copilot;`, call
  `CriteriaResponseClassifier`) and `IngestCriteriaCommand.cs` (drop the `using`). Update
  `CriteriaExtractor` (still present this phase) to delegate to the new classifier so there is one source
  of truth until it is deleted in Phase E.
- [ ] T004 [US1] `dotnet build` green; run seam tests — `dotnet test tests/Spectra.CLI.Tests --filter
  "FullyQualifiedName~Ingest|FullyQualifiedName~Criteria"` (FR-018, behaviour unchanged).

**Checkpoint A**: helpers live in a model-free home; SDK still present; everything green.

---

## Phase B — Skill rewire + changed-docs command (FR-003/004/005) · US1

- [ ] T005 [P] [US1] Add `tests/Spectra.CLI.Tests/Commands/Docs/DocsChangedTests.cs` — assert
  new/changed/unchanged classification against a seeded `_criteria_index.yaml` (doc with no source entry →
  `new`; hash mismatch → `changed`; match → `unchanged`); JSON shape per `contracts/changed-docs-command.md`.
- [ ] T006 [US1] Implement `spectra docs changed` in `src/Spectra.CLI/Commands/Docs/` (new
  `DocsChangedCommand` + handler), reusing `FileHasher.ComputeFileHashAsync` + `CriteriaIndexReader`; emit
  `{path, component, status, current_hash, indexed_hash}`; `--include-unchanged` opt; model-free.
- [ ] T007 [US1] Register the command under the `docs` group (alongside `index`/`list-suites`/`show-suite`).
- [ ] T008 [US1] Rewire `src/Spectra.CLI/Skills/Content/Skills/spectra-criteria.md` extraction recipe to
  the loop in `contracts/skill-extraction-loop.md` (`docs changed` → per-doc `compile-extraction-prompt`
  → in-session turn → `ingest-criteria --from`, bounded retry); drop the `--extract-criteria` drive and
  the `--skip-splitting` mention.
- [ ] T009 [US1] Update `SkillsManifest` per-line-flag checks to exclude `spectra-criteria` seam commands
  if needed (mirror `spectra-generate`/`spectra-update`).
- [ ] T010 [US1] `dotnet build` + targeted tests green (DocsChanged + skill manifest).

**Checkpoint B**: criteria extraction runs end-to-end inverted (skill-driven), SDK still present.

---

## Phase C — docs-index convergence (FR-006/007) · US3

- [ ] T011 [P] [US3] Update `tests/Spectra.CLI.Tests` docs-index tests: `docs index` performs no
  extraction, result has no `criteria_extracted`/`criteria_file`, no `docs/requirements/` is created.
- [ ] T012 [US3] In `src/Spectra.CLI/Commands/Docs/DocsIndexHandler.cs`: remove the inline extraction
  block (`~:194-200`), the provider read (`:253`), `TryExtractCriteriaAsync` / `ExtractCriteriaLoopAsync`
  / `PerDocumentDeadline`; drop the criteria fields from the result; make `--skip-criteria` an accepted
  no-op (or remove if no test references it).
- [ ] T013 [US3] Remove the `RequirementsExtractor` usage from docs-index (the class is deleted in Phase
  E). Confirm `docs/requirements/_requirements.yaml` is no longer produced. (Core `RequirementsWriter`/
  `RequirementDefinition`/`RequirementsParser` stay — `[Obsolete]`, pinned by `RequirementsWriterTests`.)
- [ ] T014 [US3] `dotnet build` + docs-index tests green.

**Checkpoint C**: `docs index` is index-only; one criteria artifact remains.

---

## Phase D — Drop import compound-splitting (FR-008) · US1

- [ ] T015 [P] [US1] Update `tests/Spectra.CLI.Tests` import tests: `analyze --import-criteria` is a
  deterministic pass-through (no model call); compound bullets imported as a single record;
  `--skip-splitting` removed or accepted no-op.
- [ ] T016 [US1] In `src/Spectra.CLI/Commands/Analyze/AnalyzeHandler.cs`: remove the
  `SplitAndNormalizeAsync` branch (`~:947-1018`) + the provider read (`:956`); import = parse → ID assign
  → merge/replace → write (all model-free). Remove the `--skip-splitting` flag from `AnalyzeCommand`
  (or downgrade to hidden no-op + note).
- [ ] T017 [US1] `dotnet build` + import/analyze tests green.

---

## 🚦 GATE 1 (spec §8.5) — inverted, SDK present-but-unused

- [ ] T018 Run quickstart B/C/D: skill loop works, `docs index` makes no model call, and
  `diff -r /tmp/criteria-baseline docs/criteria` is **clean** (SC-006 byte-compat). SC-004/005 pass.
  STOP if byte-compat fails — investigate, do not paper over.

---

## Phase E — Delete `Agent/Copilot/` (FR-009) · US2

- [ ] T019 [US2] Pre-flight grep: `grep -rn "Spectra.CLI.Agent.Copilot\|CopilotService\|RequirementsExtractor\|\.Ai\.Critic" src/`
  — enumerate every remaining reference so deletion is complete; confirm `.Ai.Critic` has no runtime reader
  (investigation Q5) **before** Phase F removes the type.
- [ ] T020 [US2] Delete `src/Spectra.CLI/Agent/Copilot/` (`CopilotService`, `ProviderMapping`,
  `CriteriaExtractor`, `RequirementsExtractor`, `CriteriaExtractionResult` remnants) and remove the GitHub
  Copilot SDK `PackageReference` from `src/Spectra.CLI/Spectra.CLI.csproj`.
- [ ] T021 [US2] Retire the SDK-only auth surface that depended on it: `AgentFactory`
  (`GetAuthStatusAsync`/`CheckCopilotAvailabilityAsync`) and the `spectra auth` command/`AuthHandler` —
  remove or gut so nothing references the deleted SDK. (Document the removal in CHANGELOG.)
- [ ] T022 [US2] `dotnet build` — resolve all now-dangling references surfaced by the deletion.

**Checkpoint E**: SDK code physically gone; build compiles.

---

## Phase F — Strip config schema + init (FR-010/011/019) · US2

- [ ] T023 [US2] Remove `ai.providers`/`ProviderConfig` and `ai.critic`/`CriticConfig` from the Core model
  (`src/Spectra.Core/Models/Config/`: `AiConfig.cs`, `ProviderConfig.cs`, `CriticConfig.cs`, `SpectraConfig.cs`),
  keeping any surviving cost/telemetry levers on `AiConfig`. First confirm no runtime reader of
  `.Ai.Critic` remains (esp. the Spec-055 `compile-critic-prompt` path — if it reads `ai.critic.model`,
  STOP and preserve that selection out of scope; investigation says it is dead).
- [ ] T024 [US2] In `src/Spectra.Core/Config/ConfigLoader.cs`: remove the `MISSING_PROVIDERS` validation;
  `GenerateDefaultConfig`/`GenerateConfig` emit **no** `ai.providers`/`ai.critic` (no dummy block);
  confirm unknown-key tolerance (no `JsonUnmappedMemberHandling.Disallow`). Clean
  `src/Spectra.CLI/Templates/spectra.config.json` of `ai.providers`/`ai.critic`.
- [ ] T025 [US2] `src/Spectra.CLI/Commands/Config/ConfigHandler.cs`: stop printing providers/critic.
- [ ] T026 [US2] `src/Spectra.CLI/Commands/Init/InitHandler.cs`: remove `InteractiveAuthSetupAsync`,
  `InteractiveModelPresetAsync`, `InteractiveCriticSetupAsync` (and `Commands/Init/ModelPreset.cs`);
  `spectra init` asks no AI-provider question. Remove the GenerateConfig provider/model args path.
- [ ] T027 [US2] [FR-019] Edit ONLY the provider/critic-presence Core tests, documenting each retired
  assertion in a comment: `tests/Spectra.Core.Tests/Config/ConfigLoaderTests.cs` (delete
  `Load_WithMissingProviders_ReturnsFailure`; drop provider assertions from `Load_With*`; fix
  `GenerateDefaultConfig_ReturnsValidJson` for the providerless schema),
  `tests/Spectra.Core.Tests/Config/ProviderRetirementTests.cs`,
  `tests/Spectra.Core.Tests/Models/Config/CriticConfigTests.cs`,
  `tests/Spectra.Core.Tests/Models/Config/CriticConfigClampTests.cs`. **Do not touch any other Core test.**
- [ ] T028 [US2] Remove now-dead CLI critic config plumbing only if it is unreferenced by the Spec-055
  seam (`CriticModelResolver` if uncalled); leave `compile-critic-prompt`/critic subagent (055) behavior
  intact. `dotnet build` green.

---

## 🚦 GATE 2 (spec §8.7) — SDK provably gone

- [ ] T029 Verify (SC-001/002/007): `grep -rn "config.Ai.Providers" src/` → zero outside removed sites;
  `grep -rn "Spectra.CLI.Agent.Copilot" src/` → zero; `Agent/Copilot/` absent; `spectra init`
  (throwaway dir) asks nothing and the generated `spectra.config.json` has no `ai.providers`/`ai.critic`.
- [ ] T030 (SC-003) `dotnet build -c Release` green (warnings-as-errors honored); `dotnet test` green.
  Only the named provider/critic-presence Core tests were edited; any OTHER Core test failure is a real
  regression → investigate, do not edit.

---

## Phase G — Polish & docs

- [ ] T031 [P] Update `CHANGELOG.md` (Spec 069 entry), `docs/usage.md` / `docs/cli-reference.md`
  (`docs changed`, `docs index` index-only, import no-splitting, `init` no provider prompt, `auth`
  removal), and CLAUDE.md command list.
- [ ] T032 Run `quickstart.md` end-to-end on the demo corpus; confirm SC-006 byte-compat diff clean and
  SC-001..007 all hold.

---

## Dependencies & gates

- Phase order is causal: **A → B → C → D → GATE 1 → E → F → GATE 2 → G**. Do not reorder.
- `[P]` tasks (test files in different locations) may run in parallel within their phase.
- GATE 1 must pass (byte-compat, no-model `docs index`) before any deletion (Phase E) begins.
- GATE 2 closes the feature: SDK gone, config clean, full build+test green with only the named Core tests
  edited per FR-019.
