# Research: Section-Scoped Generation Batches (Phase 5c)

All facts verified on `55b541f`. No open questions remain. Q1 of the spec was resolved with the recommended option C.

## R1 — How a behaviour's source becomes a section

**Decision:** resolve each behaviour's `source` with `BaselineContext.GetSectionAsync`, the same `SourceRefResolver` +
`SectionExtractor.Extract` path the baseline and triage use (084/085). The reasons map 1:1 to the plan's `reason`:
- `empty ref` (source empty or `#x` only);
- `not found`;
- `outside allowed roots`;
- `unreadable`;
- `anchor not found`.

An ambiguous anchor resolves to the first match, like test refs, and adds one plan note.

**Canonical ref:** the plan stores a canonical ref per resolved section. A whole document is its repo-relative path
(`/` separators). A section is `path#anchor`, where the anchor is:
- the heading's first explicit id when it has one;
- otherwise a GitHub-style slug of `CleanText`: lowercase; spaces → `-`; only letters, digits, `-` and `_` kept;
- for the n-th duplicate (n ≥ 1), suffixed `-n`.

A new pure helper `SectionRef.Canonical(relativePath, headings, index)` sits next to `SectionExtractor` in Core. It is
verified to round-trip: `SectionExtractor.Match(headings, anchor)` returns the same index. When it would not (a heading
with no letters or digits), the source as given is kept. Behaviours are grouped by (path, heading index), never by
spelling, so `#Upload Quota` and `#upload-quota` are one section.

**Rationale:** one resolver for refs keeps the plan, `source_refs`, triage and the baseline in agreement. Canonical refs
are what the tests store, so the 084 baseline records them as usual.

**Alternatives:**
- Store the source as written: different spellings would form different sections, and tests would carry odd refs.
- AnchorKey (`uploadquota`): it matches, but it is not a conventional anchor.

## R2 — Where the code lives

**Decision:**
- Core `Spectra.Core.Baseline.SectionRef` (pure canonicalisation).
- CLI `Spectra.CLI.Generation.Plan/`:
  - `GenerationPlan` (record + strict serializer, `Utf8JsonWriter`, fixed key order, LF, no timestamps);
  - `GenerationPlanBuilder` (pure: resolved behaviours + batch size + recommended → batches and counts);
  - `GenerationPlanStore` (path, atomic save via `BaselineStore.WriteAtomicAsync`, strict load);
  - `BatchPromptCompiler` (sections + criteria + template → prompt);
  - `BatchCriteriaSelector` (R6).
- `IngestAnalysisCommand` and `CompilePromptCommand` call them.

**Rationale:** the builder and compiler are pure and table-testable. The I/O stays at the command edge, as in 084/088.

## R3 — Count rule (FR-005)

**Decision:**
- `n` = batches (including `unsourced`); `b_i` = behaviours in batch i; `B` = Σ b_i; `R` = `recommended`.
- If `R ≥ n`:
  - `count_i = 1 + share_i`, where the shares split `R − n` in proportion to `b_i` by largest remainder (ties to the
    earlier batch), so Σ count_i = R;
  - `count_rule: "proportional"`.
- Else every `count_i = 1`, `planned_total = n`, `count_rule: "one_per_batch"`. The human summary says "counts raised
  to one per batch (recommended R < n batches)".
- `B = 0` (the focus filter left nothing): no batches, `planned_total 0`.

**Rationale:** no batch is ever 0, so the 093 drop cannot happen. Sums are exact whenever they can be, and otherwise the
difference is stated. "Already covered" is an aggregate, so coverage only shrinks counts; it never removes behaviours.

**Alternatives:**
- Plain proportional split with 0s dropped: this is the 093 finding.
- Count = behaviours per batch: ignores coverage.

## R4 — Batch building (FR-004)

**Decision:**
- Resolved behaviours are ordered by document (ordinal path), then by section position: a whole-document ref first
  (−1), then the heading index; then input order.
- Walk that order and open a new batch when:
  - the document changes;
  - the current batch has `batch_size` behaviours;
  - or adding the next section's behaviours would overflow it and the batch already has at least one.
- A section with more than `batch_size` behaviours fills consecutive batches by itself.
- Ids are `1…n`. `unsourced` comes last, in input order.
- A batch's `source_refs` are its distinct canonical refs in that order.

**Rationale:** few generator subagents and small prompts. A batch never spans two documents (as the request asks).
Sections are kept whole unless they alone overflow.

**Alternatives:** one section per batch — many one-behaviour subagents, each with the fixed template overhead.

## R5 — Staleness (FR-010)

