---
title: Generate test cases
parent: Guides
nav_order: 3
description: "Ask your AI agent for test cases. It proposes what to test, waits for your OK, then writes one Markdown file per test and checks each against your docs."
---

# Generate test cases

Your AI agent reads your documentation, proposes what to test and waits for your OK. Then it writes one Markdown file
per test case and checks each one against your docs.

**Before you start:** your repository is set up ([Getting started](getting-started)) and has acceptance criteria for
the docs you want to test ([Extract acceptance criteria](criteria)). Without criteria, generation stops with a
`MISSING_INPUT` message.

## What to say

> "Generate test cases for payments"
>
> "Generate negative test cases for payments"
>
> "Generate 50 test cases for gdpr-compliance"
>
> "Generate more test cases for authentication"

`payments` is the name of a *suite*: a folder under `test-cases/`. A new name creates a new suite. All four requests
use the `spectra-generate` skill.

## What you'll see

1. **A progress page opens in your browser.** It shows each step and updates while the agent works.

   {% include screenshot.html narrow=true file="progress-generate-analyzing" alt="SPECTRA progress page with step 2, Identify behaviors, active and steps 3 to 9 still to do" %}

2. **A recommendation, then a pause.** The agent lists the behaviours it found in your docs, how many tests it
   recommends, and which behaviours existing tests already cover. Then it asks "Shall I proceed?" and waits.
   Nothing is written until you answer.

   {% include screenshot.html narrow=true file="progress-generate-waiting-approval" alt="SPECTRA progress page stopped at step 4, Review (waiting for approval)" %}

3. **Writing and checking.** After your OK, the agent writes the tests. Then the *critic*, a separate AI check,
   compares every step and expected result with your docs.

   {% include screenshot.html narrow=true file="progress-generate-critic" alt="SPECTRA progress page with steps 1 to 7 done and step 8, Critic, active" %}

4. **A report.** For each test you get its ID, title and the critic's result:
   - **grounded**: everything in the test is backed by your docs. The test is kept.
   - **partial**: one or more details aren't in your docs. The agent tries one repair; if the test is still
     partial, it's **flagged** for you to review.
   - **hallucinated**: the test invents behaviour or contradicts your docs. It's deleted, and the reason is recorded.

   {% include screenshot.html narrow=true file="progress-generate-complete" alt="SPECTRA progress page showing all nine steps done and the status Complete" caption="A real run: 5 tests for the Windows Calculator demo. Three were grounded at once; two were partial, repaired, and then grounded." %}

If the report lists flagged tests, see [Review flagged tests](review).

## Make the request more specific

- **Focus:** name the kind of tests: "negative", "edge case", "security". The analysis uses it to pick behaviours.
- **Count:** give a number only if you need one. Without a number, the agent uses its recommendation.
- **One scenario:** describe it: "Add a test case to payments: a declined card shows the retry message". The agent
  skips the analysis and writes that one test.

## Where the results are

| What | Where |
|---|---|
| Test cases | `test-cases/<suite>/TC-<number>.md`, one file per test ([Test format](test-format)) |
| The critic's result | the `grounding:` section at the top of each test file |

Commit the test files like any other source file.

> **Known issue:** a new test can reuse an ID that another suite already has. After generating, ask your agent to
> "validate all test cases". If it reports duplicate IDs, run `spectra doctor ids --fix` and then
> `spectra index --suite <suite>`.

> The progress page reads `.spectra/progress.json`. If it stays on the first step, your browser blocked that file:
> serve the `.spectra` folder over HTTP (for example `npx http-server .spectra`) and open `seam-progress.html` there.

<details markdown="1">
<summary>Under the hood</summary>

The `spectra-generate` skill drives three subagents: `spectra-analyst` (finds behaviours and plans batches),
`spectra-generator` (writes one batch) and `spectra-critic` (checks one test). The `spectra` CLI never calls a
model: it builds each prompt and validates and saves what the model returns. Details:
[How SPECTRA double-checks your tests](grounding-verification), [Skills and agents](skills-integration),
[CLI reference](cli-reference).

</details>

## Other tasks

- [Extract acceptance criteria](criteria) from your docs
- [Review flagged tests](review)
- [Check coverage and build the dashboard](coverage)
- [Update tests when your docs change](keep-tests-up-to-date)
- [Run tests with your agent](execution-agents)
