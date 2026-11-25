---
title: Skills and agents
parent: Reference
nav_order: 5
description: "The 16 skills and 6 subagents SPECTRA installs into your AI agent: what each one does, an example request for each skill, and how to update or customize them."
---

# Skills and agents

`spectra init` installs **skills** (step-by-step workflows your AI agent follows when you ask for something) and
**subagents** (helpers the skills start for one job, each in its own context). Your agent picks the skill from what
you say; you never name it. For Claude Code they live in `.claude/skills/<name>/SKILL.md` and
`.claude/agents/<name>.agent.md`; for other agents see [AI agent installs](ai-agents-reference).

## Skills

The description is the skill's own, as installed. Example requests come from the skills' trigger lists.

| Skill | Description | Example request |
|---|---|---|
| `spectra-generate` | Generate test cases from documentation — analyze behaviors, get approval, generate, and verify with the mandatory spectra-critic subagent. | "generate test cases for payments" |
| `spectra-review-flagged` | Reviews and dispositions flagged (still-partial-after-repair) test cases … Also supports accept-as-is and delete. | "review the flagged tests in checkout" |
| `spectra-criteria` | Extract, import, and browse acceptance criteria for test coverage analysis. | "extract acceptance criteria" |
| `spectra-update` | Update existing test cases after documentation or acceptance criteria changes. | "update test cases for notification" |
| `spectra-coverage` | Analyzes test coverage across documentation, acceptance criteria, and automation. | "show test coverage" |
| `spectra-dashboard` | Generates the SPECTRA visual dashboard with suite browser, test viewer, and coverage visualizations. | "generate the dashboard" |
| `spectra-execute` | Execute or run manual test cases through the SPECTRA CLI (`spectra run …`) — select tests, start the run, launch the local web console, and stay on-call. | "run the smoke test cases" |
| `spectra-docs` | Index documentation, manage the v2 docs/_index/ manifest layout, and discover doc-suite IDs for analyzer filtering. | "index the docs" |
| `spectra-list` | Lists test suites, shows test case details, and browses the test case repository. | "show me TC-100" |
| `spectra-validate` | Validates all test case files for correct format, unique IDs, and required fields. | "validate all test cases" |
| `spectra-delete` | Safely delete test cases with automation and dependency checks. | "delete TC-142" |
| `spectra-suite` | Rename, delete, and manage test suites. | "rename suite checkout to payments" |
| `spectra-prompts` | View and manage prompt templates that control AI reasoning quality. | "show me the test generation prompt template" |
| `spectra-init-profile` | Creates or updates the generation profile that controls how AI generates test cases. ([Known limitation](generation-profiles#generation-profiles-spectraprofilemd)) | "create a test generation profile for our API team" |
| `spectra-help` | Shows all available SPECTRA commands and prompts available in Claude Code. | "help" |
| `spectra-quickstart` | Guided onboarding and workflow walkthroughs for SPECTRA via Claude Code. | "help me get started" |

## Subagents

Only skills start these; don't ask for them by name.

| Subagent | Started by | What it does |
|---|---|---|
| `spectra-analyst` | spectra-generate | analyses the documentation for a suite (behaviours, ISTQB techniques, coverage) and returns the recommendation and a batch plan |
| `spectra-generator` | spectra-generate | generates one batch of the suite's generation plan, ingests it, has the critic verify each created test |
| `spectra-critic` | the generator, updater and repairer (the main session as fallback) | verifies a single test against the source text it cites and returns a verdict, in a fresh context per test |
| `spectra-repairer` | spectra-generate, spectra-update | repairs tests the critic found partial or hallucinated and has the critic re-verify each one |
| `spectra-updater` | spectra-update | edits one group of tests after a documentation change and has the critic re-verify each edited test |
| `spectra-extractor` | spectra-criteria | extracts acceptance criteria from one changed document |

The critic runs on the model in `ai.critic.model` (default `sonnet`, [Configuration](configuration)); the other
subagents use your session's model. When a subagent can't start another subagent, the generator, updater and
repairer report `critic: not run`, and the main session starts the critic for each test instead. The check is the
same.

## Update and customize

- **After upgrading SPECTRA:** `spectra update-skills` refreshes the Claude Code skills, subagents, prompt templates
  and `CUSTOMIZATION.md`. Other agents: `spectra init --agent auto`.
- **Your edits are kept.** A skill file you changed is skipped with a warning. To get the new version, delete your
  copy and run `spectra update-skills` again.
- **No AI agent:** `spectra init --skip-skills` sets up the workspace without skills or subagents. All CLI commands
  still work ([CLI reference](cli-reference)).

## How a skill runs

A skill never calls a model through SPECTRA. It runs `spectra` commands that build a prompt from your docs
(`spectra ai compile-…`), lets your agent or a subagent answer it, and hands the answer back to SPECTRA
(`spectra ai ingest-…`), which validates and saves it. Every command a skill runs adds `--output-format json` so the
agent reads structured results. See [Architecture overview](architecture/overview) and
[How test case generation works](analysis/cli-vs-chat-generation).
