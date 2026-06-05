# Feature Specification: Authoring Orchestration Port (skills → `.claude/skills/`)

**Feature Branch**: `056-orchestration-port-skills`
**Created**: 2026-06-05
**Status**: Draft
**Input**: User description: "Spec 055 — Authoring orchestration port. Translate the CLI-bundled Copilot agents/skills to Claude Code skills, with the generation skill mandating the critic subagent as an explicit step and driving the compile → generate → validate → persist loop."

> **Series note**: This is migration spec **4 of 6** (the 052–057 series) and the **convergence of pista A** — it is the "head" that pulls together the model-free CLI levers delivered by the three preceding specs (the prompt-compiler / generation handoff, the criteria-extraction re-homing, and the critic subagent). The conceptual numbering in the source material calls this "Spec 055"; in the repository it lives in directory `056-` because of the established one-step offset (conceptual spec *N* → directory *N+1*). Like every spec in this series so far, it ships **additively** (see Assumptions — "Additive surface precedent"): the new `.claude/skills/` orchestration surface and the mandated critic step are delivered here, while the literal removal of the in-process C# model paths is coupled to and completed by the provider-retirement spec that follows (the in-process call cannot be torn out without retiring the provider chain that powers it).

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Authoring orchestration installs as `.claude/skills/<name>/SKILL.md` (Priority: P1)

A developer runs `spectra init` or `spectra update-skills` and works entirely in an interactive Claude Code session. After this change, the install pipeline lays down the authoring orchestration as Claude Code `.claude/skills/<name>/SKILL.md` files — not GitHub Copilot agent/skill artifacts — using the **same** `SkillResourceLoader` / `SkillsManifest` install-and-hash-tracking pipeline that exists today. The content changes format; the delivery mechanism does not. (The execution agent is explicitly excluded and ported by the next spec; this spec ports the generation agent and the 13 authoring skills it delegates to.)

**Why this priority**: This is the core port the spec exists to deliver. Without the authoring orchestration in Claude Code's skill format, a Claude Code user has no way to drive Spectra's authoring verbs from an interactive session. It is the foundation the other stories build on — every other requirement is about *what* those installed skills say and do.

**Independent Test**: Run the install/update path against a clean workspace and confirm it produces `.claude/skills/<name>/SKILL.md` files (not Copilot-format agents/skills), tracked by the existing manifest/hash mechanism, for the generation agent and the authoring skills in scope.

**Acceptance Scenarios**:

1. **Given** a clean workspace, **When** `spectra init` or `spectra update-skills` runs, **Then** it installs `.claude/skills/<name>/SKILL.md` files through the existing `SkillResourceLoader` / `SkillsManifest` pipeline.
2. **Given** the installed orchestration, **When** the manifest is inspected, **Then** each installed SKILL is tracked by the same hash mechanism used today (update detection is unchanged).
3. **Given** the set of ported artifacts, **When** it is enumerated, **Then** it covers the generation agent and the 13 authoring skills, and **excludes** the execution agent (deferred to the next spec).

---

### User Story 2 - No Copilot-isms remain in any ported skill (Priority: P1)

A maintainer inspects any ported skill and finds none of the three recurring Copilot-isms. After this change: there is no `model: GPT-4o` pin (the interactive Claude Code session selects the model); there is no `disable-model-invocation: true` (its reason — Copilot's auto-invocation guard — disappears under Claude Code); and there is no unexpanded `{{…TOOLS}}` placeholder (tool access is resolved to Claude Code's tool model). The `runInTerminal` / `awaitTerminal` "do NOTHING while waiting" discipline carries over **in spirit** to Claude Code's terminal model, and the Copilot confirmation-avoidance lines ("do NOT ask clarifying questions…") are **translated** to Claude Code's confirmation model rather than copied verbatim.

**Why this priority**: A skill that still pins `GPT-4o` or carries an unexpanded placeholder is factually broken under Claude Code — it would mis-route the model or emit a literal `{{…TOOLS}}` token. Removing these is what makes the ported skills actually correct in the new runtime, so it is P1 alongside the port itself.

**Independent Test**: Inspect every ported skill and assert zero occurrences of `model: GPT-4o`, zero occurrences of `disable-model-invocation`, and zero unexpanded `{{…TOOLS}}` placeholders; confirm the terminal/confirmation discipline is present but expressed in Claude Code terms.

**Acceptance Scenarios**:

1. **Given** any ported skill, **When** it is inspected, **Then** it contains no `model: GPT-4o` pin.
2. **Given** any ported skill, **When** it is inspected, **Then** it contains no `disable-model-invocation` directive.
3. **Given** any ported skill, **When** it is inspected, **Then** it contains no unexpanded `{{…TOOLS}}` placeholder — tool access is expressed in Claude Code's tool model.
4. **Given** the ported generation skill, **When** its waiting/confirmation guidance is read, **Then** the "do NOTHING while waiting" discipline is present in Claude Code terms and the Copilot confirmation-avoidance phrasing has been translated, not copied verbatim.

