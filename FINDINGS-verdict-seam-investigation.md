# Findings: Critic Verdict Seam & seam-progress Investigation

**Date:** 2026-06-18  
**Repo:** `C:\SourceCode\Spectra`  
**Mode:** Read-only. No code edited.  
**Tags:** CONFIRMED (file:line read) | INFERRED (reasoning from available evidence)

---

## Q1 — ingest-verdict resolution

### What `ingest-verdict` actually does

**CONFIRMED** (`src/Spectra.CLI/Commands/Generate/IngestVerdictCommand.cs:27–38`)  
`ingest-verdict` has **one and only one input option: `--from <file>`** (or stdin). There is no `--suite`, no `--test`, no test-identity parameter of any kind.

**CONFIRMED** (`IngestVerdictCommand.cs:58–77`)  
The handler calls `VerdictIngestor.Classify(content)` and **writes nothing to disk**. It emits a gate decision (`verdict`, `score`, `drop: true/false`) to stdout and exits. The class comment confirms this explicitly:

> "Unlike those [IngestTestsCommand / IngestCriteriaCommand], it persists nothing (the grounding write-back stays in the reused `CreateTestWithGrounding`), so it needs no config." — `IngestVerdictCommand.cs:14–18`

**CONFIRMED** (`src/Spectra.CLI/Skills/Content/Agents/spectra-critic.agent.md:77–81`)  
The critic agent's specified invocation is:
```
spectra ai ingest-verdict --from .spectra/critic-verdict.json
```
The filename is **fixed** (not per-test), and **no `--suite` or `--test` flags are present**. This is the authoritative spec for correct ingest-verdict usage.

### The compile vs. ingest asymmetry

**CONFIRMED** (`spectra-critic.agent.md:31–43`)  
`compile-critic-prompt` accepts `--suite <suite> --test <id>` to resolve the test artifact path from `_index.json`. It needs these because it performs a test-identity lookup.

**CONFIRMED** (`IngestVerdictCommand.cs:27–38`)  
`ingest-verdict` accepts **no** `--suite`/`--test`. It is intentionally a pure JSON classifier — the test identity was already resolved upstream by `compile-critic-prompt`; the verdict is just a blob of JSON to classify.

Asymmetry summary:

| Command | `--suite` | `--test` | Does test-identity resolution? |
|---|---|---|---|
| `compile-critic-prompt` | ✅ | ✅ | Yes — reads `_index.json` to find the test artifact |
| `ingest-verdict` | ❌ | ❌ | No — pure JSON classifier; no lookup, no write-back |

### How the live run ended up with `--suite`/`--test` on ingest-verdict

**INFERRED** (from reading the generate skill's Step 8 and critic agent's procedure)  
`spectra-generate.md:193` describes the Task tool invocation by saying the critic "ingests it (`spectra ai ingest-verdict`)" without showing the `--from` flag or filename explicitly. The critic agent (`spectra-critic.agent.md`) is the authoritative source for the correct invocation, but the generator skill's shorthand description left the model room to improvise. The model appears to have extrapolated the `--suite`/`--test` flags from the nearby `compile-critic-prompt` signature.

### Fix decision: SKILL-FIX

**CONFIRMED** — the CLI's design is correct and complete. `ingest-verdict` needs no new flags. The per-test writeback is intentionally not the CLI's job (the comment at `IngestVerdictCommand.cs:14` confirms this). The correct fix is:

1. The **`spectra-critic.agent.md`** Step 3 already specifies the correct invocation (`--from .spectra/critic-verdict.json`, no other flags). Make this instruction more defensive by adding an explicit note: "The ONLY valid flags for `ingest-verdict` are `--from` and `--output-format` — do NOT add `--suite` or `--test`."

2. The **`spectra-generate.md`** Step 8 description should echo the correct ingest call form (`spectra ai ingest-verdict --from .spectra/critic-verdict.json`) rather than the bare `spectra ai ingest-verdict` which the model filled in incorrectly.

No CLI change required. The `cp` workaround seen in the live run was model improvisation caused by the ambiguous description, not a CLI gap.

### Sequential call safety (single verdict file)

**CONFIRMED** (`spectra-generate.md:184–195`)  
The generate skill iterates tests **sequentially** — one Task tool call per test, waiting for the result before advancing the loop counter. This makes `.spectra/critic-verdict.json` (a single fixed filename) safe: the critic never runs concurrently, so the file is never clobbered by a parallel call.

---

## Q2 — Verdict file lifecycle / cleanup

### Who writes `critic-verdict*.json`

**CONFIRMED** (`spectra-critic.agent.md:77–78`)  
The `spectra-critic` subagent writes `.spectra/critic-verdict.json` via the **Write tool** (skill prose). No C# code emits this path.

**CONFIRMED** (grep of `critic-verdict` across `src/`)  
The only C# reference to `critic-verdict` is a comment in `ClaudeSettingsInstaller.cs:15` listing it as an example `.spectra/` scratch file: `"critic-verdict.json, etc."` — documentary only, no file emission.

**The per-test filenames (`critic-verdict-TC-NNN.json`) seen in the live run are model improvisation**, not specified by the critic agent. The agent specifies only the fixed name `.spectra/critic-verdict.json`.

### Does any code delete these files

**CONFIRMED** (`src/Spectra.CLI/Commands/Dashboard/DashboardHandler.cs:52–55`)  
`dashboard --clean` only removes the `--output` directory (e.g. `./site`). It has no knowledge of `.spectra/` scratch files.

