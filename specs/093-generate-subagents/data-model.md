# Data Model: Generate Skill as Orchestrator (Phase 5b)

## `ingest-verdict --suite S --all` result (FR-016, FR-017) — additions in bold

```json
{"command":"ingest-verdict","mode":"batch","suite":"storage",
 "grounded":1,"partial":1,"hallucinated":0,"errors":0,"skipped_stale":0,"stale":[],
 "results":[{"id":"TC-101","verdict":"grounded","score":0.92,"gate":"pass"},
            {"id":"TC-102","verdict":"partial","score":0.71,"gate":"pass"}],
 "not_found":[]}
```

| Field | Meaning |
|---|---|
| **`results`** | one entry per classified verdict file of the suite (and, with `--ids`, of a listed id); sorted by file; `verdict` ∈ grounded / partial / hallucinated / manual; `gate` = `drop` for hallucinated, else `pass` |
| **`not_found`** | `--ids` entries not in the suite index; `[]` without `--ids` |
| existing fields | unchanged; stale / unreadable files have no `results` entry |

Human summary (stdout in human mode, stderr in JSON mode): the counts line, then `TC-101: grounded 0.92 (pass)` per
result and `TC-999: not found in suite 'storage'` per unknown id.

Options: `--ids a,b` (with `--all` only; 091 parsing). Checks, in order: `--from` with `--all` → existing message;
`--ids` without `--all` → `--ids is only valid with --all.` (exit 1).

## Bundled agents (Spec 093 additions)

| File | name | tools | Input | Writes |
|---|---|---|---|---|
| `spectra-analyst.agent.md` | spectra-analyst | Bash, Read, Write | `suite`, `doc_suite`, `focus?` | `.spectra/analysis.json` |
| `spectra-generator.agent.md` | spectra-generator | Bash, Read, Write | `suite`, `focus`, `count` | `.spectra/generated.json` |
| `spectra-repairer.agent.md` (092, return changed) | spectra-repairer | Bash, Read, Write | `suite`, `ids` | `.spectra/repairs/<suite>/repaired-<id>.json` |

All: `model: inherit`, `disable-model-invocation: true`, `context: fork`; never ask, never start a subagent, never edit
a test `.md`; NON-STOP; `stop: <reason>` when stopped.

## Return lines

```text
analyst   := recommendation-lines batch-line* stop-line?
           | "refused: " <error_code> refusal-line* stop-line?
recommendation-lines :=
             "already_covered: " N
             "recommended: " N
             "documents_analyzed: " N
             "breakdown: " <category>=N (", " <category>=N)*            (non-zero entries; "breakdown: none" if empty)
             "techniques: " <TECH>=N (", " <TECH>=N)*                    (non-zero; "techniques: none" if empty)
             ("boundary_gap: " <kind> " · " <field> " — " <description> [" [" <source> "]"])*
batch     := "batch " n " | count " k " | focus " <source doc> ": " <behaviour title> ("; " <behaviour title>)*
             (≤ 8 behaviours, one source doc; counts add up to `recommended`; none when recommended = 0)
refusal   := "refusal: " <a line of the CLI's refusal message, verbatim — candidate doc suites, remedies>

generator := ( <id> " | " <title> )* baseline-line? warning-line* ( "failed " <error_code> ": " <first error> )? stop-line?
baseline  := "baseline: " <the ingest's baseline summary>

repairer  := ( <id> " | " ( "written" | "unchanged" | "failed " <error_code> ": " <first error> ) )* stop-line?
             (one line per `results` entry over its ingest calls — leftovers included, by id — plus a failed line for
              an input id whose compile was refused; a retry's line replaces the earlier one)
```

## Main-session state (generation run)

| Item | Source | Use |
|---|---|---|
| recommendation, batches | analyst lines | Step 4 presentation; batch plan |
| created ids + titles | generator lines | 8a critic list; report |
| per-test verdict | `ingest-verdict --all --ids` `results` | route: kept / repair / drop / unverified |
| kept-grounded | count of `grounded` results | the written==0 guard |
| critic line | critic | progress only; disagreement with `results` → unverified |
| unverified | no `results` entry after one critic retry, or a disagreement | kept, no grounding block, never regenerated / deleted; Step 9 "Unverified (critic could not judge)" |
| repair outcome per id | repairer lines | re-critic set (`written` / `unchanged`), unverified (`failed`) |
| dropped | record-drop + delete (main) | report |
