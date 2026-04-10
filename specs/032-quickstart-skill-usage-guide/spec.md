# Feature Specification: Quickstart SKILL & Usage Guide

**Feature Branch**: `032-quickstart-skill-usage-guide`
**Created**: 2026-04-10
**Status**: Draft
**Input**: User description: "Spec 024c — Quickstart SKILL & USAGE.md offline guide. Provide a workflow-oriented onboarding entry point for users interacting with SPECTRA via VS Code Copilot Chat, plus a matching offline reference document."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - New user opens Copilot Chat and asks how to begin (Priority: P1)

A QA engineer who has just installed SPECTRA opens VS Code Copilot Chat for the first time. They don't know which natural-language phrases trigger SPECTRA workflows. They type something open-ended like "help me get started" or "what can I do?" and expect the assistant to present a guided overview of available workflows with concrete example prompts they can copy.

**Why this priority**: Without an onboarding entry point, new users churn before reaching first value. Every other workflow assumes the user already knows what to say. This is the gateway story — without it, the rest of the SKILL ecosystem is unreachable for newcomers.

**Independent Test**: A user with a freshly initialized SPECTRA project opens Copilot Chat, asks "help me get started," and receives a numbered list of 11 SPECTRA workflows with example prompts. Selecting any workflow leads them through a complete scenario without needing to consult external docs.

**Acceptance Scenarios**:

1. **Given** a project with SPECTRA initialized and the bundled SKILLs installed, **When** the user asks Copilot Chat "help me get started" or "what can I do with SPECTRA?", **Then** the quickstart SKILL is invoked and presents the 11-workflow overview with example prompts.
2. **Given** the user has been shown the workflow overview, **When** they ask about a specific workflow (e.g., "show me how to generate tests"), **Then** the SKILL describes the phases of that workflow with example conversation patterns.
3. **Given** the user runs `spectra init` in a new project, **When** initialization completes, **Then** `.github/skills/spectra-quickstart/SKILL.md` exists in the project.

---

### User Story 2 - Reading the workflow guide outside of Copilot Chat (Priority: P2)

A team lead reviewing SPECTRA before adopting it (or onboarding a new teammate, or wiring up CI) needs to understand the available workflows without launching VS Code or Copilot Chat. They want a readable markdown document in the project root that explains every workflow with the same example prompts and expected outcomes.

**Why this priority**: Decision-makers and reviewers don't always have Copilot Chat open. CI engineers need to understand the surface area without an interactive session. Documentation outside the in-chat SKILL serves these audiences and supports async onboarding.

**Independent Test**: After running `spectra init`, the file `USAGE.md` exists in the project root and contains all 11 workflows with example prompts and expected outcomes, suitable for offline reading.

**Acceptance Scenarios**:

1. **Given** a fresh project, **When** the user runs `spectra init`, **Then** `USAGE.md` is created in the project root.
2. **Given** the project already contains `USAGE.md` with default content, **When** the user runs `spectra update-skills`, **Then** the file is refreshed with the latest bundled content.
3. **Given** the user has manually edited `USAGE.md`, **When** they run `spectra update-skills`, **Then** their customizations are preserved (the file is skipped) per the existing hash-tracking convention.

---

### User Story 3 - Onboarding requests routed correctly by agents (Priority: P2)

A user is interacting with the SPECTRA generation or execution agent and asks an onboarding question ("how do I use this?", "walk me through it"). The agent should defer to the quickstart SKILL rather than attempting to answer from its own (necessarily abbreviated) instructions.

**Why this priority**: Without explicit routing, agents may give partial or inconsistent onboarding answers. The quickstart SKILL is the authoritative onboarding source — agents should defer rather than duplicate.

**Independent Test**: The bundled `spectra-generation.agent.md` and `spectra-execution.agent.md` files contain explicit instructions to defer onboarding requests to the quickstart SKILL.

**Acceptance Scenarios**:

1. **Given** the bundled agent prompt files, **When** inspected, **Then** both contain a delegation reference to the spectra-quickstart SKILL for onboarding intents.
2. **Given** the user asks an onboarding question, **When** the agent processes it, **Then** the agent invokes the quickstart SKILL rather than improvising an answer.

---

### Edge Cases

