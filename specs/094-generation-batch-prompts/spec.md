# Feature Specification: Section-Scoped Generation Batches (Phase 5c)

**Feature Branch**: `094-generation-batch-prompts`
**Created**: 2026-10-12
**Status**: Draft
**Input**: User description: "094-generation-batch-prompts: Phase 5c of the doc-change → criteria → test workflow.
Generation works per batch of behaviours from the same doc section. The analysis records which section each behaviour
comes from; the CLI saves a generation plan; `compile-prompt --batch` inlines exactly those sections and their criteria,
so the generator subagent no longer browses the docs. Adapted from the token-economy package's
04-section-scoped-prompts-and-batches.md part 2 to the 093 flow. Grounding quality must not drop."
(Background, FR-A–FR-C, constraints, acceptance and scope as quoted in the request.)

## Context

Since 093 the generation skill orchestrates subagents: the analyst analyses and plans batches, a generator subagent
generates one batch. But the generator still receives only a suite, a free-text focus and a count. It compiles a
generation prompt that tells it to read the documentation "on demand", so every batch re-reads whole documents in its
own context, and the prompt names four tools (list documents, read the test index, check duplicates, get next ids) that
have not existed since spec 069. The batch plan itself is free text invented by the analyst; the CLI never sees it.

This phase lets the CLI own the plan: the analysis names the section each behaviour comes from, the analysis ingest
resolves those sections and saves a generation plan, and the generation prompt for a batch carries exactly that
batch's behaviours, the text of its sections and their acceptance criteria. The generator reads no docs for such a
batch.

Facts (verified on `55b541f`):
- The analysis prompt asks for each behaviour's `source` as "which document it comes from" (a doc path). It inlines at
  most 2,000 characters per document but lists every document's section headings. Behaviours have no id.
- The analysis ingest returns counts only (already covered, recommended, category and technique breakdowns, boundary
  gaps) and persists nothing the generation prompt uses. `recommended` = behaviours − already covered, where "already
  covered" is an aggregate count, not a list of behaviours.
- The generation prompt compiler knows only suite, count and focus (and the from-description inputs). It refuses
  (exit 4) when no acceptance criteria resolve for the suite. Both the shipped `test-generation.md` and the built-in
  fallback carry the on-demand reading instruction and the four dead tools.
- The analyst plans batches itself (≤ 8 behaviours per source document, counts proportional to `recommended`) and drops
  a batch whose count rounds to 0 — a finding deferred from the 093 review.
- The baseline machinery (084/085) already resolves `doc#anchor` refs to sections (heading to the next heading of the
  same or a higher level; no anchor = the whole document) and fingerprints them. Criteria carry `source_doc` and
  `source_section` (the heading text of the section they were extracted from).
- Config has a `generation` section (default count, review, duplicate threshold, categories). `ai.generation_batch_size`
  appears in the init template but nothing reads it.

Rules that still apply:
- analyze → present → **STOP for approval** → generate; the critic for every generated test; routing by the CLI's
  per-test verdicts; the repairer; the NON-STOP contract and the written==0 guard text verbatim (075 gate); the 093
  context budget contract;
- no config section is ever required (083); config rules are checked against the real `spectra init` template and a
  pre-existing workspace;
- refusals use the shared failure shape and exit 4; human summaries are never suppressed (stderr in JSON mode);
- the CLI stays model-free; identical inputs give byte-identical prompts;
- every new rule has a test that fails on `55b541f`.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Each batch is generated from exactly its own sections (Priority: P1)

A QA engineer asks for tests for a documentation area and approves the proposed batches. Each generator subagent gets a
prompt that already contains the sections its behaviours come from and the acceptance criteria of those sections, so it
writes the tests without opening any documents, and every test points at the sections it was written from.

**Why this priority**: This is the token saving the phase exists for, and the precondition for keeping grounding
quality while generators stop browsing.

**Independent Test**: Run an analysis on a workspace with three indexed documents, then compile the prompt for one
batch: it contains that batch's behaviours, the text of its sections (each once), only the criteria of those sections,
the instruction that the sections are the complete source, and no reading instruction or dead tool name. Compiling it
again gives the same bytes.

**Acceptance Scenarios**:

