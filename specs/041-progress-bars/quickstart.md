# Quickstart: Manual Verification

After implementation, verify Spec 041 with these manual checks. Each maps to a Success Criterion.

## Prereqs

- A configured SPECTRA project with at least one suite (e.g. `checkout`) that has indexed docs.
- A working AI provider config in `spectra.config.json` (defaults from `spectra init` are fine).

## 1. Terminal generation + verification (SC-001, SC-002)

```bash
spectra ai generate checkout --count 40
```

**Expect**:

- Within 2 seconds of generation phase starting, a `Generating tests` progress bar appears.
- Bar advances after each batch completes (e.g. `10/40 → 20/40 → 30/40 → 40/40`) with a "batch N/M" detail.
- After generation finishes, a second `Verifying tests` bar appears and increments by 1 per critic call, showing the most recent `TC-XXX` and verdict.
- After completion, both bars are gone and the standard run summary panel is shown. Subsequent shell prompt is uncorrupted (SC-006).

## 2. JSON / quiet mode suppression (SC-003)

```bash
spectra ai generate checkout --count 10 --output-format json > result.json
cat result.json | jq .
```

**Expect**: stdout parses as strict JSON. No ANSI escape characters. `result.json` contains a `runSummary` and no `progress` field (SC-005).

```bash
spectra ai generate checkout --count 10 --verbosity quiet
```

**Expect**: silent run, no progress bars on stdout/stderr.

## 3. Non-TTY suppression (SC-003)

```bash
spectra ai generate checkout --count 10 | tee out.log
```

**Expect**: `out.log` contains no ANSI escape sequences (since stdout is piped, the bar is suppressed).

## 4. Progress page reflects per-test state (SC-004)

In one terminal:

```bash
spectra ai generate checkout --count 40 --output-format json > /dev/null
```

In a browser, open `.spectra-progress.html` and refresh (or wait 2s):

**Expect**:

- A "Generating tests" progress section with a filled bar and "Batch N of M" detail.
- After generation finishes, a "Verifying tests" section becomes active and advances per critic call.
- After completion, the page shows the run summary; no leftover progress bars.

## 5. `--skip-critic` (FR / SC-002)

```bash
spectra ai generate checkout --count 10 --skip-critic
```

**Expect**: only the generation bar appears. No empty/idle verification bar.

## 6. Update command (FR-005)

```bash
spectra ai update checkout
```

**Expect**: an `Updating tests` progress bar appears, advances per applied proposal, clears on completion.

## 7. Failure cleanup (SC-006)

Force a failure (e.g. invalid model in config). After the failure:

**Expect**: progress bar stops cleanly at its last value, terminal cursor restored, final `.spectra-result.json` has no `progress` field.
