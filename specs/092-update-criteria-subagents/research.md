# Research: Update and Criteria Skills as Orchestrators (Phase 5)

All facts verified on `309174b`. The spec has no open questions (FR-019 decided 2026-10-09: full changeset lines).

## R1 — Install routing (FR-016, FR-017)

**Decision**: `SkillInstallLayout.AgentPath(root, file)` returns `.claude/agents/<file>` for every agent file — one
rule, no name switch. `InitHandler` and `UpdateSkillsHandler` already iterate `AgentContent.All`, so the three new
agents are installed by both with no other change. A pre-092 workspace gets them through update-skills' existing
"missing → write and record" branch; a user-modified file stays skipped (hash differs from the manifest).

**Rationale**: the fallback to `.github/agents/` predates the Claude Code port (Spec 056/057); nothing installs there
any more (the generation/execution agents were merged into skills), so the fallback only ever misroutes.

**Alternatives considered**: adding three names to the switch (rejected by FR-016: one general rule).

## R2 — Agent frontmatter and tools (FR-001)

**Decision**: each new agent has

```yaml
name: spectra-updater | spectra-repairer | spectra-extractor
description: <one line; says it is invoked by the update / criteria skill, never auto-invoked>
tools: [Bash, Read, Write]
model: inherit
disable-model-invocation: true
context: fork
```

- `Bash` runs `spectra` commands; the body restricts it to `spectra *` (the NON-STOP contract). Claude Code's agent
  `tools` list can't scope Bash by pattern portably, so the restriction is stated, as in the skills.
