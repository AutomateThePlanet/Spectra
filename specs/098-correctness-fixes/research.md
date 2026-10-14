# Research: Correctness Fixes (Phase 6, Part 3)

All findings re-verified on `986da01`.

## R1 — Where the legacy/invalid critic model is decided (F1)

**Decision**: the typed model decides, not the loader. `CriticConfig` gets its own JSON converter that accepts any token:
- an object → the string `model` (when it is a string) plus the names of every other key it carries;
- a non-string `model` (number, object, …) or a non-object `ai.critic` → recorded as invalid, never an exception.

`AiConfig.CriticModel` returns `(Model, Warning)`:
- no block → `sonnet`, no warning;
- a retired key present (`provider`, `api_key_env`, `base_url`, `enabled`, `timeout_seconds`, `max_concurrent`,
  case-insensitive) → `sonnet`; warning only when the block has a `model`
  ("ai.critic.model from the old in-process critic is ignored; set it again (alias, model id or inherit) to choose the
  critic subagent's model");
- `model` invalid (097 pattern fails, not a string, block not an object) → `sonnet` + warning naming the value;
- otherwise the value.

`EffectiveCriticModel` = that model. `ConfigLoader.Validate` drops `INVALID_CRITIC_MODEL`.

**Rationale**: several commands deserialize `SpectraConfig` directly (validate, audit-grounding, config, …), not
through `ConfigLoader`. A converter makes every path safe — no path can fail on `ai.critic` — and keeps the rule in one
place. A loader-only fix would leave a numeric `model` crashing the direct deserializers.

**Alternatives**: strip the node in `ConfigLoader.LoadEntryAsync` (misses direct deserializers); keep `CONFIG_INVALID`
for non-legacy invalid values (the request rules it out: no command fails because of this key).

**Where the warning shows** (spec Decision): `update-skills` (one warning line, with the model installed) and
`spectra validate` (the retired-key notice, now listing `enabled` / `timeout_seconds` / `max_concurrent` and the extra
sentence). The notice list (`DetectDeprecatedKeys`) gains the three keys.

**Verified templates**:
- v1.45 (`0e84a69`): `{"enabled": true, "provider": "github-models", "model": "gpt-5-mini"}` plus `ai.fallback_strategy`;
- 058–068 (`de159cb`): `{"enabled": true, "model": "claude-sonnet-4-6", "max_concurrent": 1, "_comment_critic": …}` →
  legacy too (`enabled`, `max_concurrent`); the model was a Sonnet anyway, so `sonnet` changes nothing;
- 069+: no `ai.critic`; 097: `{"model": "sonnet"}`.

## R2 — Audit action rule (F2)

**Decision**: for an entry with a verdict file:
- stale → `critic`;
- block written → `review` (flagged) / `none`;
- no block → by verdict: `grounded` → `ground`, `partial` → `repair`, `hallucinated` → `review`, anything else →
  `critic`.

Summary: `partial_pending_repair` = `repair` entries (unchanged name), new `pending_ground`, `pending_review`.
`--action` gains `ground`.

**Rationale**: the verdict says what's missing: a grounded test only needs its block; a hallucinated test waits for
the user (the update flow keeps it, 090; the generate flow drops it only when the critic agrees, 093).

**Generate resume**: `audit-grounding --suite S --action ground,repair,review --output-format json` → `ground` ids →
`ingest-grounding --suite S --all --ids <ids>`; `repair` ids → `spectra-repairer` then re-critic as in 8b; `review` ids
→ reported ("waiting for your review"), never repaired.

## R3 — Suite delete reporting (F3)

**Decision**: `Leftovers` no longer adds `plan.VerdictFiles`; `SuiteDeleteResult` gains `verdict_files_removed`
(dry run: the plan's list; real run: `DeletionOutcome.VerdictFilesRemoved`), relative paths, same as `DeleteResult`. The
human summary prints `removed verdict files: …` once; the message counts leftovers without them.

## R4 — Manifest "newest" (F4)

**Decision**: `SkillsManifest.Normalize` ranks each original key form: 3 = absolute under the current root, 2 =
absolute re-rooted from elsewhere, 1 = relative. `Files[key]` = the hash of the highest-ranked record. If two records
share the highest rank with different hashes, the key is *ambiguous*: `SaveAsync` writes every candidate of ambiguous
keys under an additive `candidates` object (`{key: [hashes]}`), and `LoadAsync` merges it back into `Candidates`.
`Record` clears a key's ambiguity. A manifest with no ambiguous key is saved exactly as in 097.

**Rationale**: pre-096 update-skills (absolute keys) always ran after init (relative keys), so absolute is newer. Two
absolute keys under different roots: the one under the current root is the newer location.

## R5 — Keep-baseline (F5)

**Decision**: `RecordPersistedAsync` in `KeepBaseline` mode skips a test without an entry and counts it
(`BaselineWriteSummary.LeftUnbaselined`, JSON `left_unbaselined` only when > 0; human line "left unbaselined N").
`Complete` keeps its create branch for `source-baseline --init`.

## R6 — Heading ids, images, snapshots (F6)

- **Id attribute**: `(?<!\S)id\s*=\s*("…"|'…')` (start of text or whitespace before `id`), case-insensitive, in
  `SectionExtractor.IdAttributeRegex` and `SectionRef.ExplicitIdRegex`. `<a id="x">` (space before), `id="x"` at the
  start, `ID = 'x'` match; `data-id`, `aria-id`, `x-id`, `:id` don't.
- **Image alt text — reproduces**: `## ![logo](x.png) Setup` has `CleanText` "logo Setup", key `logosetup`; `#setup`
  → `AnchorNotFound`. GitHub builds the anchor from rendered text; an `<img>` renders no text, so GitHub's anchor is
  `#-setup` (key `setup`). **Decision**: `MarkdownHeading.ImageFreeKey` = the key of the text with inline/reference
  images removed before cleaning; rule 1 (text) matches a heading whose `TextKey` **or** non-empty `ImageFreeKey`
  equals the anchor key, so 085 refs (`#logo-setup`) keep matching. Ambiguity counting covers both keys. The canonical
  ref (094) is unchanged (`TextKey` slug resolves back).
- **`{#id}` snapshot — reproduces only in one order**: an 084 snapshot is `TextNormalizer.Normalize(raw)` — the heading's
  `{#setup}` kept, whitespace collapsed (no line breaks). Since 085 the current text is `ComparisonText(raw)` (block
  stripped). Snapshot taken *without* the id, id added later → equal → COSMETIC (no bug). Snapshot taken *with* the id,
  id later removed/changed → `"## setup {#setup} …"` vs `"## setup …"` → not equal → LIGHT. **Decision**:
  `SectionExtractor.SnapshotComparisonText(snapshot)` removes an attribute block that follows a heading marker's text
  (`(^| )#{1,6} ` + text without `{` + block + space/end); `ChangeClassifier` treats the section as COSMETIC when the
  snapshot **or** its stripped form equals the current text. The word diff keeps using the stored snapshot (a real edit
  is still a change; only the equality test widens), so a 085+ snapshot can never become *more* cosmetic than the
  equality allows, and body text with `{…}` is never touched by the diff.

## R7 — Per-test criterion state (F7)

**Decision**: `TestBlock` gains `CriterionStates` (parallel to `CriterionIds`). `UpdatePromptRenderer.Tests` annotates an
id with its state (`changed` / `unchanged` / `not baselined` / `removed`) only when the tests of the context that link it
have more than one distinct state. The shared list (`Criteria`) is unchanged (strongest). Same inputs → same output; a
single test or an agreeing group → 097 bytes.

## R8 — PLAN_STALE vs count_fallback (F8)

**Decision**: `CompileBatchPrompt` decides the template mode right after the batch lookup. Batch mode: staleness check
→ `PLAN_STALE` as today. `count_fallback`: the same check, but each stale line becomes part of one warning
"the documentation changed since the analysis; behaviour titles may be outdated (…)", then count mode. `unsourced`:
unchanged (returns before either).

## R9 — Coverage threshold (F9)

**Decision**:
- `GenerationConfig.CoverageThreshold` (`coverage_threshold`, `double?`); `ConfigLoader.Validate`: outside [0, 1] →
  `INVALID_COVERAGE_THRESHOLD` "generation.coverage_threshold must be a number from 0 to 1" → `CONFIG_INVALID`; a
  non-number already fails deserialization → `CONFIG_INVALID` naming the JSON path.
- `duplicate_threshold` becomes `double?` underneath (`DuplicateThresholdSetting`), with `DuplicateThreshold` =
  setting ?? 0.6, so "set" is known.
- `GenerationConfig.CoverageThresholdChoice()` → `(Value, Notice)`: coverage set → it; else duplicate set and ≠ 0.6 →
  it + notice "generation.coverage_threshold is not set; using generation.duplicate_threshold (X) for coverage as
  before — set generation.coverage_threshold (this fallback goes in the next release)"; else 0.6.
- `ingest-analysis` uses the choice (notice on stderr, both modes); 095's out-of-range warning names the key the value
  came from.
- Init template: `"coverage_threshold": 0.6`. Plan: writes `coverage_threshold`; reads `coverage_threshold` or a 095
  plan's `duplicate_threshold` (`CoverageKnown` either way).

**Rationale for "≠ 0.6"** (spec Decision): every init-created workspace has `duplicate_threshold: 0.6` from the
template, equal to the coverage default; the fallback would change nothing and a notice there would be noise (083
lesson: check the rule against the real template).

## R10 — Verification workspaces (FR-023)

- Real init: `InitHandler` into a temp folder (existing `BaselineTestWorkspace` / `InitTemplateWorkspaceTests` pattern).
- Pre-098: the init template with `coverage_threshold` removed and `ai.critic` replaced by the v1.45 block / the 058
  block / `openai/gpt-4.1`; a pre-096 skills manifest (relative + absolute keys); an 084 snapshot with `{#id}`; a 095
  plan with `duplicate_threshold`.

Baseline `986da01`: Core 1230 / CLI 2258 / Execution 228 / Integration 8, 0 failures (097 numbers).
