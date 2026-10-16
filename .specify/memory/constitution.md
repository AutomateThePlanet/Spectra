<!--
================================================================================
SYNC IMPACT REPORT
================================================================================
Version Change: 2.0.0 → 2.1.0 (Spec 100: the constitution states the rules the code already follows)

Rationale for MINOR: Principle III gains a normative clause (the human summary in JSON mode); the other
changes clarify wording to match the product as it is since Specs 070–099. No principle is removed or
redefined.

Modified Principles:
  - III. Orchestrator-Agnostic Design — new clause: in JSON mode stdout is only the JSON document; the
    human summary is never suppressed and goes to stderr (the rule since Spec 079, applied by every
    batch command since).
  - IV. CLI-First Interface — "The AI agent MUST never write to the filesystem directly" replaced by the
    precise rule: the agent writes only staging inputs under `.spectra/` that a validated CLI ingest
    command reads; every persisted artefact is written by a CLI command. This is a clarification of
    existing behaviour (staging since Specs 077/088), not a change.
  - IV. CLI-First Interface — batch bullet notes that the batch ingest commands support `--dry-run`
    (Specs 090/097) and that `--no-review` is a global option, a no-op where nothing prompts.

Modified Sections:
  - Development Workflow › Test-Required Discipline: "MCP tool contracts" → "CLI command contracts
    (JSON output and exit codes)".
  - Development Workflow › Code Review Requirements: "new MCP tools" → "new CLI commands".
  - Governance › Compliance Review: runtime development guidance is `CLAUDE.md` (AGENTS.md points to it).

Added Sections: none. Removed Sections: none.

Templates Requiring Updates (checked: no MCP / provider / Copilot wording):
  - .specify/templates/plan-template.md: ✅ Compatible (Constitution Check section exists)
  - .specify/templates/spec-template.md: ✅ Compatible
  - .specify/templates/tasks-template.md: ✅ Compatible

Follow-up TODOs: None
================================================================================
-->

# SPECTRA Constitution

## Core Principles

### I. GitHub as Source of Truth

All test definitions, documentation, and configuration MUST be stored in Git repositories. GitHub is the authoritative source for:

- Test cases as Markdown files with YAML frontmatter in `test-cases/{suite}/`
- Documentation in `docs/` that drives test generation
- Configuration in `spectra.config.json` at repository root
- Metadata indexes (`_index.json`) committed to enable deterministic builds

**Rationale**: Git provides version control, collaboration, and CI/CD integration. No external database or proprietary storage creates vendor lock-in or synchronization complexity.

### II. Deterministic Execution

The execution engine MUST be a deterministic state machine with explicit states and validated transitions:

- Same inputs MUST produce the same execution queue
- State transitions MUST be validated before execution — invalid sequences are rejected
- Run state MUST be durable (SQLite) and reconstructable by any short-lived process, so behavior does not depend on a live session
- Every command response MUST be self-contained with run status, progress, and the next expected action
- The AI orchestrator MUST never manage state — the engine is the authoritative state machine

