---
title: Testimize integration
parent: Guides
nav_order: 9
description: "What the optional Testimize setting does in SPECTRA 2.7.0, why it no longer changes the generated test data, and how to check it or turn it off."
---

# Testimize integration

[Testimize](https://github.com/AutomateThePlanet/Testimize) computes test data with algorithms: exact boundary
values, equivalence classes, pairwise combinations. Earlier SPECTRA versions fed that data into test generation.

> **Status in 2.7.0: inactive.** The step that ran Testimize and put its values into the generation prompt was
> removed together with the old in-process generator (CHANGELOG, spec 099: "the Testimize generation chain").
> Generated tests get their data from the AI and your docs, with or without this setting.

## What the setting still does

With `testimize.enabled: true` in `spectra.config.json`, the analysis step asks the AI to also list every
constrained input field it finds (type, limits, required, error messages). Nothing uses that list in 2.7.0. It only
makes the analysis answer longer, so leave the setting off (the default):

```json
{
  "testimize": { "enabled": false }
}
```

## Check the setting

```bash
spectra testimize check
```

It shows whether the setting is on and the bundled Testimize library version. It doesn't call an AI model.

## Without Testimize

Boundary and negative cases still come from the analysis step: it applies six ISTQB test design techniques
(equivalence partitioning, boundary value analysis, decision tables, state transitions, error guessing and use
cases) to your docs. To push it further, edit the `behavior-analysis` and `test-generation` templates
([Tune how tests are written](generation-profiles)).

<details markdown="1">
<summary>Under the hood</summary>

`testimize.enabled` sets the `testimize_enabled` block of the `behavior-analysis` template (it asks for
`field_specs`). The `test-generation` template still has `{{testimize_dataset}}` and
`{{testimize_strategy_name}}`, but `spectra ai compile-prompt` always fills them with empty values
(`PromptCompiler.cs`). `mode`, `strategy` and `settings_file` are only shown by `spectra testimize check`;
`abc_settings` isn't read. See [Configuration](configuration).

</details>
