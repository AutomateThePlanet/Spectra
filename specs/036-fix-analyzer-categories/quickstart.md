# Quickstart: Verifying the Category-Injection Fix

**Feature**: 036-fix-analyzer-categories
**Audience**: SPECTRA users with custom analysis categories; SPECTRA maintainers verifying the fix

This is the operational walkthrough for confirming that custom analysis categories now flow through the analyzer end-to-end.

## Prerequisites

- A SPECTRA project initialized with `spectra init`.
- A documentation set in `docs/` and at least one suite under `tests/`.
- The new build of `Spectra.CLI` containing this feature.

## Step 1 — Configure custom categories

Edit `spectra.config.json` and replace (or add) the `analysis.categories` block with your domain's categories. For an accessibility-focused project:

```json
{
  "analysis": {
    "categories": [
      { "id": "keyboard_interaction", "description": "Keyboard navigation and shortcuts" },
      { "id": "screen_reader_support", "description": "ARIA labels, live regions, semantic markup" },
      { "id": "color_contrast", "description": "WCAG contrast ratios and color-only conveyance" },
      { "id": "focus_management", "description": "Focus traps, restoration, visible indicators" }
    ]
  }
}
```

## Step 2 — (Optional) Customize the analysis prompt

If you also want to customize the prompt wording itself, edit `.spectra/prompts/behavior-analysis.md`. Add a sentinel sentence near the top so you can verify the edit lands in the AI's input:

```markdown
# Behavior Analysis (CUSTOMIZED)

## SENTINEL: my-custom-edit-marker
...
```

(If you have not edited this file, the bundled built-in template is used — no further action needed.)

## Step 3 — Run analysis

```bash
spectra ai generate <suite> --analyze-only --output-format json
```

Replace `<suite>` with your suite name (e.g., `accessibility`).

## Step 4 — Verify the breakdown

The JSON output should include an `analysis.breakdown` block keyed by **your custom category IDs**, not by the legacy hardcoded names. Example:

```json
{
  "command": "generate",
  "status": "completed",
  "suite": "accessibility",
  "analysis": {
    "totalBehaviors": 14,
    "alreadyCovered": 0,
    "recommended": 14,
    "breakdown": {
      "keyboard_interaction": 5,
      "screen_reader_support": 4,
      "color_contrast": 3,
      "focus_management": 2
    }
  }
}
```

✅ **Pass**: All 4 of your configured IDs appear, and `happy_path`/`negative`/`edge_case`/etc. do not (unless you kept them).

❌ **Fail before fix**: The breakdown only shows the 5 legacy values (`happy_path`, `negative`, `edge_case`, `security`, `performance`), and your custom IDs are nowhere to be seen.

## Step 5 — Verify focus filter works on custom categories

```bash
spectra ai generate accessibility --focus "keyboard" --analyze-only --output-format json
```

Inspect the resulting breakdown. Only `keyboard_interaction` (and any other ID containing "keyboard") should appear:

```json
{
  "analysis": {
    "totalBehaviors": 5,
    "breakdown": { "keyboard_interaction": 5 }
  }
}
```

## Step 6 — Verify default-user backward compatibility

In a project that has **not** customized `analysis.categories`, run the same command:

```bash
spectra ai generate <suite> --analyze-only --output-format json
```

The breakdown keys should be the 6 Spec 030 defaults: `happy_path`, `negative`, `edge_case`, `boundary`, `error_handling`, `security`. Counts depend on the AI's analysis of your specific docs; the *shape* is what we're verifying.

## Step 7 — Verify the count selector still works (interactive)

```bash
spectra ai generate <suite>
```

Without `--analyze-only` and without `--output-format json`, the interactive count selector renders the breakdown. For default users this should look identical to before the fix. For custom-category users, your IDs render as space-separated phrases (e.g., `keyboard_interaction` displays as "keyboard interaction") with the same selector UX.

## Step 8 — (Optional) Inspect the prompt the AI receives

If you added the sentinel from Step 2, run with debug output enabled and confirm the sentinel appears in the prompt sent to the AI. The exact mechanism depends on your debugging setup; the simplest is to add a temporary `Console.WriteLine(prompt)` in `BehaviorAnalyzer.AnalyzeAsync` and rerun. The output should include `SENTINEL: my-custom-edit-marker`.

## Recovery: if a behavior comes back uncategorized

The AI may occasionally return an empty or whitespace category. In that case the breakdown will contain a single `uncategorized` bucket with the count of such behaviors. This is by design (FR-008) — the system does not crash, drop the data, or silently rebadge it. If the count is consistently non-zero, audit your `.spectra/prompts/behavior-analysis.md` template to ensure the AI is being instructed to always emit a category.
