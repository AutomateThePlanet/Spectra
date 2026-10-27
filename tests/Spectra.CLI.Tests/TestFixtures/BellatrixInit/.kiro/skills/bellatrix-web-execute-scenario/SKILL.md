---
name: bellatrix-web-execute-scenario
description: Skill for executing web test scenarios using the bellatrix web CLI to drive a live browser via Selenium and validate every locator before code is generated.
---

# Execute Web Test Scenario

Use the `bellatrix web` CLI to drive a live browser, capture page snapshots, and validate every test step and locator **before** any test code is generated. The CLI runs no model and holds no credentials — all locator intelligence is yours, delegated to the isolated locator specialist.

> **MANDATORY — context economy:** `snapshot` returns only a compact reference to a snapshot on disk. NEVER load a full DOM into this conversation — no `snapshot --inline`, no `get-page-source`, no reading the snapshot file. Delegate locator resolution to the isolated **`bellatrix-web-locator-specialist`** (pass the snapshot `path`), and use `find` for narrow lookups.

Every command prints JSON. Always pass `--json`. The success JSON is the contract; when a command fails, report its error JSON verbatim.

---

## Phase 1: Session Setup

### Browser Driver Backend — Who Owns What

There are two modes, chosen at `start-session`:

| Mode | How | Who runs the driver |
|------|-----|---------------------|
| **Local** (default) | `start-session` without `--remote-url` | The CLI starts a detached local browser driver (chromedriver / geckodriver / msedgedriver) and `close-session` stops it. The browser itself must be installed on the machine. |
| **Remote** | `start-session --remote-url <selenium-server-url>` | A Selenium Grid / standalone server that **the user** runs. |

**FORBIDDEN actions:**
- Starting, stopping, or probing Selenium Grid / standalone servers or browser-driver processes yourself (`java -jar selenium-server…`, `chromedriver`, `Get-Process`, `taskkill`, `pkill`, …)
- "Warming up" or health-checking the driver before calling `bellatrix web start-session`
- Killing a browser window that a session still owns — use `close-session`

**Correct behavior:** call `bellatrix web start-session` directly. If it fails with a connection error against a `--remote-url` (e.g. "could not reach http://127.0.0.1:4444"), the Selenium server is not running — **ask the user to start it**, then retry. If a local start fails because the browser or its driver is missing, report the error JSON and ask the user to install/update the browser. Example message:

> "The CLI cannot reach the Selenium server at http://127.0.0.1:4444. Please start it (or tell me to use a local browser instead) and let me know when it's up."

### Start a New Session

Choose a short `--session` name of your own for this run (e.g. `shop`):

```
bellatrix web start-session --browser chrome --session shop --json
```

Options:

| Option | Meaning |
|--------|---------|
| `--browser chrome\|firefox\|edge` | Browser to launch (default `chrome`) |
| `--headless` | Run without a visible window |
| `--width <px>` / `--height <px>` | Window size (default 1920×1080) |
| `--remote-url <url>` | Use a user-run Selenium server instead of a local driver |

The response is `{ "sessionId", "browser", "serverUrl", "driverServer": "local" | "remote" }`.

The session is addressed by the `--session <name>` you chose, not by the real Selenium `sessionId`. Every later CLI invocation is a **separate process** — no daemon — so pass the same `--session <name>` every time; the CLI re-attaches to the live browser for you. You never copy the real `sessionId` around yourself.

**Multiple sessions in parallel.** To test two users or two browsers in one scenario, call `start-session` once per browser, each under its own `--session` name, and route later calls by that name.

**Idle sessions expire.** `list-sessions` prunes records idle for more than 10 minutes (and stops their local driver). Keep a scenario moving; if a session disappears, start a new one and re-run the steps.

### Navigate and Verify the Page

```
bellatrix web navigate --session shop --url "https://example.com/login" --json
bellatrix web wait-for-page-ready --session shop --json
bellatrix web get-title --session shop --json
bellatrix web get-url --session shop --json
```

---

## Phase 2: Capture Page Snapshots (MANDATORY)

**NEVER skip this step.** Every page, and every state change you interact with (opened modal, expanded menu, new route), MUST be snapshot first.

```
bellatrix web snapshot --session shop --json
```

This returns only a **compact reference** — `{ "path": "<path>", "sessionId": "…", "elementCount": N, "summary": { "interactive": N, "topControlTypes": {…} } }` — never the element tree.

