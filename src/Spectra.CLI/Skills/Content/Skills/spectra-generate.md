---
name: spectra-generate
description: Generate test cases from documentation — analyze behaviors, get approval, generate, and verify with the mandatory spectra-critic subagent. No `spectra ai generate` command; uses the compile→in-session→ingest seam.
tools: [{{GENERATE_TOOLS}}]
---

# SPECTRA Test Generation

You generate test cases **in this session** through a deterministic CLI seam. The CLI never calls a model — it **compiles a grounded prompt**, *you* perform the generative turn in your own context (reading the docs with your file tools), and the CLI **ingests** and validates what you produced. Every test is then verified by the mandatory `spectra-critic` subagent before it counts as accepted.

The seam, for every flow:

```
compile (CLI, deterministic)  →  you generate in-session  →  ingest (CLI, fail-loud)  →  critic subagent (mandatory)
```

There is **no** `spectra ai generate` command anymore. Do not look for one. The commands you use are `spectra ai compile-prompt`, `compile-analysis-prompt`, `ingest-tests`, `ingest-analysis`, plus the critic's `compile-critic-prompt` / `ingest-verdict`.

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

| Command / flag | Description |
|------|-------------|
| `compile-analysis-prompt --suite {name}` | Emits the behavior-analysis prompt (deterministic). REQUIRED suite — directory under `test-cases/`. |
| `compile-analysis-prompt --doc-suite {id}` | **Spec 040** Doc-suite ID from the manifest (`spectra docs list-suites`). Filters which documentation feeds the analyzer. **Required when `--suite` does not match a doc-suite ID** — otherwise the pre-flight budget check (default 96K tokens) refuses the run (exit 4). |
| `compile-prompt --suite {name} --count {n}` | Emits the generation prompt for `{n}` tests (deterministic). NEVER invent `{n}` — use a number the user stated, or the `recommended` field from the analyze step. |
| `compile-prompt --from-description "{text}"` | Single-test mode: emits a one-test prompt from a plain-language description. |
| `--focus {text}` | Focus area: "negative", "edge cases", "high priority security", etc. Pass the SAME focus to analyze and generate. |
| `--context {text}` | Extra context for `--from-description` (page, module, flow). |
| `--include-archived` | **Spec 040** Include suites flagged `skip_analysis: true` in analyzer input. |
| `ingest-tests {suite} --from {file}` | Validates and persists the tests you generated (fail-loud). Omit `--from` to read stdin. |
| `ingest-analysis --suite {name} --from {file}` | Turns your behavior JSON into a recommendation (deterministic accounting). |

**There is NO `--priority`, `--type`, or `--category` flag.** Use `--focus` for ALL filtering by type, priority, or category. Capture the user's FULL intent in `--focus`:
- "generate 15 negative test cases" → `--focus "negative tests"` (count 15)
- "generate high priority edge cases" → `--focus "high priority edge cases"`
- "cover its acceptance criteria" → `--focus "acceptance criteria"`

### Pre-flight token-budget check (Spec 040, exit code 4)

`compile-analysis-prompt` inlines documentation, so before emitting it estimates the prompt size against `ai.analysis.max_prompt_tokens` (default 96K). If the estimate exceeds the budget it exits with code 4 and an actionable message naming every candidate doc-suite + token cost. Two recovery paths:

1. **Narrow with `--doc-suite {id}`** — re-run targeting one doc-suite that fits. Run `spectra docs list-suites` to discover IDs.
2. **Raise the budget** — bump `ai.analysis.max_prompt_tokens` in `spectra.config.json`.

---

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

### Step 1 — Compile the analysis prompt

Run with the Bash tool and capture stdout:
```
spectra ai compile-analysis-prompt --suite {suite} --doc-suite {docSuite} [--focus "{focus}"] --output-format json
```
- Exit 4 → the budget was exceeded (or no suite given). Show the message verbatim and follow a recovery path above. Do NOT proceed.
- Exit 1 → tell the user the error.
- Exit 0 → stdout is the compiled analysis prompt. Proceed.

Update `.spectra/progress.json`: `{"active":1}`

### Step 2 — Identify behaviors IN-SESSION

Read the compiled prompt. It instructs you to identify the distinct testable behaviors in the named documentation, each tagged with a category and an ISTQB technique. Do that now, in this turn, using your file tools to read the referenced docs as needed. Produce a JSON array (or `{"behaviors":[…]}`) exactly as the prompt specifies. **Write that JSON to `.spectra/analysis.json`** with the Write tool.

Update `.spectra/progress.json`: `{"active":2}`

### Step 3 — Ingest the analysis

```
spectra ai ingest-analysis --suite {suite} --from .spectra/analysis.json --output-format json
```
- Exit 5 (empty) or 6 (unparseable) → your behavior JSON was rejected. Re-read the compiled prompt, regenerate stricter JSON, and re-ingest. **Bounded: at most 2 attempts**, then STOP and report.
- Exit 0 → the JSON holds the recommendation.

Update `.spectra/progress.json`: `{"active":3}`

### Step 4 — Present the recommendation and STOP

From the `ingest-analysis` JSON show:

**{already_covered}** test cases already exist. I recommend generating **{recommended}** new test cases:

Category breakdown (one bullet per non-zero entry in `breakdown`, e.g. `happy_path`, `boundary`, `negative`, `edge_case`, `security`, `error_handling`):
- {category}: {count}

