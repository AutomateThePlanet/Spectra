---
name: spectra-generate
description: Generates test cases from documentation with AI verification and gap analysis.
tools: [{{GENERATE_TOOLS}}]
---

# SPECTRA Test Generation

You generate test cases by running CLI commands. Follow the EXACT tool sequence below — no extra tool calls.

**ALWAYS follow the full analyze → approve → generate flow. Never skip the analysis step.**

## MANDATORY: Analyze first, every time

If the user is asking you to **generate, create, add, write, or build test cases for an area, feature, module, suite, page, or topic** — you **MUST** start with the analysis step (`--analyze-only`), present the recommendation, and **STOP and wait for the user to approve** before generating anything.

**These trigger phrases ALL require the analyze-first flow:**
- "create test cases for {X}"
- "generate test cases for {X}"
- "generate test cases for {X}"
- "add test cases to {X}"
- "test the {X} module"
- "I need test cases for {X}"
- "cover {X} with test cases"
- "write test cases for {X}"

**Forbidden behaviors when the user names an area:**
- Do NOT call `spectra ai generate` without `--analyze-only` on the first call.
- Do NOT invent a `--count` value. There is no default — if the user didn't say a number, you DO NOT pass `--count` at all on the analyze call.
- Do NOT skip Steps 1–4 below.
- Do NOT ask the user how many test cases they want — the analyze step will recommend a number.

The ONLY time you skip analysis is when the user describes a single concrete scenario (see "When the user wants to create a specific test case" further down).

**CRITICAL: First open `.spectra-progress.html?nocache=1` — it auto-refreshes so the user can watch progress live. Then run the command with the Bash tool. While it runs, do NOTHING — don't poll the terminal, list directories, or read files; the progress page already shows live status. You ONLY read `.spectra-result.json` AFTER the command finishes.**

## CLI flags reference

| Flag | Type | Description |
|------|------|-------------|
| `--suite {name}` | string | Target test suite (REQUIRED) — directory under `test-cases/` where new tests will land. |
| `--doc-suite {id}` | string | **Spec 040 (1.51.2+)** Doc-suite ID from the manifest (`spectra docs list-suites`). Filters which documentation feeds the analyzer. **Required when `--suite` does not match a doc-suite ID** — otherwise the pre-flight budget check (default 96K tokens) will reject the run. |
| `--count {n}` | int | Number of test cases to generate. NEVER invent a value — use ONLY a number the user explicitly stated, or the `recommended` field returned by the analyze step. |
| `--focus {text}` | string | Focus area: "negative", "edge cases", "high priority security", etc. |
| `--skip-critic` | bool | Skip grounding verification |
| `--analyze-only` | bool | Only analyze, don't generate |
| `--include-archived` | bool | **Spec 040** Include suites flagged `skip_analysis: true` (Old/, legacy/, archive/, release-notes/) in analyzer input |
| `--dry-run` | bool | Preview without writing |

### Pre-flight token-budget check (Spec 040, exit code 4)

Before any AI call, Spectra estimates the analyzer prompt size against `ai.analysis.max_prompt_tokens` (default 96K). If the estimate exceeds the budget, the command exits with code 4 and an actionable message naming every candidate doc-suite + token cost. Two recovery paths:

