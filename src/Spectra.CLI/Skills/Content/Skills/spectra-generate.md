---
name: spectra-generate
description: Generates test cases from documentation with AI verification and gap analysis.
tools: [{{GENERATE_TOOLS}}]
model: GPT-4o
disable-model-invocation: true
---

# SPECTRA Test Generation

You generate test cases by running CLI commands. Follow the EXACT tool sequence below — no extra tool calls.

**ALWAYS follow the full analyze → approve → generate flow. Never skip the analysis step.**

**CRITICAL: First open `.spectra-progress.html` in Simple Browser — it auto-refreshes so the user can watch progress live. Then runInTerminal. Between runInTerminal and awaitTerminal, do NOTHING — no readFile, no listDirectory, no checking terminal output, no status messages. The progress page already shows live status. You ONLY read `.spectra-result.json` AFTER awaitTerminal returns.**

## CLI flags reference

| Flag | Type | Description |
|------|------|-------------|
| `--suite {name}` | string | Target suite name (REQUIRED) |
| `--count {n}` | int | Number of tests to generate (default: 5) |
| `--focus {text}` | string | Focus area: "negative", "edge cases", "high priority security", etc. |
| `--skip-critic` | bool | Skip grounding verification |
| `--analyze-only` | bool | Only analyze, don't generate |
| `--dry-run` | bool | Preview without writing |

**There is NO `--priority`, `--type`, or `--category` flag.** Use `--focus` for ALL filtering by type, priority, or category. Capture the user's FULL intent in `--focus` — don't split or drop parts. Examples:
- "generate 15 negative tests" → `--focus "negative tests"` `--count 15`
- "generate high priority edge cases" → `--focus "high priority edge cases"`
- "generate security tests only" → `--focus "security tests"`
- "cover its acceptance criteria" → `--focus "acceptance criteria"`
- "happy path tests covering acceptance criteria" → `--focus "happy path acceptance criteria"`

## When user asks to generate test cases:

**Determine focus**: Extract the user's full intent into a `--focus` value. Include ALL qualifiers (type + topic). If no focus, omit `--focus`.

**Step 1**: show preview .spectra-progress.html?nocache=1

**Step 2** — runInTerminal (include `--focus` if user specified any filtering):
```
spectra ai generate --suite {suite} --analyze-only [--focus "{focus}"] --no-interaction --output-format json
```

**Step 3** — awaitTerminal. Do NOTHING else until this completes. Do NOT type anything into the terminal.

**Step 4** — readFile `.spectra-result.json` — check `status`:
- `"failed"` → tell user the `error`.
- `"analyzed"` → show this:

**{analysis.already_covered}** tests already exist. I recommend generating **{analysis.recommended}** new test cases:

- Happy Path: {breakdown.HappyPath}
- Negative: {breakdown.Negative}
- Edge Case: {breakdown.EdgeCase}
- Security: {breakdown.Security}
- Performance: {breakdown.Performance}

Shall I proceed?

STOP. Wait for user.

---

## After user approves:

**Step 5** — runInTerminal (keep the SAME `--focus` from analysis):
```
spectra ai generate --suite {suite} --count {count} [--focus "{focus}"] --no-interaction --output-format json
```

**Step 6** — awaitTerminal. Do NOTHING else until this completes. Do NOT type anything into the terminal.

**Step 7** — readFile `.spectra-result.json` — check `status`:
- `"failed"` → tell user the `error`.
- `"completed"` → "Generated **{generation.tests_written}** test cases." List `files_created`. If tests_written < tests_requested, say "Run again to generate more."
