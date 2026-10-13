---
name: spectra-generate
description: Generate test cases from documentation — analyze behaviors, get approval, generate, and verify with the mandatory spectra-critic subagent. No `spectra ai generate` command; uses the compile→in-session→ingest seam.
tools: [{{GENERATE_TOOLS}}]
---

# SPECTRA Test Generation

You generate test cases through a deterministic CLI seam. The CLI never calls a model — it **compiles a grounded prompt**, a model performs the generative turn (reading the docs), and the CLI **ingests** and validates what was produced. Every test is then verified by the mandatory `spectra-critic` subagent before it counts as accepted.

In the main flow (Spec 093) this session **orchestrates**: the `spectra-analyst` subagent analyses, the `spectra-generator` subagent generates each batch, and the `spectra-repairer` subagent repairs partial tests — each in its own context, returning a few lines. This session asks the user, runs the critic per test, routes each test by the verdict the CLI classified, and reports. Only the single-test from-description flow still generates in this session. Since Spec 094 the analysis saves a generation plan: each batch is a set of behaviours from the same doc sections, and its prompt carries exactly those sections and their criteria, so the generator reads no docs for it.

```
spectra-analyst (compile-analysis-prompt → analyse → ingest-analysis)  →  you present + STOP for approval
  →  per batch: spectra-generator (compile-prompt --batch → generate → ingest-tests)
  →  per test: spectra-critic  →  ingest-verdict --all --ids (CLI results route each test)
  →  partial: spectra-repairer  →  critic again  →  ingest-grounding
```

There is **no** `spectra ai generate` command anymore. Do not look for one. The seam commands are `spectra ai compile-prompt`, `compile-analysis-prompt`, `ingest-tests`, `ingest-analysis` (run by the subagents in the main flow), plus the critic's `compile-critic-prompt` / `ingest-verdict`.

**ISTQB techniques**: SPECTRA's behavior analysis applies six ISTQB techniques (EP, BVA, DT, ST, EG, UC). The `ingest-analysis` recommendation includes a `technique_breakdown` map alongside `breakdown`. When you present the analyze recommendation, show BOTH the category breakdown and the technique breakdown so the user sees, e.g., how many BVA boundary test cases will be generated.

**Boundary-coverage gaps (Spec 062)**: `ingest-analysis` also surfaces a `boundary_gaps` array — boundary/edge conditions the docs imply should be tested (min/max, off-by-one, empty/null, overflow, timeout) that aren't covered by existing/planned tests. When non-empty, present these **alongside** the two breakdowns as an advisory list ("These edges look untested: …") so the user can decide whether to generate them. It is **advisory only** — it never blocks generation and never changes the recommended count. This is the analysis phase's completeness check; it is distinct from the `spectra-critic` grounding check (which only judges whether each claim traces to the docs).

**ALWAYS follow the full analyze → approve → generate flow. Never skip the analysis step.**

## IMPORTANT RULES

- **HELP**: If user asks "help", "what can I do", or "what commands": follow the **`spectra-help`** SKILL. Read it and reply with its content.
- **QUICKSTART**: If user asks "how do I get started", "walk me through", "tutorial", "quickstart", "I'm new": follow the **`spectra-quickstart`** SKILL.
- **Do NOT probe at startup**: do not Glob `.claude/skills/**` or run `spectra --help` to discover commands or skills when a conversation starts. Skills are loaded by the harness — act on the user's request directly.

## MANDATORY: Analyze first, every time

If the user is asking you to **generate, create, add, write, or build test cases for an area, feature, module, suite, page, or topic** — you **MUST** start with the analysis step, present the recommendation, and **STOP and wait for the user to approve** before generating anything.

**These trigger phrases ALL require the analyze-first flow:**
- "create test cases for {X}"
- "generate test cases for {X}"
- "add test cases to {X}"
- "test the {X} module"
- "I need test cases for {X}"
- "cover {X} with test cases"
- "write test cases for {X}"

**Forbidden behaviors when the user names an area:**
- Do NOT generate tests before running the analysis step and getting approval.
- Do NOT invent a `--count` value. There is no default — the analysis step recommends a number.
- Do NOT ask the user how many test cases they want — the analyze step will recommend a number.

