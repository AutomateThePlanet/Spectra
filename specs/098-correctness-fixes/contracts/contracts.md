# Contracts: Correctness Fixes (Phase 6, Part 3)

## C1 — `ai.critic` (F1)

- No command fails because of `ai.critic` (any shape, any value).
- `spectra update-skills`: installs `model: <effective>`; one warning line (stderr) when the configured model was
  ignored:
  - legacy: `ai.critic.model from the old in-process critic is ignored; set it again (alias, model id or inherit) to choose the critic subagent's model. Installed: sonnet`
  - invalid: `ai.critic.model <value> is not valid (one word: an alias such as sonnet, a model id, or inherit); using sonnet`
- `spectra validate`: notice `spectra.config.json contains retired key(s): <list>. These are ignored (the critic runs as
  the spectra-critic subagent).` + for a legacy block with `model`: ` ai.critic.model from the old in-process critic is
  ignored; set it again (alias, model id or inherit) to choose the critic subagent's model.`

## C2 — `audit-grounding` (F2)

```json
{"command":"audit-grounding","status":"success","suite":"s",
 "tests":[{"id":"TC-1","verdict":"grounded","score":0.9,"grounding_written":false,"flagged_for_review":false,"action_needed":"ground","file":"…"}],
 "summary":{"total":3,"grounding_written":0,"partial_pending_repair":1,"pending_ground":1,"pending_review":1,"flagged_for_review":0,"unverified":0,"stale":0}}
```

`--action` values: `critic`, `ground`, `repair`, `review`, `none`.

## C3 — `suite delete` (F3)

```json
{"command":"suite delete","status":"completed","suite":"s","tests_removed":3,
 "verdict_files_removed":[".spectra/verdicts/critic-verdict-TC-1.json", "…"],
 "leftovers_removed":[".spectra/updates/s/", ".spectra/generation-plan/s.json"], "…": "…"}
```

Human: `removed verdict files: a, b, c` once; message `(… N leftover(s) removed)` counts folders and the plan only.

## C4 — ingest `baseline` object (F5)

`{"created":0,"updated":0,"unchanged":1,"snapshots_written":0,"mode":"keep","left_unbaselined":1}` (`left_unbaselined`
only when > 0).

## C5 — group update prompt (F7)

```text
### TEST TC-201
Sections: S1
Criteria: AC-PAY-004 (unchanged)
### TEST TC-202
Sections: S1
Criteria: AC-PAY-004 (changed)
```

Annotation only on ids whose state differs across the group's tests. States: `changed`, `unchanged`, `not baselined`,
`removed`.

## C6 — `compile-prompt --batch` (F8)

`count_fallback` + stale section → exit 0, prompt on stdout, stderr warning
`the documentation changed since the analysis; behaviour titles may be outdated (<ref> changed since the analysis; …)`.
Batch mode → `PLAN_STALE` exit 4 (unchanged).

## C7 — coverage threshold (F9)

`ingest-analysis` stderr notice (fallback only):
`generation.coverage_threshold is not set; using generation.duplicate_threshold (0.75) for coverage as before. Set generation.coverage_threshold — this fallback will be removed in the next release.`
