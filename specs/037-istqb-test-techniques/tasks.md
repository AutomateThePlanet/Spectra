# Tasks: ISTQB Test Design Techniques in Prompt Templates

**Feature**: 037-istqb-test-techniques
**Branch**: `037-istqb-test-techniques`
**Plan**: [plan.md](./plan.md) · **Spec**: [spec.md](./spec.md) · **Data Model**: [data-model.md](./data-model.md)

> Tests are included alongside implementation tasks because the spec's success criteria (SC-010) require ≥15 net new tests, and the existing repo follows xUnit discipline. Test tasks are not pre-written before code (not strict TDD).

---

## Phase 1: Setup

No project initialization needed — this feature lives entirely inside the existing solution. Skipped.

---

## Phase 2: Foundational (blocking prerequisites)

These tasks must complete before any user-story phase, because every story depends on the new model field and the new technique enumeration.

- [X] T001 Add `Technique` string property (default `""`, `[JsonPropertyName("technique")]`) to `IdentifiedBehavior` in `src/Spectra.CLI/Agent/Analysis/IdentifiedBehavior.cs`
- [X] T002 Add `TechniqueBreakdown` `Dictionary<string,int>` property to `BehaviorAnalysisResult` in `src/Spectra.CLI/Agent/Analysis/BehaviorAnalysisResult.cs` (initialize empty)
- [X] T003 [P] Add `TechniqueHint` `string?` property (`[JsonPropertyName("technique_hint")]`, default `null`) to `AcceptanceCriterion` in `src/Spectra.Core/Models/AcceptanceCriterion.cs`

---

## Phase 3: User Story 1 — Technique-Driven Behavior Analysis (P1)

**Goal**: Behavior analysis applies six ISTQB techniques and tags every identified behavior with the technique that produced it. The user sees both a category breakdown and a technique breakdown.

**Independent test**: Run `spectra ai generate --analyze-only` against a fixture document containing a numeric range and a multi-condition rule. Verify `.spectra-result.json` contains `analysis.technique_breakdown` with `BVA ≥ 4` and `DT ≥ 1`, and no category in `analysis.breakdown` exceeds 40% of `total_behaviors`.

### Implementation

- [X] T004 [US1] Rewrite `src/Spectra.CLI/Prompts/Content/behavior-analysis.md` to add the six TECHNIQUE sections (EP, BVA, DT, ST, EG, UC), the OUTPUT INSTRUCTIONS section requesting `technique` in the JSON, and the DISTRIBUTION GUIDELINES section with the 40%-of-any-category cap
- [X] T005 [US1] In `src/Spectra.CLI/Agent/Copilot/BehaviorAnalyzer.cs`, after parsing the AI response populate `BehaviorAnalysisResult.TechniqueBreakdown` by grouping non-empty `Technique` values; ensure empty-technique behaviors are excluded from the map
- [X] T006 [US1] In `src/Spectra.CLI/Agent/Copilot/BehaviorAnalyzer.cs`, update the legacy hardcoded fallback prompt in `BuildAnalysisPrompt` to include condensed ISTQB technique instructions and request the `technique` field in the JSON output
- [X] T007 [US1] Add `TechniqueBreakdown` `Dictionary<string,int>` (`[JsonPropertyName("technique_breakdown")]`) to the analysis subobject in `src/Spectra.CLI/Results/GenerateResult.cs`; serialize as `{}` when empty
- [X] T008 [US1] In `src/Spectra.CLI/Output/AnalysisPresenter.cs`, render a "Technique Breakdown" section beneath the existing category breakdown, in fixed display order BVA, EP, DT, ST, EG, UC; map short codes to human-readable labels at render time
- [X] T009 [US1] In `src/Spectra.CLI/Output/ProgressPageWriter.cs`, render a "Technique Breakdown" section beneath the existing category breakdown using the same display order; suppress the section when the map is empty
- [X] T010 [US1] Wire `BehaviorAnalysisResult.TechniqueBreakdown` through to `GenerateResult.Analysis.TechniqueBreakdown` in the analyze/generate handler (likely `src/Spectra.CLI/Commands/Generate/GenerateHandler.cs`)

### Tests

