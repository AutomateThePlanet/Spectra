# Tasks: Quickstart SKILL & Usage Guide

**Feature**: 032-quickstart-skill-usage-guide
**Branch**: `032-quickstart-skill-usage-guide`
**Spec**: [spec.md](./spec.md) · **Plan**: [plan.md](./plan.md)

This is a content + wiring feature. There are no new commands, no new abstractions, no new infrastructure. Tasks add embedded resources, plug them into the existing `InitHandler`/`SkillsManifest`/`update-skills` pipeline, and update agents and project docs.

---

## Phase 1: Setup

No new project setup required. The feature reuses existing build, embedded-resource, and CLI infrastructure.

---

## Phase 2: Foundational

No foundational/blocking work required. The existing `SkillResourceLoader` auto-discovers SKILL files in `Skills/Content/Skills/` and the existing `ProfileFormatLoader` pattern handles bundled docs in `Skills/Content/Docs/`.

---

## Phase 3: User Story 1 — Quickstart SKILL invoked from Copilot Chat (P1)

**Story Goal**: A new user opens VS Code Copilot Chat after `spectra init` and asks "help me get started" — the assistant invokes the spectra-quickstart SKILL and presents the 11-workflow overview with example prompts.

**Independent Test**: Run `spectra init` in a fresh directory; confirm `.github/skills/spectra-quickstart/SKILL.md` exists, contains the 11 workflows, and the manifest tracks it.

- [X] T001 [US1] Author the bundled quickstart SKILL content at `src/Spectra.CLI/Skills/Content/Skills/spectra-quickstart.md`. Includes 12 workflows (added a 12th — Customize — for completeness).
- [X] T002 [US1] Added `Quickstart` accessor to `src/Spectra.CLI/Skills/SkillContent.cs` and updated `SkillContent_HasAllSkills` test to expect 12.
- [X] T003 [US1] No code change needed — `CreateBundledSkillFilesAsync` already iterates `SkillContent.All`, which auto-discovers the new file via `SkillResourceLoader`. The `--skip-skills` flag and manifest hashing already cover it.
- [X] T004 [P] [US1] Added `QuickstartSkill_NotEmpty_AndContainsWorkflows` test asserting frontmatter, 12 workflow headers, and trigger phrases.
- [X] T005 [P] [US1] Added `HandleAsync_CreatesQuickstartSkill` test in `tests/Spectra.CLI.Tests/Commands/InitCommandTests.cs`.
- [X] T006 [P] [US1] Added `HandleAsync_SkipSkills_DoesNotCreateQuickstartSkill` test in the same file.

**Checkpoint**: After Phase 3, the spectra-quickstart SKILL ships, init writes it, and tests prove it. US1 is complete and independently shippable as an MVP increment.

---

## Phase 4: User Story 2 — USAGE.md offline reference (P2)

**Story Goal**: After `spectra init`, `USAGE.md` exists at the project root with the 11 workflows in offline-readable form. `update-skills` refreshes it when unmodified and preserves user edits otherwise.

**Independent Test**: Run `spectra init` in a fresh directory; confirm `USAGE.md` exists at the root, mentions all 11 workflows, contains no `runInTerminal`/`awaitTerminal` references, and is tracked by the manifest.

- [X] T007 [US2] Authored `src/Spectra.CLI/Skills/Content/Docs/USAGE.md` with all 11 sections plus a Complete Pipeline; offline-clean (no `runInTerminal`/`awaitTerminal`/`browser/openBrowserPage`).
- [X] T008 [US2] No edit required — `Spectra.CLI.csproj` already has `<EmbeddedResource Include="Skills\Content\Docs\*.md" />` which covers USAGE.md.
- [X] T009 [US2] Added `EmbeddedUsageResource` constant and `LoadEmbeddedUsageGuide()` to `ProfileFormatLoader.cs`.
- [X] T010 [US2] Added `CreateUsageGuideAsync` to `InitHandler.cs`, called from inside the `if (!skipSkills)` block.
- [X] T011 [P] [US2] Added `LoadEmbeddedUsageGuide_IsNonEmptyMarkdown` test to `ProfileFormatLoaderTests.cs` covering content + offline-clean assertions.
- [X] T012 [P] [US2] Added `HandleAsync_CreatesUsageGuide` test to `InitCommandTests.cs`.
- [X] T013 [P] [US2] Added `HandleAsync_SkipSkills_DoesNotCreateUsageGuide` test to `InitCommandTests.cs`.

**Checkpoint**: After Phase 4, both deliverables (SKILL + offline doc) are present, hash-tracked, and tested.

---

## Phase 5: User Story 3 — Agent prompt delegation (P2)

**Story Goal**: When users ask onboarding questions of either the generation or execution agent, the agent prompt instructs them to defer to the spectra-quickstart SKILL rather than answering ad hoc.

