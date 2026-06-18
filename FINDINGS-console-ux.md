# Findings: Execution Console UX Investigation

**Date:** 2026-06-18  
**Repo:** `C:\SourceCode\Spectra`  
**Mode:** Read-only. No code edited.  
**Tags:** CONFIRMED (file:line read) | INFERRED (reasoning from available evidence)

---

## Console File Layout

The console transport is complete and lives under:

```
src/Spectra.CLI/Commands/Run/WebConsole/
  ConsoleCommand.cs      — launch / stop / serve dispatch (detached worker model)
  ConsoleEndpoints.cs    — transport-free endpoint logic (GET /current, POST /advance|note|finalize|screenshot)
  ConsoleServer.cs       — HttpListener host; routes HTTP into ConsoleEndpoints calls
  ConsolePage.cs         — single-file inline HTML+CSS+JS (~200 lines, C# string literal)
  ConsoleMarker.cs       — .execution/console.json PID/port marker

src/Spectra.Execution/
  Execution/ExecutionEngine.cs       — engine (RetestAsync, etc.)
  Storage/ExecutionDb.cs             — SQLite schema
  Storage/ResultRepository.cs        — result read/write
  Reports/ReportWriter.cs            — HTML report generator
```

Key architecture notes:
- **Server**: `HttpListener`-based, not ASP.NET/Kestrel.
- **Page**: C# string literal embedded in `ConsolePage.cs`. No external static files.
- **Poll interval**: 1800 ms (`ConsolePage.cs:198`).
- **Routes**: `/`, `/current`, `/advance`, `/note`, `/finalize`, `/screenshot` — no static-file route.

---

## Issue-by-Issue Root Causes

### BLOCKER 1 — Comment field deletes text while typing

**CONFIRMED** (`ConsolePage.cs:115–144, :198`)

`setInterval(refresh, 1800)` fires every 1800 ms. Inside `refresh()`, `render(d)` unconditionally replaces `$('panel').innerHTML` with a fresh HTML string that contains a new `<textarea id="comment">` with no value. The existing DOM node is destroyed and its unsaved text is discarded.

There is no "preserve focused/dirty input" guard. The `busy` flag (`ConsolePage.cs:88`) only prevents duplicate `advance()` network calls — it does NOT suppress the background poll.

**Fix routing:** Render-side JS.  
Options: (a) snapshot `$('comment').value` and `document.activeElement` before `innerHTML =`, restore after render; (b) skip re-render when `document.activeElement.id === 'comment'` and value is non-empty; (c) refactor `render()` to do targeted DOM updates, leaving the textarea node in place.

---

### BLOCKER 2 — Final report not rendered at console URL after finalize

**CONFIRMED** (`ConsoleEndpoints.cs:150–175`, `ConsoleServer.cs:64–72`, `ConsolePage.cs:165–172`)

After `POST /finalize`, the server returns `{ runStatus, report }` where `report` is only `Path.GetFileName(htmlPath)` — a bare filename (`ConsoleEndpoints.cs:169`). The console JS `finalize()` function (`ConsolePage.cs:165–172`) shows a toast with the filename, then calls `refresh()`.

`GET /current` now returns `{ runStatus: "none" }` because `GetActiveRunByUserAsync` only returns `Running`/`Paused` runs — a finalized (`Completed`) run is excluded. `render(d)` then writes the "No active run" message.

The report is written to `.execution/reports/{filename}`. `ConsoleServer` has no route to serve files from that directory — only the six hardcoded routes are registered (`ConsoleServer.cs:64–72`). The report cannot be accessed through the console URL.

**Fix routing:** Server-side (add `GET /reports/{filename}` static-file route) + render-side (after finalize, navigate to or embed the report instead of falling back to "no run" state).

---

### FRICTION 3 — Steps rendered comma-joined, not line-by-line

**CONFIRMED** (`ConsolePage.cs:126, :147`, `TestCase.cs:127`, `ConsoleEndpoints.cs:72`)

`TestCase.Steps` is `IReadOnlyList<string>` (`TestCase.cs:127`). `BuildCurrentAsync` passes it directly as `steps = tc?.Steps` (`ConsoleEndpoints.cs:72`), so the browser receives a proper JSON array.

The page's generic `fld(k, v)` renderer calls `esc(v)` on the array value (`ConsolePage.cs:126, :147`). JavaScript coerces an array to a string via `.toString()`, which joins elements with commas: `["Step 1","Step 2","Step 3"]` → `"Step 1,Step 2,Step 3"`.

The array structure is correct at every layer up to the JS renderer. The loss of line-structure happens only in the browser.

**Fix routing:** Render-side JS only. Add an array-aware branch for the steps field (render `<ol><li>` per step). No data, server, or ingest change needed.