- [X] T011 [P] [US1] Add `BehaviorAnalysisTemplateTests.cs` under `tests/Spectra.CLI.Tests/Prompts/` asserting the embedded `behavior-analysis.md` contains: "Equivalence Partitioning", "Boundary Value Analysis", "Decision Table", "State Transition", "Error Guessing", "Use Case", a 40%-cap distribution rule, and a `technique` field instruction
- [X] T012 [P] [US1] Add `IdentifiedBehaviorTechniqueTests.cs` under `tests/Spectra.CLI.Tests/Agent/` asserting JSON with `"technique": "BVA"` round-trips into `IdentifiedBehavior.Technique`, AND that JSON without the field deserializes to `Technique == ""`
- [X] T013 [P] [US1] Add `BehaviorAnalyzerLegacyFallbackTests.cs` under `tests/Spectra.CLI.Tests/Agent/` asserting the legacy fallback prompt string contains the six condensed technique mentions and requests `technique` in JSON
- [X] T014 [P] [US1] Add `BehaviorAnalyzerTechniqueBreakdownTests.cs` asserting `BehaviorAnalysisResult.TechniqueBreakdown` correctly groups behaviors by technique and excludes empty techniques
- [X] T015 [P] [US1] Add `GenerateResultTechniqueBreakdownTests.cs` under `tests/Spectra.CLI.Tests/Results/` asserting the analysis section serializes a `technique_breakdown` JSON object even when empty
- [X] T016 [P] [US1] Extend `tests/Spectra.CLI.Tests/Output/AnalysisPresenterTests.cs` (create if absent) to verify a Technique Breakdown line block is rendered with the fixed order
- [X] T017 [P] [US1] Extend `tests/Spectra.CLI.Tests/Output/ProgressPageWriterTests.cs` (create if absent) to verify the HTML contains a Technique Breakdown section with the configured techniques, and is omitted when empty

**Checkpoint**: After Phase 3, US1 is independently shippable. The product produces technique-tagged analysis output even if generation/update/critic still write generic test bodies.

---

## Phase 4: User Story 2 — Technique-Aware Test Step Writing (P2)

**Goal**: Generated test steps reference exact boundary values for BVA, name equivalence classes for EP, state condition values for DT, state transitions for ST, and concrete error scenarios for EG.

**Independent test**: Provide a synthetic behavior set with `technique: BVA` titled "Field rejects 21 chars (max 20)". Generate. Assert the produced test step contains the literal `21`.

**Depends on**: Phase 2 (T001 — `Technique` field must exist on the behavior so the generation prompt can receive it).

### Implementation

- [X] T018 [US2] Modify `src/Spectra.CLI/Prompts/Content/test-generation.md` to add a "TEST DESIGN TECHNIQUE RULES" section before the output-format instructions, with sub-rules for BVA / EP / DT / ST / EG (each with a WRONG/RIGHT example as in the spec)
- [X] T019 [US2] In `src/Spectra.CLI/Agent/Copilot/CopilotGenerationAgent.cs` (or wherever the generation prompt is composed), ensure each behavior's `Technique` value is forwarded into the prompt context so the AI sees which rule to apply per behavior

### Tests

- [X] T020 [P] [US2] Add `TestGenerationTemplateTests.cs` under `tests/Spectra.CLI.Tests/Prompts/` asserting `test-generation.md` contains the BVA exact-values rule, the EP class-naming rule, the DT condition-values rule, the ST starting/action/resulting-state rule, and the EG concrete-scenario rule

**Checkpoint**: After Phase 4, technique-tagged behaviors flow through to technique-disciplined generated test bodies.

---

## Phase 5: User Story 6 — Migration of Existing Prompt Templates (P2)

**Goal**: Existing user template files are preserved across upgrades; users can opt in via `spectra prompts reset` or `spectra update-skills`; new projects get the new templates from `spectra init`.

**Independent test**: In a project with an existing `behavior-analysis.md`, upgrade SPECTRA and verify the file is unchanged. Run `spectra prompts reset behavior-analysis` and verify the file now contains the ISTQB sections.

**Depends on**: Phase 3 (T004) for the new template content.

### Implementation

