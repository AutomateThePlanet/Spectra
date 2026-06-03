---

description: "Task list for Spec 050 — From-Description Criteria Injection"
---

# Tasks: From-Description Criteria Injection

**Input**: Design documents from `/specs/050-from-desc-criteria-injection/`
**Prerequisites**: plan.md (required), spec.md (required), research.md, data-model.md, contracts/prompt-contract.md, quickstart.md
**Tests**: Included (the spec § Test Plan explicitly enumerates six required test cases plus two pre-existing tests to adapt).

**Organization**: Tasks grouped by user story so each P1/P2 slice can be implemented and merged independently.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Different file from other tasks in the same phase, no dependency on incomplete prior task
- **[Story]**: `[US1]`, `[US2]`, `[US3]` for user-story-scoped tasks; absent for Setup/Foundational/Polish
- All paths absolute or repo-rooted; file paths explicit in every implementation task

## Path Conventions

- Source: `src/Spectra.CLI/Commands/Generate/UserDescribedGenerator.cs` (modified)
- Tests: `tests/Spectra.CLI.Tests/Commands/Generate/UserDescribedGeneratorTests.cs` (modified)
- Docs: `docs/skills-integration.md`, `docs/coverage.md`, `docs/PROJECT-KNOWLEDGE.md`, plus `spectra-generate` SKILL content

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Confirm working tree is on the right branch and the existing build is green before touching anything.

- [X] T001 Verify current branch is `050-from-desc-criteria-injection` and `git status` is clean (or only contains expected spec-artifact files). If dirty in unrelated ways, stop and ask.
- [X] T002 Run `dotnet build` from repo root and confirm zero errors/warnings against the pre-change baseline. Capture warning count for later regression check.
- [X] T003 Run `dotnet test tests/Spectra.CLI.Tests` and confirm the full suite passes pre-change. Capture pass count.

**Checkpoint**: Baseline green build and green tests recorded.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Introduce the test seam on `UserDescribedGenerator.GenerateAsync` so the six tests required by the spec can capture the prompt and `criteriaContext` the agent receives. This is the only structural change; both US1/US2/US3 test additions depend on it.

**⚠️ CRITICAL**: All user-story tests in Phase 3+ depend on this seam being in place.

- [X] T004 Read the current `AgentFactory.CreateAgentAsync` signature in `src/Spectra.CLI/Agent/AgentFactory.cs` to capture the exact return type and parameter list — needed to type the new factory delegate correctly.
- [X] T005 In `src/Spectra.CLI/Commands/Generate/UserDescribedGenerator.cs`, add an optional trailing parameter to `GenerateAsync` whose type matches the captured `AgentFactory.CreateAgentAsync` signature (delegate). Default value: `null`. When non-null, invoke the delegate instead of the static `AgentFactory.CreateAgentAsync`. When null, behavior is byte-identical to today. Do **not** change any other call signature, return shape, or behavior in this task — this is the seam only, with the `criteriaContext` forwarding bug still present.
- [X] T006 Run `dotnet build` from repo root and confirm: (a) build still green; (b) zero new warnings; (c) all existing `UserDescribedGeneratorTests` still pass via `dotnet test`. The seam is additive and defaulted; nothing should regress.

**Checkpoint**: Test seam exists. Production call sites unchanged. All existing tests green.

---

## Phase 3: User Story 1 — Criteria-Coverage Participation (Priority: P1) 🎯 MVP

**Goal**: A from-description generation in a workspace with matching criteria produces a test file whose `criteria:` frontmatter is reliably populated. The MANDATORY criteria-mapping instruction reaches the model. The loose body section is removed so criteria content appears exactly once.

**Independent Test**: In a workspace with `docs/criteria/_criteria_index.yaml` containing at least one criterion matching the target suite, run `spectra ai generate --suite X --from-description "..."`. The written `test-cases/X/TC-XXX.md` MUST have `criteria:` populated with at least one ID. The outbound prompt MUST contain the "ACCEPTANCE CRITERIA — MANDATORY" header exactly once and no `## Related Acceptance Criteria` loose section.

### Tests for User Story 1 (write FIRST, expect FAIL before implementation)