1. **Given** an analysis whose behaviours name sections of two documents, **When** the analysis is ingested, **Then** a
   generation plan for the suite is saved, each behaviour has an id (`B1`, `B2`, … in input order) and a resolved
   section, and the batches group behaviours of the same document by section in document order.
2. **Given** a saved plan, **When** the prompt for batch 2 is compiled, **Then** it holds batch 2's behaviours (id,
   title, category, technique), each referenced section's text once, the criteria whose document and section match
   those sections, and the instruction that each test's `source_refs` are the section refs of the behaviours it covers.
3. **Given** the prompt for a batch, **When** the generator subagent runs, **Then** it compiles, generates, ingests and
   returns its lines without reading any document.
4. **Given** the same plan, documents, criteria and template, **When** the batch prompt is compiled twice, **Then** the
   two outputs are byte-identical.

---

### User Story 2 - The plan never silently loses a batch (Priority: P1)

The user approves a recommendation. Every behaviour the analysis found ends up in some batch, every batch has at least
one test to write, and the batch counts add up to the recommendation in a way the plan states.

**Why this priority**: The deferred 093 finding: a batch whose share rounded to 0 was dropped, and its behaviours were
never generated.

**Independent Test**: Ingest an analysis with more batches than the recommendation and one with fewer, and check the
counts: every batch ≥ 1; the sum equals `recommended` when that is possible; otherwise the plan reports the planned
total and why it differs.

**Acceptance Scenarios**:

1. **Given** 20 behaviours over 5 batches and `recommended` 12, **When** the plan is built, **Then** each batch has
   count ≥ 1 and the counts add up to 12.
2. **Given** 5 batches and `recommended` 3, **When** the plan is built, **Then** each batch has count 1, the planned
   total is 5, and the result says the counts were raised to one per batch.
3. **Given** behaviours whose source does not resolve (an unknown document, a missing heading, an empty source),
   **When** the plan is built, **Then** they are listed under `unresolved` with the reason and grouped into one
   `unsourced` batch, which is generated the pre-094 way (reading the docs).

---

### User Story 3 - A stale or oversized batch is refused, not guessed (Priority: P2)

Between the analysis and the generation the documentation changes, or a batch's sections are too large for one prompt.
The CLI refuses to compile that batch, says why, and the assistant offers to re-run the analysis.

**Why this priority**: A batch prompt built from changed sections would ground tests in text that no longer matches
the plan; an oversized prompt would fail inside the model.

**Independent Test**: Save a plan, edit one of a batch's sections, compile that batch: exit 4 `PLAN_STALE`. Compile a
batch of a suite with no plan: `PLAN_MISSING`; an unknown batch id: `BATCH_NOT_FOUND`; a batch whose prompt exceeds the
budget: `PROMPT_TOO_LARGE`.

**Acceptance Scenarios**:

1. **Given** a saved plan, **When** a section used by batch 1 is edited and batch 1 is compiled, **Then** the command
   exits 4 with `PLAN_STALE` naming the changed section; a batch whose sections did not change still compiles.
2. **Given** a generator gets `PLAN_STALE` or `PROMPT_TOO_LARGE`, **When** it returns, **Then** the main session reports
   it and offers to re-run the analysis; the other batches still run.
3. **Given** the user accepts the re-run, **When** the new analysis returns, **Then** the main session presents the new
   recommendation and STOPs for approval (Step 4), naming the batches that already ran in this session with their
   created test ids and suggesting a focus on the stale sections; nothing is regenerated automatically.

---

### User Story 4 - The prompts stop naming tools that do not exist (Priority: P2)

Every generation prompt — batch mode, count mode, from-description, the shipped template and the built-in fallback —
drops the dead tool workflow. Count mode says to read the documentation with the file tools, and that ids are assigned
when the tests are ingested.

**Why this priority**: A prompt that names missing tools wastes the model's turns and invites it to improvise.

**Independent Test**: Compile a count-mode prompt with the shipped template and with the fallback: neither contains
`ListDocumentationFiles`, `ReadTestIndex`, `CheckDuplicates` or `GetNextTestIds`; both say ids are assigned by the
ingest.

**Acceptance Scenarios**:

1. **Given** the shipped template, **When** a count-mode prompt is compiled, **Then** it contains none of the four tool
   names and tells the model to read the documents with its file tools.