The ONLY time you skip analysis is when the user describes a single concrete scenario (see "When the user wants to create a specific test case").

## CLI flags reference

In the main flow (Spec 093/094), `compile-analysis-prompt` and `ingest-analysis` are run by `spectra-analyst`, `compile-prompt --batch` and `ingest-tests` by `spectra-generator`, and `ingest-update --all --keep-baseline` by `spectra-repairer`; this session runs `compile-prompt --from-description` and `ingest-tests` only in the from-description flow. `compile-repair-batch` is not used by this skill (the command stays). `ingest-verdict --suite {name} --all --ids a,b` returns `results` — one `{id, verdict, score, gate}` per classified test — which this skill routes by.

| Command / flag | Description |
|------|-------------|
| `compile-analysis-prompt --suite {name}` | Emits the behavior-analysis prompt (deterministic). REQUIRED suite — directory under `test-cases/`. |
| `compile-analysis-prompt --doc-suite {id}` | **Spec 040** Doc-suite ID from the manifest (`spectra docs list-suites`). Filters which documentation feeds the analyzer. **Required when `--suite` does not match a doc-suite ID** — otherwise the pre-flight budget check (default 96K tokens) refuses the run (exit 4). |
| `compile-prompt --suite {name} --batch {id}` | **Spec 094** Emits the prompt for one batch of the suite's generation plan (saved by `ingest-analysis`): the batch's behaviours, its doc sections and their criteria. `--count {n}` overrides the plan's count. Refusals (exit 4): `PLAN_MISSING`, `BATCH_NOT_FOUND`, `PLAN_STALE`, `PROMPT_TOO_LARGE`, `MISSING_INPUT`. |
| `compile-prompt --suite {name} --count {n}` | Emits the generation prompt for `{n}` tests (deterministic). NEVER invent `{n}` — use a number the user stated, or the `recommended` field from the analyze step. |
| `compile-prompt --from-description "{text}"` | Single-test mode: emits a one-test prompt from a plain-language description. |
| `--focus {text}` | Focus area: "negative", "edge cases", "high priority security", etc. Pass the SAME focus to analyze and generate. |
| `--context {text}` | Extra context for `--from-description` (page, module, flow). |
| `--include-archived` | **Spec 040** Include suites flagged `skip_analysis: true` in analyzer input. |
| `ingest-tests {suite} --from {file}` | Validates and persists the tests you generated (fail-loud). Omit `--from` to read stdin. |
| `ingest-analysis --suite {name} --from {file}` | Turns your behavior JSON into a recommendation (deterministic accounting). |
| `config --raw` | Returns the resolved config JSON — use this instead of `cat spectra.config.json` (command already exists, no shell needed). |
| `audit-grounding --suite {name}` | **Spec 072** Per-test grounding state: id, verdict, grounding_written, flagged, action_needed, summary counts. Use instead of `ls .spectra/verdicts/` improvisation. |
| `ingest-grounding --suite {name} --all` | **Spec 073** Batch form: write grounding blocks for all eligible tests in one call. Without `--repaired`: writes grounded verdicts only (partial-pre-repair filter). With `--repaired --repair-attempts N`: writes all ungrounded tests after repair. Idempotent — skips already-written blocks. |
| `ingest-verdict --suite {name} --all` | **Spec 077** Batch form: classify all verdict files for the suite in one call; emits summary `{grounded, partial, hallucinated, errors}`. Replaces per-test `--from` loops. Advisory-gate only — no file writes. |
| `ingest-update {suite} --all --keep-baseline` | **Spec 077** Batch form: ingest all staged repairs from `.spectra/repairs/{suite}/repaired-{id}.json` in one call (**Spec 089:** repairs have their own folder; this call never reads `.spectra/updates/`); emits `{written, unchanged, skipped_no_original, errors, results}`; exit `8` when a staged file failed (read `results`, retry). Replaces per-entry `--test-id` loops. **Spec 085:** repairs always pass `--keep-baseline` — a repair fixes grounding, not doc drift, so the test's source-change baseline must not move (a pending doc change stays in `update-triage`). |