---

### FRICTION 4 — "Blocked By: Unknown" in report despite known executor

**CONFIRMED** (`ReportWriter.cs:344`, `ExecutionEngine.cs:317–321`, `ResultRepository.cs:165–188`)

**Note:** "Blocked By:" appears only in the HTML report, not in the console page itself.

`ReportWriter.cs:344`:
```csharp
<div class=""detail-row""><strong>Blocked By:</strong> {Escape(t.BlockedBy ?? "Unknown")}
```

`t.BlockedBy` stores the **test ID of the blocking dependency** (e.g. `TC-045`), not a person's name. It is set only by the engine's automatic dependency-propagation path (`ExecutionEngine.cs:317–321`): when a dependency fails, downstream tests are blocked with `blockedBy: testId`.

When a tester manually records `--status blocked` (via CLI or console BLOCKED button), the engine's advance path does NOT populate `blocked_by` — it is only populated by the dependency-propagation code path. No `recorded_by`/actor field exists anywhere in the schema.

**Fix routing:** Two-level fix:
1. **Quick win (report template):** Change label to "Blocked By Test:" and emit "(manual block)" when `blocked_by` is null. Render-side only.
2. **Full fix (data):** Add `recorded_by` column to `test_results`, write `identity.GetCurrentUser()` at advance time. Server-side + schema change.

---

### FRICTION 5 — Screenshot attach is drag-only, no file picker

**CONFIRMED** (`ConsolePage.cs:68–70, :174–195`)

The drop zone HTML is a bare `<div>`:
```html
<div class="drop" id="drop">Drop or paste a screenshot here to attach it</div>
```

There is no `<input type="file">` anywhere in the page. `wireDrop()` (`ConsolePage.cs:174–180`) wires only `dragover`, `dragleave`, `drop`. A `paste` handler covers clipboard images (`ConsolePage.cs:182–185`). A file picker is entirely absent — not present-but-unwired.

**Fix routing:** Render-side HTML/JS only. Add a hidden `<input type="file" accept="image/*">`, a visible "Browse…" button, and wire its `onchange` to the existing `sendFile()` function. No server change needed.

---

### FRICTION 6 — Screenshots show as links, not viewable/deletable

**CONFIRMED** (`ConsolePage.cs:72, :127`, `ConsoleServer.cs:64–72`, `ResultRepository.cs:263–286`)

Screenshots are stored as a JSON-encoded list of relative paths in the `screenshot_paths` SQLite column (`ResultRepository.cs:263–286`). The page renders them as text spans:
```js
const shots = (t.screenshotPaths || []).map(p => `<span>${esc(p)}</span>`).join('');
```
This produces plain text labels — no `<img>` tags, no hyperlinks, no delete buttons.

Even if `<img src="...">` were added, `ConsoleServer` has no static-file route for the `.execution/reports/` directory — the same gap as BLOCKER 2. There is no delete attachment endpoint in `ConsoleEndpoints.cs`.

**Fix routing:** Server-side (add static-file route) + render-side (change spans to `<img src="/reports/${p}">`). A delete endpoint is a new feature not currently scaffolded.

---

### ENHANCEMENT 7 — No back/edit of already-submitted verdicts

**CONFIRMED** (`ConsoleEndpoints.cs` — no edit/revert endpoint; `ExecutionEngine.cs:446` — `RetestAsync` exists)

The console exposes five endpoints: `GetCurrentAsync`, `AdvanceAsync`, `NoteAsync`, `FinalizeAsync`, `ScreenshotAsync`. There is no `GET /result/{testId}`, `POST /revert`, or `POST /retest` in `ConsoleEndpoints.cs`.

The engine already has `RetestAsync` (`ExecutionEngine.cs:446`) which creates a new attempt row — the correct underlying mechanism. It is exposed at the CLI level (`spectra run retest`) but not wired into `ConsoleEndpoints`.

`GET /current` already returns a `results` array (`ConsoleEndpoints.cs:40–43`) containing past results, but the page's `render()` function ignores it — there is no results-list section rendered in the HTML.

**Fix routing:** Server-side (new `POST /retest` route in `ConsoleEndpoints` + `ConsoleServer`) + render-side (render completed results with per-row Retest buttons). The engine already supports it.

---

### ENHANCEMENT 8 — No progress indicator (X of N executed)

**CONFIRMED** (`ConsoleEndpoints.cs:48–51`, `ConsolePage.cs:104–113`)

`GET /current` already returns both `total` (queue total) and `counts` (dictionary keyed `PASSED`, `FAILED`, `BLOCKED`, `SKIPPED`, `PENDING`, `INPROGRESS`). "Executed" is fully computable from the existing payload:
```
executed = total − counts.PENDING − counts.INPROGRESS
```

