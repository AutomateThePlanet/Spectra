# Implementation Plan: ATP Shared-Namespace `init` Contract (v2)

**Branch**: `068-init-namespace-contract` | **Date**: 2026-06-09 | **Spec**: [spec.md](./spec.md)
**Input**: Feature specification from `/specs/068-init-namespace-contract/spec.md`

## Summary

Bring SPECTRA's `spectra init` into conformance with the shared-namespace `init` contract. The
only present-tense SPECTRA gap is **FR-018 / FR-013–FR-015**: `init` writes `.vscode/mcp.json`
with **skip-if-exists** semantics (silent loss when another tool wrote the file first). This plan
converts that single writer to **merge-by-key** on the `servers.spectra` entry, preserving all
foreign server keys, failing loud on an unparseable file, and skipping the write when the entry is
already present and identical (true idempotence). All other SPECTRA behaviors are already
contract-compliant (FR-019) and are pinned by tests, not changed.

The technical approach mirrors the existing, proven `ClaudeSettingsInstaller` (a pure
`Ensure…(string? existingJson)` JSON-node merge + a read-modify-write `EnsureInstalledAsync`). We
add a sibling `VsCodeMcpConfigInstaller` and rewire `InitHandler.CreateVsCodeMcpConfigAsync` to it.

**BELLATRIX conformance (FR-020–FR-025) lives in a different repository
(`C:/SourceCode/BELLATRIX-AI-Agents`) and is OUT OF SCOPE for this branch's code.** It is tracked
as a separate workstream in `tasks.md` and must NOT be implemented from this repo.

## Technical Context

**Language/Version**: C# 12, .NET 8+
**Primary Dependencies**: System.Text.Json (`JsonNode`/`JsonObject`), System.CommandLine (CLI), xUnit (tests)
**Storage**: File-based — `.vscode/mcp.json` (shared config), `.claude/settings.json`, `.spectra/skills-manifest.json`
**Testing**: xUnit (`tests/Spectra.CLI.Tests`) — unit tests for the pure merge + integration test for `init`
**Target Platform**: Cross-platform CLI (Windows/macOS/Linux); dev host is Windows 11
**Project Type**: Single-project CLI (the change is confined to `src/Spectra.CLI`)
**Performance Goals**: N/A — one-shot config write during `init`
**Constraints**: Behavior-preserving for already-compliant paths; never drop a foreign config key; fail loud (not silent) on malformed JSON
**Scale/Scope**: One new installer class (~60 lines) + one rewired method; ~6 unit + 1–2 integration tests

**Resolved unknowns** (see `research.md`):
- JSONC handling in `.vscode/mcp.json` (comments / trailing commas) → tolerant read, clean write, no-op when entry already present.
- Whether the shared config is hash-tracked in the skills-manifest → no; shared config is merge-managed, never self-owned (R6 vs R7 boundary).

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Assessment |
|---|---|
| **I. GitHub as Source of Truth** | ✅ Pure file-based; no external store. The merge preserves committed config. |
| **II. Deterministic Execution** | ✅ The merge is a pure function of (existing JSON, fixed entry); same inputs → same output. Engine/state-machine untouched. |
| **III. Orchestrator-Agnostic** | ✅ No MCP tool surface change; no orchestrator coupling. |
| **IV. CLI-First** | ✅ Change is inside an existing CLI command handler; output through validated writers, deterministic exit codes (fail-loud on malformed config → non-zero). |
| **V. Simplicity (YAGNI)** | ✅ Reuses the existing `ClaudeSettingsInstaller` shape; one small class, no new abstraction layer, no new dependency. JSONC-comment round-tripping explicitly NOT built (documented limitation). |

**Gate result: PASS.** No violations; Complexity Tracking not required.

Post-Phase-1 re-check: **PASS** (design adds exactly one pure helper + one rewire; no new projects, dependencies, or MCP tools).

## Project Structure

### Documentation (this feature)

```text
specs/068-init-namespace-contract/
├── plan.md              # This file
├── research.md          # Phase 0 output
├── data-model.md        # Phase 1 output
├── quickstart.md        # Phase 1 output
├── contracts/
│   └── vscode-mcp-merge.md   # Behavioral contract for the merge writer
├── checklists/
│   └── requirements.md  # Spec quality checklist (already created)
└── tasks.md             # Phase 2 output (/speckit.tasks)
```

### Source Code (repository root)

```text
src/Spectra.CLI/
├── Skills/
│   ├── ClaudeSettingsInstaller.cs      # EXISTING — reuse as the pattern
│   └── VsCodeMcpConfigInstaller.cs     # NEW — pure merge-by-key on servers.spectra
└── Commands/Init/
    └── InitHandler.cs                  # MODIFY CreateVsCodeMcpConfigAsync (lines ~684-714)

tests/Spectra.CLI.Tests/
├── Skills/
│   └── VsCodeMcpConfigInstallerTests.cs   # NEW — unit tests (mirror McpAllowlistTests.cs)
└── Commands/Init/
    └── InitVsCodeMcpMergeTests.cs          # NEW — integration: init preserves foreign server
```

**Structure Decision**: Single-project CLI. The change is fully contained in `src/Spectra.CLI/Skills`
(new installer) and `src/Spectra.CLI/Commands/Init` (one method rewire), with tests in the existing
`tests/Spectra.CLI.Tests` project. No new projects, no engine/MCP/Core changes.

## Out of Scope (this branch)

- **BELLATRIX conformance (FR-020–FR-025)** — separate repo `C:/SourceCode/BELLATRIX-AI-Agents`,
  separate branch. Listed in `tasks.md` for traceability only; do NOT edit that repo from here.
- **SPECTRA instruction fragment + `@import` (FR-005 for SPECTRA)** — SPECTRA injects no standing
  `CLAUDE.md` instructions (it delivers via skills + the `spectra-critic` subagent + `settings.json`),
  so it is **vacuously compliant** with FR-003/FR-004 and FR-005 does not apply. Adopting a
  `.spectra/spectra.instructions.md` fragment is a deferred, optional enhancement, not part of this fix.
- **`.claude/settings.json` merge** — already compliant (`ClaudeSettingsInstaller`); unchanged.

## Complexity Tracking

No constitution violations. Table intentionally empty.