### Analyze the Snapshot — via delegation, NEVER inline

**MANDATORY:** Delegate locator resolution to the isolated **`bellatrix-web-locator-specialist`** subagent. Pass it the element description and the snapshot `path`; it reads the snapshot in its own context and returns a single `{ strategy, selector }`. For your own narrow lookups use `find` against the saved snapshot (driver-free, fast):

```
bellatrix web find --session shop --snapshot <path> --data-testid "login" --json
bellatrix web find --session shop --snapshot <path> --id "email" --json
bellatrix web find --session shop --snapshot <path> --role "button" --json
bellatrix web find --session shop --snapshot <path> --name "username" --json
bellatrix web find --session shop --snapshot <path> --text "Sign in" --json
bellatrix web find --session shop --snapshot <path> --grep "checkout|cart" --json
bellatrix web find --session shop --snapshot <path> --interactive-only --json
```

Use exactly ONE filter per `find`. Matching rules:

| Flag | Matches | Comparison |
|------|---------|------------|
| `--data-testid <v>` | `DataTestid` key | contains, case-insensitive |
| `--id <v>` | `Id` key | contains, case-insensitive |
| `--name <v>` | `Name` key (the HTML `name` attribute) | contains, case-insensitive |
| `--role <v>` | `Role` key (ARIA `role` attribute) | equals, case-insensitive |
| `--text <v>` | `Text`, `AriaLabel`, `Placeholder` | contains, case-insensitive |
| `--grep <regex>` | any of `Id`, `DataTestid`, `Name`, `Role`, `AriaLabel`, `Text`, `Placeholder`, `Href`, `Class`, `Value`, `Tag` | regex, case-insensitive |
| `--interactive-only` | tags `button`, `input`, `select`, `a`, `textarea`, `option` and ARIA roles `button`, `link`, `checkbox`, `radio`, `textbox`, `combobox`, `menuitem`, `tab`, `switch` | — |

Because matching is *contains*, a `find` hit is a candidate, not proof of uniqueness — confirm the chosen selector against the live page (Phase 3) before you rely on it.

Snapshot node keys: `Tag` (lowercase), `Id`, `DataTestid`, `Name`, `Role`, `AriaLabel`, `Text`, `Type`, `Placeholder`, `Href`, `Class`, `Value`.

### Locator Priority (the specialist follows this)

| Priority | Locator | Strategy | Example |
|----------|---------|----------|---------|
| 1 (best) | `data-testid` | `css` | `[data-testid='login-submit']` |
| 2 | Static `id` | `id` / `css` | `email` / `#email` |
| 3 | `name` attribute | `css` | `[name='username']` |
| 4 | `aria-label` | `css` | `[aria-label='Close dialog']` |
| 5 | Label / placeholder | `xpath` / `css` | `//label[normalize-space()='Email']/following-sibling::input`, `input[placeholder='Search']` |
| 6 | Tag + visible text | `xpath` | `//button[normalize-space()='Log In']` |
| 7 | Stable structural CSS | `css` | `form#checkout button[type='submit']` |
| 8 (last) | Positional XPath | `xpath` | `(//table[@id='orders']//tr)[2]` |

**Never use** generated / hashed classes (`css-1a2b3c4d`, `sc-AxjAm`, `jss123`), dynamic ids (`quantity_4e732ced`, `react-select-7-input`, `:r3:`), or framework internals (`ng-star-inserted`, `_ngcontent-…`).

### What a Snapshot Does NOT Contain

- **Hidden elements.** Only rendered, visible elements (not `display:none`, not `visibility:hidden`, non-zero size) with some locator identity are captured. Open the menu/modal/accordion first, then snapshot again.
- **iframe content.** Elements inside an `<iframe>` are not scanned, and element verbs act on the top-level document only. Record the iframe and the element you need; the generated code must switch into the frame.
- **Shadow DOM targets.** Shadow-root children DO appear in the snapshot, but a document-level CSS/XPath cannot reach them. Flag such elements in your findings instead of inventing a selector.

### Optional: Screenshot for Visual Context

```
bellatrix web take-screenshot --session shop --out shot.png --json
```

Always pass `--out` — without it the PNG comes back as base64 inside the JSON and floods the context.

---

## Phase 3: Execute Test Steps

Interact with the page following the test scenario. Every element verb takes `--locator <expr>` and an optional `--strategy css|xpath|id|class|name|tag|linktext|partiallinktext`. When `--strategy` is omitted, a locator starting with `/` or `(/` is treated as XPath, anything else as CSS. (`--selector` is a legacy alias of `--locator`.)