- [X] T021 [US6] Verify `src/Spectra.CLI/Prompts/BuiltInTemplates.cs` continues to load `behavior-analysis.md` and the other four templates as embedded resources; no code change expected — confirm `.csproj` `EmbeddedResource` glob covers `Prompts/Content/*.md` so the new content is picked up at build time
- [X] T022 [US6] Verify `src/Spectra.CLI/Skills/SkillsManifest.cs` (or equivalent SHA-256 hash table) recomputes hashes from the rebuilt embedded resources so `spectra update-skills` detects the template change for unmodified files (no code change unless the hash table is hand-maintained — if so, regenerate the entries for the five templates)
- [X] T023 [US6] Confirm `spectra init` (`src/Spectra.CLI/Commands/Init/InitHandler.cs`) writes templates from `BuiltInTemplates`; if it currently writes a hand-coded copy of `behavior-analysis.md`, replace that path with the embedded resource read

### Tests

- [X] T024 [P] [US6] Add `PromptTemplateMigrationTests.cs` under `tests/Spectra.CLI.Tests/Prompts/` with cases: (a) `spectra init` produces a `behavior-analysis.md` containing "TECHNIQUE 1: Equivalence Partitioning"; (b) given a pre-existing user-edited file, the upgrade path does NOT modify it; (c) `spectra prompts reset behavior-analysis` rewrites it to the new content
- [X] T025 [P] [US6] Add a test asserting `BuiltInTemplates` exposes content for all five template ids (`behavior-analysis`, `test-generation`, `test-update`, `critic-verification`, `criteria-extraction`) and each contains the expected technique markers for its file (BVA mention etc.)

**Checkpoint**: After Phase 5, both new and existing projects can adopt the new templates without data loss.

---

## Phase 6: User Story 3 — Technique-Aware Update Classification (P3)

**Goal**: `spectra ai update` flags tests as OUTDATED when documented ranges/rules/states/boundaries change in ways the existing tests do not cover via the appropriate technique.

**Independent test**: Take a test using mid-range value `50` for documented range `1–100`. Change docs to `1–200`. Run update. Verify the test is OUTDATED with a boundary-specific reason.

**Depends on**: Phase 2 (T001), Phase 3 (T004 — analysis side must already produce technique tags so update can compare).

### Implementation

- [X] T026 [US3] Modify `src/Spectra.CLI/Prompts/Content/test-update.md` to add a "Technique Completeness Check" section before the classification instructions, containing the four bullet conditions from the spec (new range → BVA, new rule → DT, new state → ST, changed boundary → specific update)

### Tests

- [X] T027 [P] [US3] Add `TestUpdateTemplateTests.cs` under `tests/Spectra.CLI.Tests/Prompts/` asserting `test-update.md` contains the technique completeness check section with phrases referencing BVA, Decision Table, State Transition, and boundary-value updates

---

## Phase 7: User Story 4 — Critic Verification of Technique Claims (P3)

**Goal**: The critic verifies that BVA boundary values, EP equivalence classes, ST transition paths, and DT condition combinations claimed by tests match the source documentation.

**Independent test**: Feed the critic a test claiming `21` is above a 20-char max, alongside docs stating max is 25. Verify verdict is PARTIAL with a boundary-mismatch reason.

**Depends on**: Phase 2 (T001), Phase 4 (T018 — generated tests must already carry technique information for the critic to verify).

### Implementation

- [X] T028 [US4] Modify `src/Spectra.CLI/Prompts/Content/critic-verification.md` to add a "Technique Verification" section with the four sub-rules from the spec (BVA boundary mismatch → PARTIAL, EP class undocumented → PARTIAL, ST transition unsupported → PARTIAL, DT condition not in docs → HALLUCINATED)

### Tests

- [X] T029 [P] [US4] Add `CriticVerificationTemplateTests.cs` under `tests/Spectra.CLI.Tests/Prompts/` asserting `critic-verification.md` contains the four technique verification rules with the expected verdict words (PARTIAL, HALLUCINATED)

---

## Phase 8: User Story 5 — Technique Hints on Acceptance Criteria (P3)

**Goal**: Extracted acceptance criteria carry an optional `technique_hint` field; the generation prompt receives those hints via the existing `{{acceptance_criteria}}` placeholder.

