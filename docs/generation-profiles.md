---
title: Tune how tests are written
parent: Guides
nav_order: 8
description: "Change the fields every generated test has, the instructions the AI follows when it analyzes docs and writes tests, and where to find each setting."
---

# Tune how tests are written

Three files decide what generated test cases look like. All three have sensible defaults, so change them only when
the output doesn't fit your team.

| To change | Edit | Takes effect |
|---|---|---|
| The fields of every test (for example add `test_data` or a custom field) | `profiles/_default.yaml` | next generation |
| How the AI analyzes docs, writes tests, extracts criteria and checks tests | `.spectra/prompts/<template>.md` | next run |
| The concrete test data values (boundaries, combinations) | `testimize` in `spectra.config.json` ([Testimize](testimize-integration)) | next generation |

## What to say

```prompt
Show me the test generation prompt template
```
```prompt
Reset the prompt templates to the defaults
```
```prompt
Create a test generation profile for our API team
```

The first two use the `spectra-prompts` skill; the last one uses `spectra-init-profile`.

## The test format: `profiles/_default.yaml`

`spectra init` writes this file. Its `format` field is the JSON shape the AI must return for each test: `title`,
`priority`, `steps`, `expected_result`, `source_refs` and the rest. Add a field there and new tests get it.
Remove one and new tests won't have it. Existing tests aren't changed.

## The instructions: prompt templates

Five templates control the AI's reasoning:

| Template | Used when |
|---|---|
| `behavior-analysis` | finding what to test in your docs |
| `test-generation` | writing the tests |
| `criteria-extraction` | extracting acceptance criteria |
| `critic-verification` | checking a test against the docs |
| `test-update` | editing tests after a doc change |

Your copies are in `.spectra/prompts/`. Edit one, for example to add domain rules ("every payment test needs a
currency"), and the next run uses it. After you upgrade SPECTRA, ask the agent to reset a template to get the new
default; your other edits stay.

## Generation profiles (`spectra.profile.md`)

The `spectra-init-profile` skill asks about detail level, negative scenarios, domain and default priority, and saves
the answers in `spectra.profile.md` (or `test-cases/<suite>/_profile.md` for one suite).

> **Known limitation (2.7.0):** test generation doesn't read `spectra.profile.md` yet. Only `spectra profile show`
> and `spectra validate` use it. To change generated tests today, edit `profiles/_default.yaml` or the
> `test-generation` template.

<details markdown="1">
<summary>Under the hood</summary>

`spectra ai compile-prompt` fills the `test-generation` template and inserts the `format` from
`profiles/_default.yaml` as `{{profile_format}}`. Template commands: `spectra prompts list|show|validate|reset`.
Profile commands: `spectra init-profile` and `spectra profile show`. All flags: [CLI reference](cli-reference).
Every setting in one place: [Customization](customization).

</details>
