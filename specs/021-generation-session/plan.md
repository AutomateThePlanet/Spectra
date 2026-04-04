# Implementation Plan: Generation Session Flow

**Branch**: `021-generation-session` | **Date**: 2026-04-04 | **Spec**: [spec.md](spec.md)
**Input**: Feature specification from `/specs/021-generation-session/spec.md`

## Summary

Implement a four-phase generation session (Analysis → Generation → Suggestions → User-Described) with persistent session state, new CLI flags (`--from-suggestions`, `--from-description`, `--context`, `--auto-complete`), duplicate detection via fuzzy matching, and proper grounding metadata for user-described tests.

## Technical Context

**Language/Version**: C# 12, .NET 8+
**Primary Dependencies**: System.CommandLine (CLI), Spectre.Console (terminal UX), System.Text.Json (serialization), GitHub Copilot SDK (AI generation)
**Storage**: File-based session state (`.spectra/session.json`)
**Testing**: xUnit (existing 1211+ tests)
**Target Platform**: Cross-platform CLI (.NET 8)
**Project Type**: CLI tool
**Constraints**: Backward compatibility with existing generate command; session state single-user local file
**Scale/Scope**: Extends GenerateHandler + new session/suggestion/duplicate infrastructure

## Constitution Check

| Principle | Status | Notes |
|-----------|--------|-------|
| I. GitHub as Source of Truth | PASS | Session state is ephemeral (1h TTL), not committed to git |
| II. Deterministic Execution | PASS | Same inputs produce same session; session state is explicit |
| III. Orchestrator-Agnostic Design | PASS | CLI flags work with any orchestrator |
| IV. CLI-First Interface | PASS | All phases available as CLI flags; interactive is optional |
| V. Simplicity (YAGNI) | PASS | Builds on existing GenerateHandler; session is a thin state layer |

## Project Structure

### Source Code

```text
src/Spectra.CLI/
├── Session/                           # NEW: Session state management
│   ├── GenerationSession.cs           # Session model (state, suggestions, counts)
│   ├── SessionStore.cs                # Read/write .spectra/session.json
│   └── SessionSummary.cs              # Summary display at exit
├── Validation/
│   └── DuplicateDetector.cs           # NEW: Fuzzy title matching
├── Commands/Generate/
│   ├── GenerateCommand.cs             # MODIFY: Add new CLI flags
│   ├── GenerateHandler.cs             # MODIFY: Session flow orchestration
│   └── UserDescribedGenerator.cs      # NEW: Create test from description
├── Results/
│   └── GenerateResult.cs              # MODIFY: Add suggestions, duplicate_warning fields
└── Output/
    └── SuggestionPresenter.cs         # NEW: Display suggestions menu
```

**Structure Decision**: Extends existing CLI project. New `Session/` directory for session state. DuplicateDetector in existing Validation/. UserDescribedGenerator alongside GenerateHandler.

## Complexity Tracking

No constitution violations — table not needed.
