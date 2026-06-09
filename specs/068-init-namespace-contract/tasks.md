---
description: "Task list for ATP Shared-Namespace init Contract (v2) — SPECTRA conformance"
---

# Tasks: ATP Shared-Namespace `init` Contract (v2)

**Input**: Design documents from `/specs/068-init-namespace-contract/`
**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/vscode-mcp-merge.md

**Tests**: Included — the spec defines acceptance scenarios and the SPECTRA constitution requires
tests for public APIs/critical paths.

**Scope note**: The only present-tense SPECTRA gap is the `.vscode/mcp.json` skip-if-exists →
merge-by-key fix (FR-013–FR-015 / FR-018), which realizes US1 for SPECTRA. US2/US3/US4 are already
contract-compliant for SPECTRA and are covered by verification tasks. **BELLATRIX conformance
(FR-020–FR-025) is a different repository and is NOT implemented from this branch** — see the final
section.

## Path Conventions

Single-project CLI: `src/Spectra.CLI/`, `tests/Spectra.CLI.Tests/` at repo root.

---

## Phase 1: Setup

- [x] T001 Confirm branch `068-init-namespace-contract`, `dotnet build` green, and existing init tests
  pass as a baseline (`dotnet test tests/Spectra.CLI.Tests --filter "FullyQualifiedName~Init|FullyQualifiedName~McpAllowlist"`).

---

## Phase 2: Foundational

**Purpose**: The shared merge writer that US1 depends on.

- [x] T002 [US1] Create `src/Spectra.CLI/Skills/VsCodeMcpConfigInstaller.cs` mirroring
  `ClaudeSettingsInstaller.cs`: a pure `static string EnsureSpectraServer(string? existingJson)` that
  merges `servers.spectra = { "command": "spectra-mcp", "args": ["."] }` into the parsed JSON
  (`JsonNode`/`JsonObject`), preserving all foreign `servers.*` keys and top-level keys (`inputs`).
  Parse with tolerant options (`CommentHandling = Skip`, `AllowTrailingCommas = true`). On a real
  parse failure throw a typed `InvalidMcpConfigException` (new, in the same file or `Skills/`).
- [x] T003 [US1] Add `static Task<string> EnsureInstalledAsync(string workingDirectory, CancellationToken ct)`
  to `VsCodeMcpConfigInstaller`: create `.vscode/`, read existing file, and write the merged JSON —
  with the **no-op optimization** (if the file exists and `servers.spectra` already deep-equals the
  desired value, do NOT rewrite, to preserve user comments and stay idempotent). Returns the path.

**Checkpoint**: writer compiles; no caller wired yet.

---

## Phase 3: User Story 1 — Two tools coexist without collision (Priority: P1) 🎯 MVP

**Goal**: `spectra init` merges its MCP server entry into an existing `.vscode/mcp.json` instead of
skipping, preserving any foreign tool's servers (kills the silent-loss path; FR-018 / FR-013–015).

**Independent Test**: `init` into a dir whose `.vscode/mcp.json` already has a foreign server → after
init both that server and `spectra` are present (SC-003).

### Tests for User Story 1 (write first, expect fail) ⚠️

- [x] T004 [P] [US1] Create `tests/Spectra.CLI.Tests/Skills/VsCodeMcpConfigInstallerTests.cs`
  (mirror `Skills/McpAllowlistTests.cs`) covering: fresh-from-null adds `servers.spectra`; foreign
  server preserved + `spectra` added; idempotent (apply twice → single `spectra`, equal output);
  top-level `inputs` preserved; JSONC `//` comments parse without throw; malformed JSON throws
  `InvalidMcpConfigException`; `EnsureInstalledAsync` writes `.vscode/mcp.json` and performs a no-op
  (no rewrite) when already equal.
- [x] T005 [P] [US1] Create `tests/Spectra.CLI.Tests/Commands/Init/InitVsCodeMcpMergeTests.cs`:
  seed a temp working dir with a `.vscode/mcp.json` containing a foreign server (`bellatrix-desktop-mcp`)
  + `inputs`, run the init MCP-config step (via `InitHandler` or the installer as `init` invokes it),
  then assert the foreign server AND `spectra` are both present and `inputs` is preserved (SC-003).
  Add a second case: pre-existing file already containing `spectra` → file is byte-unchanged (SC-005).

### Implementation for User Story 1

- [x] T006 [US1] Rewire `InitHandler.CreateVsCodeMcpConfigAsync` (`src/Spectra.CLI/Commands/Init/InitHandler.cs`,
  ~lines 684–714): remove the skip-if-exists early-return and the hardcoded literal; call
  `VsCodeMcpConfigInstaller.EnsureInstalledAsync(_workingDirectory, ct)`. Preserve the existing debug
  log + the init-summary line. Catch `InvalidMcpConfigException` and surface it as an actionable init
  error (do not swallow, do not overwrite the file).