**Rationale**: Determinism enables reproducibility, debugging, and trust. LLMs are stateless; the engine must enforce order. (Execution is delivered through the `spectra run` CLI; SPECTRA's MCP execution adapter was removed in Spec 070.)

### III. Orchestrator-Agnostic Design

SPECTRA's surface MUST work with any LLM orchestrator (Claude Code, custom agents) and equally from a bare terminal or CI:

- Commands MUST be self-contained — an orchestrator need not remember prior calls; each invocation re-derives the state it needs
- Command output MUST remain minimal and structured (`--output-format json`) to avoid context overflow
- In JSON mode stdout MUST be only the JSON document; the human summary is never suppressed — it goes to stderr, so the user always sees what happened
- No bidirectional sync with external test management systems — one-directional integration only
- SPECTRA MUST NOT run an in-process model — all inference is the user's own agent session (the model-free generation/criteria/critic seams of Specs 058/059/069); there is no provider chain or API key to manage

**Rationale**: Teams have different agent subscriptions and preferences. A model-free, command-driven surface avoids single-vendor lock-in and keeps SPECTRA usable by any orchestrator, terminal, or pipeline.

### IV. CLI-First Interface

All functionality MUST be exposed via CLI commands before any UI:

- Every operation is a named command with explicit parameters — no chat loops
- Commands MUST be CI-friendly with deterministic exit codes
- The AI agent MUST NOT write tests, criteria, indexes, baselines or grounding directly. It writes only staging inputs under `.spectra/` (e.g. `.spectra/updates|repairs/<suite>/*.json`, `.spectra/generated.json`, `.spectra/analysis.json`, `.spectra/criteria.json`, critic verdict files) that a validated CLI ingest command reads; every persisted artefact is written by a CLI command
- Batch operations MUST support `--dry-run` and `--no-review` flags for automation: the batch ingest commands (`ingest-update --all`, `ingest-verdict --all`, `ingest-grounding --all`, `ingest-analysis`) support `--dry-run` (Specs 090/097); `--no-review` is a global option and a no-op for commands that never prompt

**Rationale**: CLI enables automation, scripting, and CI/CD pipelines. GUIs can be built on top of a solid CLI foundation.

### V. Simplicity (YAGNI)

Start with the simplest solution that works. Complexity MUST be justified:

- No abstractions until the third similar use case
- No feature flags or backwards-compatibility shims when code can be changed directly
- No premature optimization — profile first, optimize measured bottlenecks
- Dependencies MUST be evaluated for necessity — prefer standard library when adequate

**Rationale**: Premature complexity is the root of unmaintainable systems. SPECTRA is early-stage; agility matters more than architectural purity.

## Quality Gates

All code and artifacts MUST pass these gates before merge:

| Gate | Requirement | Enforced By |
|------|-------------|-------------|
| Schema Validation | All test files have valid YAML frontmatter | `spectra validate` |
| ID Uniqueness | All test IDs are unique across the repository | `spectra validate` |
| Index Currency | All `_index.json` files are up to date | `spectra validate` |
| Dependency Resolution | All `depends_on` references point to existing test IDs | `spectra validate` |
| Priority Enum | All priority values are in allowed enum (high/medium/low) | `spectra validate` |

**Exit Codes**: `spectra validate` MUST return exit code 0 for valid, exit code 1 for errors. CI pipelines MUST fail on non-zero exit.

## Development Workflow

### Test-Required Discipline

Tests MUST exist for all public APIs and critical paths:

- Tests are required but MAY be written after implementation (not strict TDD)
- Integration tests MUST cover: CLI command contracts (JSON output and exit codes), state machine transitions, CLI command workflows
- Unit tests MUST cover: parsing, validation, index operations
- Test coverage expectations: Core library 80%+, CLI commands 60%+

### Code Review Requirements

All changes MUST be reviewed before merge:

- At least one approval required for feature branches
- Architecture changes (new projects, new dependencies, new CLI commands) require explicit justification
- Complexity additions MUST reference why simpler alternatives were rejected

## Governance

### Amendment Procedure

1. Propose amendment via pull request to `.specify/memory/constitution.md`
2. Document rationale in PR description
3. Update version number following semantic versioning:
   - MAJOR: Backward incompatible principle removals or redefinitions
   - MINOR: New principle/section added or materially expanded guidance
   - PATCH: Clarifications, wording, typo fixes
4. Update `LAST_AMENDED_DATE` to amendment date
5. Ensure dependent templates remain consistent

### Compliance Review

- All PRs MUST verify compliance with Constitution principles
- Violations MUST be documented in Complexity Tracking table (see plan template)
- Runtime development guidance is in `CLAUDE.md` (`AGENTS.md` points to it)

### Supersession

This Constitution supersedes all other practices when conflicts arise. If a pattern in existing code conflicts with these principles, new code MUST follow the Constitution and technical debt SHOULD be logged for remediation.

**Version**: 2.1.0 | **Ratified**: 2026-03-13 | **Last Amended**: 2026-10-16