**Independent test**: Run `spectra ai analyze --extract-criteria` on a doc containing "Username must be 3-20 characters". Verify the resulting criterion has `technique_hint: BVA`.

**Depends on**: Phase 2 (T003 — `TechniqueHint` field must exist on `AcceptanceCriterion`).

### Implementation

- [X] T030 [US5] Modify `src/Spectra.CLI/Prompts/Content/criteria-extraction.md` to add a "Technique Hints" section instructing the AI to emit `technique_hint` for criteria with numeric ranges (BVA), multi-condition outcomes (DT), workflow/state changes (ST), or valid/invalid input categories (EP), with the three example mappings from the spec
- [X] T031 [US5] In the criteria extractor (likely `src/Spectra.CLI/Agent/Copilot/CriteriaExtractor.cs`), parse the `technique_hint` field from the AI response into `AcceptanceCriterion.TechniqueHint`
- [X] T032 [US5] Verify the YAML reader/writer for `.criteria.yaml` files (likely `src/Spectra.CLI/Criteria/CriteriaFileReader.cs` / `CriteriaFileWriter.cs`) round-trips `technique_hint`; absent fields must load as null, null values must be omitted on write
- [X] T033 [US5] In the generation prompt context builder (where `{{acceptance_criteria}}` is populated), include each criterion's `technique_hint` value (when present) in the formatted criteria block so the AI sees it

### Tests

- [X] T034 [P] [US5] Add `CriteriaExtractionTemplateTests.cs` under `tests/Spectra.CLI.Tests/Prompts/` asserting `criteria-extraction.md` contains the four hint mapping rules and the three example mappings
- [X] T035 [P] [US5] Add `AcceptanceCriterionTechniqueHintTests.cs` under `tests/Spectra.Core.Tests/Models/` asserting (a) `technique_hint: BVA` round-trips through JSON+YAML, (b) absent field deserializes to `TechniqueHint == null`, (c) null serializes as omitted (not as the literal `null`)

---

## Phase 9: Polish & Cross-Cutting

### SKILL & agent prompts

- [X] T036 [P] In `src/Spectra.CLI/Skills/SkillContent.cs`, update the `spectra-generate` SKILL text to document `technique_breakdown` in the JSON output and add an example showing the technique distribution
- [X] T037 [P] In `src/Spectra.CLI/Skills/SkillContent.cs`, update the `spectra-help` SKILL with a section explaining the six ISTQB techniques and how to customize via `spectra prompts reset`
- [X] T038 [P] In `src/Spectra.CLI/Skills/SkillContent.cs`, update the `spectra-quickstart` SKILL to mention that generation uses systematic test design techniques by default
- [X] T039 [P] In `src/Spectra.CLI/Skills/AgentContent.cs`, update the `spectra-generation` agent prompt with a note that analysis output now includes a technique breakdown and that test writing is technique-aware

### SKILL test updates

- [X] T040 [P] Update `tests/Spectra.CLI.Tests/Skills/SkillContentTests.cs` to assert the SKILL text references `technique_breakdown` and ISTQB techniques (extend existing tests, do not duplicate)
- [X] T041 [P] Update any existing tests asserting exact prompt content for the five templates so they continue to match (these will fail until updated; expected casualties: any `*_LineCount` or `*_Contains_*` test against the rewritten templates)

### Documentation

- [X] T042 [P] Update `docs/getting-started.md` with a short note about ISTQB techniques in generation and a pointer to `spectra prompts reset` to get the latest templates
- [X] T043 [P] Update `docs/generation-profiles.md` to clarify the difference between profiles (output format) and templates (reasoning strategy), noting techniques live in templates
- [X] T044 [P] Update `docs/cli-reference.md` to mention systematic test design techniques in the `spectra ai generate` description
- [X] T045 [P] Update `docs/coverage.md` to note that technique-aware generation produces better boundary/negative coverage
- [X] T046 [P] Update `docs/skills-integration.md` to mention `technique_breakdown` in the JSON output example for the `spectra-generate` SKILL
- [X] T047 [P] Update `README.md` "AI Test Generation" feature description to mention ISTQB techniques
- [X] T048 [P] Update `PROJECT-KNOWLEDGE.md` template descriptions to reflect the technique-driven content
- [X] T049 [P] Update `USAGE.md` with a workflow example showing technique breakdown in the analysis output
- [X] T050 Add a "Recent Changes" entry for spec 037 to `CLAUDE.md` summarizing the prompt-template ISTQB enhancement