### Type Text
```
bellatrix web type-text --session shop --locator "[data-testid='email']" --text "user@example.com" --json
```
`type-text` clears the field first, then sends the keys.

### Click Elements
```
bellatrix web click --session shop --locator "//button[normalize-space()='Log In']" --json
```

### Clear Fields
```
bellatrix web clear --session shop --locator "email" --strategy id --json
```

### Choose a Dropdown Option (native `<select>`)
```
bellatrix web select --session shop --locator "#country" --value "Bulgaria" --json
```
`--value` is the option's **visible text**, not its `value` attribute. Custom (non-`<select>`) dropdowns are driven with `click` on the trigger, a fresh `snapshot`, then `click` on the option.

### Read Values
```
bellatrix web get-text --session shop --locator "[data-testid='welcome']" --json
bellatrix web get-attribute --session shop --locator "#email" --attribute value --json
bellatrix web get-attribute --session shop --locator "#terms" --attribute checked --json
```

### Check State
```
bellatrix web is-visible --session shop --locator "[data-testid='error']" --json
bellatrix web is-enabled --session shop --locator "//button[@type='submit']" --json
```

### Wait for Asynchronous UI
```
bellatrix web wait-for-element --session shop --locator "[data-testid='results']" --timeout-ms 10000 --json
bellatrix web wait-for-page-ready --session shop --json
```
`wait-for-element` reports `{ "success": false, "found": false, … }` on timeout — check `found`, do not assume. `wait-for-page-ready` blocks until `document.readyState` is `complete`.

### History and Navigation
```
bellatrix web back --session shop --json
bellatrix web forward --session shop --json
bellatrix web refresh --session shop --json
```

### JavaScript (diagnostics only)
```
bellatrix web execute-js --session shop --script "return document.querySelectorAll('[data-testid=row]').length;" --json
```
Use `execute-js` to answer a narrow question (a count, a computed value, scrolling). Never return `document.body.innerHTML` or other large payloads — that defeats context economy. Never use it to perform the test action itself when an element verb exists.

### IMPORTANT: Re-snapshot After Every DOM Change

After any action that changes the page (route change, submit, opening a modal, expanding a menu, AJAX re-render):

```
bellatrix web wait-for-page-ready --session shop --json
bellatrix web snapshot --session shop --json
```

Resolve the next locator from the NEW snapshot `path`. Never reuse a snapshot taken before the change.

### Common Web Obstacles

| Obstacle | What to do |
|----------|------------|
| Cookie / consent banner | Snapshot, resolve its accept button, click it, re-snapshot. Record it — the generated code needs the same step. |
| Modal / drawer | It appears only after the trigger; snapshot after opening it. |
| New tab / window | The CLI stays on the current tab. Record the link and its `target`/`href`; continue with `navigate --url <href>` if the scenario needs the new page. |
| Native `alert` / `confirm` | Element verbs cannot see it. Record the expected dialog text and button; the generated code handles it with the framework's dialog service. |
| iframe | Element verbs act on the top document only. Record the frame locator and inner element; the generated code switches frames. |
| Lazy / infinite list | Scroll with `execute-js --script "window.scrollTo(0, document.body.scrollHeight);"`, wait, re-snapshot. |

---

## Phase 4: Close Session (MANDATORY)

**You MUST close the session before proceeding to code generation.** The execution phase ends here — no `bellatrix web` commands after this point.

```
bellatrix web close-session --session shop --json
```

`close-session` also stops the local browser driver the CLI started (`driverStopped` in the response). Verify:

```
bellatrix web list-sessions --json
```

The closed session must no longer appear. If it does, call `close-session` again. Close the session even when the scenario failed or was interrupted.

**DO NOT proceed to code generation until the session is closed.**

---

## Phase 5: Record Findings

After closing the session, compile:

1. **Validated locators** — only `{ strategy, selector }` pairs that actually worked against the live page in Phase 3
2. **Element-to-component mapping** — which HTML element / `type` / role maps to which BELLATRIX component (table below)
3. **Page transitions** — which action leads to which URL / page / modal
4. **Test data used** — input values, expected outputs, the exact texts observed (error messages, toasts, headings)
5. **Assertions needed** — what the generated test must verify, with the exact observed text
6. **Obstacles** — cookie banners, waits that were needed, iframes, dialogs, new tabs

