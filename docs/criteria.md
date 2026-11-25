---
title: Extract acceptance criteria
parent: Guides
nav_order: 2
description: "Turn the MUST, SHOULD and MAY statements in your docs into acceptance criteria, or import them from a CSV, YAML or JSON export, so coverage can track them."
---

# Extract acceptance criteria

*Acceptance criteria* are the testable requirements in your docs: the sentences with MUST, SHOULD or MAY. Your AI
agent pulls them out of each document, or imports them from an export you already have. Test generation needs them,
and coverage shows which ones have tests.

## What to say

> "Extract acceptance criteria"
>
> "Import criteria from jira-export.csv"
>
> "List acceptance criteria"
>
> "Show criteria for payments"

All four use the `spectra-criteria` skill.

## What you'll see

1. **A progress page** with three steps: find changed docs, extract, summarize.

   {% include screenshot.html narrow=true file="progress-criteria-extracting" alt="SPECTRA progress page with step 2, Extract criteria, active" %}

2. **Only new or changed documents are processed.** SPECTRA remembers what each document looked like at the last
   extraction. If nothing changed, the agent says "Acceptance criteria are up to date" and stops. To redo every
   document, say so: "Re-extract acceptance criteria for all docs".
3. **Moved or deleted documents.** If a document was renamed, its criteria and their IDs move with it. If it was
   deleted, the agent shows its criteria and the tests that still link to them, and asks before it removes them.
4. **A summary** per document: how many criteria were added, changed or removed.

   {% include screenshot.html narrow=true file="progress-criteria-done" alt="SPECTRA progress page showing all three criteria steps done and the status Complete" caption="A real run: 70 criteria from the Calculator demo's general-app-features.md." %}

**Importing** reads a `.csv`, `.yaml`/`.yml` or `.json` file and merges it with what you have. Say "replace" if the
import should overwrite existing criteria instead. No AI model is used for an import.

## Where the results are

| What | Where |
|---|---|
| Criteria for one document | `docs/criteria/` + the document's path, ending `.criteria.yaml`. For `docs/checkout.md`: `docs/criteria/docs/checkout.criteria.yaml` |
| The list of all criteria | `docs/criteria/_criteria_index.yaml` |

Each criterion has a stable ID, for example `AC-GENERAL-APP-FEATURES-005`. Test cases list the criteria they cover in
their `criteria:` field.

## Next

- [Generate test cases](usage) for the documents.
- [Check coverage](coverage) to see which criteria have no tests yet.

<details markdown="1">
<summary>Under the hood</summary>

`spectra docs changed` lists the documents whose content changed since the last extraction. For each one the
`spectra-extractor` subagent reads the document and returns the criteria; the CLI validates and saves them. Import
is `spectra ai analyze --import-criteria <file> [--replace]`. File format: [Configuration](configuration),
commands: [CLI reference](cli-reference).

</details>
