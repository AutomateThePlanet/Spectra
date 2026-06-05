# Phase 1 Data Model: Authoring Orchestration Port

This feature introduces **no persisted data model** — no new on-disk schema, no DB change, no change
to test/criteria/grounding formats. The "entities" here are the **artifact taxonomy** of the port:
the kinds of orchestration files, their frontmatter fields, install locations, and the validation
rules that make a port correct. They are documented so the contracts and tasks can reference a stable
vocabulary.

---

## Entity: Ported Authoring Skill

A `.claude/skills/<name>/SKILL.md` file — the Claude Code form of one bundled authoring artifact.

| Field (frontmatter) | Rule |
|---|---|
| `name` | Required; the skill slug (e.g., `spectra-coverage`). Unchanged from source. |
| `description` | Required; one line. Unchanged or lightly edited from source. |
| `tools` | Required; a Claude Code tool list resolved from the repointed `{{…TOOLS}}` constant — **no** Copilot tool names, **no** unexpanded placeholder. |
| `model` | **Absent** (dropped — the interactive session selects the model). |
| `disable-model-invocation` | **Absent** (dropped — the skill is session-invokable). |

**Body rules**: no `runInTerminal` / `awaitTerminal` / `show preview` Copilot verbs; the terminal and
confirmation discipline is expressed in Claude Code terms (D5). CLI command blocks and their flags
(`--no-interaction`, `--output-format json`, `--verbosity quiet`, `.spectra-result.json` reads) are
preserved — these are the reused CLI-verb surface.

**Members of this set (14)**: the 13 authoring skills (`spectra-coverage`, `spectra-criteria`,
`spectra-dashboard`, `spectra-delete`, `spectra-docs`, `spectra-help`, `spectra-init-profile`,
`spectra-list`, `spectra-prompts`, `spectra-quickstart`, `spectra-suite`, `spectra-update`,
`spectra-validate`) **plus** the generation skill (below). `spectra-generate` is the generation
skill's body.

---

## Entity: Generation Skill (specialization of Ported Authoring Skill)

`.claude/skills/spectra-generation/SKILL.md` — the main-session orchestrator ported from
`spectra-generation.agent.md` (+ the `spectra-generate` skill body it delegates to).

**Additional rules beyond the base skill**:
- Drives the loop: compile prompt → take generated content → call the model-free ingest/validate
  boundary → on a fail-loud error, regenerate with the *specific* error, bounded by the existing
  retry limit; stop and surface at the limit (FR-003 / D6).
- Contains a **mandatory, explicit, non-skippable** step that invokes the `spectra-critic` subagent
  before persistence (FR-004 / D4) — no "skip"/"if enabled"/auto-invocation branch in the mandated
  flow.
- `tools` includes `Task` (to invoke the critic subagent) in addition to the read/run set.

---

## Entity: Invoked Critic Subagent (reused, not shipped here)

`.claude/agents/spectra-critic.agent.md` — shipped by the preceding spec; **relocated** here from
`.github/agents/` to `.claude/agents/` so the generation skill can invoke it. Its content is **not**
redefined (SC-005).

| Field | Value (unchanged) |
|---|---|
| `name` | `spectra-critic` |
| `tools` | resolved `{{READONLY_TOOLS}}` (Claude Code read set) |
| `model` | `claude-sonnet-4-6` (a subagent pins its own model — kept) |
| `disable-model-invocation` | `true` (explicit-only — kept; this is the subagent guard, not a Copilot-ism) |
| `context` | `fork` (isolation — kept) |

---

## Entity: Excluded Execution Agent (out of scope)

`spectra-execution.agent.md` — **not** modified, **not** relocated, **not** translated. Keeps its
current `.github/agents/` install until the next spec. Listed here only to pin the scope boundary:
the port set must contain **0** occurrences of it (SC-005).

---

## Entity: Install Manifest Entry (reused mechanism)

A `(absolutePath → contentHash)` pair in the `.spectra/` skills manifest. Mechanism unchanged
(`SkillsManifestStore`, `FileHasher`); only the **path** value changes (`.github/…` → `.claude/…`)
for the ported set. Update-detection (user-modified → skip; bundled-changed → update) is unchanged.

---

## Entity: CLAUDE.md Runtime Declaration

The project-guidelines file's runtime statement.

| State | Value |
|---|---|
| Before | "GitHub Copilot SDK (sole AI runtime)" (line 6) |
| After | Claude-Code-only runtime described; `.claude/skills/` named as the authoring path of record; **0** occurrences of "sole AI runtime" (SC-006) |

---

## State transitions

There are no runtime state machines in this feature. The only "transition" is the install/update
lifecycle, unchanged in mechanism:

```
(not installed) --init/update-skills--> (.claude/skills/<name>/SKILL.md written, hash recorded)
(installed, unmodified) --update-skills, bundled changed--> (rewritten, hash updated)
(installed, user-modified) --update-skills--> (skipped, preserved)
```

The only change to this lifecycle is the destination directory of the authoring set.