1. **Narrow with `--doc-suite {id}`** — re-run targeting one doc-suite that fits the budget. Run `spectra docs list-suites` to discover IDs.
2. **Raise the budget** — bump `ai.analysis.max_prompt_tokens` in `spectra.config.json` (within the model's context-window capacity).

**There is NO `--priority`, `--type`, or `--category` flag.** Use `--focus` for ALL filtering by type, priority, or category. Capture the user's FULL intent in `--focus` — don't split or drop parts. Examples:
- "generate 15 negative test cases" → `--focus "negative tests"` `--count 15`
- "generate high priority edge cases" → `--focus "high priority edge cases"`
- "generate security test cases only" → `--focus "security tests"`
- "cover its acceptance criteria" → `--focus "acceptance criteria"`
- "happy path tests covering acceptance criteria" → `--focus "happy path acceptance criteria"`

## When user asks to generate test cases:

**Determine focus**: Extract the user's full intent into a `--focus` value. Include ALL qualifiers (type + topic). If no focus, omit `--focus`.

**Determine count for the LATER generate step**:
- If the user said an explicit number ("generate 10 test cases", "give me 3"), use that.
- Otherwise leave `count` blank for now — Step 4 will give you `analysis.recommended` to use in Step 5.
- NEVER fall back to "5". There is no default.

**Step 1**: Open `.spectra-progress.html?nocache=1` so the user can watch progress live.

**Step 2** — Run with the Bash tool (include `--focus` if user specified any filtering):
```
spectra ai generate --suite {suite} --doc-suite {docSuite} --analyze-only [--focus "{focus}"] --no-interaction --output-format json
```

**Step 3** — Wait for the command to finish. Do NOTHING else until it completes.

**Step 4** — Read `.spectra-result.json` — check `status`:
- `"failed"` → tell user the `error`.
- `"analysis_failed"` → DO NOT show a recommendation. The recommended count is a fallback default, NOT a real analysis. Show the `message` field verbatim, and ask the user whether to bump `ai.analysis_timeout_minutes` in `spectra.config.json` and re-run, or proceed with the fallback count anyway. STOP and wait for the user to decide. Do NOT auto-proceed to generation.
- `"analyzed"` → show this:

**{analysis.already_covered}** test cases already exist. I recommend generating **{analysis.recommended}** new test cases:

Category breakdown (read keys directly from `analysis.breakdown`, e.g. `happy_path`, `boundary`, `negative`, `edge_case`, `security`, `error_handling`, plus any custom categories):

- {category}: {count} (one bullet per non-zero entry in `analysis.breakdown`)

ISTQB technique breakdown (read from `analysis.technique_breakdown` — keys are `BVA`, `EP`, `DT`, `ST`, `EG`, `UC`; show only non-zero entries; the section may be empty for legacy responses):

- BVA (Boundary Value Analysis): {count}
- EP (Equivalence Partitioning): {count}
- DT (Decision Table): {count}
- ST (State Transition): {count}
- EG (Error Guessing): {count}
- UC (Use Case): {count}

Shall I proceed?

STOP. Wait for user.

---

## After user approves:

**Step 5** — Run with the Bash tool (keep the SAME `--focus` from analysis). Pass `--skip-critic`: in-session verification is performed by the mandatory `spectra-critic` subagent step below, which is the single critic of record — not the in-process critic.
```
spectra ai generate --suite {suite} --doc-suite {docSuite} --count {count} [--focus "{focus}"] --skip-critic --no-interaction --output-format json
```

**Step 6** — Wait for the command to finish. Do NOTHING else until it completes.

**Step 7** — Read `.spectra-result.json` — check `status`:
- `"failed"` → tell user the `error`.
- `"completed"` → proceed to the MANDATORY critic verification below, then report. Collect `generation.files_created`.
- If `token_usage` is present, include a one-line cost/usage summary from `token_usage.total.total_tokens` and `token_usage.cost_display` (e.g. "Token usage: **89K tokens** in {run_summary.duration_seconds}s. Cost: {token_usage.cost_display}"). Do NOT invent numbers if the field is absent.
- **Notes surfacing (Spec 048)**: if `notes` is present and non-empty, render each entry verbatim as a short note after the results summary. Notes describe situations the user should know about (e.g. no acceptance criteria matched the suite) but are NOT failures — `status` will still be `completed`. Do NOT prompt or block on a note.

## MANDATORY: verify every generated test with the critic subagent

Verification is **mandatory and explicit — never skipped, never auto-invoked**. Generation ran with
`--skip-critic`, so the `spectra-critic` subagent (a fresh, isolated `context: fork`) is the single
critic of record. A test is not accepted until its critic step has passed.

For EACH file in `generation.files_created`:

**Step 8** — Invoke the `spectra-critic` subagent with the Task tool, passing only the test file path
and its source docs (no generator state). It compiles the prompt
(`spectra ai compile-critic-prompt`), renders a JSON verdict, and ingests it
(`spectra ai ingest-verdict`). Act on the gate:
- gate `pass` (verdict `grounded` / `partial`) → keep the test.
- gate `drop` (verdict `hallucinated`) → remove it: `spectra delete {id} --force --no-interaction --output-format json --verbosity quiet`.
- ingest exit `5` (empty), exit `6` (missing/unparseable `verdict`/`score` — **damage**), or compile
  exit `4` (refused) → **fail loud**, NOT a pass. Regenerate that single test addressing the
  *specific* error and re-verify. This is **bounded by the retry limit** (default 2 attempts) — if it
  still fails at the limit, STOP and report the failing test and the specific error; never keep an
  unverified test.

**Step 9** — Report: "Generated **{kept}** verified test cases ({dropped} dropped as hallucinated,
{failed} unresolved)." List the kept `files_created`. If kept < requested, say "Run again to
generate more." Never present a test as accepted unless its critic step passed.

