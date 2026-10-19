# spectra-generate — reference

Reference for the `spectra-generate` skill. Read only the part SKILL.md names, when it names it — never during
Steps 7–9 (each of their steps is a single `spectra` call).

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
| `audit-grounding --suite {name} [--action critic,ground,repair,review,none] [--ids a,b]` | **Spec 072/097/098** Per-test grounding state of the suite's indexed tests: id, verdict, grounding_written, flagged, action_needed (`ground` = grounded verdict, block not written yet; `repair` = partial; `review` = hallucinated or flagged — the user decides; `critic` = stale or no verdict), `stale` (a verdict the ingest would refuse), summary counts (whole suite). Filter with `--action` / `--ids` to keep the output small. Use instead of `ls .spectra/verdicts/` improvisation. |
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

`compile-analysis-prompt` inlines documentation, so before emitting it estimates the prompt size against `analysis.max_prompt_tokens` (default 96K). If the estimate exceeds the budget it exits with code 4 and an actionable message naming every candidate doc-suite + token cost. Two recovery paths:

In the main flow this refusal arrives as `spectra-analyst`'s `refused:` / `refusal:` lines; re-running the analysis — with another doc suite from the candidates, or after the user raises the budget — means invoking `spectra-analyst` again with the new input. This session never runs `compile-analysis-prompt` itself.

1. **Narrow with `--doc-suite {id}`** — re-run targeting one doc-suite that fits. Run `spectra docs list-suites` to discover IDs.
2. **Raise the budget** — bump `analysis.max_prompt_tokens` in `spectra.config.json`.

## Step 4 presentation

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

## ISTQB techniques and boundary gaps

**ISTQB techniques**: SPECTRA's behavior analysis applies six ISTQB techniques (EP, BVA, DT, ST, EG, UC). The `ingest-analysis` recommendation includes a `technique_breakdown` map alongside `breakdown`. When you present the analyze recommendation, show BOTH the category breakdown and the technique breakdown so the user sees, e.g., how many BVA boundary test cases will be generated.

**Boundary-coverage gaps (Spec 062)**: `ingest-analysis` also surfaces a `boundary_gaps` array — boundary/edge conditions the docs imply should be tested (min/max, off-by-one, empty/null, overflow, timeout) that aren't covered by existing/planned tests. When non-empty, present these **alongside** the two breakdowns as an advisory list ("These edges look untested: …") so the user can decide whether to generate them. It is **advisory only** — it never blocks generation and never changes the recommended count. This is the analysis phase's completeness check; it is distinct from the `spectra-critic` grounding check (which only judges whether each claim traces to the docs).

## Resume an interrupted run

**Resume note:** If the run is interrupted (session end, cancellation), re-invoke the generate skill and run `spectra ai audit-grounding --suite {suite} --action ground,repair,review --output-format json` (Spec 097/098: only the entries that need something reach this conversation), then route each id by its `action_needed`: `action_needed: ground` — the critic confirmed it and only the block is missing: run `spectra ai ingest-grounding --suite {suite} --all --ids {ground ids} --output-format json` (no repair); `action_needed: repair` — partial tests still to repair: hand them to the `spectra-repairer` subagent and continue with the re-critic phase; `action_needed: review` — hallucinated tests kept for the user: list them under "Waiting for your review" and never send them to the repairer. Tests whose grounding block is already written are never re-processed.

## Analyze-first trigger phrases

**These trigger phrases ALL require the analyze-first flow:**
- "create test cases for {X}"
- "generate test cases for {X}"
- "add test cases to {X}"
- "test the {X} module"
- "I need test cases for {X}"
- "cover {X} with test cases"
- "write test cases for {X}"

## From-description detail

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

## Other tasks (delegation)

Read the named SKILL first, then follow its steps exactly. Do NOT invent CLI commands — the commands below are the ONLY valid forms.

| Task | SKILL | CLI command |
|------|-------|-------------|
| Update test cases | `spectra-update` | `spectra ai update-triage --suite {suite} --dry-run --output-format json --verbosity quiet` |
| Coverage analysis | `spectra-coverage` | `spectra ai analyze --coverage --auto-link --no-interaction --output-format json --verbosity quiet` |
| Dashboard | `spectra-dashboard` | `spectra ai analyze --coverage --auto-link ... && spectra dashboard --output ./site ...` |
| Extract criteria | `spectra-criteria` | `spectra docs changed --no-interaction --output-format json --verbosity quiet` |
| Validate test cases | `spectra-validate` | `spectra validate --no-interaction --output-format json --verbosity quiet` |
| List / show test cases | `spectra-list` | `spectra list --no-interaction --output-format json --verbosity quiet` |
| Docs index | `spectra-docs` | `spectra docs index [--force] --no-interaction --output-format json --verbosity quiet` |
| Delete test case(s) | `spectra-delete` | `spectra delete {id...} --dry-run/--force --no-interaction --output-format json --verbosity quiet` |
| Suite list/rename/delete | `spectra-suite` | `spectra suite list/rename/delete ... --no-interaction --output-format json --verbosity quiet` |
| Stop a running operation | (this skill) | `spectra cancel --no-interaction --output-format json --verbosity quiet` |
| Diagnose test ID issues | `spectra-help` | `spectra doctor ids [--fix] --no-interaction --output-format json --verbosity quiet` |

**Never re-run a command that completed successfully.** If the result shows it persisted, present the results and stop. **Dashboard**: after results, also open `site/index.html` to show the dashboard.

## Notes

- The compile/ingest commands are fast and deterministic — the thinking time is *your* in-session generation, not a CLI model call. There is no long-running background command to watch.
- Always write your generated JSON to a file under `.spectra/` and pass it with `--from`; this keeps large payloads off the shell.
- Surface any non-blocking `notes` from ingest/critic output verbatim after the results summary (Spec 048). Notes are informational (e.g. "no acceptance criteria matched suite '{suite}'") — they do not signal failure.
