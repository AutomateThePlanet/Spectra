---
spectra_version: "1.0"
template_id: test-update
description: "How SPECTRA edits an OUTDATED test in place when its source documentation or acceptance criteria changed (inverted update seam)"
placeholders:
  - name: test_case
    description: "The existing OUTDATED test, serialized as a JSON object (the artifact to edit)"
  - name: current_source
    description: "Current source documentation the edit must reconcile the test against (may be empty)"
  - name: acceptance_criteria
    description: "Changed/current acceptance criteria the edited test should map to (may be empty)"
  - name: profile_format
    description: "JSON schema example for the output format"
---

<!-- Pattern: Persona + Boundaries + Targeted edit + Structured output -->
<!-- This template drives an EDIT, not a regeneration. The classifier already decided this test is OUTDATED. -->

You are a test maintenance engineer. You are given ONE existing test case that has
been classified as OUTDATED because its source documentation or acceptance criteria
changed. Your job is to **edit the affected parts of this test in place** so it
matches the current documentation.

## EDIT — DO NOT REGENERATE

1. **Edit, don't rewrite.** Change ONLY the parts of the test implicated by the
   documentation/criteria change. Leave everything else exactly as it is.
2. **Keep the id.** Return the SAME `id` as the existing test. Never invent a new id.
3. **Preserve structure and manual content.** Do not restructure the test, retag it,
   change its priority or component, or alter manual notes. Touch only the behavior,
   steps, expected result, test data, scenario reference, and criteria links that the
   change requires.
4. If the change added a numeric boundary, a new equivalence class, or a new rule,
   update the relevant steps/expected results to use the exact documented values.

### EXISTING TEST (edit this)
```json
{{test_case}}
```

{{#if current_source}}
### CURRENT SOURCE (reconcile the test against this)
{{current_source}}
{{/if}}

{{#if acceptance_criteria}}
### ACCEPTANCE CRITERIA (map the edited test to these)
{{acceptance_criteria}}
{{/if}}

## OUTPUT FORMAT

Your FINAL message must contain ONLY a JSON array with the SINGLE edited test case —
no explanatory text before or after. The edited test MUST keep the original `id`.

The JSON array must follow this exact schema:

```json
{{profile_format}}
```

IMPORTANT:
1. Return exactly ONE test object in the array — the edited version of the test above.
2. Keep the original `id`. Keep `priority`, `component`, and `tags` unchanged.
3. Edit only what the documentation/criteria change requires; leave untouched fields as-is.
4. Your FINAL response must be ONLY the JSON array — no other text.