---

## XPath Rules for Web (HTML DOM)

HTML XPath is the opposite of desktop UIA XPath:

| CORRECT (web) | WRONG |
|---------------|-------|
| `//button[normalize-space()='Log In']` | `//Button[@Name='Log In']` (desktop UIA syntax) |
| `//input[@name='email']` | `//INPUT[@NAME='email']` |
| `//label[normalize-space()='Email']/following-sibling::input` | `//label[text()=' Email ']` (brittle whitespace) |

- Tags and attribute names are **lowercase**.
- `normalize-space()` and `contains()` are fully supported — prefer `normalize-space()='Text'` for visible text, `contains(@class,'token')` only for stable, semantic class tokens.
- Use single quotes inside the expression: `//a[@href='/cart']`.
- Prefer a relationship to a stable anchor (label → input, row text → cell button) over long absolute paths.
- **NEVER** use absolute paths (`/html/body/div[3]/…`), bare positional indexes (`//div[5]`), or generated class names.

---

## HTML Element → BELLATRIX Component Mapping

Use the snapshot `Tag` + `Type` (+ `Role`) of each validated element to pick the C# component type for the generated Map file.

| HTML element (snapshot `Tag` / `Type`) | BELLATRIX component | Typical actions |
|----------------------------------------|---------------------|-----------------|
| `button`, `input type=submit/button` | `Button` | `Click()` |
| `input type=reset` | `Reset` | `Click()` |
| `input type=text` (or no type) | `TextField` | `SetText(…)` |
| `input type=password` | `Password` | `SetPassword(…)` |
| `input type=email` | `Email` | `SetEmail(…)` |
| `input type=tel` | `Phone` | `SetPhone(…)` |
| `input type=search` | `Search` | `SetSearch(…)` |
| `input type=url` | `Url` | `SetUrl(…)` |
| `input type=number` | `Number` | `SetNumber(double)` |
| `input type=range` | `Range` | `SetRange(int)` |
| `input type=date` | `Date` | `SetDate(year, month, day)` |
| `input type=time` | `Time` | `SetTime(hours, minutes)` |
| `input type=datetime-local` / `month` / `week` | `DateTimeLocal` / `Month` / `Week` | — |
| `input type=color` | `Color` | — |
| `input type=checkbox` | `CheckBox` | `Check()`, `Uncheck()` |
| `input type=radio` | `RadioButton` | `Click()` |
| `input type=file` | `InputFile` | `Upload(path)` |
| `select` | `Select` | `SelectByText(…)`, `SelectByIndex(…)` |
| `option` | `Option` | — (read `InnerText`, `IsSelected`) |
| `textarea` | `TextArea` | `SetText(…)` |
| `a` | `Anchor` | `Click()`, `Href` |
| `img` | `Image` | `Click()`, `Src` |
| `label` | `Label` | read `InnerText` |
| `h1`–`h6` | `Heading` | read `InnerText` |
| `span` | `Span` | read `InnerText` |
| `div`, `p`, `li`, `section`, `form`, `td` (text containers) | `Div` | read `InnerText` |
| `iframe` | `Frame` | switch into it via `App.Browser.SwitchToFrame(frame)` |
| `table` | `Table` | rows/cells API |
| clickable `div`/`span`/`li` (role `button`/`tab`/`menuitem`) | `Button` | `Click()` — `Div`/`Span`/`Label` have NO `Click()` |
| ARIA `role=checkbox` on a non-input | `Button` (or a custom component) | `Click()` |

There is no `H1`, `ListItem`, `Paragraph`, or `ComboBox` component in BELLATRIX Web — use the types above.

---

## Output

After completing scenario execution, provide:

```
## Execution Log
- Started session: <name> (browser: chrome, driverServer: local)
- Navigated: <url>
- Captured snapshot: <page> (<N> elements; locators via specialist)
- Executed: <action description>
- Captured snapshot: <next page / modal> (<N> elements)
- ...
- Closed session (driverStopped: true)

## Validated Locators
| Element | Strategy | Selector | Component Type | Purpose |
|---------|----------|----------|----------------|---------|
| ... | ... | ... | ... | ... |

## Pages / States Visited
1. <PageName> — <url> — <description>
2. ...

## Test Data
- Input: ...
- Expected (observed text): ...

## Obstacles / Waits
- ...
```

This output feeds directly into the **`bellatrix-web-generate-code`** skill.
