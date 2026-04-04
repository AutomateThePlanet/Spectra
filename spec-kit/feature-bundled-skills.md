# SPECTRA Feature Spec: Bundled SKILLs and Agent Prompts

**Status:** Draft — ready for spec-kit cycle  
**Priority:** HIGH  
**Depends on:** CLI Non-Interactive spec (must be implemented first)  
**Affects:** `spectra init`, `.github/skills/`, `.github/agents/`

---

## 1. Problem

Users must manually configure Copilot agents and learn CLI flags to use SPECTRA effectively. The CLI has powerful features but the commands and arguments are hard to discover and remember.

## 2. Solution

`spectra init` creates ready-to-use SKILL files that translate natural language into CLI commands. Users say what they want in Copilot Chat, SKILLs call the CLI with correct arguments, and present results in the chat.

---

## 3. Files Created by `spectra init`

```
.github/
├── agents/
│   ├── spectra-execution.agent.md      ← test execution via MCP
│   └── spectra-generation.agent.md     ← test generation conversations
├── skills/
│   ├── spectra-generate/SKILL.md       ← wraps spectra ai generate
│   ├── spectra-coverage/SKILL.md       ← wraps spectra ai analyze --coverage
│   ├── spectra-dashboard/SKILL.md      ← wraps spectra dashboard
│   ├── spectra-validate/SKILL.md       ← wraps spectra validate
│   ├── spectra-list/SKILL.md           ← wraps spectra list / spectra show
│   └── spectra-init-profile/SKILL.md   ← wraps spectra init-profile
templates/
└── bug-report.md                       ← bug report template
```

---

## 4. SKILL File Contents

### 4.1 spectra-generate/SKILL.md

```markdown
---
name: SPECTRA Generate
description: Generate test cases from documentation. Analyzes docs, recommends count, generates with AI verification.
---

When the user asks to generate, create, or write test cases:

1. Determine the suite name from the user's request
2. Determine any filters: focus area, count, priority, tags
3. Run the CLI command in terminal:

   spectra ai generate --suite {suite} [--count {n}] [--focus "{focus}"] --output-format json --verbosity quiet

4. Parse the JSON output and present results:
   - How many tests were generated
   - Grounding breakdown (grounded/partial/rejected)
   - Any remaining gaps or suggestions

5. If the user wants to generate from suggestions:

   spectra ai generate --suite {suite} --from-suggestions --output-format json --verbosity quiet

6. If the user describes a test case to create:

   spectra ai generate --suite {suite} --from-description "{description}" [--context "{context}"] --output-format json --verbosity quiet

7. Continue the conversation — offer to generate more, switch suites, or check coverage

### Examples of user requests:
- "Generate test cases for the checkout suite"
- "Create negative test cases for authentication"
- "Generate 10 high-priority tests for payments"
- "Add a test case for IBAN validation — it's not documented"
- "What gaps do we still have in the search suite?"
```

### 4.2 spectra-coverage/SKILL.md

```markdown
---
name: SPECTRA Coverage
description: Analyze test coverage across documentation, requirements, and automation.
---

When the user asks about coverage, gaps, or what needs testing:

1. Run:

   spectra ai analyze --coverage --auto-link --output-format json --verbosity quiet

2. Parse JSON and present:
   - Documentation coverage: X%
   - Requirements coverage: X%
   - Automation coverage: X%
   - Uncovered areas with specific docs/requirements
   - Undocumented tests count

3. If the user asks about a specific area:
   - Reference the uncovered_areas from the JSON
   - Suggest generating tests for uncovered docs

4. If the user wants to improve coverage:
   - Suggest: "I can generate tests for {uncovered_doc}. Want me to?"
   - If yes, use the spectra-generate SKILL

### Examples:
- "How's our test coverage?"
- "What areas don't have tests yet?"
- "Show me coverage for the authentication module"
- "Which requirements aren't tested?"
```

### 4.3 spectra-dashboard/SKILL.md

