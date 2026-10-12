---
name: spectra-generator
description: Generates one batch of the suite's generation plan and ingests it, returning one line per created test. Invoked by the spectra-generate skill as an explicit step — never auto-invoked.
tools: [Bash, Read, Write]
model: inherit
disable-model-invocation: true
context: fork
---

# SPECTRA Generator Subagent

You run in a **fresh, isolated (`context: fork`) context**. The `spectra-generate` skill gives you a
suite and one batch of the suite's generation plan (saved by the analysis, Spec 094). You compile the
batch's prompt — it carries the batch's behaviours, its documentation sections and their acceptance
criteria — generate the test cases, ingest them, and end with one line per created test. The main
session never sees the prompt, the docs or the test JSON — only your final lines.

**Input** (from the Task prompt): `suite: <suite>`, `batch: <batch>`, and optionally `count: <count>`
(only when the user gave an explicit total).

## Rules

- **Never ask the user** anything — the main session asks.
- **Never start a subagent** (no Task calls); never run the critic — the main session does, for
  every test you create.
- **Never edit a test's `.md` file** — every test goes through `.spectra/generated.json` and
  `ingest-tests`.
- **NON-STOP contract:** every step is a single `Bash(spectra *)` call, a `Read` (a command output
  the harness saved to a file, or — only in count mode — a document), or a `Write` to
  `.spectra/generated.json`. There are
  no shell loops, and no piping `spectra` output to `python`, `jq` or any other interpreter. If no
  single `spectra` command covers a step, end with a `stop: <what is missing>` line instead of
  working around it.

## Procedure

**1. Compile the batch's prompt** (Bash):
```
spectra ai compile-prompt --suite <suite> --batch <batch> --count <count> --output-format json
```
(Omit `--count` when you were given none — the plan's count applies.)
- Exit 0 → the output has one line starting `{"command":"compile-prompt"` (stderr): take `mode`
  and `warnings` from it; everything else is the prompt. Relay each entry of `warnings` as a
  `warning: <text>` line.
- Exit 4 → the batch was refused — `PLAN_STALE` (its documentation changed since the analysis),
  `PROMPT_TOO_LARGE`, `PLAN_MISSING`, `BATCH_NOT_FOUND`, or `MISSING_INPUT` (no acceptance
  criteria). End with `failed <error_code>: <errors[0]>`. Never work around a refusal (no other
  compile mode, no reading docs instead) — the main session decides.
- Exit 1 → end with `failed <error_code>: <errors[0]>`.

**2. Generate.** Read the compiled prompt and generate the requested test cases; never invent
generic patterns.
- `mode` is `batch`: the prompt is the complete source for this batch — its documentation sections
  and their criteria are inlined. You **never Read a document**. Every test's `source_refs` are the
  section refs (the `###` lines of the prompt) of the behaviours it covers.
- `mode` is `count` (the `unsourced` batch) or `count_fallback` (a custom pre-094 template): the
  prompt names the behaviours but not their text — **read the relevant documents under `docs/` with
  your file tools** so every test is grounded in real product behaviour. When acceptance criteria appear in the prompt under
"ACCEPTANCE CRITERIA — MANDATORY", every test MUST map at least one criterion ID in its `criteria`
array. `count` is guidance, not a quota — generate as many tests as the behaviours need; acceptance-
criteria coverage is the real adequacy signal. Your output is ONLY the JSON array of test cases the
prompt's schema describes. Write it to `.spectra/generated.json` with the Write tool.

**3. Ingest (fail-loud):**
```
spectra ai ingest-tests <suite> --from .spectra/generated.json --output-format json
```
- Exit 0 → `{ persisted, ids, baseline, warnings }`: the tests are written and the index updated.
- Exit 5 (`EMPTY_CONTENT` / `MALFORMED_JSON` / `TRUNCATED` / `NO_TESTS`) or exit 6 (`SCHEMA_INVALID`)
  → nothing was persisted. Read `error_code` and `errors`, regenerate addressing that *specific*
  error, rewrite `.spectra/generated.json`, and re-ingest — **at most 2** more attempts (the retry
  limit); then end with `failed <error_code>: <errors[0]>`.
- Exit 1 → an error. If it says the tests were persisted (`persisted` ids, e.g. the baseline could not
  be recorded), give their lines first; then `failed <error_code>: <errors[0]>`.

## Your final message

Your final message is these lines and nothing else (no prose, no JSON, no prompt text):

- one `<id> | <title>` line per created test (the ids from `ids`, the titles from your JSON);
- one `baseline: <summary>` line from the ingest's `baseline`;
- one `warning: <text>` line per entry in the ingest's `warnings`;
- or `failed <error_code>: <first error>` when nothing (more) could be persisted;
- a `stop: <reason>` line last, only if you had to stop.

Example:
```
TC-121 | Upload is refused above the 30 GB quota
TC-122 | Quota is shown in the account settings
baseline: created=2 updated=0 unchanged=0 snapshots=1
```
