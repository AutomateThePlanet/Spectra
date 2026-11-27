---
title: Review flagged tests
parent: Guides
nav_order: 4
description: "Go through the tests SPECTRA couldn't fully confirm from your docs. Accept, fix, regenerate or delete each one in chat; your decision is saved with your name."
---

# Review flagged tests

A test is **flagged** when the critic found a detail your docs don't back up, and one automatic repair didn't fix it.
Its result stays **partial**. You decide what happens next: SPECTRA never accepts or deletes a flagged test on its
own.

## What to say

```prompt
Review the flagged tests in checkout
```

Then, for each test the agent shows you:

```prompt
Looks fine, accept it
```
```prompt
Accept it — I checked the runbook and the interval is right
```
```prompt
Regenerate this one
```
```prompt
The step about the retry limit is wrong, fix it
```
```prompt
This one's redundant, remove it
```

All of these use the `spectra-review-flagged` skill.

## What you'll see

The agent goes through the flagged tests one at a time. For each one it shows the ID, the title, the critic's score
and what the critic couldn't find in your docs. For example, from the Calculator demo:

> **TC-332**: Verify currently active mode is visually highlighted in the hamburger menu, partial, score 0.90.
> Step 1: the documentation doesn't say which mode the application launches in.

Then you choose:

| You say | What happens |
|---|---|
| "accept it" | The test stays as it is and is no longer flagged. Your name, the date and your note (if any) are saved in the test. |
| "regenerate this one" | One more repair attempt, then the critic checks the test again. In the example, step 1 became "Open the hamburger menu and select 'Standard'", and the test became **grounded**. |
| "the step about X is wrong, fix it" | Your note is saved, and the repair uses it. Works on any test, also one the critic marked grounded. |
| "remove it" | The reason is recorded, then the test file is deleted. |

At the end, the agent lists what was repaired, accepted, deleted and skipped.

## Where the results are

Everything is saved in the test file's `grounding:` section: the critic's result and score, and your review
(`reviewed_by`, `reviewed_at`, `note`). Accepting never changes the critic's result; only a repair followed by a new
check does. A later re-check keeps your review.

<details markdown="1">
<summary>Under the hood</summary>

The skill lists flagged tests with `spectra ai review-flagged --suite <suite>` and records decisions with
`--action accept|feedback|delete` (`--note`, `--fix`). A repair runs `compile-repair-prompt`, `ingest-update` and a
fresh critic check. See [How SPECTRA double-checks your tests](grounding-verification) and the
[CLI reference](cli-reference).

</details>
