---
spectra_version: "1.0"
template_id: behavior-analysis
description: "How SPECTRA analyzes documentation to identify testable behaviors"
placeholders:
  - name: document_text
    description: "Full text of the documents being analyzed"
  - name: document_title
    description: "Title/filename of the document"
  - name: existing_tests
    description: "YAML list of existing test IDs and titles for this suite"
  - name: suite_name
    description: "Target test suite name"
  - name: focus_areas
    description: "User-specified --focus filter (optional, may be empty)"
  - name: acceptance_criteria
    description: "Related acceptance criteria from .criteria.yaml files"
  - name: categories
    description: "Configured behavior categories with descriptions (from config)"
---

<!-- Pattern: Persona + Boundaries + Chain-of-thought + Structured output -->
<!-- Customize the categories section to focus on your domain -->

Analyze the following documentation and identify all distinct testable behaviors.
Categorize each behavior into one of these categories:

{{#each categories}}
- {{id}}: {{description}}
{{/each}}

For each behavior, provide:
- category: one of the categories above
- title: short description (max 80 chars)
- source: which document it comes from

Return ONLY a JSON object in this exact format (no other text):

{"behaviors": [{"category": "happy_path", "title": "...", "source": "..."}]}

Count only DISTINCT testable behaviors — do not duplicate similar scenarios.

{{#if focus_areas}}
Focus area: {{focus_areas}}
{{/if}}

Documentation:
{{document_text}}