**Decision:**
- The plan maps every canonical section ref to `SourceFingerprint.OfSection(raw)` at ingest time.
- `compile-prompt --batch` recomputes that for the batch's refs only, via `GetSectionAsync`.
- A ref that no longer resolves, or whose fingerprint differs, gives `PLAN_STALE`. `errors` lists every such ref, e.g.
  `docs/storage.md#upload-quota changed since the analysis` or `… no longer resolves: anchor not found`. The message says
  to re-run the analysis.
- Other batches stay compilable.

**Rationale:** a batch is only as valid as its own sections, and this check costs one read per document.

## R6 — Criteria for a batch (FR-009; Q1 = C)

**Decision:**
- Scan the criteria files once (`CriteriaStore.ScanFilesAsync` over `coverage.criteria_dir`, default `docs/criteria`).
  When an id occurs twice, the first wins (as `BaselineContext`).
- For each criterion, normalise `source_doc` with `CriteriaPaths.NormalizeSourceDoc` and compare it with the batch's
  documents (`CriteriaProvenance.SameDocument`).
- With an empty `source_section`, the criterion is document-level: included when the batch has that document's
  whole-document ref.
- Otherwise `SectionExtractor.Match(headings, source_section)`:
  - matched → included when the heading index is covered by one of the batch's refs of that document
    (`CoveredHeadings`, or a whole-document ref);
  - unmatched → treated as document-level, plus one warning `criterion <id>: section '<s>' not found in <doc>; treated as
    document-level`.
- If nothing is selected, fall back to all criteria of the batch's documents, with the warning `no criteria for the
  batch's sections; using all criteria of <docs>`.
- If that is empty too, refuse with `MISSING_INPUT` (`missing_input: "criteria_context"`, the existing message).
- Sort by id (ordinal). Render with the existing `CriteriaContextLoader` line format, extracted to
  `CriteriaContextLoader.Format`, so the output is byte-identical for count mode.

**Rationale:** the batch keeps the "map at least one criterion" rule without blocking sections that have no criteria.
Count mode's suite-level refusal is unchanged.

## R7 — Template (FR-009, FR-012)

**Decision:** the engine supports only `{{#if}}` (no else), so there are two new placeholders, declared in the
frontmatter:
- `source_sections` — the fenced sections; batch mode only.
- `read_docs` — `yes` in count mode, empty in batch mode.

The shipped `test-generation.md`:
- drops the CRITICAL RULES item 2/3 tool text, the WORKFLOW list, "After using tools to gather information", and
  IMPORTANT items 1 and 3;
- `{{#if read_docs}}` says: read the relevant documentation with your file tools before writing; test ids are assigned
  when the tests are ingested — do not invent them;
- `{{#if source_sections}}` holds the SOURCE SECTIONS block: the instruction that these sections are the complete
  source for this batch (do not read other files), the per-test `source_refs` rule, then `{{source_sections}}`.

The built-in fallback in `PromptCompiler.Assemble` gets the count-mode text (it never renders batch mode).

**Pre-094 copies (decision 2026-10-12, analyze H1):** a template predates batch prompts when it is user-loaded
(`IsUserCustomized`) and its body lacks `{{source_sections}}`. Then `--batch` never refuses (see R16). Count mode
renders a user template as before; its text is the user's.

**Rationale:** no engine change, and one placeholder decides whether a copy predates 094, as 088 does — but `spectra
init` copies every template into `.spectra/prompts/`, so a refusal would block every pre-094 workspace (the 083
lesson).

## R8 — Section rendering (FR-009, FR-013)

**Decision:** for each of the batch's refs in order, skip a ref absorbed by another ref of the same batch (a
whole-document ref absorbs all its sections; a section absorbs the sections inside it per `CoveredHeadings`). Otherwise
emit `### <canonical ref>` followed by the raw section text in a `~~~~markdown` fence. The fence is longer than any tilde
run, LF only, and comes from the 088 fence helper (`UpdatePromptRenderer.AppendFenced`, made `internal static` and
shared). The behaviours are rendered into `{{behaviors}}` as one line each:
`- B3 [boundary · BVA] Upload is refused above the 30 GB quota (docs/storage.md#upload-quota)`.

## R9 — Config (FR-006)

**Decision:**
- `GenerationConfig.BatchSize` (`batch_size`, `int?`) with `EffectiveBatchSize => BatchSize ?? 8`.
- `ConfigLoader` validation: `INVALID_GENERATION_BATCH_SIZE` "generation.batch_size must be an integer from 1 to 50"
  when the value is outside 1–50.
- `("generation", ConfigSections.None)` joins `CheckedSections`, so `"generation": null` gets defaults (the 084 `update`
  precedent).