**Independent Test**: Inspect both bundled agent prompt files; both must contain a textual reference to `spectra-quickstart` for onboarding intents.

- [X] T014 [US3] Added `**QUICKSTART**:` delegation line to `spectra-generation.agent.md` next to the existing HELP line (one-line addition, agent line count: 72 — well under the 100-line limit).
- [X] T015 [US3] Added the same `QUICKSTART` delegation bullet to `spectra-execution.agent.md` (agent line count: 125 — well under the 200-line limit).
- [X] T016 [P] [US3] Added `GenerationAgent_References_QuickstartSkill` test to `SkillsManifestTests.cs`.
- [X] T017 [P] [US3] Added `ExecutionAgent_References_QuickstartSkill` test to `SkillsManifestTests.cs`.

**Checkpoint**: After Phase 5, both agents will route onboarding intents to the new SKILL.

---

## Phase 6: Polish & Cross-Cutting Concerns

- [X] T018 [P] Added "Using SPECTRA from VS Code Copilot Chat" subsection to `README.md` after Quick Start, pointing to `USAGE.md` and mentioning `spectra-quickstart`.
- [X] T019 [P] Added an `[Unreleased]` entry to `CHANGELOG.md` describing the new SKILL, the new USAGE.md, the loader method, and the agent delegation updates.
- [X] T020 [P] Updated `PROJECT-KNOWLEDGE.md` — bumped SKILL count to 12, added rows for both `spectra-prompts` (was missing from the table — pre-existing gap) and `spectra-quickstart`, and added a "Bundled Project-Root Docs" section listing `CUSTOMIZATION.md` and `USAGE.md`.
- [X] T021 [P] Added a 032 entry at the top of `CLAUDE.md` "Recent Changes" with the full summary.
- [X] T022 Added a one-line cross-reference to `CUSTOMIZATION.md` near the top pointing to `USAGE.md` for workflow how-tos.
- [X] T023 Ran `dotnet build` (0 errors, 0 warnings) and `dotnet test` (478 + 605 + 351 = **1434 passing**, was 1417 before this feature; +17 net new tests across the feature).
- [ ] T024 Manual verification (optional) — not run; the test suite covers all behaviors that the manual quickstart steps would verify.

---

## Dependencies

```
Phase 1 (Setup) — empty
       │
       ▼
Phase 2 (Foundational) — empty
       │
       ▼
Phase 3 (US1: Quickstart SKILL) ────┐
       │                            │
       ▼                            │
Phase 4 (US2: USAGE.md)             │  (US1, US2, US3 can be developed in any order;
       │                            │   they touch different files except for InitHandler.cs
       ▼                            │   and the project-doc updates in Phase 6)
Phase 5 (US3: Agent delegation) ────┘
       │
       ▼
Phase 6 (Polish)
```

**Within-phase dependencies**:
- T002 depends on T001 (accessor references the auto-discovered key from the new file).
- T003 depends on T001 and T002 (init writes the loaded SKILL content).
- T004–T006 depend on T003 (test the init behavior).
- T009 depends on T007 and T008 (loader reads the registered embedded resource).
- T010 depends on T009 (init calls the loader).
- T011–T013 depend on T010.
- T014–T015 are independent of US1/US2 file edits and can start immediately.
- T016–T017 depend on T014–T015 respectively.
- Phase 6 docs tasks (T018–T022) depend only on the conceptual decisions in earlier phases, not on the actual file writes — they can begin in parallel with US1/US2/US3 implementation.
- T023 (build/test) depends on all prior implementation tasks.

## Parallel execution opportunities

**Within US1**: T004, T005, T006 are independent test files and can run in parallel after T003.

**Within US2**: T011, T012, T013 are independent tests and can run in parallel after T010.

**Within US3**: T014 and T015 edit different files and can run in parallel; T016 and T017 likewise.

**Across phases**: Polish documentation tasks T018–T021 are pure markdown edits in different files and can be done in parallel with US1/US2/US3 implementation.

**Cross-story parallelism**: An aggressive plan can run US1, US2, US3 implementation concurrently because the only shared file is `InitHandler.cs`. Sequential edits to `InitHandler.cs` (T003 then T010) are required to avoid conflicts; everything else parallelizes cleanly.

## Implementation strategy

**MVP scope (User Story 1 only)**: Phases 1+2+3. This delivers the in-chat onboarding SKILL — the highest-value deliverable. Users can already invoke "help me get started" in Copilot Chat.

**Incremental delivery**:
1. Ship US1 → users get in-chat onboarding.
2. Add US2 → reviewers and CI engineers get the offline reference.
3. Add US3 → agents stop improvising onboarding answers and route reliably.
4. Polish in Phase 6 syncs project-level documentation with the new state.

Each user story is independently testable, independently deployable, and delivers standalone value. US1 alone is sufficient for an MVP release; US2 and US3 enhance reach and reliability.
