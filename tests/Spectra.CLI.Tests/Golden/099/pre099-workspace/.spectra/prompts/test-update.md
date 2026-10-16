---
spectra_version: "1.0"
template_id: test-update
description: "How SPECTRA edits tests in place when the documentation sections or acceptance criteria they reference changed (inverted update seam; one test or a whole triage group)"
placeholders:
  - name: source_diff
    description: "Each referenced doc section once: its current text, the previous (normalised) version the tests were written against, and what changed — or why it no longer resolves"
  - name: acceptance_criteria
    description: "Only the criteria the tests link, with their current text and state (changed since baseline / removed); may be empty"
  - name: tests
    description: "The tests to review, each under '### TEST <id>' with the sections and criteria it links and its JSON"
  - name: profile_format
    description: "JSON schema example for the output format"
  - name: test_case
    description: "Legacy (pre-088 custom templates): the single test to edit, serialized as a JSON object"
  - name: current_source
    description: "Legacy (pre-088 custom templates): the source documentation to reconcile against"
---

<!-- Pattern: Persona + Boundaries + Targeted edit + Structured output -->
<!-- Spec 088: the prompt carries each changed doc section (current, previous, what changed) once, only the linked
     criteria, and one or more tests. It drives an EDIT, not a regeneration. -->

You are a test maintenance engineer. The tests below reference documentation sections or acceptance criteria that
changed. For each test, **edit only the parts the change implicates** so the test matches the current documentation.

## EDIT — DO NOT REGENERATE

1. **Edit only what the source change implicates.** Compare each section's previous version with its current one
   (and "What changed"), and change only the steps, expected results, test data, scenario reference or criteria links
   that depend on it. Leave everything else exactly as it is.
2. **A test that nothing in the change implicates is returned unchanged** — exactly as given.
3. **Keep the original ids.** Return each test with its original `id`. Never invent, merge or split tests.
4. **Preserve structure and manual content.** Do not restructure a test, change its priority, component or tags, or
   alter manual notes.
5. **Use the documented values.** If the change moved a numeric boundary, added a rule or an equivalence class, use
   the exact values the current section states.
6. **Don't invent documentation.** A section marked REMOVED or RELINK has no text here; do not guess its content.

## SOURCE SECTIONS

Each section is shown as it is now ("Current section"), as it was when the tests were written ("Previous version" —
normalised: lowercased with markup removed, so compare meaning, not formatting), and what changed between them.

{{source_diff}}

{{#if acceptance_criteria}}
## ACCEPTANCE CRITERIA (linked by these tests)

{{acceptance_criteria}}
{{/if}}

## TESTS TO REVIEW

{{tests}}

## OUTPUT FORMAT

Your FINAL message must contain ONLY a JSON array with ONE object per `### TEST` above, in the same order, each
keeping its original `id`, following this schema:

```json
{{profile_format}}
```
