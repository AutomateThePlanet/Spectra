# Phase 0 Research: Authoring Orchestration Port

All unknowns are resolved below. Each decision cites the in-repo reference that grounds it — chiefly
the already-shipped critic subagent (`Content/Agents/spectra-critic.agent.md`, from the preceding
spec), which is the canonical Claude-Code-shaped artifact this port mirrors.

---

## D1 — Claude Code skill format & frontmatter for the ported authoring set

**Decision**: A ported authoring skill is `.claude/skills/<name>/SKILL.md` with frontmatter
`name`, `description`, and `tools: [...]` — and **no** `model:` line and **no**
`disable-model-invocation`. The body is plain prose + fenced CLI command blocks (no `runInTerminal`/
`awaitTerminal`/`show preview` Copilot verbs).

**Rationale**: The critic subagent already in the repo is the established convention: it carries
`name` / `description` / `tools` / `model` and a plain-CLI body, with no Copilot terminal verbs. The
authoring skills differ from the subagent in two ways dictated by the spec: (a) they drop the
`model:` line entirely — FR-002 says the interactive session selects the model (the subagent keeps
`model: claude-sonnet-4-6` because a `context: fork` subagent must pin its own model); (b) they drop
`disable-model-invocation` — that directive is the explicit-only guard a subagent needs, but an
authoring skill is meant to be invokable by the session both by `/name` and by relevance, so the
guard is removed (FR-002).

**Alternatives considered**:
- *Keep a `model:` line pointing at a Claude model* — rejected: FR-002 is explicit that the model
  pin is dropped for the authoring set; pinning would re-introduce the exact coupling the port
  removes.
- *Keep `disable-model-invocation` "to be safe"* — rejected: it would prevent the session from
  surfacing the skill by relevance, degrading the authoring UX the port exists to enable.

---

## D2 — `{{…TOOLS}}` placeholder resolution to Claude Code's tool model

**Decision**: Repoint the `SkillResourceLoader` tool-list constants for the ported set to Claude
Code tool names so no unexpanded `{{…TOOLS}}` token ships and no Copilot tool name (`execute/…`,
`read/…`, `browser/…`) remains:
- `{{READONLY_TOOLS}}` / `{{GENERATE_TOOLS}}` / `{{GENERATION_TOOLS}}` → a Claude Code read/run set:
  `Read, Grep, Glob, Bash` (plus `Task` for the generation skill, which must invoke the critic
  subagent — see D4). The execution set (`{{EXECUTION_TOOLS}}`) is **not** repointed in this spec
  (the execution agent is excluded; next spec).

**Rationale**: Keeping the loader's placeholder-substitution mechanism intact honors the
reused-verbatim pipeline (FR "Reused Verbatim") while guaranteeing the resolved value is Claude
Code's tool model. The ported authoring skills are read/run choreography over CLI verbs, so a small
read+`Bash` set covers them; the generation skill additionally needs `Task` to invoke the
`context: fork` critic subagent.

**Alternatives considered**:
- *Drop the `tools:` frontmatter line entirely for the ported set* — rejected (see Complexity
  Tracking): it diverges loader behavior per-artifact and complicates the still-`.github/` execution
  agent that shares the loader; repointing the constant is the smaller, uniform change.
- *Leave the Copilot tool names* — rejected: FR-002/SC-002 require no Copilot-ism ships; Copilot
  `execute/runInTerminal` etc. are meaningless to Claude Code.

---

## D3 — Install target path: `.claude/skills/` vs `.claude/agents/`

**Decision**: Authoring skills (the 13 + the generation skill) install to
`.claude/skills/<name>/SKILL.md`. The **generation agent** is ported into a generation **skill** at
`.claude/skills/spectra-generation/SKILL.md` (it runs in the main session). The invoked **critic
subagent** installs to `.claude/agents/spectra-critic.agent.md` (a `context: fork` subagent belongs
in the agents directory so the session/`Task` tool can invoke it). The **execution agent** keeps its
current `.github/agents/` install untouched (excluded — next spec).

**Rationale**: Claude Code reads main-session skills from `.claude/skills/<name>/SKILL.md` and
subagents from `.claude/agents/`. The generation artifact is authoring choreography for the main
session → a skill; the critic is an isolated verifier → a subagent. Splitting the install loop by
role (skills vs the one critic agent) keeps each artifact in the directory Claude Code expects.

**Alternatives considered**:
- *Install the critic under `.claude/skills/` too* — rejected: a `context: fork` subagent is invoked
  via the agent/`Task` mechanism, not surfaced as a main-session skill; the agents directory is
  correct.
- *Relocate the execution agent to `.claude/` now* — rejected: out of scope (SC-005); it pulls the
  execution content port and MCP-allowlist setup into this spec.

---

## D4 — Mandatory-critic invocation from the generation skill

