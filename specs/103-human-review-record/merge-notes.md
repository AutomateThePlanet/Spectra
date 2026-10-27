# Merge notes: 078-human-review-record → 103

This spec was written and implemented as `078-human-review-record` (Oleg Kuzmanov, 2026-09-28) on `main` at
`a86f4d2`. The integration branch had already used 078 (lossless test writes) and gained specs 079–102, so it was
merged as 103: the folder and every "Spec 078" reference it added were renumbered. The documents in this folder are
otherwise as written; where the merged behaviour differs, this file wins.

## Differences from the documents here

| Topic | As specified | As merged | Why |
|---|---|---|---|
| Decision 7: repair keeps the block | `ingest-update` keeps the grounding block and clears the marker | Spec 090 drops the critic block on any edit; `feedback --fix` stages the review in `.spectra/reviews/<suite>/<id>.json` and the chain's final `ingest-grounding` writes it to the new block | Without this the review was lost at the chain's `ingest-update` |
| `--re-verify` | Rewrites every grounded test, with `--all --repaired` | Rewrites a grounded test only from a verdict file newer than its `verified_at`; writes a re-verified `partial` without `--repaired` | Re-ingesting an old verdict moved `verified_at` past the review and spent its suppression; `--all --repaired` needs `--ids` (091) and would mark every test repaired |
| `audit-grounding` `action_needed` | New value `reviewed` | Unchanged; review fields and counts are added instead | `--action` and the skills rely on `none` |
| `feedback --fix` on a `grounded` test | `compile-repair-prompt` refuses (no findings, grounded verdict) | Reviewer feedback alone is something to repair | It is the case the feature exists for |
| `feedback --all --fix` | Accepted; flags every test, prints no chain | Refused (`BULK_FIX_REFUSED`) | One repair per test |
| File output | String-built YAML with `EscapeYamlString` | The lossless emitter (078); timestamps in the `verified_at` format; names unquoted when safe | The writer was replaced by 078 |
| Test lookup | `<id>.md` | Suite index, then `<id>.md` | Matches the other handlers |
| JSON shape | New fields always written | Batch review counts only with `--re-verify` or a review involved; audit counts omitted at 0 | Default output stays byte-identical (099 golden) |
| SDK | `global.json` 10.0.101, CI 8.0.x + 10.0.x | Not merged | Unrelated to the feature; a separate change |