- `ingest-analysis` moves from its hand-rolled `JsonSerializer` load to `ConfigLoader.LoadForWriteAsync(root,
  ConfigSections.Source, ct)` — it now writes the plan. A missing config is still exit 1, now with the loader's message
  and `CONFIG_MISSING`; an out-of-range value gives `CONFIG_INVALID`.
- `compile-prompt --batch` uses `ConfigLoader.LoadForReadAsync(root, ConfigSections.Source, ct)`.
- Count mode keeps its loader (no behaviour change for old callers).
- The init template is not changed: the key is optional and the default applies.

**Rationale:** the 083 rule. The range is checked wherever the checked loader is used.

## R10 — Gitignore (FR-008)

**Decision:** add `.spectra/generation-plan/` to `SpectraGitIgnore.Patterns`.

`Pre090Offered` is today "Patterns minus the two staging folders", which would wrongly mark the new entry as already
offered in pre-090 workspaces. It becomes the 089 list explicitly: Patterns minus updates, repairs **and**
generation-plan.

A 090–093 workspace has a recorded offered list without the entry, so `update-skills` offers it once. `init` records the
full list.

## R11 — The analysis ingest result (FR-007)

**Decision:**
- On success the ingest writes the plan, then adds to its JSON:
  - `plan` (repo-relative path);
  - `planned_total`, `count_rule`;
  - `batches` (`id`, `count`, `source_refs`, `behaviors` = titles);
  - `unresolved` (`id`, `title`, `source`, `reason`);
  - `notes` (ambiguous anchors).
- The human summary adds one line per batch, `batch <id> | count <k> | <refs, comma-separated> | <title>; <title>; …`,
  and one line per unresolved behaviour, `unresolved: <id> <title> — <reason>`. In JSON mode the summary goes to stderr
  (the 091 rule).
- On a failed analysis (empty, parse failure) nothing is written and an earlier plan stays.
- A failed plan write is exit 1 with `PLAN_WRITE_FAILED` (shared failure shape).

**Rationale:** the analyst copies the CLI's lines verbatim (FR-014), so the line format is defined once, in the CLI.

## R12 — Refusal plumbing

**Decision:** batch-mode refusals go through `BaselineCommandIO.Fail` (shared failure shape on stderr, exit by
`Refusals`). `PLAN_MISSING`, `BATCH_NOT_FOUND`, `PLAN_STALE` and `MISSING_INPUT` are added to the refusal set (exit 4).
`PROMPT_TOO_LARGE` is already in it. `--batch` has no `TEMPLATE_OUTDATED` refusal (R16). `MISSING_INPUT` is used through
`BaselineCommandIO.Fail` by no other command (`compile-extraction-prompt` writes it through `CriteriaCommandIO`), so the
set change affects no existing caller.

Count-mode refusals keep their pre-094 shape (`{refused, missing_input, message}` on stderr) — unchanged for old callers.
The generator reads `error_code` or else `missing_input`.

## R13 — Agents and skill (FR-014–FR-017)

