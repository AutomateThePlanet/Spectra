# Implementation Plan: Bundled SKILLs and Agent Prompts

**Branch**: `022-bundled-skills` | **Date**: 2026-04-04 | **Spec**: [spec.md](spec.md)

## Summary

Create 6 SKILL files and 2 agent prompts that `spectra init` writes to `.github/`. Add `--skip-skills` flag to init, new `update-skills` command with hash-based modification detection.

## Technical Context

**Language/Version**: C# 12, .NET 8+
**Primary Dependencies**: System.CommandLine, System.Security.Cryptography (SHA-256 hashing)
**Storage**: File-based (SKILL and agent files as embedded string constants)
**Testing**: xUnit
**Project Type**: CLI tool
**Constraints**: SKILL files are pure Markdown; must match GitHub Copilot SKILL.md convention

## Constitution Check

| Principle | Status | Notes |
|-----------|--------|-------|
| I. GitHub as Source of Truth | PASS | SKILL files are committed to git |
| II. Deterministic Execution | PASS | Init always produces identical files |
| III. Orchestrator-Agnostic | PASS | SKILLs are Copilot-specific but CLI works independently |
| IV. CLI-First | PASS | SKILLs wrap CLI commands; CLI is the source of truth |
| V. Simplicity | PASS | Static Markdown files, no runtime templating |

## Project Structure

```text
src/Spectra.CLI/
├── Commands/
│   ├── Init/
│   │   ├── InitHandler.cs              # MODIFY: Create SKILL + agent files, --skip-skills
│   │   └── InitCommand.cs              # MODIFY: Add --skip-skills option
│   └── UpdateSkills/
│       ├── UpdateSkillsCommand.cs      # NEW: update-skills command
│       └── UpdateSkillsHandler.cs      # NEW: hash-based update logic
├── Skills/                             # NEW: Bundled SKILL content as string constants
│   ├── SkillContent.cs                 # All 6 SKILL file contents
│   └── AgentContent.cs                 # Both agent file contents
└── Infrastructure/
    └── FileHasher.cs                   # NEW: SHA-256 hash computation
```
