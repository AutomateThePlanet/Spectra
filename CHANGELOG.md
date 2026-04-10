# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added
- `spectra-quickstart` SKILL (12th bundled SKILL) — workflow-oriented onboarding for Copilot Chat. Triggered by phrases like "help me get started", "tutorial", "walk me through". Presents 12 SPECTRA workflows with example conversations.
- `USAGE.md` — offline workflow reference written to the project root by `spectra init`. Mirrors the quickstart SKILL content in a format suitable for async onboarding, code review, and CI documentation. Hash-tracked by `update-skills` so customizations are preserved.
- `ProfileFormatLoader.LoadEmbeddedUsageGuide()` for resolving the bundled `USAGE.md` content.
- Generation and execution agent prompts now defer onboarding requests to the `spectra-quickstart` SKILL.

## [1.35.0] - 2026-04-10

### Added
- `spectra-update` SKILL (10th bundled SKILL) for test update workflow via Copilot Chat
- Agent delegation tables updated for update command routing
- `UpdateResult` extended with `totalTests`, `testsFlagged`, `flaggedTests`, `duration`, `success` fields

## [1.34.6] - 2026-04-09

### Fixed
- Dashboard test file path resolution & version bump

## [1.34.0] - 2026-04-08

### Fixed
- Criteria coverage, index generation, dashboard SKILL & CI pragma fixes

## [1.33.0] - 2026-04-07

### Fixed
- Criteria generation, polling, progress caching & CI build fixes

## [1.32.0] - 2026-04-06

### Changed
- Refined SPECTRA agents/skills CLI workflows

## [1.31.0] - 2026-04-05

### Added
- Coverage semantics fix & criteria-generation pipeline (spec 028)
- `TestCaseParser` now propagates `Criteria` field from frontmatter to `TestCase`
- Criteria loading wired into `GenerateHandler` for per-doc `.criteria.yaml` context

## [1.30.0] - 2026-04-04

### Added
- Docs index SKILL integration, progress page, coverage fix & terminology rename (spec 024)
- `spectra-docs` SKILL (9th bundled SKILL) with structured tool-call-sequence
- `--skip-criteria` flag for docs index command

### Fixed
- Dashboard coverage null-crash fix with zero-state defaults