ISTQB technique breakdown (non-zero entries from `technique_breakdown` — keys `BVA`, `EP`, `DT`, `ST`, `EG`, `UC`):
- BVA (Boundary Value Analysis): {count}
- EP (Equivalence Partitioning): {count}
- DT (Decision Table): {count}
- ST (State Transition): {count}
- EG (Error Guessing): {count}
- UC (Use Case): {count}

Shall I proceed?

**STOP. Wait for user approval.** If `recommended` is 0, tell the user every identified behavior is already covered and do not generate.

---

## After user approves

Update `.spectra/progress.json`: `{"active":4}`

### Step 5 — Compile the generation prompt

Keep the SAME `--focus` from analysis. Use `{count}` = the user's explicit number or `recommended`. **`recommended` is advisory guidance, not a quota** — generate as many tests as the behavior needs (more or fewer is fine). **Acceptance-criteria coverage is the real adequacy signal**, not hitting the count.
```
spectra ai compile-prompt --suite {suite} --count {count} [--focus "{focus}"] --output-format json
```
- Exit 4 → refuse-to-emit; the `missing_input`/`message` names what's missing (e.g. no acceptance criteria resolved). Show it and follow the guidance.
- Exit 0 → stdout is the compiled generation prompt.

Update `.spectra/progress.json`: `{"active":5}`

### Step 6 — Generate the tests IN-SESSION

Read the compiled prompt and generate the requested test cases now, in this turn. **You have file access — read the relevant documents under `docs/` so every test is grounded in real product behavior**; never invent generic patterns. When acceptance criteria appear in the prompt under "ACCEPTANCE CRITERIA — MANDATORY", every test MUST map at least one criterion ID in its `criteria` array. Your output must be ONLY the JSON array of test cases the prompt's schema describes. **Write that JSON array to `.spectra/generated.json`** with the Write tool.

Update `.spectra/progress.json`: `{"active":6}`

### Step 7 — Ingest (fail-loud)

```
spectra ai ingest-tests {suite} --from .spectra/generated.json --output-format json
```
- Exit 0 → `{ persisted, ids }`. The tests are written and the index updated. Proceed to the MANDATORY critic step.
- Exit 5 (`EMPTY_CONTENT`/`MALFORMED_JSON`/`TRUNCATED`/`NO_TESTS`) or exit 6 (`SCHEMA_INVALID`) → **fail loud, nothing was persisted.** Read the `error_code` + `errors`, regenerate addressing that *specific* error, rewrite `.spectra/generated.json`, and re-ingest. **Bounded: at most 2 attempts.** If it still fails, STOP and report the error; never present unverified tests as done.

---

## MANDATORY: verify every generated test with the critic subagent

Verification is **mandatory and explicit — never skipped, never auto-invoked**. The `ingest-tests` boundary only validates schema; the `spectra-critic` subagent (a fresh, isolated `context: fork`) is the single critic of record for grounding. A test is not accepted until its critic step has passed.

For EACH id in the `ingest-tests` `ids` list:

For each id in the `ingest-tests` `ids` list, update `.spectra/progress.json` before invoking the critic:
```json
{"phases":["Step 1 — Compile analysis","Step 2 — Identify behaviors","Step 3 — Ingest analysis","Step 4 — Review (waiting for approval)","Step 5 — Compile generation","Step 6 — Generate tests","Step 7 — Ingest","Step 8 — Critic","Step 9 — Report"],"active":7,"loop":{"current":i,"total":N,"label":"critic for <id>"}}
```

### Step 8 — Invoke the `spectra-critic` subagent

Invoke it with the Task tool, passing only the **suite name + the test id** (from the `ingest-tests` `ids` list) and its source docs — no generator state, and **never a hand-built file path**. The subagent compiles the prompt (`spectra ai compile-critic-prompt --suite {suite} --test {id}`, which resolves the id→path from `_index.json` on disk), renders a JSON verdict in-session, and ingests it (`spectra ai ingest-verdict --from .spectra/verdicts/critic-verdict.json --output-format json`). Act on the gate:
- gate `pass` (verdict `grounded` / `partial`) → keep the test.
- gate `drop` (verdict `hallucinated`) → remove it: `spectra delete {id} --force --no-interaction --output-format json --verbosity quiet`.
- ingest exit `5` (empty), exit `6` (missing/unparseable `verdict`/`score` — **damage**), or compile exit `4` (refused) → **fail loud**, NOT a pass. Regenerate that single test addressing the *specific* error and re-verify. **Bounded by the retry limit (2 attempts).** If it still fails at the limit, STOP and report the failing test and the specific error; never keep an unverified test.

Update `.spectra/progress.json`: `{"active":8}` (marks Step 9 active — complete sentinel is `active >= 9`).

### Step 9 — Report

"Generated **{kept}** verified test cases ({dropped} dropped as hallucinated, {failed} unresolved)." List the kept ids. If the ingest output contains a non-blocking `notes` entry (e.g. criteria coverage), surface it verbatim here. If kept < requested, say "Run again to generate more." Never present a test as accepted unless its critic step passed.

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
| Update test cases | `spectra-update` | `spectra ai update --suite {suite} --no-interaction --output-format json --verbosity quiet` |
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