---

### User Story 3 - The generation skill drives the loop and mandates the critic subagent (Priority: P1)

A developer triggers test generation in an interactive Claude Code session. After this change, the ported generation skill drives the model-free CLI loop end to end: it calls the prompt-compiler, takes the generated content, calls the model-free ingest/validate boundary, and — on a fail-loud validation error or a fail-loud critic damage error — regenerates addressing that **specific** error, bounded by a retry limit. Critically, before a generated test is persisted, the skill invokes the **critic subagent as a mandatory explicit step** — never via auto-invocation, never skippable.

**Why this priority**: This is the behavioral heart of the convergence — the skill is the orchestrator that turns the three preceding specs' model-free levers into a working authoring flow, and the mandatory-critic guarantee is the safety property that makes "verified" mean something. If the critic step were optional or auto-invoked, the verification guarantee the whole pista was built for would not hold. P1.

**Independent Test**: Inspect the ported generation skill's procedure and assert it (a) drives the compile → generate → ingest/validate loop, (b) regenerates on a fail-loud error using the specific error and stops at the retry limit, and (c) invokes the critic subagent as a mandatory, explicit, non-skippable step before persistence.

**Acceptance Scenarios**:

1. **Given** the generation skill runs, **When** a test is generated, **Then** the skill invokes the critic subagent as a mandatory step before persistence — verification cannot be skipped or left to auto-invocation.
2. **Given** the generation skill runs, **When** the CLI returns a fail-loud validation error, **Then** the skill regenerates addressing that specific error, bounded by the retry limit.
3. **Given** the generation skill runs, **When** the CLI returns a fail-loud critic damage error, **Then** the skill regenerates addressing that specific error, bounded by the retry limit.
4. **Given** repeated fail-loud errors, **When** the retry limit is reached, **Then** the skill stops retrying and surfaces the failure rather than persisting an unverified test or looping unbounded.

---

### User Story 4 - In-process model paths retired; `CLAUDE.md` no longer names Copilot (Priority: P2)

A maintainer reads `CLAUDE.md` and the documentation and sees a Claude-Code-only model. After this change, with the generation / critic / extraction skills in place as the converged authoring surface, the in-process model paths that those specs replaced are retired (the additive surfaces from the prior specs become the path of record), and `CLAUDE.md` is refreshed to remove the "GitHub Copilot SDK (sole AI runtime)" declaration and describe the Claude-Code-only runtime.