**Shell improvisation is forbidden.** SPECTRA provides CLI verbs for all state reads. Never improvise with shell commands:
- Grounding state: `spectra ai audit-grounding --suite {suite} --output-format json` — NOT `ls .spectra/verdicts/`, NOT `grep grounding`
- Test file path: `test-cases/{suite}/{id}.md` — test IDs always map to `{id}.md` in the suite directory. Never `find test-cases -name {id}.md`; never guess a flat path like `test-cases/{id}.md`
- Resolved config: `spectra config --raw` — NOT `cat spectra.config.json`

**There is NO `--priority`, `--type`, or `--category` flag.** Use `--focus` for ALL filtering by type, priority, or category. Capture the user's FULL intent in `--focus`:
- "generate 15 negative test cases" → `--focus "negative tests"` (count 15)
- "generate high priority edge cases" → `--focus "high priority edge cases"`
- "cover its acceptance criteria" → `--focus "acceptance criteria"`

### Pre-flight token-budget check (Spec 040, exit code 4)

`compile-analysis-prompt` inlines documentation, so before emitting it estimates the prompt size against `ai.analysis.max_prompt_tokens` (default 96K). If the estimate exceeds the budget it exits with code 4 and an actionable message naming every candidate doc-suite + token cost. Two recovery paths:

In the main flow this refusal arrives as `spectra-analyst`'s `refused:` / `refusal:` lines; re-running the analysis — with another doc suite from the candidates, or after the user raises the budget — means invoking `spectra-analyst` again with the new input. This session never runs `compile-analysis-prompt` itself.

1. **Narrow with `--doc-suite {id}`** — re-run targeting one doc-suite that fits. Run `spectra docs list-suites` to discover IDs.
2. **Raise the budget** — bump `ai.analysis.max_prompt_tokens` in `spectra.config.json`.

---

## Context budget contract

This session holds only what it needs to orchestrate (Spec 093). It **never reads** docs, compiled prompts, test files, generated / staged JSON or verdict files — not to check a result, not to explain one. Any step that would need them is done by the named subagent, which returns a few lines. This session's own tool calls are:
- `spectra` commands with small JSON output (`ingest-verdict --all --ids`, `ingest-grounding`, `audit-grounding`, `record-drop`, `spectra delete`, `init-seam-progress`);
- writes to `.spectra/progress.json`;
- Task calls to `spectra-analyst`, `spectra-generator`, `spectra-critic` and `spectra-repairer`.

Report every line a subagent returns, as returned — the user doesn't see a subagent's own output, so this session's report is how they learn what happened.

## Main generation flow (user names an area)

**Determine focus**: Extract the user's full intent into a `--focus` value (type + topic). If no focus, omit `--focus`.

**Determine count for the LATER generate step**:
- If the user said an explicit number, use that.
- Otherwise leave count blank — the analyze step gives you `recommended` to use.
- NEVER fall back to "5". There is no default.

**Step 0 — Initialize live progress.** Run this FIRST, before anything else:

Run with the Bash tool:
```
spectra ai init-seam-progress
```

Write `.spectra/progress.json` with the Write tool:
```json
{"phases":["Step 1 — Compile analysis","Step 2 — Identify behaviors","Step 3 — Ingest analysis","Step 4 — Review (waiting for approval)","Step 5 — Compile generation","Step 6 — Generate tests","Step 7 — Ingest","Step 8 — Critic","Step 9 — Report"],"active":0}
```

### Steps 1–3 — Analysis (the `spectra-analyst` subagent)

Invoke the `spectra-analyst` subagent with the Task tool, passing `suite: {suite}`, `doc_suite: {docSuite}` and, when there is one, `focus: {focus}` — nothing else. It compiles the analysis prompt, identifies the behaviours in its own context, ingests them (with its own bounded retries) — the ingest saves the suite's generation plan — and returns the recommendation and the plan's batches:
- `already_covered: N`, `recommended: N`, `documents_analyzed: N`, `breakdown: …`, `techniques: …`, one `boundary_gap: …` line per gap;
- the CLI's `batch <id> | count <k> | <section refs> | <behaviour titles>` lines, `covered: <title> (<covered_by>)` lines for behaviours an existing test already covers (Spec 095: never batched, never generated), and `unresolved: <id> <title> — <reason>` lines for behaviours whose source didn't resolve (they are in the `unsourced` batch, which is generated by reading the docs);
- or `refused: <code>` and `refusal: …` lines — the pre-flight budget refusal: show the `refusal:` lines verbatim and follow a recovery path above (narrow the doc suite, or raise the budget) — re-running means invoking `spectra-analyst` again. Do NOT proceed;
- a `stop: …` line → report it and stop.

