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

## Technique Completeness Check

Before classifying, assess whether the test applies appropriate ISTQB test
design techniques to the current documentation:

- If documentation added a NEW numeric range → flag OUTDATED if no BVA tests exist for that range
- If documentation added NEW business rules with conditions → flag OUTDATED if no Decision Table coverage
- If documentation added NEW workflow states → flag OUTDATED if no State Transition tests
- If documentation changed boundary values (e.g., max changed from 100 to 200) → flag OUTDATED with specific boundary update needed
- If the test uses generic values where boundaries are documented → propose update to use exact boundary values

When proposing updates for OUTDATED tests:
- Replace generic test values with boundary values from the current documentation
- Add new steps for newly documented equivalence classes
- Update expected results to match changed boundary conditions

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