- [x] T007 [US1] Run T004/T005 → green. Run the full init suite to confirm no regression in the other
  init writers (`dotnet test tests/Spectra.CLI.Tests --filter "FullyQualifiedName~Init|FullyQualifiedName~VsCodeMcp"`).

**Checkpoint**: US1 done — coexistence with a foreign MCP server is guaranteed for SPECTRA.

---

## Phase 4: User Story 2 — Self-scoped safe-update (Priority: P2) — verification only

**Goal**: Confirm (don't change) that SPECTRA's `update-skills` is manifest-scoped, hash-aware, and
never touches foreign files (FR-010–FR-012 already satisfied by `UpdateSkillsHandler` + `SkillsManifest`).

- [x] T008 [US2] Confirm existing coverage asserts manifest-scoped update + user-edit-skip behavior; if
  a gap exists, add one focused test in `tests/Spectra.CLI.Tests` asserting a hash-diverged self-authored
  skill file is skipped and a foreign-prefixed file under `.claude/skills/` is never enumerated. No
  production code change expected (note it in the task if confirmed already-covered).

**Checkpoint**: US2 verified for SPECTRA.

---

## Phase 5: User Story 4 — Idempotent re-run / namespace-local `--force` (Priority: P3) — verification

**Goal**: Confirm SPECTRA `init` aborts without `--force`, and that `--force` never touches the shared
`.vscode/mcp.json` beyond merge-by-key (FR-016/FR-017). The Phase-2 no-op optimization already makes the
MCP write idempotent.

- [x] T009 [US4] Add/confirm a test: running the init MCP step twice (including with `--force`) against a
  file with a foreign server leaves the foreign server intact and does not duplicate `spectra` (SC-006).
  Reuse the `InitVsCodeMcpMergeTests` fixture.

> User Story 3 (human-owned `CLAUDE.md` via `@import`) has **no SPECTRA in-repo task**: SPECTRA writes no
> `CLAUDE.md` and injects no standing instructions, so it is vacuously compliant (FR-003/FR-004) and
> FR-005 does not apply. Adopting a `.spectra/spectra.instructions.md` fragment is deferred (see plan
> "Out of Scope").

---

## Phase 6: Polish & Cross-Cutting

- [x] T010 [P] Run `quickstart.md` end-to-end manually (foreign-server preservation, idempotent re-run,
  malformed fail-loud) and confirm outputs match.
- [x] T011 [P] Update `CHANGELOG.md` and any init/usage docs that describe `.vscode/mcp.json` behavior
  to say "merge-by-key" instead of "skip if exists".
- [x] T012 Full `dotnet build` + `dotnet test` (all projects) green; confirm no behavioral change to
  `.claude/settings.json` merge or other init writers.

---

## BELLATRIX repo — SEPARATE BRANCH, NOT implemented here (traceability only)

> These tasks live in `C:/SourceCode/BELLATRIX-AI-Agents` and MUST NOT be executed from this branch.
> Listed so the contract's full conformance is traceable.

- [ ] B01 [US1] Remove the staged SPECTRA bundle (`newest-agents-skills/spectra-*`, and `bifrost-*` if it
  duplicates SPECTRA names) so BELLATRIX can never emit foreign files (FR-001/FR-020).
- [ ] B02 [US1] Re-home `ScaffoldCommand` output: emit `bellatrix-*` skills to `.claude/skills/<prefix>-<name>/SKILL.md`
  and `bellatrix-*` uniquely-named subagents to `.claude/agents/` (FR-006/FR-008/FR-021); resolve the
  `bellatrix-` vs `bifrost-` prefix (open item 1).
- [ ] B03 [US2] Add an authoring manifest + SHA-256 hash tracking; replace unconditional
  `File.WriteAllText` with author-scoped, hash-aware writes (FR-010–FR-012/FR-022).
- [ ] B04 [US1] Register `bellatrix-web-mcp` / `bellatrix-desktop-mcp` into `.vscode/mcp.json` (+ optional
  `.mcp.json`) and `.claude/settings.json` by merge-by-key (FR-013/FR-023).
- [ ] B05 [US4] Make `init`/`scaffold` idempotent and honor the force/regenerate flag as a namespace-local
  reset (FR-016/FR-017/FR-024).
- [ ] B06 [US3] Write `.bellatrix/bellatrix.instructions.md` and surface its `@import` line in the init
  summary (FR-003–FR-005/FR-025).

---

## Dependencies & Execution Order

- **Setup (T001)** → **Foundational (T002–T003)** → **US1 (T004–T007)**.
- T004 and T005 are [P] (different files). T006 depends on T002–T003. T007 depends on T004–T006.
- **US2 (T008)** and **US4 (T009)** depend only on the foundation/US1 fixture; can follow US1.
- **Polish (T010–T012)** after US1 (and US2/US4 if done).
- BELLATRIX B01–B06 are out-of-repo and independent of all the above.

## Implementation Strategy

MVP = Phases 1–3 (T001–T007): the `.vscode/mcp.json` merge fix. Ship and validate via SC-003 before
the verification/polish phases. The BELLATRIX block is a separate deliverable in its own repo.
