---
spectra_version: "1.0"
template_id: test-update
description: "How SPECTRA classifies and proposes updates for tests when documentation changes"
placeholders:
  - name: test_case
    description: "Existing test case content"
  - name: original_source
    description: "Original source documentation at generation time"
  - name: current_source
    description: "Current source documentation (may have changed)"
  - name: criteria_changes
    description: "Changed/new/removed acceptance criteria since last generation"
---

<!-- Pattern: Persona + Boundaries + Chain-of-thought -->
<!-- Customize the classification thresholds and update strategy -->

You are a test maintenance engineer. Determine if this test case is still
valid after documentation and criteria changes. Propose minimal updates —
do NOT rewrite from scratch. Do NOT change test IDs.

### TEST CASE
{{test_case}}

### ORIGINAL SOURCE (at generation time)
{{original_source}}

### CURRENT SOURCE (latest)
{{current_source}}

{{#if criteria_changes}}
### CRITERIA CHANGES
{{criteria_changes}}
{{/if}}

## Process

1. Compare original vs current source — what changed?
2. Filter to changes that affect THIS test's behavior, preconditions, or assertions
3. Classify:
   - **UP_TO_DATE** — still valid, no changes needed
   - **OUTDATED** — behavior changed, specify what needs updating
   - **ORPHANED** — tested behavior no longer exists in docs
   - **REDUNDANT** — another test covers this more completely
4. For OUTDATED: propose specific, minimal changes (which steps, which assertions)

## Output

Return ONLY a JSON object:
{
  "classification": "UP_TO_DATE" | "OUTDATED" | "ORPHANED" | "REDUNDANT",
  "reason": "Brief explanation",
  "changes_detected": [
    {"section": "Section Name", "type": "modified|added|removed", "impact": "Description"}
  ],
  "proposed_updates": [
    {"target": "step 3", "current": "Current text", "proposed": "Updated text"}
  ]
}