- `Read` reads a command output the harness spilled to a file; `Write` writes only the files named in FR-002/004/005.
- No `Task` (subagents can't start subagents), no `Edit` (never edits test `.md` files), no `Grep`/`Glob` (not needed),
  so `{{READONLY_TOOLS}}` is not used.
- The critic keeps its pinned model and its tools (Phase 6).

## R3 — Return-line grammar (FR-003, FR-004, FR-006, FR-007)

**Decision** (data-model.md has the full grammar):
- Updater: one line per `results` entry, in `results` order across every ingest call of the run (a retry's line
  replaces the earlier line for that id): `<id> | written`, `<id> | written (manual verdict kept)` (the id is in
  `manual_verdict_kept` — main must not send it to the critic, as Step 5a requires today), `<id> | unchanged`,
  `<id> | unchanged_pending <BUCKET>: <why>`, `<id> | failed <error_code>: <first error>`. Then one `baseline: created=N
  updated=N unchanged=N snapshots=N` line (summed over the run's ingest calls); then `warning: <text>` lines.
- Repairer: one line per input id: `<id> | written`, `<id> | unchanged`, `<id> | failed <error_code>: <first error>`.
- Extractor: `<doc> | extracted` (changes persisted), `<doc> | unchanged` (no criterion changed, or an empty
  document — then a `note: empty document, nothing to extract` line), `<doc> | rejected`, `<doc> | failed <code>`;
  then the changeset summary lines exactly as the CLI prints them (`+`, `~`, `-` with referencing tests, `*`, or
  `No changes.`), then duplicate-id `Warning:` lines and any other warnings.
- Critic: last message exactly `<id> | <verdict> <score>` or `<id> | failed: <reason>`.
- Every agent: when the NON-STOP contract makes it stop, one `stop: <what no single spectra command covers>` line
  after the lines it has.

**Rationale**: the manual-verdict suffix carries the one piece of `ingest-update` JSON that main's routing needs; the
`stop:` line makes a NON-STOP stop visible instead of looking like a missing outcome.

## R4 — Retry ownership

**Decision**: inside one call the updater and repairer apply the bounded retry (at most two retries per failed test,
the 089/090/091 rules). Across calls, main owns the count: "edit again" for an `unchanged_pending` test is offered at
most twice per test (today's rule: it "counts toward the same at most two retries"). A failure that survives the
subagent's retries is reported, never redone by main.

## R5 — Critic routing in main (FR-010, FR-011)

**Decision**: Step 5a, per group: for each `written` id (not `manual verdict kept`), Task `spectra-critic`; parse its
final line. `grounded` → done; `partial` / `hallucinated` → collected for the repairer; `failed:` or a missing /
malformed line → unverified. Then `ingest-verdict --suite S --all` and `ingest-grounding --suite S --all` (small JSON;
091 lists kept hallucinated tests under `hallucinated`). Then one Task `spectra-repairer` {suite, collected ids}; for
ids it returns `written` / `unchanged`, the critic again per id, then `ingest-verdict --all` and `ingest-grounding --all
--repaired --repair-attempts 1 --ids <those ids>`; `failed` → "repair failed: <code>" (unverified if it was partial,
waiting for the user if hallucinated). A re-critic line `hallucinated` → the user's choice (edit again = updater with
that id; restore from git; delete via spectra-delete).

**Rationale**: the grounding block is still written by the CLI from the verdict file; main only needs the verdict
word to route, and the critic now says it.

## R6 — Repairer flow (FR-004)

**Decision**: compile the repair prompt per id, patch, Write `.spectra/repairs/<suite>/repaired-<id>.json` per id; then
one `ingest-update <suite> --all --keep-baseline`; failed ids re-patched per `error_code` and re-ingested, at most
twice. No grounding, no critic, no update-flow assumptions — the inputs are only {suite, ids}, so Phase 5b's generation
repair can call it unchanged.

## R7 — Extractor flow (FR-005, FR-006, FR-019)

**Decision**: the existing Step 2a–2c recipe moves into the agent verbatim in substance (compile; short-circuit empty
doc; refusals; extract; Write `.spectra/criteria.json`; ingest with `--output-format json`; exit 5/6/7 regeneration at
most 2 attempts; exit 1 with one `LOCK_TIMEOUT` retry). The changeset lines are copied from the ingest's human summary,
which the CLI prints on stderr in JSON mode — the agent copies them verbatim, never rewrites them.

## R8 — Skill rewrites and the moved assertions

**Decision**:
- spectra-update: a "Context budget contract" section after the overview; NON-STOP lists Task calls to the three agents
  and `Write` only to `.spectra/progress.json`; Step 5 = per group Task `spectra-updater`, relay, `unchanged_pending`
  questions; Step 5a = R5; the overview diagram shows the delegation. Steps 0–4, 6, 7 keep their content.
- spectra-criteria: Step 2 = per doc Task `spectra-extractor`, relay; Step 3 builds the summary from the returned lines;
  Steps 0–1 and the import/list flows unchanged.
- **Test rule**: every existing assertion (088–091) on text that describes work now done by an agent is retargeted to
  that agent's content with the same string (the rule moved, it wasn't lost); assertions about main-session behaviour
  stay on the skill. `AgentContent_HasAllAgents` (`Single`) becomes the four-agent set. The inventory is in tasks.md.

## R9 — Relay (FR-018)

**Decision**: both skills say: "Report every line a subagent returns, as returned" — per group / per document, then the
final summary (Step 7 / Step 3) built from the same lines. Main never paraphrases away an outcome, baseline line,
warning or changeset line.

## R10 — Measuring the context budget (SC-001, SC-002)

**Decision**: quickstart.md provides a scratch workspace script: one doc, 30+ tests linked to its sections, a baseline,
then a doc change that makes ≥ 30 tests LOAD in ≥ 3 groups (default group size 10), and 5+ docs for extraction. The
user runs the update / criteria skills in Claude Code and reads `/context` before and after. These runs need a live
Claude Code session and are the user's manual acceptance; the automated tests cover install, frontmatter and text
contracts.

## R11 — Verification workspaces (083 lesson)

**Decision**: install rows run the real `spectra init` (embedded template). The pre-092 row: a real init, then the
three new agent files and their manifest entries removed (as a 091 workspace has them), and the update skill modified;
`update-skills` must add the three agents and skip the modified skill.
