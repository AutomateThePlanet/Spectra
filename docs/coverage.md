---
title: Check coverage and build the dashboard
parent: Guides
nav_order: 5
description: "See which docs, acceptance criteria and test cases are covered, and publish everything as a static dashboard: suites, test details, coverage and run history."
---

# Check coverage and build the dashboard

SPECTRA measures coverage three ways: which **docs** have test cases, which **acceptance criteria** have test cases,
and which **test cases** have automated tests. The dashboard shows all of it, with your suites and run history, as a
static website.

## What to say

> "Show test coverage"
>
> "What areas don't have test cases?"
>
> "Which acceptance criteria aren't tested?"
>
> "Generate the dashboard"

The first three use the `spectra-coverage` skill; the last one uses `spectra-dashboard`.

## What you'll see

**Coverage.** A progress page opens while SPECTRA scans your repository. Then the agent lists the three numbers in
chat, with the uncovered docs, the untested criteria and the test cases that have no automation.

{% include screenshot.html narrow=true file="progress-coverage-done" alt="SPECTRA progress page for the coverage analysis, all steps done" %}

While it scans, SPECTRA also links test cases to your automated tests. It searches your automation code for test IDs,
for example `[ManualTestCase("TC-1186")]`, and records each match in the test case.

**Dashboard.** The agent builds the site and opens it. The first view lists your suites, on a desktop and on a phone:

<div class="screenshot-pair">
{% include screenshot.html file="dashboard-suites" alt="SPECTRA dashboard suite cards for the Windows Calculator demo: tests per priority, total tests and automation per suite" %}
{% include screenshot.html phone=true file="dashboard-suites-mobile" alt="The same SPECTRA dashboard on a 390-pixel-wide phone screen" %}
</div>

Click a test to see its priority, tags, preconditions, steps and expected result:

{% include screenshot.html file="dashboard-test-detail" narrow=true alt="Dashboard test detail panel for TC-1186 with tags, suite, preconditions, four steps and the expected result" %}

The **Coverage** tab shows the three numbers with drill-down lists:

{% include screenshot.html file="dashboard-coverage" alt="Dashboard coverage tab: documentation 100 percent (7 of 7), acceptance criteria 98.6 percent, automation 25.2 percent" caption="The Calculator demo: every doc has tests, 69 of 70 extracted criteria have tests, and 275 of 1,091 test cases are automated." %}

The **Run History** tab lists past test runs ([Run tests with your agent](execution-agents)).

## Where the results are

| What | Where |
|---|---|
| Dashboard | `site/` (static files; open `site/index.html` or [publish it](deployment)) |
| Automation links | the `automated_by:` field of each linked test case |

Criteria coverage counts only criteria that exist, so [extract acceptance criteria](criteria) first. Documents such
as release notes can be left out of the documentation number: see
[Coverage reference](coverage-reference#excluding-docs-from-the-coverage-percentage).

> The dashboard's automation number and the number in the agent's coverage summary are counted differently and can
> differ. This is a known issue.

<details markdown="1">
<summary>Under the hood</summary>

Coverage is `spectra ai analyze --coverage --auto-link`; the dashboard is `spectra dashboard --output ./site`. Neither
uses an AI model. Which folders are scanned and which patterns count as a link are set in `spectra.config.json`
(`coverage.automation_dirs`, `coverage.scan_patterns`). Details: [Coverage reference](coverage-reference),
[Configuration](configuration), [CLI reference](cli-reference).

</details>