2. **Given** no template loader (the fallback), **When** a prompt is compiled, **Then** the same holds.
3. **Given** a from-description request, **When** its prompt is compiled, **Then** its behaviour is unchanged apart from
   the removed tool text.

---

### User Story 5 - Existing workspaces keep working (Priority: P3)

A user upgrades in a workspace created before 094: an old analysis file, customised analysis and generation templates,
no `generation.batch_size`. Generation keeps working; `update-skills` offers the new gitignore entry once. `spectra init`
copies every prompt template into `.spectra/prompts/`, so every pre-094 workspace has a `test-generation.md` without the
batch placeholders: an unedited copy is refreshed by `spectra update-skills`, a customised one is reset by the user
(`spectra prompts reset test-generation`). Until then, batches still compile — never refused (H1, decision 2026-10-12).

**Why this priority**: The 083 lesson — rules checked only against defaults broke real workspaces.

**Independent Test**: In a pre-094 workspace, run the analysis with the customised analysis template (bare doc paths as
sources): the plan's batches use whole-document refs; compiling a batch with the customised generation template exits
0 in `count_fallback` mode with the reset warning; with an unedited pre-094 copy it exits 0 in `batch` mode through the
bundled template with the update-skills warning; `update-skills` offers `.spectra/generation-plan/` once and not
again.

**Acceptance Scenarios**:

1. **Given** no `generation.batch_size`, **When** the plan is built, **Then** the default 8 is used, with no warning.
2. **Given** `generation.batch_size` 0 or 51, **When** the analysis ingest (or any command using the checked config
   loader) loads the config, **Then** it fails with `CONFIG_INVALID` naming the range 1–50.
3. **Given** an analysis whose sources are bare document paths, **When** the plan is built, **Then** each such behaviour
   resolves to its whole document.
4. **Given** an unedited pre-094 `test-generation.md` (its hash equals the one the skills manifest recorded), **When** a
   batch is compiled, **Then** it compiles in `batch` mode with the bundled 094 template and the warning "using the
   bundled test-generation template; run spectra update-skills to refresh your copy"; after `update-skills`, the same
   batch compiles in `batch` mode with no warning.
5. **Given** a customised pre-094 `test-generation.md` (or no manifest record for it, e.g. a fresh clone), **When** a
   batch is compiled, **Then** it compiles in `count_fallback` mode with the user's template, the batch's behaviour
   titles and section refs as the focus and the batch's count, and the warning "custom test-generation template
   predates batch prompts — this batch is compiled in count mode (the generator reads the docs); run spectra prompts
   reset test-generation to use section batches".

---

### Edge Cases

- A behaviour's source names a section that occurs twice in the document (ambiguous anchor): resolved by the 085
  matcher's rules, like test refs; the plan records the section it chose.
- A batch references a whole document and one of its sections (or a section and its sub-section): the outer text is
  inlined once; the inner ref is not inlined again.
- One section alone has more behaviours than the batch size: its behaviours are split over consecutive batches, each
  of which inlines that section.
- One section alone is larger than the prompt budget: `PROMPT_TOO_LARGE` for its batch; the skill reports it and offers
  to re-run the analysis (for example with more specific sources).
- `recommended` is 0: the plan still lists every batch with count 1; the skill presents "everything is covered" and
  generates nothing unless the user asks for tests (as today).
- The user gives an explicit total smaller than the number of batches: the skill splits it as in 093; the generator is
  never invoked for a batch left with 0; such batches are listed in the final report as "not generated (count 0 after
  splitting your total)".
- A focus filter on the analysis: the plan holds only the behaviours that passed the filter.
- A second analysis for the same suite replaces that suite's plan; plans of other suites are untouched.
- A section in the plan is deleted or its heading renamed after the analysis: `PLAN_STALE` (it no longer resolves).
- A criterion's `source_section` matches no heading of its document: it is treated as a document-level criterion
  (included for whole-document refs of that document) and the batch compile lists it in `warnings`.
- A batch's sections have no matching criteria: the prompt falls back to all criteria of the batch's documents and
  the compile lists a warning; only when those documents have no criteria either is the batch refused, as count mode
  refuses a suite without criteria (decision 2026-10-12, option C — the user proceeded to planning without answering;
  the recommended option was applied).
