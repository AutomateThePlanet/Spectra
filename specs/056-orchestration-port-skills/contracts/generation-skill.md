# Contract: Generation Skill (loop choreography + mandatory critic)

**Surface**: the ported generation skill (`.claude/skills/spectra-generation/SKILL.md`, from
`spectra-generation.agent.md` + the `spectra-generate` body).

## Behavior

| # | Given | When | Then |
|---|---|---|---|
| 1 | the generation skill | a test is generated | the procedure invokes the `spectra-critic` subagent as a **mandatory explicit step before persistence** — no skip/auto-invoke/"if enabled" branch in the mandated flow |
| 2 | the generation skill | the CLI returns a fail-loud validation error | the procedure regenerates addressing the **specific** error, bounded by the existing retry limit |
| 3 | the generation skill | the CLI returns a fail-loud critic-damage error (ingest exit 6) | the procedure regenerates addressing the **specific** error, bounded by the existing retry limit |
| 4 | repeated fail-loud errors | the retry limit is reached | the procedure **stops** and surfaces the failure — it does not loop unbounded and does not persist an unverified/invalid test |
| 5 | the generation skill | inspect its `tools` | includes `Task` (to invoke the critic subagent) plus the read/run set |

## Invariants

- The skill choreographs the **existing** model-free CLI verbs and their exit-code contract
  (compile refuse → 4; ingest empty → 5; ingest parse/damage → 6); it introduces no new CLI behavior
  and no new retry-limit value.
- The mandatory-critic step appears as a required, numbered stage in the procedure — its presence and
  non-skippability are statically inspectable in the skill text (the basis for the FR-004 test).
- The analyze → approve → generate discipline and the intent-routing (`--focus` vs
  `--from-description` vs `--from-suggestions`) carry over; only the Copilot terminal/confirmation
  verbs are translated (D5).
