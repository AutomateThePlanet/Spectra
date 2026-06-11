<!--
================================================================================
SYNC IMPACT REPORT
================================================================================
Version Change: 1.1.0 → 2.0.0 (Spec 070: de-MCP / de-provider redefinition of Principles II & III)

Rationale for MAJOR: Principles II and III were redefined to remove the now-removed MCP execution
adapter (Spec 070) and the in-process provider chain / BYOK model (Specs 058/059/069). Principle II
"Deterministic Execution" now describes the engine + `spectra run` CLI with durable, reconstructable
state; Principle III "Orchestrator-Agnostic Design" now describes the model-free, command-driven CLI
surface (no MCP API, no provider chain). No principles added or removed; II & III redefined.

Added Sections:
  - Core Principles (5 principles)
  - Quality Gates
  - Development Workflow
  - Governance

Templates Requiring Updates:
  - .specify/templates/plan-template.md: ✅ Compatible (Constitution Check section exists)
  - .specify/templates/spec-template.md: ✅ Compatible (requirements structure aligns)
  - .specify/templates/tasks-template.md: ✅ Compatible (phase structure supports principles)

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
- No bidirectional sync with external test management systems — one-directional integration only
- SPECTRA MUST NOT run an in-process model — all inference is the user's own agent session (the model-free generation/criteria/critic seams of Specs 058/059/069); there is no provider chain or API key to manage

**Rationale**: Teams have different agent subscriptions and preferences. A model-free, command-driven surface avoids single-vendor lock-in and keeps SPECTRA usable by any orchestrator, terminal, or pipeline.

### IV. CLI-First Interface

All functionality MUST be exposed via CLI commands before any UI:

- Every operation is a named command with explicit parameters — no chat loops
- Commands MUST be CI-friendly with deterministic exit codes
- The AI agent MUST never write to the filesystem directly — all output goes through validated tool handlers
- Batch operations MUST support `--dry-run` and `--no-review` flags for automation

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
- Integration tests MUST cover: MCP tool contracts, state machine transitions, CLI command workflows
- Unit tests MUST cover: parsing, validation, index operations
- Test coverage expectations: Core library 80%+, CLI commands 60%+

### Code Review Requirements

All changes MUST be reviewed before merge:

- At least one approval required for feature branches
- Architecture changes (new projects, new dependencies, new MCP tools) require explicit justification
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
- Runtime development guidance is in `AGENTS.md`

### Supersession

This Constitution supersedes all other practices when conflicts arise. If a pattern in existing code conflicts with these principles, new code MUST follow the Constitution and technical debt SHOULD be logged for remediation.

**Version**: 2.0.0 | **Ratified**: 2026-03-13 | **Last Amended**: 2026-06-11
