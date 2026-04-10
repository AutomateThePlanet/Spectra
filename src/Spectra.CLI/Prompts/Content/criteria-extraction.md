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
{{#if component}}
The document belongs to the "{{component}}" component.
{{/if}}

Respond ONLY with a JSON array. No markdown, no explanation, no code fences. Example:
[
  {"text": "System MUST validate IBAN format before payment", "rfc2119": "MUST", "source_section": "Payment Validation", "priority": "high", "tags": ["payment", "validation"]},
  {"text": "System SHOULD display inline error within 500ms", "rfc2119": "SHOULD", "source_section": "UX Requirements", "priority": "medium", "tags": ["ux", "performance"]}
]

Document path: {{document_title}}

Document content:
{{document_text}}
