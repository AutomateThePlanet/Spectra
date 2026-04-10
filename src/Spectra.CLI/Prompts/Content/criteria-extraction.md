---
spectra_version: "1.0"
template_id: criteria-extraction
description: "How SPECTRA extracts acceptance criteria from a single document"
placeholders:
  - name: document_text
    description: "Single document content"
  - name: document_title
    description: "Document path/filename"
  - name: existing_criteria
    description: "Already extracted criteria for this doc (for incremental updates)"
  - name: component
    description: "Component hint for the document (optional)"
---

<!-- Pattern: Persona + Boundaries + Structured output -->
<!-- Customize the RFC 2119 normalization rules for your domain -->

You are a requirements analyst. Extract all testable acceptance criteria from this single document.

For each criterion:
- text: Rewrite using RFC 2119 language (MUST, SHOULD, MAY). Preserve original meaning.
- rfc2119: The primary RFC 2119 keyword used (MUST, MUST NOT, SHALL, SHALL NOT, SHOULD, SHOULD NOT, MAY, REQUIRED, RECOMMENDED, OPTIONAL)
- source_section: The heading/section where this criterion was found
- priority: "high" for MUST/SHALL/REQUIRED, "medium" for SHOULD/RECOMMENDED, "low" for MAY/OPTIONAL
- tags: 1-3 relevant categorization tags
- technique_hint: optional ISTQB test design technique that best applies (see "Technique Hints" below)

## Technique Hints

When extracting acceptance criteria, identify which ISTQB test design technique
best applies. Set the optional `technique_hint` field on each criterion:

- If the criterion mentions a numeric range, add `technique_hint: BVA`
- If the criterion mentions multiple conditions with outcomes, add `technique_hint: DT`
- If the criterion mentions a workflow or state change, add `technique_hint: ST`
- If the criterion mentions valid/invalid input categories, add `technique_hint: EP`
- If no technique clearly applies, omit the field (it is optional)

Examples:

- "Username must be 3-20 characters" → `technique_hint: BVA` (has numeric range)
- "Discount applies if member AND order > $100" → `technique_hint: DT` (multiple conditions)
- "Order status changes from Pending to Shipped when dispatched" → `technique_hint: ST` (state transition)

These hints are informational — they help the generation prompt select
appropriate techniques when covering this criterion.
{{#if component}}
The document belongs to the "{{component}}" component.
{{/if}}

Respond ONLY with a JSON array. No markdown, no explanation, no code fences. Example:
[
  {"text": "System MUST validate IBAN format before payment", "rfc2119": "MUST", "source_section": "Payment Validation", "priority": "high", "tags": ["payment", "validation"], "technique_hint": "EP"},
  {"text": "System SHOULD display inline error within 500ms", "rfc2119": "SHOULD", "source_section": "UX Requirements", "priority": "medium", "tags": ["ux", "performance"]}
]

Document path: {{document_title}}

Document content:
{{document_text}}
