# Findings — Console UX v2 (tail + regressions + new items)

Investigation date: 2026-06-18  
Branch: `claude-code-v2` (commit `0f39c24`)  
Scope: 2 regressions introduced by the Console Polish brief, 3 partial fixes, 3 new findings, 2 developer-friction items.

---

## R1 — Comment textarea cleared too late (regression)

**Severity:** High — the snapshot/restore logic that was supposed to *prevent* clobbering now *perpetuates* it across test transitions.

**Root cause:** `advance()` (`ConsolePage.cs:220-228`) submits notes, then calls `render(res.d.current)` at line 227 without first clearing the textarea. Inside `render()`, the snapshot at lines 173-174 captures `prevComment = $('comment')?.value ?? ''` — which still holds the just-submitted text — and restores it into the *new* test's textarea at lines 195-196. Result: every advance carries the previous test's comment forward.

**Fix (3 lines in `ConsolePage.cs`):** In `advance()`, before the `render()`/`refresh()` call, add:
```js
const c = $('comment'); if (c) c.value = '';
```
The snapshot in `render()` then sees `''` and restores nothing.

---

## R2 — Drag/drop silent after Fix 5 (regression)

**Severity:** High — drag-and-drop was working before Fix 5; now drops are silently consumed.

**Root cause:** Fix 5 nested `<input type="file" id="fp" style="display:none">` *inside* `<div class="drop" id="drop">` (`ConsolePage.cs:189`). A hidden `<input type="file">` is a native OS drop target in Chromium/Firefox. When the user drops a file onto `#drop`, the browser routes the drop event to `#fp` (the innermost eligible drop target), not to the parent div where the `drop` listener is registered. The event is silently consumed — `wireDrop()`'s listener never fires.

**Fix:** Move `<input type="file" id="fp" ...>` outside `#drop` (keep it inside `.test` but not a descendant of the drop zone). This also fixes the latent focus-trap described in P3.

---

## P1 — Screenshot delete not implemented

**Status:** Confirmed absent — no `/screenshot/delete` route in `ConsoleServer.cs`, no delete method in `ConsoleEndpoints.cs`, no remove path in `ResultRepository`.

**Storage:** Screenshots are stored as a JSON array in the `screenshot_paths` column via `ResultRepository.AppendScreenshotPathAsync`. A delete requires: read current array → remove matching path → write back → optional `File.Delete()` on disk.

**Work required:** New `DELETE /screenshot` (or `POST /screenshot/delete`) endpoint + route + engine/repo delegation. Deferred — view-only was the stated scope of this brief.

---

## P2 — Browse picker accepts only one file

**Status:** Confirmed. `ConsolePage.cs:189` — `<input type="file" id="fp" accept="image/*" style="display:none">` has no `multiple` attribute. `wireDrop()` line 255: `if (fp.files[0]) { sendFile(fp.files[0]); fp.value = ''; }` — processes only index 0.

**Fix:** Add `multiple` to the `<input>`, change the onchange handler to:
```js
Array.from(fp.files).forEach(f => sendFile(f)); fp.value = '';
```

---

## P3 — Keyboard shortcuts inert after Browse interaction

**Status:** Confirmed with two causes.

**Primary cause (R2-related):** `<input type="file" id="fp">` is nested inside `#drop`. After `.click()` opens the file dialog and the user cancels or accepts, Chromium/Firefox often return focus to the last focused element — if that was the hidden `#fp`, the keydown guard at `ConsolePage.cs:281` (`if (tag === 'INPUT') return;`) blocks all shortcuts until the user clicks elsewhere. Moving `#fp` outside `#drop` (the R2 fix) also resolves this.

**Secondary cause:** The `?` key opens the help overlay but does not close on Escape — there is no `keydown` handler for `Escape`. The overlay must be dismissed by pressing `?` again. Low friction; no separate fix needed.

---

## N1 — BLOCKED summary card: orange fill + invisible count

**Severity:** Medium — the BLOCKED card is visually broken.

