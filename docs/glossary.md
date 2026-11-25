---
title: Glossary
parent: Architecture
nav_order: 5
description: "Every SPECTRA term defined once: suite, test case, acceptance criteria, critic, grounded, partial, hallucinated, flagged, unverified, skill, subagent and more."
---

# Glossary

Each term is defined once, here. The labels in **bold** are exactly what SPECTRA shows.

acceptance criteria
: The testable requirements in your docs: sentences with MUST, SHOULD or MAY. Extracted per document or imported
  from a file, each with a stable ID such as `AC-CHECKOUT-003`. See [Extract acceptance criteria](criteria).

AI coding agent
: The assistant you work with in your editor or terminal: Claude Code, GitHub Copilot, Cursor and
  [six others](ai-agents). SPECTRA's AI work runs inside it.

coverage
: How much is tested, measured three ways: docs with at least one test case, acceptance criteria with at least one
  test case, and test cases with an automated test. See [Check coverage](coverage).

critic
: The AI step that checks one test case against the doc sections it cites and gives it a label. It runs in its own
  context and never sees how the test was written. See [How SPECTRA double-checks tests](grounding-verification).

dashboard
: A static website built from your repository: suites, test cases, coverage and run history.

docs index
: SPECTRA's summary of your docs (`docs/_index/`), which keeps prompts small. Built by `spectra init` and
  `spectra docs index`. See [Document index](document-index).

**flagged**
: A **partial** test that one automatic repair didn't fix. It waits for a person to accept, fix, regenerate or delete
  it. See [Review flagged tests](review).

generation profile
: Saved preferences for generated tests (detail level, negative cases, domain). See
  [Tune how tests are written](generation-profiles).

**grounded**
: Every step and expected result of the test is backed by your docs.

**hallucinated**
: The test invents behaviour or contradicts your docs. SPECTRA deletes it and records why.

**partial**
: Most of the test is backed by your docs, but at least one detail isn't.

run, run console
: A run is one pass through a set of test cases with a result per test. The run console is the local web page where
  you record **PASS**, **FAIL**, **BLOCKED** or **SKIP**. See [Run tests with your agent](execution-agents).

skill
: A step-by-step workflow SPECTRA installs into your AI agent. Your request picks the skill. See
  [Skills and agents](skills-integration).

subagent
: A helper a skill starts for one job (analysing, writing a batch, checking one test) in its own context.

suite
: A folder of test cases under `test-cases/`, for example `test-cases/checkout/`.

test case
: One manual test as a Markdown file: title, preconditions, steps, expected result, and links to its docs and
  criteria. See [Test format](test-format).

**unverified**
: The critic hasn't checked the test yet, or couldn't finish. Ask your agent to run the critic again.
