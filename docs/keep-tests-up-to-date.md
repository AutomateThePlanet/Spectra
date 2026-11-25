---
title: Update tests when your docs change
parent: Guides
nav_order: 6
description: "When a doc changes, your AI agent finds the test cases it affects, shows you what changed and edits them after your OK. Edited tests are checked again."
---

# Update tests when your docs change

SPECTRA remembers which section of which doc each test case was written from. When a doc changes, your AI agent
finds the affected tests, shows you what changed and edits them after your OK. Each edited test is checked against
the docs again.

## What to say

> "Update test cases for notification"
>
> "Update all"
>
> "Stop updating"

All three use the `spectra-update` skill. "Update all" checks every suite. "Stop updating" finishes the test in
progress and stops; the rest are listed again next time.

## What you'll see

1. **A summary of what changed.** The agent groups the affected tests by change, for example "numbers: 30→45".
   Changes to formatting only (markup, whitespace, letter case) are skipped automatically. It also lists sections
   of changed docs that no test covers yet: candidates for [generating new tests](usage).
2. **Renamed docs.** If a doc file was renamed or moved, the agent shows each old → new link and asks before it
   updates the tests' references.
3. **Removed sections.** Tests whose doc section or criterion is gone are listed for you to review. They're never
   edited or deleted automatically.
4. **Your choice.** The agent asks which changes to process:
   - real content changes only (recommended),
   - those plus small wording changes,
   - groups you pick, or
   - none (report only).

   Then it asks once: "Re-verify the edited tests with the critic now?" Say yes unless you're in a hurry.
5. **Editing and checking.** The progress page follows the work. Each edited test keeps its ID, priority,
   component and tags.
6. **A report:** edited, unchanged, and the check results. **grounded** tests are done; **partial** ones are
   flagged for [review](review); for a **hallucinated** one the agent asks what to do.

## The first time

A test can only be compared with "before" if SPECTRA recorded its docs at some point. For tests created before you
used this feature, the agent offers to record the docs as they are now. From then on, every change is tracked. If
your docs are in git, the agent can also use the git history.

## Where the results are

The edited test files in `test-cases/<suite>/`, and per suite a record of the doc sections each test was last
checked against (`_source-baseline.json`, commit it with the tests).

<details markdown="1">
<summary>Under the hood</summary>

`spectra ai update-triage` compares each test's referenced doc sections and criteria with the recorded fingerprints;
no AI model is involved. `spectra ai relink-sources` handles renamed docs. The `spectra-updater` subagent edits one
group of tests, `spectra-critic` checks each edited test, and `spectra-repairer` fixes partial ones.
`spectra ai source-baseline --init` records the first baseline. See [CLI reference](cli-reference) and
[Architecture overview](architecture/overview).

</details>