The `counts()` JS function (`ConsolePage.cs:104–113`) renders five summary cards but does not compute or display an "X of N executed" fraction or progress bar.

**Fix routing:** Render-side JS only. No server change needed — the data is already in every poll response.

---

### ENHANCEMENT 9 — No keyboard shortcuts for verdict buttons

**CONFIRMED** (`ConsolePage.cs` — full JS reviewed)

There is no `keydown` or `keyup` event listener anywhere in the page. Events wired: `dragover`/`dragleave`/`drop` (drop zone), `paste` (document), `onclick` on buttons only.

Care required: a keyboard handler must check `document.activeElement.tagName !== 'TEXTAREA'` before intercepting P/F/B/S to avoid stealing keystrokes from the comment field.

**Fix routing:** Render-side JS only.

---

### SEAM HYGIENE 10 — Moving verdict files to `.spectra/verdicts/` subfolder

**CONFIRMED** (`src/Spectra.CLI/Skills/Content/Agents/spectra-critic.agent.md:78–81`, `IngestVerdictCommand.cs:41–56`, `ClaudeSettingsInstaller.cs:15`)

The path `.spectra/critic-verdict.json` is defined only in the agent markdown (`spectra-critic.agent.md:78`). No C# code hardcodes this path.

`spectra ai ingest-verdict` reads from `--from <path>` or stdin (`IngestVerdictCommand.cs:41–56`) — the path is fully caller-controlled. The only C# reference to `"critic-verdict"` is a comment in `ClaudeSettingsInstaller.cs:15` explaining why `Write(.spectra/**)` permission is needed.

Moving to `.spectra/verdicts/critic-verdict.json` requires updating only `spectra-critic.agent.md` (and the `Write(.spectra/**)` permission already covers the subdirectory). No C# changes needed.

**Fix routing:** Agent/skill markdown only.

---

## Cross-Cutting Note — Poll Mechanism

The single `setInterval(refresh, 1800)` (`ConsolePage.cs:198`) is the common thread for three issues:

| Issue | How the poll is involved |
|---|---|
| BLOCKER 1 | Poll fires while user types → full `innerHTML =` replace destroys textarea value |
| ENHANCEMENT 7 | `d.results` is returned every poll but `render()` ignores it — no past-verdict UI |
| ENHANCEMENT 8 | `total` + `counts` arrive every poll; not rendered as "X / N executed" |

The root of BLOCKER 1 is the **full `panel.innerHTML =` replacement** at `ConsolePage.cs:128`. Issues 7 and 8 are additive to `render()` — no poll-interval change is needed for them.

A single fix to BLOCKER 1 (preserve dirty/focused inputs before replacement, or switch to targeted DOM updates) will unblock the most critical issue and make 7 and 8 safe to add incrementally.

---

## Summary Table

| # | Label | Severity | Tag | Fix Surface | Key Evidence |
|---|---|---|---|---|---|
| 1 | Textarea clobbered by poll | BLOCKER | CONFIRMED | JS render | `ConsolePage.cs:128,134,198` |
| 2 | No report after finalize | BLOCKER | CONFIRMED | Server route + JS render | `ConsoleEndpoints.cs:169`; `ConsoleServer.cs:64–72`; `ConsolePage.cs:165–172` |
| 3 | Steps comma-joined | FRICTION | CONFIRMED | JS render only | `ConsolePage.cs:126,147`; `TestCase.cs:127` |
| 4 | "Blocked By: Unknown" | FRICTION | CONFIRMED | Report template + optional schema | `ReportWriter.cs:344`; `ExecutionEngine.cs:317–321` |
| 5 | No file picker | FRICTION | CONFIRMED | HTML/JS only | `ConsolePage.cs:68–70, 174–195` |
| 6 | Screenshots as text not thumbnails | FRICTION | CONFIRMED | Server route + JS render | `ConsolePage.cs:72,127`; `ConsoleServer.cs:64–72` |
| 7 | No back/edit of verdicts | ENHANCEMENT | CONFIRMED | New server endpoint + JS render | `ConsoleEndpoints.cs` (absent); `ExecutionEngine.cs:446` |
| 8 | No progress indicator | ENHANCEMENT | CONFIRMED | JS render only | `ConsolePage.cs:104–113`; `ConsoleEndpoints.cs:48–51` |
| 9 | No keyboard shortcuts | ENHANCEMENT | CONFIRMED | JS render only | `ConsolePage.cs` (absent) |
| 10 | Verdict file path hardcoding | SEAM HYGIENE | CONFIRMED | Agent/skill markdown only | `spectra-critic.agent.md:78–81`; `IngestVerdictCommand.cs:41–56` |