**Decision:**
- **Analyst:** step 4 "Plan the batches" is replaced by "Copy the CLI's batch lines" (from the ingest's stderr summary)
  and the `unresolved:` lines. The final message lists those lines instead of `batch <n> | count <k> | focus …`.
- **Generator:**
  - input `suite`, `batch`, optional `count`;
  - step 1 is `spectra ai compile-prompt --suite <suite> --batch <batch> [--count <count>] --output-format json`;
  - for a numbered batch it never Reads a document (Read is only for command output the harness saved);
  - for `unsourced`, and for a batch the compile reports as `count_fallback`, it reads the docs as before and relays
    the compile's warning as a `warning:` line;
  - refusals end with `failed <code>: <first error>`.
- **Skill:**
  - Step 4 shows the batch lines and the unresolved lines;
  - Steps 5–7 pass `batch: <id>`, and `count: <k>` only for a user's explicit total, split as in 093;
  - after the loop, `PLAN_STALE` / `PROMPT_TOO_LARGE` lines are reported, with one offer to re-run the analysis (invoke
    the analyst again);
  - Step 9 names batches left without tests by a small user total.

The 093 text (critic, routing, repairer, NON-STOP, the written==0 guard) is untouched.

**Retargeted 093 rows (deliberate):** `GenerateAgentTests` analyst batch-line row and generator compile-line row;
`GenerateOrchestratorTests` focus row.

## R14 — Determinism (FR-013, FR-003)

**Decision:** the plan and the prompts have no timestamps, use ordinal sorts and LF. The criteria come from a
deterministic scan, sorted by id. Profile format and Testimize come from the existing code paths (Testimize is `null` in
compile-prompt today, unchanged). Tests compile twice and compare bytes.

## R15 — Baseline and acceptance

**Decision:** test baseline at `55b541f` is Core 1219 / CLI 2092 (one known failure, `ProfileFormatLoaderTests`) /
Execution 228 / Integration 8, with 4 known warnings. Every new test row is proven by running it on the untouched `src`
(`git diff --quiet 55b541f -- src`).

The SC-003 baseline (093 count mode, 2 runs) is measured by the user on `55b541f` before the implementation lands. The
acceptance script builds the same workspace for both builds.

## R16 — Pre-094 generation templates (FR-010a; analyze H1)

**Decision:** when `.spectra/prompts/test-generation.md` predates batch prompts (R7), `compile-prompt --batch` decides
per call:
- **(a) unedited copy:** `FileHasher.ComputeFileHashAsync(file)` equals the hash the skills manifest records for it →
  compile with the bundled 094 template (`BuiltInTemplates.GetTemplate("test-generation")`) in `batch` mode, with the
  warning "using the bundled test-generation template; run spectra update-skills to refresh your copy".
- **(b) otherwise** (hash differs, or no record, e.g. a fresh clone where `.spectra/skills-manifest.json` is
  gitignored) → compile in count mode with the user's template (`read_docs` = `yes`), focus = the batch's behaviour
  titles and section refs (`<title> (<ref>); …`, then `--focus` when given), count = the batch's count or `--count`;
  mode `count_fallback`, with the warning "custom test-generation template predates batch prompts — this batch is
  compiled in count mode (the generator reads the docs); run spectra prompts reset test-generation to use section
  batches". The count-mode criteria rule (suite match, refusal when none) applies; `PLAN_STALE` is still checked first,
  because the focus names the plan's sections.

**Manifest key forms:** `spectra init` records prompt templates under the relative key `.spectra/prompts/<id>.md` (built
with `Path.Combine`, so `\` on Windows), while `update-skills` records them under the absolute path. The lookup accepts
either: the absolute path, the relative path with the platform separator, and the relative path with `/`. The first
present record is used.

**Residual (analyze L2):** `FileHasher` hashes the raw text (no newline normalisation) and the manifest is gitignored,
so an unedited copy checked out with CRLF, or in a teammate's clone, counts as customised and gets `count_fallback` —
the safe direction (the user's text is kept). The no-record warning therefore also names `update-skills`: "custom
test-generation template predates batch prompts — this batch is compiled in count mode (the generator reads the docs);
run spectra prompts reset test-generation (or spectra update-skills if you never edited it) to use section batches".

**Rationale:** never refuse a batch over a template the user did not choose to keep; an unedited copy gets exactly what
`update-skills` would install; a customised copy keeps the user's wording at the cost of the token saving for that
batch.

**Observed, out of scope:** because of the two key forms, the first `update-skills` after `init` finds no record for a
prompt template and overwrites it when it differs from the bundled one — including a user edit. This is a pre-existing
defect in `UpdateSkillsHandler.ProcessFileAsync`; flagged as a separate task, not changed by 094.

## R17 — Reporting the compile mode

**Decision:** stdout stays the prompt, unchanged for every caller. In batch mode, stderr carries the mode and the
warnings:
- JSON mode: one line, `{"command":"compile-prompt","success":true,"mode":"batch|count_fallback|count","batch":"<id>","warnings":[…]}`;
- human mode: `Mode: <mode>` then one `Warning: <text>` line per warning.

Count mode without `--batch` prints nothing new. The generator compiles with `--output-format json` and reads `mode` from
that stderr line.

**Rationale:** a JSON wrapper on stdout would break every existing caller that pipes the prompt; the 091 rule already
puts summaries on stderr in JSON mode.

## R18 — Re-run after `PLAN_STALE`, 0-count batches, `|` in titles (analyze M2, M3, L5)

**Decision:**
- After the user accepts a re-run, the skill invokes the analyst, then returns to Step 4: it presents the new
  recommendation, names the batches that already ran in this session with their created test ids, suggests narrowing
  the focus to the stale sections, and STOPs. Nothing is regenerated automatically.
- A batch whose count is 0 after splitting the user's explicit total is never sent to the generator; Step 9 lists it as
  "not generated (count 0 after splitting your total)".
- The human `batch …` line shows `|` inside a title as `/`; the JSON keeps the title as given.

## R19 — Constitution IV (`--dry-run`; analyze L4)

**Decision:** recorded in plan.md Complexity Tracking: `ingest-analysis` writes the generation plan and has no
`--dry-run`. The plan is scratch, overwritten per analysis and never committed. This is deferred with the other
`--dry-run` gaps to Phase 6.