---

## When the user wants to create a specific test case

Use this flow when the user describes a concrete test scenario — a behavior they want captured as a test case. **Do NOT run analysis. Do NOT ask how many test cases to generate. This always produces exactly 1 test case.**

**Step 1**: Open `.spectra-progress.html?nocache=1` so the user can watch progress live.

**Step 2** — Run with the Bash tool:
```
spectra ai generate --suite {suite} --doc-suite {docSuite} --from-description "{description}" --context "{context}" --no-interaction --output-format json --verbosity quiet
```

- `{suite}` — target suite name (ask the user only if not obvious from context)
- `{description}` — the user's test scenario description (verbatim)
- `{context}` — optional additional context (page, module, flow); omit `--context` if not given

**Step 3** — Wait for the command to finish. While it runs, do NOTHING — don't poll the terminal, list directories, or read files; just wait for it to complete.

**Step 4** — Read `.spectra-result.json`.

**Step 5** — Present the result. From the JSON, show:
- Test ID and title (from `files_created` or `generation`)
- Suite it was added to
- Linked acceptance criteria, if any (from the test's `criteria` field). When the suite has matching criteria, the from-description flow injects them as the mandatory mapping instruction (Spec 050), so the test's `criteria` field is populated and the test counts toward acceptance-criteria coverage.
- Grounding verdict (will be `manual` — from-description runs no critic, so the test is excluded from grounded statistics even when its `criteria` field is populated; populating criteria is not verification)
- Any duplicate warnings
- **Notes (Spec 048)**: if the JSON includes a `notes` array, render each entry verbatim as a short non-blocking note after the result. Notes are informational (e.g. "no acceptance criteria matched suite '{suite}'"); they do not signal failure.

---

## How to choose between generation flows

| User intent | Signal | Flow |
|-------------|--------|------|
| Explore a feature area | "Generate test cases for...", "Test the... module", "Cover... error handling" | Main generation flow (with `--focus`) |
| Create a specific test case | "Add a test case for...", "Create a test case where...", "I need a test case that verifies..." | From-description flow |
| Generate from previous suggestions | "Generate from suggestions", "Use the previous suggestions" | `--from-suggestions` flow |

**Key rule**: If you can read the user's request as a single test case title, it's `--from-description`. If it's a topic to explore, it's `--focus`. Do NOT ask the user about count or scope to disambiguate — the topic-vs-scenario shape is the only signal you need.

---

## Cancel the current run

If the user says "stop", "cancel", "kill it", "stop the analysis", "stop generating":

**Step 1** — Run with the Bash tool:
```
spectra cancel --no-interaction --output-format json --verbosity quiet
```

**Step 2** — Wait for the command to finish, then Read `.spectra-result.json`.

**Step 3** — Report what happened:
- `status: completed` with `shutdown_path: cooperative` → "Cancelled at phase {phase}. Tests/files written before stopping are preserved."
- `status: completed` with `shutdown_path: forced` → "Force-killed after grace window."
- `status: no_active_run` → "Nothing was running."

If the original command's progress page is still open, point the user at it — it now shows the "Cancelled" terminal phase.