**Root cause:** CSS class collision at `ConsolePage.cs:66`:
```css
.blocked { background: var(--color-blocked); }   /* verdict button */
```
This rule has no selector constraint. `cell('blocked', ...)` at line 145 emits `<div class="card blocked">`, which matches `.blocked` and gets an orange fill. The number is then rendered by `.card.blocked .n { color: var(--color-blocked); }` (line 51) — orange text on orange background, count invisible.

**Fix:** Scope the button background rule to `button.blocked` instead of `.blocked`.

---

## N2 — Validation message hidden below fold

**Severity:** Medium — `#msg` is the last child of `.container` (`ConsolePage.cs:100`), placed after `#results`. With ≥10 completed tests in the results list, the message div is off-screen.

**Fix (two options):**
- *Option A (simple):* Move `<div class="msg" id="msg">` above `<div id="panel">` in the HTML.
- *Option B (better UX):* Make `#msg` `position:fixed` at the top/bottom so it's always visible as a toast — no layout dependency on scroll position.

---

## N3 — Results list has no pagination

**Severity:** Low — `renderResults()` at `ConsolePage.cs:199-209` renders every completed test on every poll with no page boundary. With 50+ tests a run becomes a very long scroll.

**Fix:** Add page state:
```js
let resultsPage = 0;
const RESULTS_PAGE = 10;
```
Slice the completed array, render prev/next buttons. The `retest()` function already calls `refresh()`, so paging state resets on each retest — acceptable.

---

## D1 — Model improvises `mkdir` for verdicts directory

**Severity:** Low — Claude Code's Write tool auto-creates parent directories. When the critic agent writes to `.spectra/verdicts/critic-verdict.json`, no mkdir is needed. The agent is improvising it because it sees an unfamiliar subdirectory path.

**Fix (skill-only):** Add one sentence to `spectra-critic.agent.md` Step 3, after the Write tool instruction:
> "The Write tool creates parent directories automatically — do not run mkdir."

---

## D2 — Model improvises Python to consume `compile-critic-prompt` output

**Severity:** Low — misdirected effort; no correctness risk.

**Root cause:** Confirmed from `CompileCriticPromptCommand.cs:244-248`: when called with `--suite <suite> --test <id>` (single test, no `--output-format json`), the command emits the prompt as **plain text on stdout** — byte-identical to the legacy single-prompt path. No JSON wrapping. The agent should read the output directly from the command's stdout; no file write, no Python parsing is needed.

The `--output-format json` path at line 239 emits `{ "prompts": [{ "id": ..., "prompt": "..." }] }` — the agent is apparently using that flag and then improvising a Python parser to extract `prompts[0].prompt`.

**Fix (skill-only):** Update `spectra-critic.agent.md` Step 1 to make the consumption pattern explicit:
> "Run `spectra ai compile-critic-prompt --suite <suite> --test <id>` (no `--output-format` flag). The plain-text prompt is emitted to stdout. Read it directly from the command output — no file write or parsing step is needed."

---

## Summary table

| ID | Category | Severity | Files | Fix scope |
|----|----------|----------|-------|-----------|
| R1 | Regression | High | `ConsolePage.cs:220-228` | 3 JS lines in `advance()` |
| R2 | Regression | High | `ConsolePage.cs:189` | Move `#fp` outside `#drop` |
| P1 | Partial (absent) | — | ConsoleEndpoints + Server + ResultRepository | New endpoint (deferred) |
| P2 | Partial | Low | `ConsolePage.cs:189,255` | `multiple` attr + loop |
| P3 | Partial | Medium | `ConsolePage.cs:281` | R2 fix resolves primary cause |
| N1 | New | Medium | `ConsolePage.cs:66` | `button.blocked` selector |
| N2 | New | Medium | `ConsolePage.cs:100` | Move or fix `#msg` |
| N3 | New | Low | `ConsolePage.cs:199-209` | Slice + prev/next buttons |
| D1 | Friction | Low | `spectra-critic.agent.md` | One sentence |
| D2 | Friction | Low | `spectra-critic.agent.md` | Clarify stdout consumption |

R1 and R2 are regressions introduced in the Console Polish brief and should be the first items in any follow-up brief. N1 (CSS collision) should travel with R1/R2 as it is a one-line fix. D1 and D2 are skill-only and can be batched with the next agent-markdown pass.