- **User customized USAGE.md** — `update-skills` must skip files with modified hashes (existing convention) so user edits aren't overwritten.
- **User customized the quickstart SKILL** — Same hash-tracking behavior applies.
- **User runs `init --skip-skills`** — Quickstart SKILL and USAGE.md are skipped (consistent with existing init behavior).
- **User installed SPECTRA before this feature shipped** — Running `spectra update-skills` adds the new files; they don't have to re-init.
- **User asks an onboarding question to the help SKILL instead** — Help SKILL remains a command reference; documentation should clarify the distinction so users (and agents) know when to use which.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The `spectra init` command MUST create a quickstart SKILL file at `.github/skills/spectra-quickstart/SKILL.md` containing workflow-oriented onboarding content covering all 11 SPECTRA workflows with example prompts and expected outcomes.
- **FR-002**: The `spectra init` command MUST create a `USAGE.md` file in the project root containing offline-readable workflow documentation mirroring the quickstart SKILL's coverage but adapted for non-interactive reading.
- **FR-003**: Both new artifacts MUST be tracked by the `update-skills` hash system so unmodified files are refreshed to the latest bundled version while user-customized files are skipped.
- **FR-004**: The `--skip-skills` flag on `spectra init` MUST also skip creation of the quickstart SKILL and `USAGE.md`.
- **FR-005**: The quickstart SKILL content MUST list all 11 workflows: generate tests, extract acceptance criteria, check coverage, create dashboard, validate tests, browse tests, update tests, execute tests, create profile, index docs, import criteria.
- **FR-006**: The quickstart SKILL content MUST include trigger phrase examples (e.g., "help me get started", "what can I do", "tutorial", "quickstart") so Copilot Chat reliably invokes it for onboarding intents.
- **FR-007**: The quickstart SKILL content MUST be teaching-focused — it instructs the user on what to say, rather than executing CLI commands itself. Execution remains the responsibility of the workflow-specific SKILLs.
- **FR-008**: The bundled generation agent prompt MUST contain an explicit instruction to defer onboarding requests to the quickstart SKILL.
- **FR-009**: The bundled execution agent prompt MUST contain an explicit instruction to defer onboarding requests to the quickstart SKILL.
- **FR-010**: The `USAGE.md` content MUST be free of in-chat tool references (e.g., `runInTerminal`, `awaitTerminal`) since it is an offline document.
- **FR-011**: Project-level documentation (README, CUSTOMIZATION.md cross-reference, CHANGELOG, PROJECT-KNOWLEDGE.md, CLAUDE.md) MUST be updated to reflect the new SKILL count, the new file structure, and the existence of `USAGE.md` as the offline workflow reference.
- **FR-012**: The new SKILL MUST be discoverable as an additional bundled SKILL in the SKILL count tracked by project documentation.

### Key Entities *(include if feature involves data)*

- **Quickstart SKILL** — A bundled markdown file with frontmatter, delivered as an embedded resource and written to `.github/skills/spectra-quickstart/SKILL.md` during init. Tracked by hash for safe updates.
- **Usage Guide (USAGE.md)** — A bundled markdown document, delivered as an embedded resource and written to the project root during init. Tracked by hash for safe updates. Mirrors the quickstart SKILL's workflow coverage in offline-friendly form.
- **Skills Manifest entries** — Two new hash entries (one for the SKILL, one for `USAGE.md`) so `update-skills` can detect drift and refresh unmodified files.
- **Agent prompt delegation** — Lines in `spectra-generation.agent.md` and `spectra-execution.agent.md` directing onboarding intents to the quickstart SKILL.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A new user opening Copilot Chat for the first time after `spectra init` can locate a guided onboarding response within their first natural-language question (no need to read external docs first).
- **SC-002**: After `spectra init`, both the quickstart SKILL and `USAGE.md` exist on disk, and 100% of the 11 documented workflows are present in each.
- **SC-003**: Running `spectra update-skills` on a project with default (unmodified) versions of the new files refreshes them to the latest bundled content; running it on a project with user-edited versions preserves the edits.
- **SC-004**: Onboarding questions ("help me get started", "what can I do", etc.) directed to either the generation or execution agent are routed to the quickstart SKILL rather than answered ad hoc by the agent itself.
- **SC-005**: The total bundled SKILL count reported in project documentation increases by exactly one and matches the actual count of files created by `init`.
- **SC-006**: All existing tests continue to pass and the new tests for this feature (at minimum: quickstart SKILL content non-empty, USAGE.md content non-empty, init creates both files) pass.

## Assumptions

- The existing bundled-skills delivery mechanism (embedded resources, `SkillContent`/`SkillsManifest`, hash-tracked updates via `spectra update-skills`) is the right vehicle for both new artifacts. No new infrastructure is required.
- The 11-workflow taxonomy in the input description is authoritative and matches the current set of SPECTRA capabilities.
- `USAGE.md` belongs in the project root alongside `CUSTOMIZATION.md` (also a bundled, hash-tracked doc), not under `docs/` or `.spectra/`.
- The quickstart SKILL is teaching-oriented and does not duplicate execution logic from other SKILLs. It points users at what to say, and the workflow-specific SKILLs handle the actual CLI invocations.
- No new CLI command is needed (e.g., no `spectra quickstart`). The same content is available in-chat (via SKILL) and offline (via `USAGE.md`).
- The exact "before/after" SKILL count will be confirmed during planning by reading the current `SkillsManifest` rather than assumed from documentation.