- The plan file is missing or unreadable when a batch is compiled: `PLAN_MISSING` (unreadable says so in the message).
- The unsourced batch: `compile-prompt --batch unsourced` compiles a count-mode prompt (documents read with the file
  tools) whose focus lists the unsourced behaviours; only the plan and batch checks apply, then count mode's own
  refusals.
- A behaviour title contains `|`: the human `batch …` line shows it as `/`; the JSON keeps the original title.

## Requirements *(mandatory)*

### Functional Requirements

**FR-A — the analysis records sections; the CLI saves a plan**

- **FR-001**: The shipped analysis template MUST ask, for each behaviour, for `source` as the most specific ref it comes
  from, in the same `doc#anchor` form tests use in `source_refs`, or a bare document path when the behaviour spans the
  document. A user template that still asks for a document path keeps working: a bare path means the whole document.
- **FR-002**: The analysis ingest MUST give each behaviour an id `B1`, `B2`, … in input order (after the focus
  filter), and resolve each source with the same ref and section rules the baseline uses (084/085). A behaviour whose
  source is empty, names an unknown or unreadable document, lies outside the allowed roots, or names a heading the
  document does not have, is **unresolved** with that reason.
- **FR-003**: The analysis ingest MUST save a generation plan per suite at `.spectra/generation-plan/<suite>.json`,
  written atomically, replacing that suite's previous plan. It holds: the suite; `recommended`; the behaviours (id,
  title, category, technique, source as given, the resolved section ref, `resolved` true/false and the reason when
  false); the fingerprint of every resolved section; and the batches (`batch_id`, behaviour ids, section refs, count);
  plus the planned total and the count rule applied. It also records its format `version`, the `focus` filter, the
  effective `batch_size`, the `count_rule` and `notes` (ambiguous anchors), as data-model.md defines. Identical inputs
  give a byte-identical plan.
- **FR-004**: Batches MUST be built from resolved behaviours: documents in ordinal path order; within a document,
  sections in document order (a whole-document ref first); consecutive sections of one document are packed into a
  batch until it holds `generation.batch_size` behaviours; a section's behaviours are split only when they alone exceed
  the batch size; a batch never spans two documents. Batch ids are `1`, `2`, … in that order. All unresolved behaviours
  form one batch with id `unsourced`, last, in input order.
- **FR-005**: Counts MUST follow one stated rule. With `n` batches: if `recommended` ≥ `n`, each batch gets 1 plus its
  share of `recommended − n` proportional to its number of behaviours (largest remainder, ties to the earlier batch), so
  the counts add up to `recommended`; otherwise every batch gets 1 and the planned total is `n`, which the result
  reports as raised to one per batch. No batch with behaviours ever has count 0 or is left out. This supersedes the
  analyst's own batching (the deferred 093 finding).
- **FR-006**: `generation.batch_size` is a new, optional setting: default 8, valid 1–50, `CONFIG_INVALID` outside the
  range. Its absence never warns or fails (083 rule).
- **FR-007**: The analysis ingest result MUST add `batches` (id, count, section refs, behaviour titles), `unresolved`
  (behaviour id, title, reason), `planned_total` and the plan path; all existing fields keep their names and meanings.
  The human output lists the batches and the unresolved behaviours (stderr in JSON mode), with `|` inside a title shown
  as `/`. A plan that cannot be written fails with `PLAN_WRITE_FAILED` (exit 1); nothing else about the ingest's exit
  codes changes.
- **FR-008**: `.spectra/generation-plan/` MUST be added to the shared gitignore list: `spectra init` writes it;
  `update-skills` offers it once in an existing workspace (090 rule) and never again.

**FR-B — the batch generation prompt**