Update `.spectra/progress.json`: `{"active":3}`

### Step 4 — Present the recommendation and STOP

From the analyst's lines show:

**{already_covered}** test cases already exist. I recommend generating **{recommended}** new test cases:

Category breakdown (one bullet per entry of the `breakdown:` line, e.g. `happy_path`, `boundary`, `negative`, `edge_case`, `security`, `error_handling`):
- {category}: {count}

ISTQB technique breakdown (entries of the `techniques:` line — keys `BVA`, `EP`, `DT`, `ST`, `EG`, `UC`):
- BVA (Boundary Value Analysis): {count}
- EP (Equivalence Partitioning): {count}
- DT (Decision Table): {count}
- ST (State Transition): {count}
- EG (Error Guessing): {count}
- UC (Use Case): {count}

The `boundary_gap:` lines as an advisory list, the batch lines (each batch's count, doc sections and behaviours) and the unresolved behaviours.

**Already covered — not generated:** one bullet per `covered:` line — the behaviour and the existing test that covers it (Spec 095). These are not in any batch and are never generated.

Shall I proceed?

**STOP. Wait for user approval.** If `recommended` is 0, tell the user every identified behavior is already covered and do not generate.

---

## After user approves

Update `.spectra/progress.json`: `{"active":4}`

### Steps 5–7 — Generation, one batch at a time (the `spectra-generator` subagent)

Use the batch lines, in plan order. When the user stated an explicit total, it overrides `recommended`: split it over the batches in proportion to their counts (largest remainder, so the parts add up to the user's total) and pass each part as `count: <k>` — only for an explicit total; otherwise pass no count and the plan's count applies. **Never invoke the generator for a batch whose count is 0** (this happens only when the user's explicit total is smaller than the number of batches); Step 9 lists such a batch as "not generated (count 0 after splitting your total)". **`recommended` is advisory guidance, not a quota**; acceptance-criteria coverage is the real adequacy signal.

For each batch, in order — one at a time, never in parallel (the id allocator and the suite index have a single writer) — update progress, then invoke the `spectra-generator` subagent with the Task tool, passing `suite: {suite}`, `batch: <id>` and, only for an explicit total, `count: <k>`. It compiles the batch's prompt (`compile-prompt --batch`: the batch's doc sections and their criteria — it reads no docs for a section batch; the `unsourced` batch, and any batch under a customised pre-094 `test-generation.md`, compile in count mode and it reads the docs), generates in its own context, ingests (with its own bounded retries), and returns:
- one `<id> | <title>` line per created test;
- a `baseline: …` line and `warning: …` lines — report each distinct template warning (about the `test-generation` template) once per run, not once per batch;
- or `failed <error_code>: <first error>` — the batch failed: report it and run the next batch. Never generate a batch in this session. `PLAN_STALE` (its doc sections changed since the analysis) and `PROMPT_TOO_LARGE` (its sections don't fit one prompt) are handled in Step 9;
- a `stop: …` line → report it and stop.

Report every line, as returned. Collect the created ids and titles of all batches.

Update `.spectra/progress.json`: `{"active":6}`

---

## NON-STOP CONTRACT — Steps 7–9

**Every step in this section must be a single `Bash(spectra *)` call or a `Write` to a spectra-authored path.** If no single `spectra` call covers a step, **STOP and report a missing affordance** — never work around it.

**Prohibited behaviors (each is a missing-affordance signal, not a license to improvise):**
- **(a) Shell loops over per-test verbs** — `for id in …; do spectra …; done` — NEVER. Use the corresponding `--all` batch verb.
- **(b) Piping spectra output to any interpreter** — `python -c "…"`, `jq`, `node -e`, or any data-transform shell — NEVER. Read the output file with the Read tool and iterate in-context.
- **(c) Manual `.md` file editing** — rewriting test content directly instead of using `ingest-update` — NEVER.
- **(d) Verdict / grounding field rewrites by hand** — writing `grounding:`, `verdict:`, or `score:` frontmatter directly instead of using `ingest-grounding` / `record-drop` — NEVER.

**Never accept a "scripting for all projects" allowlist option** — that is the inverse of this contract. The per-seam guards below (e.g., the grounding `written:0` check) are specific instances of this general rule.

---

## MANDATORY: verify every generated test with the critic subagent

Verification is **mandatory and explicit — never skipped, never auto-invoked**. The `ingest-tests` boundary only validates schema; the `spectra-critic` subagent (a fresh, isolated `context: fork`) is the single critic of record for grounding. A test is not accepted until its critic step has passed.

For each created id, update `.spectra/progress.json` before invoking the critic:
```json
{"phases":["Step 1 — Compile analysis","Step 2 — Identify behaviors","Step 3 — Ingest analysis","Step 4 — Review (waiting for approval)","Step 5 — Compile generation","Step 6 — Generate tests","Step 7 — Ingest","Step 8 — Critic","Step 9 — Report"],"active":7,"loop":{"current":i,"total":N,"label":"critic for <id>"}}
```

### Step 8 — Critic pass (per-test) + repair (Spec 072, Spec 093)

**8a — Per-test critic.** For EACH created id:

1. Update progress, then invoke the `spectra-critic` subagent with the Task tool — suite + test id only, no generator state, **never a hand-built file path**. The subagent compiles the prompt (`spectra ai compile-critic-prompt --suite {suite} --test {id}`, resolves id→path from `_index.json`) and writes `.spectra/verdicts/critic-verdict-{id}.json`. Its final line (`<id> | <verdict> <score>` or `<id> | failed: <reason>`) is for progress only — never read the verdict file.

**After 8a loop completes — batch verdict-ingest for the created tests (one call, no loop):**
```
spectra ai ingest-verdict --suite {suite} --all --ids <created ids> --output-format json
```
Route each test by its entry in `results` — the verdict the CLI classified, not the critic's line:
2. **`grounded` (gate `pass`):** Add to kept-grounded count. (Grounding blocks are written in one batch call — see below.)
3. **`partial`:** Note the id for the repair in 8b.
4. **`hallucinated` (gate `drop`)** — only when the critic's line agrees or there is no line:
   ```
   spectra ai record-drop --suite {suite} --test {id} \
     --from .spectra/verdicts/critic-verdict-{id}.json --output-format json
   spectra delete {id} --force --no-interaction --output-format json --verbosity quiet
   ```
   → Trail written before delete. Add to dropped count.
5. **No `results` entry** (the critic's compile was refused, its verdict JSON stayed damaged, or the CLI lists the test as stale or not found): invoke the critic once more for that test and run the verdict ingest for it again. If there is still no entry, the test is kept as **unverified**: it is never counted as kept-grounded, never gets a grounding block, is never regenerated or deleted, and is listed in Step 9 under "Unverified (critic could not judge)".
6. **The critic's line disagrees with the CLI's verdict** → the test is kept as **unverified** and reported with both — never dropped. (A test kept with a `hallucinated` verdict is later listed under `skipped_hallucinated` by the grounding batch; that is expected, not a failure.)

**After batch verdict-ingest — batch grounding-ingest for grounded tests (one call, no loop):**
```
spectra ai ingest-grounding --suite {suite} --all --output-format json
```
Writes grounding blocks for all grounded tests in one pass; skips partial tests (pre-repair filter — their blocks are written after 8b). Test files are at `test-cases/{suite}/{id}.md` (never use `find` or `cat` to locate them).
`skipped_hallucinated` / `hallucinated` (Spec 091) lists tests that were kept with a hallucinated verdict — for example by the update flow, which leaves that decision to the user. That is not a failure and not part of `written`: report the ids and continue.

**After batch grounding-ingest — check the result before proceeding to 8b (MANDATORY):**

Parse the JSON result of the batch call. Check `written` against the `kept-grounded` count tracked during the 8a loop:

- **If `written == 0` AND `kept-grounded > 0`:** STOP immediately. Do NOT proceed to 8b. Emit a diagnostic:
  > GROUNDING-INGEST ANOMALY: `ingest-grounding --all` returned `written: 0` but {kept-grounded} tests were marked grounded in the critic loop. This means the grounding blocks were not written. Likely causes: `_index.json` file paths are suite-prefixed (path-doubling bug) or the suite directory could not be resolved. **STOP — do not proceed to 8b, do not edit `.md` files by hand, do not rewrite verdict fields by hand.** Run `spectra ai audit-grounding --suite {suite}` to diagnose the current grounding state, then report the anomaly to the user and halt.

- **If `written != kept-grounded`** (partial write — `written > 0` but less than expected): Surface a warning before continuing:
  > GROUNDING-INGEST WARNING: expected {kept-grounded} grounding blocks written, got {written}. Continuing to 8b — run `spectra ai audit-grounding --suite {suite}` to inspect which tests are missing their grounding block.

- **If `written == kept-grounded`** (or `kept-grounded == 0`): Proceed to 8b normally.

**NON-STOP CONTRACT (restated):** A broken or zero-result CLI verb is a STOP signal — it is NEVER a license to do the work by hand. Manual editing of `.md` files, rewriting of `grounding:` frontmatter, and synthesizing verdict fields are PROHIBITED regardless of how blocked you are. If a CLI verb returns an anomaly, stop and report. Do not improvise. Exit `8` from `ingest-update --all` is neither broken nor zero-result: the batch completed and `results` lists the failed files — retry them as 8b describes.

**8b — Repair (for partials).** If 8a noted partial ids, invoke the `spectra-repairer` subagent with the Task tool, passing `suite: {suite}` and `ids: <partial ids>`. It patches only what the critic flagged, stages the repairs in `.spectra/repairs/{suite}/`, ingests them with `ingest-update {suite} --all --keep-baseline` (keeping each test's baseline), retries a failed repair ingest at most twice (the same repair attempt), and returns one line per ingested file, attributed by id: `<id> | written`, `<id> | unchanged` or `<id> | failed <error_code>: <first error>`. Report every line.

- `failed` → the repair didn't land: report it, flag the test (it stays **unverified** with its partial verdict), run no critic for it.
- Every `written` line goes through the re-critic below — including a line for an id that wasn't in the partial list (a leftover repair file from an earlier run); a `written` test that isn't re-verified is listed as unverified in the report.

**Re-critic phase — For EACH id the repairer returned `written` or `unchanged`:** invoke the `spectra-critic` subagent again (same Task-tool procedure as 8a.1). Then:
```
spectra ai ingest-verdict --suite {suite} --all --ids <re-critiqued ids> --output-format json
```
Route by `results`, as in 8a:
- `grounded`: Add to repaired-to-grounded count.
- `partial`: Add to flagged-partial count. **Continue — do NOT stop the batch.**
- `hallucinated` (only when the critic's line agrees or there is no line): `spectra ai record-drop --suite {suite} --test {id} --from .spectra/verdicts/critic-verdict-{id}.json --output-format json` then `spectra delete {id} --force --no-interaction --output-format json --verbosity quiet` → Add to dropped count.
- no entry → the critic once more, then unverified (8a.5); a disagreement → unverified (8a.6).

**After the re-critic — batch grounding-ingest for the re-critiqued tests (one call, no loop):**
```
spectra ai ingest-grounding --suite {suite} --all --repaired --repair-attempts 1 --ids {ids} --output-format json
```
`{ids}` is the comma-separated list of the tests re-critiqued in this round — those whose repair ingest reported `written` or `unchanged` in `results` — minus any test dropped in the re-critic phase. A repair that failed is never in it (Spec 091: `--all --repaired` refuses without `--ids` with `IDS_REQUIRED`, so a test whose repair didn't land is never marked repaired). If the list is empty, skip this call. Writes grounding blocks for those tests: `grounded` blocks for successfully repaired tests, `partial` blocks (`flagged_for_review: true` unless repaired) for those still unresolved.

**Resume note:** If the repair is interrupted (session end, cancellation), re-invoke the generate skill and run `spectra ai audit-grounding --suite {suite} --output-format json`: the ids with `action_needed: repair` are the partial tests still to repair — hand them to the `spectra-repairer` subagent and continue with the re-critic phase. Tests whose grounding block is already written are never re-processed.

Update `.spectra/progress.json`: `{"active":8}` (marks Step 9 active — complete sentinel is `active >= 9`).

### Step 9 — Report

Report from the returned lines and the CLI results — every created test by id and title with its outcome:
- Kept grounded: {kept-grounded count} — ids and titles
- Repaired to grounded: {repaired count} — ids and titles
- Flagged partial (awaiting review): {flagged count} — ids and titles (incl. failed repairs)
- Dropped hallucinated: {dropped count} — ids and titles
- Failed batches: each `failed` line with its code
- Not generated: each batch with count 0 after splitting the user's total, as "not generated (count 0 after splitting your total)"
- **Already covered — not generated:** each `covered:` line from the analysis — the behaviour and the test that covers it
- **Unverified (critic could not judge):** each test with its id, title, the reason, and the next step — run this skill's critic step (8a) again for these ids, or `spectra ai audit-grounding --suite {suite}`, where it shows as `none` / `critic`

Also every `baseline:` and `warning:` line from the generator and repairer.

If {flagged} > 0: "Run `spectra ai review-flagged --suite {suite}` to accept, retry repair, or delete flagged tests."

If total-kept < requested, say "Run again to generate more." Never present a test as accepted unless its critic step passed.

**A plan from before per-behaviour coverage (Spec 095).** If a generator relays the warning "the generation plan predates per-behaviour coverage; re-run the analysis for corrected counts", report it once and offer to re-run the analysis (invoke `spectra-analyst` again, then back to Step 4 as below) — the plan's counts and batches may include behaviours existing tests already cover.

**Stale or oversized batches (Spec 094).** If a batch failed with `PLAN_STALE` or `PROMPT_TOO_LARGE`, name those batches and offer once to re-run the analysis. If the user accepts, invoke `spectra-analyst` again (same suite, doc suite and focus) and go **back to Step 4**: present the new recommendation, name the batches that already ran in this session with their created test ids, suggest narrowing the focus to the stale sections, and STOP for approval. Nothing is regenerated automatically — the user decides.

Write `.spectra/progress.json`: `{"active":9}` (terminal — seam-progress page renders Complete).

---

## When the user wants to create a specific test case

Use this flow when the user describes a concrete test scenario — a behavior they want captured as a test case. **Do NOT run analysis. Do NOT ask how many test cases to generate. This always produces exactly 1 test case.**

### Step 1 — Compile the from-description prompt

```
spectra ai compile-prompt --suite {suite} --from-description "{description}" [--context "{context}"] --output-format json
```
- `{suite}` — target suite (ask only if not obvious from context).
- `{description}` — the user's scenario, verbatim.
- `{context}` — optional (page, module, flow); omit `--context` if not given.
- Exit 0 → stdout is the single-test prompt (criteria are injected when the suite has matching ones — Spec 050).

### Step 2 — Generate the one test IN-SESSION

Generate exactly one test case from the compiled prompt, reading any referenced docs with your file tools. Write the one-element JSON array to `.spectra/generated.json`.

### Step 3 — Ingest (fail-loud)

```
spectra ai ingest-tests {suite} --from .spectra/generated.json --output-format json
```
Handle exit 5/6 with the same bounded regenerate-and-retry as the main flow.

### Step 4 — Critic (mandatory)

Run the **Step 8** critic flow above on the persisted test. From-description tests now get a real grounding verdict like every other flow (not `manual`).

### Step 5 — Present the result

From the ingest + critic output, show: test id and title; the suite; linked acceptance criteria (the test's `criteria` field — populated when the suite had matching criteria); the grounding verdict; any duplicate warnings; and any non-blocking notes.

---

## Test Creation Intent Routing

### Intent 1: Explore a feature area → use `--focus`

**Signals**: topic words ("error handling", "negative test cases", "payment module"), no specific scenario described, request implies multiple test cases, plural ("test cases").

**Examples**:
- "Generate test cases for checkout error handling"
- "I need negative test cases for the auth module"
- "Cover the refund policy with test cases"
- "Generate 10 test cases for payments"

**Action**: Use the main analyze → approve → generate flow with `--focus "{topic}"`.

### Intent 2: Create a specific test → use `--from-description`

**Signals**: describes a concrete behavior (you could read it as a test title), single scenario, action + expected outcome pattern, singular ("a test").

**Examples**:
- "Add a test for double-click submit creating duplicate orders"
- "Create a test case where expired session redirects to login"
- "I need a test that verifies IBAN validation rejects invalid checksums"
- "Add a test: guest checkout with PayPal completes successfully"

**Action**: Use the from-description flow ("When the user wants to create a specific test case"). **Produces exactly 1 test. No analysis needed. No count question.**

### Ambiguous intent

If unclear whether the user wants to explore an area or create a specific test, default to `--from-description` if they described a behavior, or `--focus` if they named a topic. **Don't stop to ask about count or scope** — the topic-vs-scenario shape is the only signal needed, so proceed without a needless confirmation round-trip.

---

## How to choose between generation flows

| User intent | Signal | Flow |
|-------------|--------|------|
| Explore a feature area | "Generate test cases for...", "Test the... module", "Cover... error handling" | Main generation flow (analyze → approve → generate, with `--focus`) |
| Create a specific test case | "Add a test case for...", "Create a test case where...", "I need a test case that verifies..." | From-description flow (`compile-prompt --from-description`) |

**Key rule**: If you can read the user's request as a single test case title, it's `--from-description`. If it's a topic to explore, it's the main flow. Do NOT ask the user about count or scope to disambiguate — the topic-vs-scenario shape is the only signal you need.

---

## Other tasks (delegation)

Read the named SKILL first, then follow its steps exactly. Do NOT invent CLI commands — the commands below are the ONLY valid forms.

| Task | SKILL | CLI command |
|------|-------|-------------|
| Update test cases | `spectra-update` | `spectra ai update-triage --suite {suite} --dry-run --output-format json --verbosity quiet` |
| Coverage analysis | `spectra-coverage` | `spectra ai analyze --coverage --auto-link --no-interaction --output-format json --verbosity quiet` |
| Dashboard | `spectra-dashboard` | `spectra ai analyze --coverage --auto-link ... && spectra dashboard --output ./site ...` |
| Extract criteria | `spectra-criteria` | `spectra ai analyze --extract-criteria --no-interaction --output-format json --verbosity quiet` |
| Validate test cases | `spectra-validate` | `spectra validate --no-interaction --output-format json --verbosity quiet` |
| List / show test cases | `spectra-list` | `spectra list --no-interaction --output-format json --verbosity quiet` |
| Docs index | `spectra-docs` | `spectra docs index [--force] --no-interaction --output-format json --verbosity quiet` |
| Delete test case(s) | `spectra-delete` | `spectra delete {id...} --dry-run/--force --no-interaction --output-format json --verbosity quiet` |
| Suite list/rename/delete | `spectra-suite` | `spectra suite list/rename/delete ... --no-interaction --output-format json --verbosity quiet` |
| Stop a running operation | (this skill) | `spectra cancel --no-interaction --output-format json --verbosity quiet` |
| Diagnose test ID issues | `spectra-help` | `spectra doctor ids [--fix] --no-interaction --output-format json --verbosity quiet` |

**Never re-run a command that completed successfully.** If the result shows it persisted, present the results and stop. **Dashboard**: after results, also open `site/index.html` to show the dashboard.

---

## Notes

- The compile/ingest commands are fast and deterministic — the thinking time is *your* in-session generation, not a CLI model call. There is no long-running background command to watch.
- Always write your generated JSON to a file under `.spectra/` and pass it with `--from`; this keeps large payloads off the shell.
- Surface any non-blocking `notes` from ingest/critic output verbatim after the results summary (Spec 048). Notes are informational (e.g. "no acceptance criteria matched suite '{suite}'") — they do not signal failure.
