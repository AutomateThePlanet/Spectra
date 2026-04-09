# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

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