- **FR-009**: `compile-prompt --suite S --batch <id> [--count N] [--focus …]` MUST emit a prompt that contains: only
  that batch's behaviours (id, title, category, technique); each of the batch's section refs' text once (raw, fenced as
  088's update prompt fences source text, outer refs absorbing inner ones); only the criteria whose `source_doc` is one
  of those documents and whose `source_section` names one of those sections or a heading inside them (document-level
  criteria — no `source_section` — only for whole-document refs; when none match, all criteria of the batch's
  documents, with a warning, and the refusal of FR-010 when those are empty too); the profile format and Testimize data as today; the
  count (the batch's planned count, or `--count` when given); the focus when given; and the instructions that the
  inlined sections are the complete source for this batch and that each test's `source_refs` must be the section refs
  of the behaviours it covers. It MUST NOT contain an on-demand reading instruction or any dead tool name.
- **FR-010**: Before compiling a batch, the CLI MUST recompute the fingerprints of that batch's sections and compare
  them with the plan. Refusals (exit 4, shared failure shape): `PLAN_MISSING` (no plan, or an unreadable one, for the
  suite); `BATCH_NOT_FOUND` (no batch with that id); `PLAN_STALE` (a section changed or no longer resolves — names it;
  re-run the analysis); `PROMPT_TOO_LARGE` (the estimate exceeds `analysis.max_prompt_tokens`, as 088 checks); and the
  existing criteria refusal (`MISSING_INPUT`, `criteria_context`) when neither the sections nor the batch's documents
  have criteria. There is no `TEMPLATE_OUTDATED` refusal for `--batch` (FR-010a). For `--batch unsourced` only the plan
  and batch checks apply. Nothing is written in any case.
- **FR-010a**: When `.spectra/prompts/test-generation.md` lacks the batch placeholders, `--batch` MUST still exit 0:
  (a) if its hash equals the hash the skills manifest recorded when it was installed (an unedited copy), the batch
  compiles with the bundled 094 template, as `update-skills` would install it, with the warning "using the bundled
  test-generation template; run spectra update-skills to refresh your copy"; (b) otherwise (customised, or no manifest
  record, e.g. a fresh clone), the batch compiles in count mode with the user's template, the batch's behaviour titles
  and section refs as the focus and the batch's count (or `--count`), with the warning "custom test-generation template
  predates batch prompts — this batch is compiled in count mode (the generator reads the docs); run spectra prompts
  reset test-generation to use section batches". The command reports the mode used: `batch`, `count_fallback`, or
  `count` for `--batch unsourced`.
- **FR-011**: `--batch unsourced` MUST compile a count-mode prompt whose focus lists the unsourced behaviours' titles
  and whose count is that batch's count (or `--count`).
- **FR-012**: Count mode (no `--batch`) MUST stay for from-description, the unsourced batch and existing callers. In
  both modes, the shipped template and the built-in fallback MUST drop the on-demand tool workflow and the four dead
  tool names. Count mode tells the model to read the documentation with its file tools and that test ids are assigned
  when the tests are ingested.
- **FR-013**: Identical plan, documents, criteria, profile, template and options MUST give byte-identical prompts in
  both modes.

**FR-C — agents and skill**