**CONFIRMED** (grep of `critic-verdict` and `.spectra` cleanup across all C# source)  
No code path deletes or rotates verdict files. There is no `spectra clean` command, no run-start cleanup, no run-end cleanup.

### `.spectra/` ephemeral-vs-durable status

**CONFIRMED** (`src/Spectra.CLI/Commands/Init/InitHandler.cs:383–389`)  
The init-generated `.gitignore` adds these `.spectra/` entries:
```
.spectra.lock
.spectra/.pid
.spectra/.cancel
.spectra/id-allocator.lock
.spectra/id-allocator.json
```

**What is NOT gitignored:** `progress.json`, `analysis.json`, `generated.json`, `updated.json`, `criteria.json`, `critic-verdict.json`, `seam-progress.html`. These scratch files would be git-tracked unless manually excluded.

**CONFIRMED** (`InitSeamProgressCommand.cs:12`, `ClaudeSettingsInstaller.cs:7,15`)  
The code comments call `.spectra/` "scratch" and "throwaway" for the HTML/JSON files, while `.spectra/prompts/` and `id-allocator.json` are intentionally durable. The directory is **mixed** — some entries are ephemeral scratch, others are durable config. No automated lifecycle separation exists.

### Candidate cleanup locations

1. **`InitHandler.UpdateGitIgnoreAsync`** — extend the gitignore entries with `.spectra/*.json` and `.spectra/*.html` (or a `.spectra/` catch-all excluding the `prompts/` subdir). This stops scratch files from being accidentally committed. INFERRED as the right fix location based on where existing `.spectra` entries live.

2. **Post-run skill cleanup** — the generate/criteria/update skills could `rm` or overwrite their own scratch files (analysis.json, generated.json, critic-verdict.json) in the final report step. No CLI command needed; pure skill prose.

3. **A `spectra clean` command** — not currently in scope; INFERRED as a future option.

---

## Q3 — seam-progress regression in merged skill

### Does `spectra-generate` call `init-seam-progress` as step 1?

**CONFIRMED** (`src/Spectra.CLI/Skills/Content/Skills/spectra-generate.md:88–96`)

```
**Progress setup** — before Step 1, initialize the live monitor:
```
spectra ai init-seam-progress
```

Write `.spectra/progress.json` with the Write tool:
```json
{"phases":["Step 1 — Compile analysis","Step 2 — Identify behaviors",...
```
```

The call IS present, in the correct position (before the first `Write .spectra/progress.json`). **There is no source-code regression** in the current working tree.

### Cross-skill comparison

| Skill | Calls `init-seam-progress` before first `Write .spectra/progress.json`? | Lines |
|---|---|---|
| `spectra-generate.md` | ✅ YES | 88–96 |
| `spectra-criteria.md` | ✅ YES | 22–29 |
| `spectra-update.md` | ✅ YES | 30–39 (labeled "Step 0") |

All three seam skills have the correct sequence in source.

### `InitSeamProgressCommand` behavior

**CONFIRMED** (`src/Spectra.CLI/Commands/Ai/InitSeamProgressCommand.cs:37`)  
Writes `.spectra/seam-progress.html` via `SeamProgressPageWriter.WriteAsync(outputPath)`. No dependency on `progress.json` existing — it creates the HTML polling shell unconditionally.

**CONFIRMED** (`InitSeamProgressCommand.cs:25–26`)  
`--no-open` option exists. Default is `noOpen = false` (i.e., browser opens by default).

**CONFIRMED** (`InitSeamProgressCommand.cs:45–47`)  
Browser opens via `Process.Start(new ProcessStartInfo(outputPath) { UseShellExecute = true })` — after `WriteAsync` completes on line 37. The HTML file is guaranteed to exist before the browser is launched; `ERR_UNEXPECTED -9` (open before file exists) **cannot occur from this code path**.

### Intended sequence (CONFIRMED from source)

```
init-seam-progress      → writes + opens .spectra/seam-progress.html (HTML polling shell)
Write progress.json     → {"phases":[...], "active":0}
<step work>
Write progress.json     → {"active":1}
...
Write progress.json     → {"active":9}  (terminal — page renders Complete)
```

This is the contract in all three skill files.

### Most likely explanation for the live run's missing seam-progress.html

**INFERRED** (cannot confirm from source alone)  
Two candidates, in decreasing likelihood:

1. **Stale demo project skill** — the live run may have used a `spectra-generate.md` from a prior pack that predated the `init-seam-progress` addition. The session history notes a stale-Release-binary problem in the 2.0.3 pack (the `--no-build` issue); a similar caching scenario is possible.

2. **Model skip** — the model executing the skill treated the "Progress setup" block as optional setup prose rather than a Bash step to execute. The block is labeled as a setup section, not a numbered step, which may reduce its salience to the model.

Either way: the **source is correct**. The fix, if any, is to make the `init-seam-progress` call more explicit in the skill — e.g., number it as "**Step 0**" (matching `spectra-update.md`'s pattern) so it has the same visual weight as the numbered steps.

---

## Summary table

| Question | Root cause | Fix type |
|---|---|---|
| Q1: ingest-verdict rejected `--suite`/`--test` | Flags extrapolated from `compile-critic-prompt`; skill description ambiguous | **Skill-fix**: make critic agent and generate skill ingest call explicit |
| Q1: `cp` workaround trips allowlist | Model used per-test filenames not in spec; single fixed file is correct | **Skill-fix**: clarify fixed filename in critic agent |
| Q2: verdict files accumulate forever | No lifecycle in code or skill prose; not gitignored | **Two fixes**: gitignore scratch files in init; skill deletes scratch after report |
| Q3: seam-progress.html missing in live run | Source is correct; likely stale demo skill or model skip | **Skill-fix** (optional): label as Step 0 to match visual weight of numbered steps |