- [X] T007 [P] [US1] Add `FromDescription_PassesCriteriaContextToAgent` to `tests/Spectra.CLI.Tests/Commands/Generate/UserDescribedGeneratorTests.cs`. Inject a fake `IAgentRuntime` via the seam (T005). Assert that the agent's `GenerateTestsAsync` receives `criteriaContext` equal to the value passed into `UserDescribedGenerator.GenerateAsync`, NOT `null`. Should FAIL initially (the bug is the discard at the call site).
- [X] T008 [P] [US1] Add `FromDescription_EmitsMandatoryCriteriaBlock` to the same test file. Wire the fake agent to delegate to a real `CopilotGenerationAgent`-equivalent system-prompt builder if accessible, or — simpler — assert that the value the fake agent received as `criteriaContext` is non-null and non-whitespace when the input has criteria. (The MANDATORY block emission in `GenerationAgent.cs:527` is already covered by the existing contract; here we only need to prove the seam delivers the value that triggers it.) Should FAIL initially.
- [X] T009 [P] [US1] Add `FromDescription_DoesNotDuplicateCriteriaSection` to the same test file. Assert that the prompt produced by `BuildPrompt` with a non-empty `criteriaContext` does NOT contain `## Related Acceptance Criteria`. Should FAIL initially (today the block is still emitted from `BuildPrompt`).
- [X] T010 [P] [US1] Add `FromDescription_PopulatesCriteriaField_WhenModelMaps` to the same test file. Configure the fake agent to return a `GenerationResult` whose single test has `Criteria = ["AC-CHECKOUT-003", "AC-CHECKOUT-007"]`. Assert that `UserDescribedGenerator.GenerateAsync` returns a `TestCase` whose `Criteria` contains those IDs (i.e., the criteria-pass-through from agent output → returned TestCase is intact). Should PASS even before the fix (verifies invariant) — record this as a regression-guard, not a fail-first test.
- [X] T011 [P] [US1] Adapt the pre-existing `BuildPrompt_WithCriteriaContext_IncludesAcceptanceCriteriaHeader` test. Its current assertions (`## Related Acceptance Criteria`, `criteria` frontmatter field` text) become invalid because the loose block is removed in T013. Replace assertions with: "the parameter is accepted without throwing" and "the prompt body does NOT contain `## Related Acceptance Criteria`". Keep test name unchanged for git-blame continuity; update its `Fact` body. Should currently PASS for the wrong reason; will PASS for the right reason after T013.
- [X] T012 [P] [US1] Adapt the pre-existing `BuildPrompt_WithBothContexts_IncludesBoth` test. Remove the `Assert.Contains("Related Acceptance Criteria", prompt)` line; keep the `Assert.Contains("Reference Documentation", prompt)` line. Rename the test to `BuildPrompt_WithBothContexts_IncludesDocReferenceOnly` to reflect the new contract. Should PASS only after T013 (today it would fail on the modified-name assumption — track via build).

### Implementation for User Story 1

- [X] T013 [US1] In `src/Spectra.CLI/Commands/Generate/UserDescribedGenerator.cs`, remove the loose body block from `BuildPrompt` — delete the entire `if (!string.IsNullOrWhiteSpace(criteriaContext)) { prompt += $"""...## Related Acceptance Criteria..."""; }` section (lines ~61–74). Keep the `criteriaContext` parameter on `BuildPrompt`'s signature (no-op going forward; preserves signature stability per Decision in research.md D2 / prompt-contract.md). Depends on: T009, T011, T012 being authored (so their pass/fail state is observable through this change).
- [X] T014 [US1] In the same file, change the `agent.GenerateTestsAsync(...)` call (line ~113) so it passes `criteriaContext: criteriaContext` instead of `criteriaContext: null`. This is the one-line behavioral fix. Depends on: T007, T008 being authored.
- [X] T015 [US1] Run `dotnet test tests/Spectra.CLI.Tests --filter UserDescribedGeneratorTests`. Confirm: T007, T008, T009 now PASS; T010 still PASSES; T011, T012 PASS with their updated assertions; all other pre-existing `UserDescribedGeneratorTests` (no-context, doc-context, source-of-truth, description-included, avoids-existing-ids, extra-context, empty-doc-context) still PASS unchanged. If any pre-existing test fails, investigate before continuing — do not amend the assertions to make them green.

**Checkpoint**: US1 fully delivered. A from-description test with matching criteria in scope reliably gets `criteria:` populated, the MANDATORY block reaches the model exactly once, the loose block is gone, and no other behavior has shifted.

---

## Phase 4: User Story 2 — Verdict Semantics Remain Trustworthy (Priority: P1)

**Goal**: A from-description test's `grounding.verdict` stays `Manual` even when the new criteria-injection path causes its `criteria:` field to be populated. Reviewers can continue to read `verdict` as a critic-verification signal.

**Independent Test**: Generate a from-description test that successfully maps criteria. Confirm the returned `TestCase.Grounding.Verdict == VerificationVerdict.Manual` and the persisted YAML frontmatter says `verdict: manual`. Run `spectra ai analyze --coverage` and confirm the test appears under acceptance-criteria coverage but NOT under grounded statistics.

### Tests for User Story 2

- [X] T016 [P] [US2] Add `FromDescription_VerdictRemainsManual` to `tests/Spectra.CLI.Tests/Commands/Generate/UserDescribedGeneratorTests.cs`. Wire the fake agent to return a test whose model output includes criteria mappings (same fake-agent setup as T010). Assert that the returned `TestCase.Grounding.Verdict == VerificationVerdict.Manual` and `TestCase.Grounding.Critic == "user-described"`. Should PASS immediately (US1 changes do not touch the verdict line; this test is a regression guard against future drift). Depends on: T005 (seam) being in place.

### Implementation for User Story 2

- [X] T017 [US2] **No source change required.** This task exists to document that fact and to confirm via reading. Open `src/Spectra.CLI/Commands/Generate/UserDescribedGenerator.cs` line ~149 and verify `Verdict = VerificationVerdict.Manual` is unchanged from main. Add a one-line code comment IF AND ONLY IF the reasoning is non-obvious to a reader who just landed on the line (per CLAUDE.md "default to writing no comments"). If the existing surrounding context already conveys "manual because no critic," skip the comment.
- [X] T018 [US2] Run `dotnet test tests/Spectra.CLI.Tests --filter UserDescribedGeneratorTests`. Confirm T016 PASSES and all previously green tests stay green.

**Checkpoint**: US2 delivered. Verdict integrity proven by a regression-guard test; no production code change was needed (the existing `Manual` hard-code is correct and intentional).

---

## Phase 5: User Story 3 — No-Criteria-Match Path Unchanged (Priority: P2)

**Goal**: In a workspace with no acceptance criteria matching the target suite, from-description behavior is byte-identical to pre-fix: no MANDATORY block, no loose section, empty `criteria:`. The fix must not regress this path.

**Independent Test**: Run `spectra ai generate --suite Y --from-description "..."` against a workspace whose criteria index contains nothing matching suite Y. Confirm the written `.md`'s `criteria:` field is empty/omitted. Confirm the outbound prompt contains zero criteria-related sections.

### Tests for User Story 3

- [X] T019 [P] [US3] Add `FromDescription_NoCriteria_OmitsBlock` to `tests/Spectra.CLI.Tests/Commands/Generate/UserDescribedGeneratorTests.cs`. Call `UserDescribedGenerator.GenerateAsync` with `criteriaContext: null` (and again with `""` and `"   "`). Assert: (a) the fake agent's `GenerateTestsAsync` received `criteriaContext` that is null-or-whitespace; (b) the prompt produced by `BuildPrompt` for the same inputs contains neither `## Related Acceptance Criteria` nor `ACCEPTANCE CRITERIA — MANDATORY`. Should PASS after Phase 3 changes; document as the negative-case regression guard. Depends on: T005 (seam), T013 (loose block removed).

### Implementation for User Story 3

- [X] T020 [US3] **No source change required.** Verify by reading `UserDescribedGenerator.BuildPrompt` (after T013) and the `GenerateAsync` call site (after T014) that when `criteriaContext` is null/whitespace: (a) `BuildPrompt` does not append any criteria section; (b) `agent.GenerateTestsAsync` receives null/whitespace and (per the existing `GenerationAgent.cs:527` contract) emits no MANDATORY block. This is a reading task, not an editing task.
- [X] T021 [US3] Run `dotnet test tests/Spectra.CLI.Tests --filter UserDescribedGeneratorTests`. Confirm T019 PASSES and the full test class is still green.

**Checkpoint**: US3 delivered. The no-criteria path is provably unchanged. The entire spec § Test Plan is now covered.

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: Documentation updates required by the spec § Documentation Update Checklist, plus a final end-to-end build/test sweep and the quickstart-driven manual smoke check.

- [X] T022 [P] Update `docs/skills-integration.md` — the from-description note: criteria are now injected as the MANDATORY mapping instruction; `criteria:` is populated when the model maps any; `verdict` stays `manual` by design (no critic runs in this flow). Keep edit targeted; do not restructure the page.
- [X] T023 [P] Update `docs/coverage.md` — add a short paragraph stating that from-description tests count toward acceptance-criteria coverage but are excluded from grounded statistics because `verdict: manual` means no independent critic verified them. Link forward to the from-description note in `skills-integration.md`.
- [X] T024 [P] Update the `spectra-generate` SKILL content (in `src/Spectra.CLI/Skills/` — locate the file backing the SKILL by searching for "from-description" or "user-described" markers). Adjust the from-description result presentation if it currently implies criteria are not linked. Keep wording tight.
- [X] T025 [P] Update `docs/PROJECT-KNOWLEDGE.md` — add the Spec 050 row (one row in the existing changelog/table, matching the format of Spec 047/048/049 rows already present). Record the verdict-stays-`manual` decision in one line.
- [X] T026 Run the full `dotnet build` and `dotnet test` (all three test projects: `Spectra.Core.Tests`, `Spectra.CLI.Tests`, `Spectra.MCP.Tests`) from repo root. Confirm zero new failures and zero new warnings relative to the Phase 1 baseline (T002, T003).
- [~] T027 **SKIPPED (manual, requires live model)** — Execute the manual verification flow from `specs/050-from-desc-criteria-injection/quickstart.md` "Post-fix verification" section against a workspace that has extracted criteria. Confirm: (a) `criteria:` is populated in the generated `.md`; (b) `grounding.verdict: manual`; (c) `spectra ai analyze --coverage` shows the test in acceptance-criteria coverage and absent from grounded. **Not run during implementation**: requires a Copilot-authenticated workspace with extracted criteria and a real AI generation call, which is unavailable in the implementation environment. The behavior is covered deterministically by the automated tests (`FromDescription_PassesCriteriaContextToAgent`, `FromDescription_EmitsMandatoryCriteriaBlock`, `FromDescription_PopulatesCriteriaField_WhenModelMaps`, `FromDescription_VerdictRemainsManual`, `FromDescription_NoCriteria_OmitsBlock`). A human reviewer should run the quickstart flow once against a real workspace before release.
- [X] T028 Self-review the diff: confirm files touched are exactly `UserDescribedGenerator.cs`, `UserDescribedGeneratorTests.cs`, and the four doc files. Confirm: no enum change, no MCP-tool change, no CLI-flag change, no schema change, no migration. If anything else changed, explain or revert.

**Checkpoint**: Ready for PR.

---

## Dependencies & Execution Order

### Phase Dependencies

- **Phase 1 (Setup)**: No dependencies. Run first to establish baseline.
- **Phase 2 (Foundational)**: Depends on Phase 1. T004 → T005 → T006 sequential. BLOCKS Phases 3–5.
- **Phase 3 (US1)**: Depends on Phase 2. Internal order: write US1 tests (T007–T012) → make source changes (T013, T014) → run US1 tests (T015).
- **Phase 4 (US2)**: Depends on Phase 2 (test seam). Can run in parallel with Phase 3 because the source path it covers (verdict line ~149) is unaffected by Phase 3 edits. In practice, easier to do after Phase 3 to keep one merge train.
- **Phase 5 (US3)**: Depends on Phase 3 — specifically T013 (loose block removal) and T014 (criteriaContext forwarding), because US3 asserts the post-fix behavior of those exact lines on the negative path.
- **Phase 6 (Polish)**: Depends on Phases 3–5 being complete (docs reflect implemented behavior; final tests gate readiness).

### User Story Dependencies

- **US1 (P1)**: Foundational only. Owner: this PR.
- **US2 (P1)**: Foundational only. Source path is independent of US1; test joins the same xUnit class. Effectively co-shipped.
- **US3 (P2)**: Foundational + US1 source changes (T013, T014). Cannot ship US3 tests before US1's source edits land.

### Within Each User Story

- Tests written and FAILING (or correctly PASSING as regression guards, as noted per task) before the source edit.
- One source file touched per implementation task — no two implementation tasks edit `UserDescribedGenerator.cs` in parallel.
- Run `dotnet test --filter UserDescribedGeneratorTests` between source edits to keep failure attribution clean.

### Parallel Opportunities

- T007, T008, T009, T010, T011, T012 are all [P] within Phase 3 — they all write to the same test file but to distinct, additive `[Fact]` methods (T011 and T012 modify pre-existing tests; coordinate sequencing if applied serially in one branch). When applied by one engineer, treat as a single editing pass on `UserDescribedGeneratorTests.cs`.
- T013 and T014 are NOT [P] — same source file (`UserDescribedGenerator.cs`).
- T016 [US2] and T019 [US3] are independent tests; can be written in parallel with each other and with T007–T012.
- T022, T023, T024, T025 are all [P] — four distinct documentation files, no shared lines.

---

## Parallel Example: Phase 3 test authoring

```text
# Author all US1 + US2 + US3 tests in one editing pass on UserDescribedGeneratorTests.cs:
T007 [P] [US1] FromDescription_PassesCriteriaContextToAgent
T008 [P] [US1] FromDescription_EmitsMandatoryCriteriaBlock
T009 [P] [US1] FromDescription_DoesNotDuplicateCriteriaSection
T010 [P] [US1] FromDescription_PopulatesCriteriaField_WhenModelMaps
T011 [P] [US1] (adapt) BuildPrompt_WithCriteriaContext_IncludesAcceptanceCriteriaHeader
T012 [P] [US1] (adapt) BuildPrompt_WithBothContexts_IncludesBoth → renamed _IncludesDocReferenceOnly
T016 [P] [US2] FromDescription_VerdictRemainsManual
T019 [P] [US3] FromDescription_NoCriteria_OmitsBlock
```

## Parallel Example: Phase 6 documentation

```text
# Four independent files, no overlapping lines:
T022 [P] docs/skills-integration.md
T023 [P] docs/coverage.md
T024 [P] spectra-generate SKILL content
T025 [P] docs/PROJECT-KNOWLEDGE.md
```

---

## Implementation Strategy

### MVP First (US1 only)

1. Phase 1 (Setup): T001–T003 — baseline.
2. Phase 2 (Foundational): T004–T006 — test seam in place.
3. Phase 3 (US1): T007–T015 — bug fixed, six tests green.
4. **STOP and VALIDATE** with `dotnet test` plus the quickstart.md manual repro.
5. Ship as MVP if US2 / US3 / docs are not yet ready and time pressure exists.

In practice for this spec, US2 and US3 land in the same PR because their tests are co-located and their production change is zero / negative-path verification. There is no incentive to split.

### Incremental Delivery

1. Setup + Foundational → seam ready.
2. US1 → bug fixed; criteria participate in coverage.
3. US2 → regression guard locks verdict semantics.
4. US3 → negative-path regression guard.
5. Polish (docs + final sweep) → PR-ready.

### Parallel Team Strategy

Not applicable — single-engineer change. The whole spec is ~1 source file + 1 test file + 4 small doc edits.

---

## Notes

- The implementation is one behavioral one-liner (T014) plus one prompt-cleanup block deletion (T013) plus one defaulted-parameter seam (T005). Everything else is tests and documentation.
- T010 and T016 are regression guards (expected to PASS even pre-fix). They are still part of the test plan because the spec § Test Plan enumerates them; their value is preventing future drift.
- Per CLAUDE.md guidance: default to no comments; do not narrate the change in the source. Per the no-backwards-compat-shims guidance: do not preserve a feature-flagged switch between "old (null-pass) behavior" and "new (forwarded) behavior" — the old behavior was a defect.
- Commits: prefer one commit per phase (Setup/Baseline → Foundational seam → US1 tests + source → US2 test → US3 test → Polish/Docs). Keep the diff easy to review.
- Avoid: editing `GenerationAgent.cs:527`, the `VerificationVerdict` enum, any MCP tool file, any frontmatter writer, any `_index.json` plumbing, or anything outside the file audit in spec.md.