### Build & verification

- [X] T051 Run `dotnet build` and resolve any compile errors introduced by model field additions
- [X] T052 Run `dotnet test` and resolve any failures (existing tests asserting old prompt content will need updates per T041)
- [X] T053 Manually walk through `quickstart.md` Scenarios A–E against a real or fixture project and confirm each acceptance gate

---

## Dependencies

```text
Phase 2 (Foundational)        ← T001, T002, T003 must complete first
        │
        ▼
Phase 3 (US1, P1)             ← T004–T017
        │
        ├─→ Phase 4 (US2, P2)   ← T018–T020      (needs T001 + T004)
        │         │
        │         ▼
        │   Phase 7 (US4, P3)   ← T028–T029      (needs T018)
        │
        ├─→ Phase 5 (US6, P2)   ← T021–T025      (needs T004)
        │
        ├─→ Phase 6 (US3, P3)   ← T026–T027      (needs T001 + T004)
        │
        └─→ Phase 8 (US5, P3)   ← T030–T035      (needs T003)
                  │
                  ▼
            Phase 9 (Polish)    ← T036–T053      (needs all prior phases)
```

User stories US3/US4/US5/US6 are independent of each other after the P1 foundation lands — they can be developed in parallel if multiple workers are available.

## Parallel Execution Examples

**Within Phase 3 (US1) tests** — all test files are independent and can be run/written in parallel:

```text
T011, T012, T013, T014, T015, T016, T017
```

**Within Phase 9 polish** — SKILL updates, doc updates, and test-content fixups all touch different files:

```text
T036, T037, T038, T039 (SKILL/agent — all in different methods of SkillContent.cs/AgentContent.cs; serialize if same file)
T042, T043, T044, T045, T046, T047, T048, T049 (doc files — all independent)
T040, T041 (test fixups — independent files)
```

**Across stories (after Phase 3)** — Phase 4, Phase 5, Phase 6, Phase 7, Phase 8 can run in parallel by different developers:

```text
US2 (T018–T020) ∥ US3 (T026–T027) ∥ US4 (T028–T029) ∥ US5 (T030–T035) ∥ US6 (T021–T025)
```

## Implementation Strategy

- **MVP scope** = Phase 2 + Phase 3 (User Story 1). After T017, the product already shifts behavior distribution toward systematic boundary/negative coverage, which is the primary user-visible win. Stop here and ship if time-constrained.
- **Phase 4 (US2)** is the highest-value follow-up because it makes the new tags actually shape generated step text.
- **Phase 5 (US6)** must ship together with anything that changes user-facing template defaults to avoid breaking existing projects.
- **Phases 6/7/8 (US3/US4/US5)** are quality-of-life improvements that compound with the MVP but are not blocking.

## Independent Test Criteria (per story)

| Story | Criterion |
|-------|-----------|
| US1 (P1) | `analysis.technique_breakdown` exists with `BVA ≥ 4` and `DT ≥ 1` for a fixture doc with a numeric range and a multi-condition rule; no category exceeds 40% of `total_behaviors` |
| US2 (P2) | A generated test for a `technique: BVA` behavior contains the literal documented boundary numeric value in its step text |
| US3 (P3) | After a documented `1–100` → `1–200` change, an existing mid-range test is flagged OUTDATED with a boundary-specific reason |
| US4 (P3) | A test asserting `21 > 20-char max` against docs stating max is `25` returns critic verdict PARTIAL with a boundary-mismatch reason |
| US5 (P3) | A criterion extracted from "Username must be 3-20 characters" carries `technique_hint: BVA` |
| US6 (P2) | An existing user-edited `behavior-analysis.md` is unchanged after upgrade; `spectra prompts reset behavior-analysis` rewrites it to the new ISTQB content |

## Format Validation

All 53 tasks above follow the required checklist format: `- [X] T### [P?] [US#?] description with file path`. Setup/Foundational/Polish tasks omit the `[US#]` label as required.
