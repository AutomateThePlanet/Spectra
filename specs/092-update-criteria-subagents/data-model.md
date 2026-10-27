# Data Model: Update and Criteria Skills as Orchestrators (Phase 5)

No CLI, config, prompt-template or file-format change. The new "data" is the text contract between the main session
and the subagents.

## Bundled agents

| File | Installed at | name | model | tools | Started by |
|---|---|---|---|---|---|
| `spectra-critic.agent.md` | `.claude/agents/` (unchanged) | spectra-critic | pinned (Phase 6) | Read, Grep, Glob, Bash, Write | main: generate, update, review-flagged |
| `spectra-updater.agent.md` | `.claude/agents/` | spectra-updater | inherit | Bash, Read, Write | main: update Step 5, edit again |
| `spectra-repairer.agent.md` | `.claude/agents/` | spectra-repairer | inherit | Bash, Read, Write | main: update Step 5a (generate in 5b) |
| `spectra-extractor.agent.md` | `.claude/agents/` | spectra-extractor | inherit | Bash, Read, Write | main: criteria Step 2 |

All: `context: fork`, `disable-model-invocation: true`. None asks the user, starts a subagent, or edits a test `.md`.

## Inputs (Task prompt from main)

| Agent | Input |
|---|---|
| updater | `suite: <suite>` and either `group: <G…>` or `ids: <id1>,<id2>` |
| repairer | `suite: <suite>`, `ids: <id1>,<id2>` |
| extractor | `doc: <repo-relative path>`, `component: <component>` |
| critic | `suite: <suite>`, `test: <id>` (unchanged) |

## Files each agent may write

| Agent | Writes |
|---|---|
| updater | `.spectra/updates/<suite>/updated-<id>.json` |
| repairer | `.spectra/repairs/<suite>/repaired-<id>.json` |
| extractor | `.spectra/criteria.json` |
| critic | `.spectra/verdicts/critic-verdict-<id>.json` (unchanged) |

Every persisted change goes through the existing ingest commands.

## Return lines (the agent's final message, nothing else)

```text
updater   := result-line* baseline-line warning-line* stop-line?
result    := <id> " | " ( "written" [" (manual verdict kept)"] | "unchanged"
                        | "unchanged_pending " <BUCKET> ": " <why>
                        | "failed " <error_code> ": " <first error> )
baseline  := "baseline: created=" N " updated=" N " unchanged=" N " snapshots=" N     (summed over the run)
warning   := "warning: " <text>

repairer  := ( <id> " | " ( "written" | "unchanged" | "failed " <error_code> ": " <first error> ) )* stop-line?

extractor := <doc> " | " ( "extracted" | "unchanged" | "rejected" | "failed " <code> )
             note-line? changeset-line* warning-line* stop-line?
note      := "note: " <text>                                   (e.g. empty document)
changeset := the CLI's changeset summary lines, verbatim (CriteriaChangesetPresenter):
             "Criteria changeset for <doc>" then
             "  + <id>  <text>" | "  ~ <id>  \"<old>\" → \"<new>\""
             | "  - <id>  \"<old>\"" ["  (still referenced by N test(s): <ids>)"]
             | "  * <id>  <field>: <old> → <new>" | "  No changes."
warning   := "Warning: criterion id <id> is defined more than once (…); not renumbered." | "warning: " <text>

critic    := <id> " | " ( <verdict> " " <score> | "failed: " <reason> )      (exactly one line)

stop-line := "stop: " <what no single spectra command covers>
```

Rules:
- The updater reports every id in `results` (including leftovers of an earlier group); a retry's outcome replaces the
  earlier line for that id.
- The repairer reports every input id exactly once.
- A missing or malformed line for an id → main reports that test as outcome unknown (updater, repairer) or unverified
  (critic); main never reads files to reconstruct it.

## Main-session state (per run)

| Item | Source | Use |
|---|---|---|
| per-test outcome | updater lines | report; `written` → critic; `unchanged_pending` → user question |
| edit-again count per test | main | at most 2 (R4) |
| critic verdict per test | critic line | route: done / repairer / unverified |
| repaired ids | repairer lines `written`/`unchanged` | re-critic; `--ids` for repaired grounding |
| changeset per doc | extractor lines | Step 3 summary, verbatim |
