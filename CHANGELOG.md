# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.36.0] - 2026-04-10

### Added
- **Quickstart SKILL & USAGE.md** (spec 032) — `spectra-quickstart` is the 12th bundled SKILL: workflow-oriented onboarding for Copilot Chat triggered by phrases like "help me get started", "tutorial", "walk me through". Presents 12 SPECTRA workflows with example conversations. Companion `USAGE.md` written to project root by `spectra init` as an offline workflow reference. Both hash-tracked by `update-skills`. Generation and execution agent prompts defer onboarding requests to the new SKILL.
- **Visible default profile format & customization guide** (spec 031) — `profiles/_default.yaml` and `CUSTOMIZATION.md` are now created by `spectra init` and bundled as embedded resources. Profile format is visible/editable instead of hardcoded. New `ProfileFormatLoader.LoadEmbeddedDefaultYaml()` and `LoadEmbeddedCustomizationGuide()` methods.
- **Customizable root prompt templates** (spec 030) — `.spectra/prompts/` directory with 5 markdown templates (behavior-analysis, test-generation, criteria-extraction, critic-verification, test-update) controlling all AI operations. Templates use `{{placeholder}}`, `{{#if}}`, `{{#each}}` syntax. New `analysis.categories` config section with 6 default categories. New `spectra prompts list/show/reset/validate` CLI commands. New `spectra-prompts` SKILL (11th bundled SKILL).
- `ProfileFormatLoader.LoadEmbeddedUsageGuide()` for resolving the bundled `USAGE.md` content.
- Bumped Spectre.Console 0.54.0 → 0.55.0, GitHub.Copilot.SDK 0.2.0 → 0.2.1, Markdig 1.1.1 → 1.1.2 (Dependabot PRs #10, #11, #12).

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