**Decision**: The generation skill's procedure includes an explicit, non-skippable step that invokes
the `spectra-critic` subagent (via the `Task`/subagent mechanism) on each generated candidate
**before** persistence. The step is written as a required stage in the numbered procedure with no
"optional"/"skip"/"if enabled" branch and no reliance on auto-invocation; the skill then hands the
critic's JSON verdict to `spectra ai ingest-verdict` (the deterministic boundary from the preceding
spec) and gates on the result.

**Rationale**: FR-004 requires the critic to be a mandatory explicit step. The critic subagent
already documents its own procedure (compile-critic-prompt → render verdict → ingest-verdict) and is
declared `disable-model-invocation: true` so it is *only* reachable by explicit invocation — the
generation skill is the explicit invoker. Writing it as a required numbered step (not an "if critic
enabled" aside) is what makes it non-skippable in the procedure.

**Alternatives considered**:
- *Rely on the critic's `disable-model-invocation` + hope the session calls it* — rejected: that
  guarantees the critic is never auto-invoked, but not that it is *always* invoked; the mandate must
  live in the generation skill's procedure.
- *Keep `--skip-critic` as the in-skill default path* — rejected: FR-004 forbids a skippable
  verification path in the mandated flow (the CLI flag may still exist for power users, but the
  ported skill's procedure does not offer skipping as a step).

---

## D5 — Translating `runInTerminal`/`awaitTerminal` + confirmation-avoidance to Claude Code

**Decision**: Replace the Copilot terminal choreography with Claude Code's model in spirit:
- `runInTerminal` + `awaitTerminal` + "do NOTHING while waiting" → "run the command with the Bash
  tool and wait for it to complete before reading the result file; do not poll or interleave other
  reads while it runs." The intent (don't thrash while a long command runs; read the result file
  only after completion) is preserved.
- `show preview .spectra-progress.html?nocache=1` → keep the progress-page affordance described in
  Claude-Code-neutral terms (open/point the user at the live progress page) without the Copilot
  Simple-Browser verb.
- "Do NOT ask the user clarifying questions about count or scope" → translate to Claude Code's
  confirmation model: state that the topic-vs-scenario shape is the only signal needed and proceed
  without a needless confirmation round-trip — re-expressed, not copied verbatim (FR-005).

**Rationale**: FR-005 requires the discipline to carry over *in spirit*, not verbatim. Claude Code's
Bash tool already blocks until a foreground command completes, so the "do NOTHING while waiting"
guard becomes "don't interleave polling reads," which is the portable form of the same rule.

**Alternatives considered**:
- *Copy the Copilot phrasing verbatim* — rejected: FR-005 explicitly forbids it; `runInTerminal` is
  a Copilot verb with no Claude Code meaning.
- *Drop the discipline entirely* — rejected: the anti-thrash and no-needless-confirmation intents are
  still valuable under Claude Code and FR-005 requires them to carry over.

---

## D6 — Bounded regenerate-on-fail-loud loop

**Decision**: The generation skill drives: compile prompt → take generated content → call the
model-free ingest/validate boundary → on a fail-loud validation error **or** a fail-loud critic
damage error, regenerate addressing the *specific* error, bounded by the existing retry limit; when
the limit is reached, stop and surface the failure rather than persisting an unverified/invalid test.
This is expressed as procedure text (the skill choreographs the existing CLI verbs and their
exit-code contract), not new CLI behavior.

**Rationale**: FR-003 requires the skill to honor the existing retry discipline and the fail-loud
exit codes the preceding specs already deliver (e.g. ingest exit 5/6, compile exit 4). No new limit
value is introduced; the skill wires the loop to the surface that already exists.

**Alternatives considered**:
- *Introduce a new retry-count flag/limit* — rejected: out of scope; FR-003 references the existing
  limit, and adding one would change the CLI surface the regression net pins.
- *Retry without using the specific error* — rejected: FR-003 requires the regenerate to address the
  specific fail-loud error, which is what makes the loop converge rather than repeat.

---

## D7 — Keeping the install pipeline & regression net intact

**Decision**: Reuse `SkillsManifestStore`, `FileHasher`, the embedded-resource reflection in
`SkillResourceLoader`, and the update-vs-skip hashing in `UpdateSkillsHandler` unchanged — only the
**target path** (`.github/…` → `.claude/…`) and the **content** change. The `Spectra.Core` suite and
the CLI-verb tests for the commands the skills call are not touched; `InitCommandTests` is rewritten
only where it asserts the old `.github/` install paths.

**Rationale**: The spec's Reused-Verbatim section pins the pipeline mechanism; the regression net pins
the CLI-verb surface the skills choreograph. Limiting the change to path + content keeps both intact
and makes a regression-net break a genuine signal.

**Alternatives considered**:
- *Refactor the manifest/hash mechanism while here* — rejected: YAGNI and it would muddy the
  reused-verbatim guarantee.
