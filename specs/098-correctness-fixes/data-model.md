# Data Model: Correctness Fixes (Phase 6, Part 3)

## Config

### `ai.critic` (F1)

| Shape | Effective model | Warning (update-skills) | Retired-key notice (validate) |
|---|---|---|---|
| absent / `null` | `sonnet` | — | — |
| `{"model": "opus"}` | `opus` | — | — |
| any retired key + `model` | `sonnet` | "ai.critic.model from the old in-process critic is ignored; …" | retired keys + same sentence |
| any retired key, no `model` | `sonnet` | — | retired keys |
| `model` fails the 097 pattern / not a string | `sonnet` | `ai.critic.model '<v>' is not valid …; using sonnet` | — |
| `ai.critic` not an object | `sonnet` | `ai.critic must be an object …; using sonnet` | — |

Retired keys (case-insensitive): `provider`, `api_key_env`, `base_url`, `enabled`, `timeout_seconds`, `max_concurrent`.
Never `CONFIG_INVALID`.

### `generation` (F9)

| Key | Type | Default | Rule |
|---|---|---|---|
| `coverage_threshold` | number 0–1 | 0.6 | missing/`null` → default; outside → `CONFIG_INVALID` (`INVALID_COVERAGE_THRESHOLD`) |
| `duplicate_threshold` | number | 0.6 | duplicate detection; coverage fallback when `coverage_threshold` absent and value ≠ 0.6 (notice) |

## Audit entry (F2)

`action_needed` for an entry with a verdict file:

| stale | block | verdict | action |
|---|---|---|---|
| yes | any | any | `critic` |
| no | yes, flagged | any | `review` |
| no | yes | any | `none` |
| no | no | grounded | `ground` |
| no | no | partial | `repair` |
| no | no | hallucinated | `review` |
| no | no | other | `critic` |

Entries without a verdict file: unchanged (090 rule). Summary: `partial_pending_repair` (= `repair`), `pending_ground`,
`pending_review` (both new, always written).

## Suite delete result (F3)

`verdict_files_removed: string[]` (new; dry run = would remove). `leftovers` / `leftovers_removed`: staging folders and
the generation plan only.

## Skills manifest (F4)

```json
{ "version": "1.0", "files": { "<key>": "<hash>" }, "candidates": { "<key>": ["<hash>", "<hash>"] } }
```

`candidates` only for ambiguous keys (omitted otherwise). Rank of an original key: absolute under root 3, re-rooted
absolute 2, relative 1.

## Baseline write summary (F5)

`left_unbaselined: int` — keep-baseline tests without an entry; in the `baseline` JSON object only when > 0.

## Heading (F6)

`MarkdownHeading.ImageFreeKey`: anchor key of the heading text with Markdown images removed. Rule 1 matches
`TextKey` or a non-empty `ImageFreeKey`.

## Test block (F7)

`TestBlock(Test, SectionLabels, CriterionIds, CriterionStates)` — `CriterionStates[i]` is this test's state of
`CriterionIds[i]`.

## Generation plan (F9)

`coverage_threshold` (new name, written); `duplicate_threshold` (095, read only). Either makes `CoverageKnown`.
