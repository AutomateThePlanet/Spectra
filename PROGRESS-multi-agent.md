# Progress: `spectra init --agent` (multi-agent install, additive only)

Resume from here in every new session. Brief: the "add the other AI agents to `spectra init`" task (s.0–s.4).
Golden rule: the Claude install stays byte-identical; ADD, don't change.

## Status

| Step | State | Notes |
|---|---|---|
| s.0 sync | done | baseline: see below |
| golden Claude test | done | `ClaudeInstallGoldenTests`, 40 files pinned (Golden/104) |
| s.1 investigation | done | `FINDINGS-multi-client-gap-v2.md`, `specs/104-multi-agent-init/spec.md` |
| s.2 implement | todo | |
| s.3 tests | todo | |
| s.4 docs + release | todo | |
| final push | todo | feature branch only, no merge, no PR |

## s.0 sync (2026-10-27)

- Local coverage deletions (49 files, 6,372 lines) saved in `stash@{0}`
  ("local coverage-file deletions (saved before pulling 2026-10-27)"). Tracked changes only; the two untracked
  files (`FINDINGS-update-generate-token-economy.md`, `spectra-token-economy-specs.zip`) stayed in place (no
  collision with incoming paths). NOT re-applied — the user decides.
- Stashed paths changed upstream: 5 — `CoverageReportWriter.cs`, `GapAnalyzer.cs`, `GapPresenter.cs`,
  `CriteriaIndex.cs`, `GapAnalyzerTests.cs`. Simulated apply (`git merge-tree`): does NOT apply cleanly —
  modify/delete conflicts on `CoverageReportWriter.cs` and `CriteriaIndex.cs` (the other 3 were deleted upstream too).
- `main` fast-forwarded to `61ea574` (= origin/main).
- Baseline tests (Debug, project by project): Core 1244/1245 (1 timing test, see below), Execution 228, Integration 8,
  CLI 2595/2596 (1 timing test). Both timing failures are environmental: `UpdateTriagePerformanceTests` fails in
  its SETUP (writing 2,500 files took 4m33s vs a 60 s budget, disk throttled by the AV); the Core one passes in Release.
  The same commit is green on GitHub CI (Linux, Release). Gate per task: build + Core/Execution/Integration in full +
  the affected CLI classes; full CLI suite (43 min here) in the background at milestones.

## Decisions (pre-approved design choices, recorded)

18 decisions in `FINDINGS-multi-client-gap-v2.md` §5. Key ones: `--agent` omitted = Claude as today (+notice),
`--agent auto` = BELLATRIX detection; new layer in `src/Spectra.CLI/Install/`; only change to existing code is an
`installClaude = true` guard in `InitHandler`; skills neutral (shared dirs), subagents per provider (all 6); the critic
keeps Write (verdict file) so no Cursor `readonly`; allowlists only Cursor/Devin/OpenCode.

## Next action

s.2: content transform (neutral text) + emitters + planner + manifests + `--agent` option; Claude golden must stay green.