**Why this priority**: Retiring the superseded paths and correcting the runtime declaration removes the last "two runtimes coexist" ambiguity from the authoring side. It is P2 because it is a consolidation-and-correctness pass on top of the user-facing capability delivered by US1–US3 — valuable, but not the capability itself. (Per the series' additive precedent, the literal C# removal is coupled to the provider-retirement spec that follows; see Assumptions.)

**Independent Test**: Confirm `CLAUDE.md` no longer declares Copilot as the sole/AI runtime and instead describes the Claude-Code-only model; confirm the converged skill surface is the documented authoring path and the superseded in-process narrative is gone from the affected docs.

**Acceptance Scenarios**:

1. **Given** `CLAUDE.md`, **When** it is read, **Then** it no longer declares "GitHub Copilot SDK (sole AI runtime)" and instead describes the Claude-Code-only runtime.
2. **Given** the authoring documentation, **When** it is read, **Then** the converged `.claude/skills/` surface is described as the path of record and the superseded in-process model-path narrative is removed or marked deprecated.

---

### Edge Cases

- **Execution agent must not be ported here**: The execution agent (`spectra-execution.agent.md`) shares the same Copilot-isms but is the independent pista — porting it in this spec would overstep scope (it lands in the next spec). The port set must exclude it.
- **Critic subagent already exists**: The critic subagent skill is shipped by the preceding spec; this spec must **invoke** it, not redefine or re-ship it. A port that duplicates the critic skill is wrong.
- **Unexpanded placeholder leak**: A ported skill that still contains a literal `{{…TOOLS}}` token (or any other unresolved placeholder) is a defect — placeholders must be resolved to Claude Code's tool model at authoring time, not left for an install-time expander that no longer applies.
- **Retry limit boundary**: The generation loop must stop at the retry limit on repeated fail-loud errors — it must neither loop unbounded nor silently persist an unverified/invalid test when the limit is hit.
- **Skippable-critic regression**: Any phrasing that lets the critic step be skipped, deferred, or auto-invoked rather than explicitly invoked is a regression against the mandatory-critic guarantee.
- **Confirmation-model mistranslation**: Copying Copilot's "do NOT ask clarifying questions" verbatim into a Claude Code skill is wrong — the intent (avoid needless confirmation churn for count/scope) must be re-expressed in Claude Code's confirmation model.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The generation agent and the 13 authoring skills under `src/Spectra.CLI/Skills/Content/` MUST be translated to `.claude/skills/<name>/SKILL.md` format and installed through the existing `SkillResourceLoader` / `SkillsManifest` pipeline (not `.github/`). The execution agent is excluded from this port (deferred to the next spec).
- **FR-002**: The three recurring Copilot-isms MUST be removed or translated in every ported skill: drop `model: GPT-4o` (the interactive session selects the model); drop `disable-model-invocation` (its reason disappears under Claude Code); and resolve every `{{…TOOLS}}` placeholder to Claude Code's tool model so no unexpanded placeholder remains.
- **FR-003**: The ported generation skill MUST drive the model-free loop: call the prompt-compiler, take the generated content, call the model-free ingest/validate boundary, and — on a fail-loud validation or critic-damage error — regenerate addressing that specific error, bounded by a retry limit (stopping and surfacing the failure when the limit is reached).
- **FR-004**: The ported generation skill MUST invoke the critic subagent (delivered by the preceding spec) as a **mandatory explicit step** before persistence — never via auto-invocation, never skippable.
- **FR-005**: The `runInTerminal` / `awaitTerminal` "do NOTHING while waiting" discipline MUST carry over in spirit to Claude Code's terminal model, and the Copilot confirmation-avoidance lines ("do NOT ask clarifying questions…") MUST be translated to Claude Code's confirmation model — re-expressed, not copied verbatim.
- **FR-006**: With the generation / critic / extraction skills in place as the converged authoring surface, the in-process model paths those specs replaced MUST be retired as the path of record (the additive surfaces become canonical). Per the series' additive precedent (see Assumptions), the literal removal of the in-process C# model call is coupled to the provider-retirement spec that follows; this spec makes the skill surface the documented path of record.
- **FR-007**: `CLAUDE.md` MUST be refreshed to remove the "GitHub Copilot SDK (sole AI runtime)" declaration (`CLAUDE.md:6`) and describe the Claude-Code-only runtime model.

### Reused Verbatim *(must not be modified)*

- **The `SkillResourceLoader` / `SkillsManifest` install-and-hash-tracking pipeline**: the install path, manifest, and update-detection hashing are unchanged. Only the *content* it installs changes format (Copilot agent/skill → `.claude/skills/<name>/SKILL.md`).
- **The model-free CLI verbs the skills call**: the prompt-compiler, the ingest/validate boundary, and the extraction verbs delivered by the preceding specs are invoked as-is; this spec authors the skills that call them, it does not redefine the verbs.
- **The critic subagent skill**: shipped by the preceding spec; this spec invokes it and must not redefine or re-ship it.
- **`Spectra.Core`**: unchanged.

### Key Entities

- **Ported authoring skill**: A `.claude/skills/<name>/SKILL.md` file — the Claude Code form of one of the bundled authoring artifacts (the generation agent or one of the 13 authoring skills), carrying no Copilot-isms.
- **Generation skill**: The ported orchestrator skill that drives the compile → generate → ingest/validate → persist loop, regenerates on fail-loud errors within a retry bound, and invokes the critic subagent as a mandatory explicit step.
- **Critic subagent (invoked, not shipped here)**: The `context: fork` critic skill from the preceding spec that the generation skill must invoke before persistence.
- **Install/manifest pipeline**: The reused `SkillResourceLoader` / `SkillsManifest` mechanism that writes the skill files and tracks them by hash for update detection — unchanged in mechanism, changed only in the content it carries.
- **Copilot-ism**: Any of the three translatable markers (`model: GPT-4o`, `disable-model-invocation`, `{{…TOOLS}}` placeholder) plus the `runInTerminal` / confirmation-avoidance discipline that must be removed or re-expressed for Claude Code.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: 100% of the in-scope authoring orchestration (the generation agent + 13 authoring skills) installs as `.claude/skills/<name>/SKILL.md` through the existing pipeline; 0 are installed as Copilot-format agents/skills and 0 land under `.github/`.
- **SC-002**: 0 ported skills contain `model: GPT-4o`; 0 contain `disable-model-invocation`; 0 contain an unexpanded `{{…TOOLS}}` placeholder.
- **SC-003**: The ported generation skill invokes the critic subagent as a mandatory explicit step before persistence in 100% of generation runs — there is no path in the skill that skips or auto-invokes verification.
- **SC-004**: On a fail-loud validation or critic-damage error, the generation skill regenerates with the specific error and stops at the retry limit in 100% of cases — 0 unbounded loops and 0 silent persistence of an unverified/invalid test at the limit.
- **SC-005**: The execution agent is **not** ported by this spec (0 occurrences in the port set), and the critic subagent skill is **not** redefined or re-shipped (0 duplicates).
- **SC-006**: `CLAUDE.md` contains 0 occurrences of "GitHub Copilot SDK (sole AI runtime)" and describes the Claude-Code-only runtime.
- **SC-007**: The protected regression net is unchanged and green: all `Spectra.Core` tests and all CLI-verb tests for the commands the skills call (which pin the surface the preceding specs deliver) pass without modification — a break signals a regression to investigate, not a test to update.

## Assumptions

- **Additive surface precedent (scope of FR-006)**: This spec is delivered **additively**, exactly as the three preceding specs in this series were (each shipped tagged "CLI surface"). It ships the net-new `.claude/skills/` authoring orchestration, the Copilot-ism removal, the mandatory-critic generation skill, and the refreshed `CLAUDE.md` — while the **literal** removal of the in-process C# model call from the live generation/critic paths is coupled to and completed by the provider-retirement spec that follows. The in-process model call cannot be torn out without retiring the provider chain that powers it, so the two are done together in that later spec. FR-006 is therefore satisfied here by making the converged skill surface the **path of record** and retiring the superseded narrative, not by deleting the C# call in this spec. This keeps the existing path working until the provider retirement lands, avoiding a window where authoring has no working model path.
- **Port scope is the authoring set**: "2 agents + 14 skills" minus the execution agent = the generation agent + 13 authoring skills (the critic subagent is shipped by the preceding spec, not part of this port). The execution agent is the independent pista and ports in the next spec.
- **Tool-model resolution**: The `{{…TOOLS}}` placeholders are resolved to Claude Code's tool model at authoring time; the install-time placeholder-expansion step that served Copilot no longer applies to the ported skills, so no unexpanded token may remain.
- **Retry limit is the existing bound**: "bounded by the retry limit" refers to the generation loop's existing retry discipline; this spec wires the skill to honor it, it does not introduce a new limit value.
- **Setup/allowlist content is the next spec's**: The setup docs reference a `.claude/settings.json` allowlist step, but the allowlist content itself (the `mcp__spectra__*` pre-approval) is the execution-side setup delivered by the next spec; this spec updates the setup docs that point at it without shipping the allowlist content.

## Out of Scope

- **The execution agent port** (the independent pista) — ported by the next spec.
- **Provider chain / config retirement** — the later spec in the series; the literal in-process C# model-call removal is coupled to it.
- **The critic subagent skill itself** — shipped by the preceding spec; this spec only invokes it.
- **The `mcp__spectra__*` allowlist content** — the execution-side setup delivered by the next spec; only the setup docs that reference it are touched here.
- **Any change to `Spectra.Core` or to the model-free CLI verbs** — those are reused as-is.

## Dependencies

- **The three preceding series specs**: the prompt-compiler / generation handoff (the loop the generation skill drives), the criteria-extraction re-homing (the extraction verbs the authoring skills call), and the critic subagent (the skill the generation skill mandates). This spec is the convergence written and implemented after all three.
- **The reused install pipeline**: `SkillResourceLoader` / `SkillsManifest`, unchanged in mechanism.
- **The subsequent specs**: the execution-agent port and the provider-retirement spec, which complete the work this spec defers (the execution pista and the literal in-process C# removal).

## Documentation Impact

- **Factually wrong (must fix)**: `CLAUDE.md` ("GitHub Copilot SDK (sole AI runtime)"); the skills-integration documentation (which describes the Copilot agent/skill format); the getting-started / quickstart setup (which now includes the `.claude/settings.json` allowlist step — the allowlist *content* lands in the next spec, but the setup step is described here).
- **Stale (update)**: the customization documentation; the Copilot chat / CLI / spaces-setup pages (superseded — mark deprecated or replace with the Claude Code equivalents).

## Tests

- **Rewrite (covers old format/behavior)**: skill-install and manifest tests that assert the Copilot agent/skill format or the `{{…TOOLS}}` placeholder expansion — rewritten to assert the `.claude/skills/<name>/SKILL.md` format and the absence of Copilot-isms.
- **Do not touch (regression net)**: all `Spectra.Core` tests; the CLI-verb tests for the commands the skills call (they pin the surface the preceding specs deliver). If one of these breaks, it signals a regression to investigate, not a test to update.
- **Net-new**:
  - `.claude/skills/` install/manifest tests: install produces `SKILL.md` files through the existing pipeline, and no Copilot-isms remain in any ported skill (FR-001, FR-002).
  - Generation-skill-mandates-critic test: the critic step is present, explicit, and not skippable (FR-004).
  - Generation-loop choreography test: a fail-loud error triggers a regenerate with the specific error, and the loop stops at the retry limit (FR-003).