- **FR-014**: The analyst MUST return the CLI's batches verbatim as `batch <id> | count <k> | <section refs> |
  <behaviour titles>` lines and one `unresolved: <id> <title> — <reason>` line per unresolved behaviour, instead of
  planning batches itself. Its other lines are unchanged.
- **FR-015**: The generator MUST take `{suite, batch, count?}`. For a numbered batch it compiles with `--batch` and
  reads no documents (the prompt is the source); for `unsourced` it compiles with `--batch unsourced` and reads the
  documents as today. When the compile reports `count_fallback`, it reads the documents for that batch, as for
  `unsourced`, and relays the compile's warning as a `warning:` line. `PLAN_STALE`, `PROMPT_TOO_LARGE`, `PLAN_MISSING`
  and `BATCH_NOT_FOUND` end it with a `failed <code>: <first error>` line. Its final lines are otherwise unchanged.
- **FR-016**: The generation skill MUST pass batch ids to the generators, in plan order, sequentially. A user's
  explicit total is split over the batches as in 093 and passed as `--count`. On `PLAN_STALE` or `PROMPT_TOO_LARGE`
  from a generator it reports the batch and offers to re-run the analysis (by invoking the analyst again); the other
  batches still run. After a re-run it returns to Step 4: it presents the new recommendation and STOPs for approval,
  names the batches that already ran in this session with their created test ids, and suggests narrowing the focus to
  the stale sections; the user decides, and nothing is regenerated automatically. It never invokes the generator for a
  batch whose count is 0 (possible only when the user's explicit total is smaller than the number of batches) and lists
  such batches in Step 9 as "not generated (count 0 after splitting your total)". A template warning relayed by the
  generators is reported once per distinct warning per run.
- **FR-017**: Everything else in the 093 skill stays as is: the critic per test, routing by `ingest-verdict --all --ids`
  results, the repairer, the context budget contract, the NON-STOP contract and the written==0 guard text verbatim, and
  the from-description flow.

**Constraints**

- **FR-018**: No change to the critic, update, criteria or repair flows; `compile-repair-batch` gets no new options.
- **FR-019**: Both behaviours are verified on a workspace made by the real `spectra init` and on a pre-094 workspace
  (an old `.spectra/analysis.json`, customised `test-generation.md` and `behavior-analysis.md`, no
  `generation.batch_size`).

### Required Tests

Each new rule has a test that fails on `55b541f`:
- plan building: ids in input order, section grouping and packing in document order, `batch_size` (default and set),
  counts reconcile (both branches of FR-005), no dropped batch, unresolved → `unsourced` with reasons, bare paths →
  whole document, byte-identical plan;
- `PLAN_STALE` after a section edit (and an untouched batch still compiling);
- `--batch` prompt content: only its behaviours, sections and criteria; outer refs absorb inner ones; no on-demand or
  dead-tool text; byte-identical rerun;
- each refusal code; the template fallback (unedited copy → bundled template; customised or unrecorded copy →
  `count_fallback`; after `update-skills` → batch mode without a warning);
- count mode and the fallback without the dead tools; the from-description prompt otherwise unchanged;
- `generation.batch_size` range and absence (init template and pre-094 config);
- the gitignore entry in init and the one-time offer in `update-skills`;
- agent and skill text guards (analyst batch lines verbatim from the CLI, generator `--batch` with no document reads,
  skill passes batch ids, offers to re-run the analysis and returns to Step 4 after it, never invokes a 0-count batch;
  the 093 guards still pass).

### Key Entities

- **Behaviour**: one thing to test found by the analysis — id (`B<n>`), title, category, technique, the source as
  given, the resolved section ref (or the reason it did not resolve).
- **Generation plan**: per suite; the behaviours, the fingerprints of the resolved sections, the batches, the
  recommended and planned totals and the count rule. Scratch: replaced by the next analysis of the suite, gitignored.
- **Batch**: an id (`1`, `2`, …, or `unsourced`), its behaviour ids, its section refs and its count.
- **Section ref**: `doc` or `doc#anchor`, resolved like a test's `source_refs` entry; fingerprinted as in 084.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: In a generation of at least 24 tests in at least 3 batches, the generator subagents of numbered batches
  compiled in `batch` mode read 0 documentation files.
- **SC-002**: Every test created from a numbered batch has `source_refs` that are section refs of its batch.
- **SC-003**: The share of created tests the critic judges grounded in batch mode is at least the 093 count-mode share
  minus 5 percentage points, on the same suite and model, averaged over 2 runs each (the 093 baseline is measured on
  `55b541f` before implementation).
- **SC-004**: No behaviour found by the analysis is left out of the plan: behaviours in batches + unresolved =
  behaviours after the focus filter, and every batch has a count ≥ 1.
- **SC-005**: A pre-094 workspace and a new `spectra init` workspace both run analysis → plan → batch (or count-mode)
  generation without a config error.

## Assumptions

- The analysis prompt's section list per document lets the model name sections beyond the 2,000-character excerpt; a
  model that still gives bare document paths gets whole-document batches, which are correct but larger.
- "Already covered" stays an aggregate count; behaviours cannot be matched to existing tests one by one, so the plan
  keeps every behaviour and only the counts reflect coverage.
- Section text is inlined raw (no normalisation), so the critic and the generator see the same words.
- The plan is per-suite scratch; it is not locked (one generation per suite at a time, as today), only written
  atomically.
- The unsourced batch reads documents as in 093; its grounding is not expected to change.

## Out of Scope

- Section-scoped critic input.
- `existing_tests` / duplicate detection in the generation prompt.
- The unused `ai.generation_batch_size` (Phase 6 cleanup).
- Parallel batches.
- `compile-repair-batch --limit/--exclude-ids` (package 04 part 3): the repairer works per id.
- The rest of the Phase 6 list.