```markdown
---
name: SPECTRA Dashboard
description: Generate the SPECTRA visual dashboard with suite browser, test viewer, and coverage visualizations.
---

When the user asks to generate, update, or build the dashboard:

1. Run:

   spectra dashboard --output ./site --output-format json --verbosity quiet

2. Parse JSON and confirm:
   - "Dashboard generated at ./site/index.html"
   - Include: X suites, Y tests, Z runs

3. If the project has Cloudflare Pages configured:
   - Mention: "Push to main to auto-deploy, or open ./site/index.html locally"

4. If the user asks to open it:
   - Suggest: "Open ./site/index.html in your browser"

### Examples:
- "Generate the dashboard"
- "Update the dashboard with latest results"
- "Build the site"
```

### 4.4 spectra-validate/SKILL.md

```markdown
---
name: SPECTRA Validate
description: Validate all test case files for correct format, unique IDs, and required fields.
---

When the user asks to validate, check, or verify test files:

1. Run:

   spectra validate --output-format json --verbosity quiet

2. Parse JSON and present:
   - If all valid: "✓ All {total} tests are valid"
   - If errors: list each error with file, line, and message

3. If errors found, suggest fixes:
   - Missing field → "Add {field} to the frontmatter in {file}"
   - Duplicate ID → "Change the ID in {file} — {id} is already used in {other_file}"

### Examples:
- "Validate all test cases"
- "Are there any formatting errors?"
- "Check if everything is valid before I push"
```

### 4.5 spectra-list/SKILL.md

```markdown
---
name: SPECTRA List
description: List test suites, show test case details, and browse the test repository.
---

When the user asks to list, show, browse, or find test cases:

1. To list suites:

   spectra list --output-format json --verbosity quiet

   Present: suite names with test counts

2. To show a specific test:

   spectra show {test-id} --output-format json --verbosity quiet

   Present: full test case details (title, steps, expected results, metadata)

3. To find tests matching criteria:
   - By tag: "show all payment tests"
   - By priority: "list high priority tests"
   - By component: "what tests do we have for auth?"

### Examples:
- "List all test suites"
- "Show me TC-101"
- "What tests do we have for checkout?"
- "How many tests are in the authentication suite?"
```

### 4.6 spectra-init-profile/SKILL.md

```markdown
---
name: SPECTRA Profile
description: Create or update the generation profile that controls how AI generates test cases.
---

When the user asks to configure, set up, or change generation preferences:

1. Ask what they want to configure:
   - Detail level (high-level / detailed / very detailed)
   - Negative scenario focus (minimum count per feature)
   - Domain-specific needs (payments, auth, GDPR, etc.)
   - Default priority
   - Formatting preferences

2. Build the CLI command:

   spectra init-profile --detail-level {level} --min-negative {n} --domain {domain} --default-priority {priority} --output-format json --verbosity quiet --no-interaction

3. Confirm the profile was created/updated

### Examples:
- "Set up a generation profile"
- "I want more detailed test steps"
- "Configure SPECTRA for payment domain testing"
- "Change default priority to high"
```

---

## 5. Agent Prompt Updates

### 5.1 spectra-execution.agent.md

Update the frontmatter to:

```yaml
---
name: SPECTRA Execution
description: Executes manual test cases through SPECTRA with optional documentation lookup.
tools:
  - "spectra/*"
  - "github/get_copilot_space"
  - "github/list_copilot_spaces"
  - "read"
  - "edit"
  - "search"
  - "terminal"
  - "browser"
model: GPT-4o
disable-model-invocation: true
---
```

Add Documentation Assistance section (see feature-bug-logging.md for details).

### 5.2 spectra-generation.agent.md

```yaml
---
name: SPECTRA Generation
description: Generates test cases from documentation with AI verification and gap analysis.
tools:
  - "terminal"
  - "read"
  - "search"
  - "github/get_copilot_space"
  - "github/list_copilot_spaces"
model: GPT-4o
disable-model-invocation: true
---
```

This agent uses terminal to call CLI commands (via SKILLs pattern) rather than MCP tools directly. The CLI handles provider chain, critic, and profile internally.

Body content should cover:
- Generation session flow (Phase 1-4 from feature-generation-session.md)
- How to call CLI with --output-format json
- How to parse and present results
- Copilot Spaces integration for documentation context

---

## 6. Update to `spectra init`

### 6.1 New files created

Add to the init command — create all SKILL and agent files listed in Section 3.

### 6.2 Init output

```
  ✓ Repository initialized

  Created:
    spectra.config.json          Configuration
    tests/                       Test cases directory
    docs/                        Documentation directory
    templates/bug-report.md      Bug report template

  Copilot integration:
    .github/agents/spectra-execution.agent.md
    .github/agents/spectra-generation.agent.md
    .github/skills/spectra-generate/SKILL.md
    .github/skills/spectra-coverage/SKILL.md
    .github/skills/spectra-dashboard/SKILL.md
    .github/skills/spectra-validate/SKILL.md
    .github/skills/spectra-list/SKILL.md
    .github/skills/spectra-init-profile/SKILL.md

  Next steps:
    Open VS Code and use Copilot Chat to start generating tests.
    Or use the CLI directly: spectra ai generate --suite {name}

  ℹ Optional: Create a Copilot Space with your product documentation
    for inline help during test execution.
    See docs/copilot-spaces-setup.md
```

### 6.3 Skip SKILL creation

Add `--skip-skills` flag to `spectra init` for projects that don't use Copilot:

```bash
spectra init --skip-skills
```

This creates only the core files (config, directories, templates) without `.github/agents/` and `.github/skills/`.

---

## 7. SKILL Update Mechanism

When SPECTRA CLI is updated to a new version, SKILLs may need updating too.

Add `spectra update-skills` command:

```bash
spectra update-skills
```

```
  Checking SKILL files...
  
  Updated:
    .github/skills/spectra-generate/SKILL.md  (v1.0 → v1.1)
    .github/skills/spectra-coverage/SKILL.md   (v1.0 → v1.1)
  
  Unchanged:
    .github/skills/spectra-validate/SKILL.md   (v1.0, current)
    .github/skills/spectra-list/SKILL.md       (v1.0, current)
  
  Skipped (customized by user):
    .github/agents/spectra-execution.agent.md  (user modified)
```

Rules:
- If user has NOT modified the file → overwrite with new version
- If user HAS modified the file → skip and warn
- Detection: compare hash of installed version vs current file

---

## 8. Documentation Updates

### New files:
- `docs/skills-integration.md` — how SKILLs work, the CLI → JSON → Chat pattern
- `docs/copilot-spaces-setup.md` — setting up a Space for documentation assistance

### Updated files:
- `docs/getting-started.md` — mention SKILLs as primary workflow, CLI as alternative
- `docs/cli-reference.md` — note which SKILLs correspond to which CLI commands
- `README.md` — update Quick Start to show Copilot Chat workflow first, CLI second

---

## 9. Spec-Kit Prompt

```
/speckit.specify Create bundled SKILL files and agent prompts for SPECTRA.

Read spec-kit/feature-bundled-skills.md for the complete design.

spectra init creates ready-to-use SKILL files that translate natural
language in Copilot Chat into CLI commands with structured JSON output.

Key deliverables:
- 6 SKILL files: generate, coverage, dashboard, validate, list, init-profile
- 2 agent prompts: execution (MCP-based), generation (CLI-based via terminal)
- All created by spectra init in .github/skills/ and .github/agents/
- Each SKILL calls CLI with --output-format json --verbosity quiet
- SKILLs parse JSON and present results conversationally
- --skip-skills flag for projects not using Copilot
- spectra update-skills command for updating SKILLs on CLI upgrade
- Agent frontmatter with tools whitelist and model specification
- docs/skills-integration.md explaining the pattern
- Updated getting-started.md and README.md

Tech: SKILL files are pure Markdown (no code). Agent files are Markdown
with YAML frontmatter. spectra init writes them as embedded resources.
spectra update-skills compares file hashes for safe updates.
```
