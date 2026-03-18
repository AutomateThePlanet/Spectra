# Spectra Development Guide

This guide explains how to build, test, and run Spectra locally.

## Table of Contents

- [Quick Start](#quick-start-tldr)
- [Prerequisites](#prerequisites)
- [Building](#building)
- [Running Tests](#running-tests)
- **[E2E Workflow: Generate Tests and Execute via Copilot](#end-to-end-workflow-generate-tests-and-execute-via-github-copilot)**
  - [Step 1: Build and Install Tools](#step-1-build-and-install-spectra-tools)
  - [Step 2: Set Up Project with Documentation](#step-2-set-up-your-project-with-documentation)
  - [Step 3: Generate Tests](#step-3-generate-tests-from-documentation)
  - [Step 4: Configure VS Code MCP](#step-4-configure-vs-code-with-mcp-server)
  - [Step 5: Execute Tests via Copilot](#step-5-execute-tests-via-github-copilot)
  - [Step 6: View History](#step-6-view-execution-history)
- **[Dashboard Portal](#dashboard-portal-setup)**
- **[Test Generation Profiles](#test-generation-profiles)**
- [Running Locally (Debug Mode)](#running-locally-debug-mode)
- [Troubleshooting](#troubleshooting)
- [Using TestFixtures](#using-testfixtures-for-development)

---

## Quick Start (TL;DR)

```bash
# 1. Build and install tools
dotnet build -c Release -p:NoWarn=CA1062
dotnet pack src/Spectra.CLI/Spectra.CLI.csproj -c Release -p:NoWarn=CA1062
dotnet pack src/Spectra.MCP/Spectra.MCP.csproj -c Release -p:NoWarn=CA1062
dotnet tool install -g --add-source src/Spectra.CLI/nupkg Spectra.CLI
dotnet tool install -g --add-source src/Spectra.MCP/nupkg Spectra.MCP

# 2. In your project folder with docs/
spectra init
spectra ai generate checkout --count 10

# 3. Create .vscode/mcp.json with: {"servers":{"spectra":{"command":"spectra-mcp","args":["."]}}}

# 4. Open VS Code, start Copilot Chat, and say: "Start a test run for checkout"
```

For the full walkthrough, see [End-to-End Workflow](#end-to-end-workflow-generate-tests-and-execute-via-github-copilot).

---

## Prerequisites

- .NET 8.0+ SDK
- Git
- VS Code with GitHub Copilot (for MCP execution)
- GitHub Copilot Chat extension (for MCP support)
- One of the following authentication options (for AI test generation):
  - GitHub CLI authenticated (`gh auth login`)
  - `GITHUB_TOKEN` environment variable
  - `OPENAI_API_KEY` or `ANTHROPIC_API_KEY` for alternative providers

---

## Authentication Setup

SPECTRA requires authentication to access AI providers for test generation. The CLI provides helpful error messages and automatic fallback when auth is missing.

### Quick Status Check

```bash
spectra auth
```

This shows authentication status for all configured providers:

```
SPECTRA Authentication Status
========================================

github-models        [OK] via gh-cli
openai               [NOT CONFIGURED]
                       Set the OPENAI_API_KEY environment variable
anthropic            [NOT CONFIGURED]
                       Set the ANTHROPIC_API_KEY environment variable
```

### GitHub Models (Recommended)

GitHub Models is the default provider. You can authenticate in two ways:

**Option 1: GitHub CLI (Recommended)**

```bash
# Install GitHub CLI if needed
# https://cli.github.com/

# Authenticate
gh auth login

# Verify
spectra auth -p github-models
```

SPECTRA automatically detects `gh auth token` and uses it.

**Option 2: Environment Variable**

```bash
# Create a GitHub token at https://github.com/settings/tokens
# Required scopes: read:user

# Set environment variable
export GITHUB_TOKEN="ghp_your_token_here"

# Windows PowerShell
$env:GITHUB_TOKEN = "ghp_your_token_here"

# Windows CMD
set GITHUB_TOKEN=ghp_your_token_here
```

### OpenAI

```bash
# Get API key from https://platform.openai.com/api-keys
export OPENAI_API_KEY="sk-your_key_here"

# Verify
spectra auth -p openai
```

Update `spectra.config.json` to use OpenAI:

```json
{
  "ai": {
    "providers": [
      { "name": "openai", "model": "gpt-4o", "enabled": true }
    ]
  }
}
```

### Anthropic

```bash
# Get API key from https://console.anthropic.com/
export ANTHROPIC_API_KEY="sk-ant-your_key_here"

# Verify
spectra auth -p anthropic
```

Update `spectra.config.json` to use Anthropic:

```json
{
  "ai": {
    "providers": [
      { "name": "anthropic", "model": "claude-sonnet-4-5-20250514", "enabled": true }
    ]
  }
}
```

### Custom Environment Variables

Override the default environment variable name in config:

```json
{
  "ai": {
    "providers": [
      {
        "name": "openai",
        "model": "gpt-4o",
        "api_key_env": "MY_CUSTOM_OPENAI_KEY",
        "enabled": true
      }
    ]
  }
}
```

### Troubleshooting Authentication

**Stack trace instead of helpful error?**

This shouldn't happen anymore. If you see a stack trace for missing auth, please report it as a bug.

**"GitHub CLI is installed but not authenticated"**

Run `gh auth login` and follow the prompts.

**"GitHub CLI is not installed"**

Install from https://cli.github.com/ or use the `GITHUB_TOKEN` environment variable instead.

**"API key not found"**

Verify the environment variable is set:

```bash
# Linux/macOS
echo $OPENAI_API_KEY

# Windows PowerShell
echo $env:OPENAI_API_KEY
```

**Wrong provider being used?**

Check which provider is enabled first in `spectra.config.json`. SPECTRA uses the first enabled provider.

## Project Structure

```
src/
├── Spectra.CLI/       # AI test generation CLI
├── Spectra.MCP/       # MCP execution server
├── Spectra.Core/      # Shared library (parsing, validation, models)
└── Spectra.GitHub/    # GitHub integration (future)

tests/
├── Spectra.CLI.Tests/     # CLI unit/integration tests
├── Spectra.MCP.Tests/     # MCP server tests
├── Spectra.Core.Tests/    # Core library tests
└── TestFixtures/          # Sample test data
    ├── docs/              # Sample documentation
    └── tests/             # Sample test suites with _index.json
```

---

## Building

```bash
# Build entire solution
dotnet build

# Build specific project
dotnet build src/Spectra.MCP/Spectra.MCP.csproj

# Build in Release mode
dotnet build -c Release
```

---

## Running Tests

```bash
# Run all tests
dotnet test

# Run specific project tests
dotnet test tests/Spectra.MCP.Tests/

# Run with verbose output
dotnet test -v n
```

---

# End-to-End Workflow: Generate Tests and Execute via GitHub Copilot

This section walks through the complete workflow from existing documentation to test execution in VS Code.

## Overview

1. **Build and install** the Spectra tools
2. **Generate tests** from your documentation using the CLI
3. **Set up a VS Code project** with MCP configuration
4. **Execute tests** interactively via GitHub Copilot

---

## Step 1: Build and Install Spectra Tools

From the Spectra repository root:

```bash
# Build everything
dotnet build -c Release -p:NoWarn=CA1062

# Pack both tools
dotnet pack src/Spectra.CLI/Spectra.CLI.csproj -c Release -p:NoWarn=CA1062
dotnet pack src/Spectra.MCP/Spectra.MCP.csproj -c Release -p:NoWarn=CA1062

# Install globally (remove old versions first)
dotnet tool uninstall -g Spectra.CLI 2>$null
dotnet tool uninstall -g Spectra.MCP 2>$null

dotnet tool install -g --add-source src/Spectra.CLI/nupkg Spectra.CLI
dotnet tool install -g --add-source src/Spectra.MCP/nupkg Spectra.MCP

# Verify installation
spectra --help
spectra-mcp --help
```

After installation:
- `spectra` - CLI for test generation, validation, indexing
- `spectra-mcp` - MCP server for test execution

---

## Step 2: Set Up Your Project with Documentation

Create a new project folder with your existing documentation:

```bash
# Create project folder
mkdir my-qa-project
cd my-qa-project

# Initialize Spectra
spectra init
```

This creates:
```
my-qa-project/
├── spectra.config.json       # Configuration
├── docs/                     # Put your documentation here
├── tests/                    # Generated tests go here
└── .github/skills/...        # AI skill definition
```

### Add Your Documentation

Copy your existing documentation into the `docs/` folder. For example:

```
docs/
├── features/
│   ├── authentication.md
│   ├── checkout.md
│   └── user-profile.md
├── api/
│   └── endpoints.md
└── requirements/
    └── business-rules.md
```

### Configure the Source

Edit `spectra.config.json` to point to your docs:

```json
{
  "source": {
    "mode": "local",
    "local_dir": "docs/",
    "include_patterns": ["**/*.md"],
    "exclude_patterns": ["**/CHANGELOG.md", "**/README.md"]
  },
  "tests": {
    "dir": "tests/",
    "id_prefix": "TC",
    "id_start": 100
  },
  "ai": {
    "providers": [
      {
        "name": "copilot",
        "model": "gpt-4o",
        "enabled": true,
        "priority": 1
      }
    ]
  }
}
```

---

## Step 3: Generate Tests from Documentation

```bash
# Generate tests for a specific suite (feature area)
spectra ai generate checkout --count 10

# Or with more options
spectra ai generate authentication --count 15 -v

# Generate in CI mode (no interactive review)
spectra ai generate checkout --count 10 --no-review
```

The CLI will:
1. Scan your `docs/` folder for relevant documentation
2. Use AI to generate test cases
3. Present tests for interactive review (unless `--no-review`)
4. Write accepted tests to `tests/{suite}/`
5. Create/update `tests/{suite}/_index.json`

### Example Output

```
Scanning source documentation...
Found 5 source file(s)
Found 0 existing test(s) in 'checkout'
Using AI provider: copilot
Generating tests...
Generated 10 test(s)

Review generated tests:
(y=accept, n=reject, e=edit, a=accept all, q=quit)

[TC-100] Checkout with valid credit card
  Priority: high
  Steps: 5
  Accept? [y/n/e/a/q]: a

  Created: checkout/TC-100.md
  Created: checkout/TC-101.md
  ...

Updating index...

Generated 10 test(s) in 'checkout'
```

### Validate and Rebuild Index

```bash
# Validate all tests
spectra validate

# Rebuild indexes if needed
spectra index --rebuild

# List available suites
spectra list
```

---

## Step 4: Configure VS Code with MCP Server

### Option A: Project-Level Configuration (Recommended)

Create `.vscode/mcp.json` in your project:

```json
{
  "servers": {
    "spectra": {
      "command": "spectra-mcp",
      "args": ["."]
    }
  }
}
```

### Option B: Run from Spectra Source (Development)

If you want to run the MCP server from source (for debugging):

```json
{
  "servers": {
    "spectra": {
      "command": "dotnet",
      "args": [
        "run",
        "--project",
        "C:/SourceCode/Spectra/src/Spectra.MCP/Spectra.MCP.csproj",
        "--",
        "${workspaceFolder}"
      ]
    }
  }
}
```

### Option C: User-Level Configuration

Add to VS Code settings (`settings.json`):

```json
{
  "github.copilot.chat.mcpServers": {
    "spectra": {
      "command": "spectra-mcp",
      "args": ["C:/path/to/my-qa-project"]
    }
  }
}
```

---

## Step 5: Execute Tests via GitHub Copilot

Open your project in VS Code and start a Copilot Chat session.

### Discover Available Suites

**You:** What test suites are available?

**Copilot:** *(calls `list_available_suites`)*
```
Available test suites:
- checkout (10 tests)
- authentication (15 tests)
```

### Start a Test Run

**You:** Start a test run for the checkout suite

**Copilot:** *(calls `start_execution_run`)*
```
Started execution run for checkout suite.
Run ID: a1b2c3d4-e5f6-7890-abcd-ef1234567890
Total tests: 10
Progress: 0/10

First test ready: TC-100 - Checkout with valid credit card
Would you like me to show the test details?
```

### View Test Details

**You:** Show me the test

**Copilot:** *(calls `get_test_case_details`)*
```
## TC-100: Checkout with valid credit card

**Priority:** high
**Tags:** smoke, checkout, payments
**Component:** checkout

### Preconditions
- User is logged in
- Cart contains at least one item

### Steps
1. Navigate to the checkout page
2. Verify cart summary is displayed
3. Enter valid shipping address
4. Select "Credit Card" payment method
5. Enter valid card details:
   - Card: 4111 1111 1111 1111
   - Expiry: 12/2028
   - CVV: 123
6. Click "Place Order"

### Expected Result
- Order is created successfully
- Order confirmation page displays with order number
- Confirmation email is sent to user
```

### Record Test Results

**You:** The test passed

**Copilot:** *(calls `advance_test_case` with status=PASSED)*
```
Recorded: TC-100 PASSED

Next test: TC-101 - Checkout with expired card
Progress: 1/10
```

**You:** This test failed - the error message said "Invalid card" instead of "Card expired"

**Copilot:** *(calls `advance_test_case` with status=FAILED and notes)*
```
Recorded: TC-101 FAILED
Note: Error message said "Invalid card" instead of "Card expired"

2 dependent tests blocked: TC-105, TC-106

Next test: TC-102 - Checkout with PayPal
Progress: 2/10
```

### Skip a Test

**You:** Skip this test - PayPal sandbox is down

**Copilot:** *(calls `skip_test_case`)*
```
Skipped: TC-102
Reason: PayPal sandbox is down

Next test: TC-103 - Checkout with empty cart
Progress: 3/10
```

### Pause and Resume

**You:** Pause the test run

**Copilot:** *(calls `pause_execution_run`)*
```
Run paused at 5/10 tests.
Run ID: a1b2c3d4-e5f6-7890-abcd-ef1234567890

You can resume anytime by saying "Resume my checkout test run"
```

Later:

**You:** Resume my checkout test run

**Copilot:** *(calls `resume_execution_run`)*
```
Resumed run a1b2c3d4-...
Progress: 5/10

Next test: TC-105 - Checkout with discount code
```

### Complete the Run

**You:** Finalize the test run

**Copilot:** *(calls `finalize_execution_run`)*
```
## Execution Complete

**Suite:** checkout
**Duration:** 45 minutes
**Executed by:** john.doe

### Summary
| Status  | Count |
|---------|-------|
| Passed  | 6     |
| Failed  | 2     |
| Skipped | 1     |
| Blocked | 1     |
| **Total** | **10** |

### Failed Tests
- TC-101: Checkout with expired card
  Note: Error message said "Invalid card" instead of "Card expired"
- TC-104: Checkout with insufficient funds
  Note: No error displayed, page just refreshed

Report saved: .execution/reports/a1b2c3d4-....json
```

---

## Step 6: View Execution History

**You:** Show me the last 5 checkout test runs

**Copilot:** *(calls `get_run_history`)*
```
## Checkout Run History

| Date       | User     | Status    | Passed | Failed | Skipped |
|------------|----------|-----------|--------|--------|---------|
| 2026-03-18 | john.doe | Completed | 6/10   | 2      | 1       |
| 2026-03-15 | jane.doe | Completed | 8/10   | 1      | 1       |
| 2026-03-12 | john.doe | Cancelled | 3/10   | 0      | 0       |
```

---

## Directory Structure After E2E Flow

```
my-qa-project/
├── spectra.config.json
├── .vscode/
│   └── mcp.json                  # MCP server config
├── docs/
│   └── features/
│       ├── checkout.md           # Your documentation
│       └── authentication.md
├── tests/
│   ├── checkout/
│   │   ├── _index.json           # Suite metadata index
│   │   ├── TC-100.md             # Generated test case
│   │   ├── TC-101.md
│   │   └── ...
│   └── authentication/
│       ├── _index.json
│       └── ...
├── .execution/
│   ├── spectra.db                # Execution state database
│   └── reports/
│       └── a1b2c3d4-....json     # Execution reports
└── .github/
    └── skills/
        └── test-generation/
            └── SKILL.md
```

---

## Available MCP Tools Reference

| Tool | Description |
|------|-------------|
| `list_available_suites` | List all test suites with test counts |
| `start_execution_run` | Start a new test run (with optional filters) |
| `get_execution_status` | Get current run progress and status |
| `pause_execution_run` | Pause an active run |
| `resume_execution_run` | Resume a paused run |
| `cancel_execution_run` | Cancel a run |
| `finalize_execution_run` | Complete a run and generate reports |
| `get_test_case_details` | Get full test content |
| `advance_test_case` | Record pass/fail result |
| `skip_test_case` | Skip a test with reason |
| `add_test_note` | Add a note to current test |
| `retest_test_case` | Re-queue a completed test |
| `get_run_history` | View past execution runs |
| `get_execution_summary` | Get run statistics |

---

---

# Dashboard Portal Setup

Generate and serve an interactive HTML dashboard showing test suites, execution history, and coverage analysis.

## Generate the Dashboard

```bash
# Basic generation
spectra dashboard --output ./site

# With custom title
spectra dashboard --output ./site --title "My Project QA Dashboard"

# Preview what would be generated
spectra dashboard --output ./site --dry-run
```

The dashboard reads from:
- `tests/*/_index.json` - Test suite metadata
- `.execution/spectra.db` - Execution history and results
- `reports/*.json` - Execution reports

## Serve Locally

After generation, serve the static files:

```bash
# Using Python
cd site && python -m http.server 8080

# Using Node.js (npx)
npx serve site

# Using .NET
dotnet serve -d site -p 8080
```

Open `http://localhost:8080` in your browser.

## Dashboard Features

The generated dashboard includes:

- **Suite Overview**: All test suites with test counts and status
- **Execution History**: Recent test runs with pass/fail metrics
- **Coverage Map**: Visual representation of test coverage (D3.js)
- **Test Browser**: Browse and search individual test cases
- **Trend Analysis**: Pass rate trends over time

## Deploy to Cloudflare Pages

The `dashboard-site/` folder includes Cloudflare Pages configuration:

```bash
# Copy generated data to template
cp site/* dashboard-site/

# Deploy via Wrangler
cd dashboard-site
npx wrangler pages deploy . --project-name my-qa-dashboard
```

The template includes:
- `functions/_middleware.js` - OAuth authentication
- `functions/auth/callback.js` - OAuth callback handler
- `access-denied.html` - Auth error page

## Custom Templates

Use a custom template directory:

```bash
spectra dashboard --output ./site --template ./my-template
```

Your template directory should have:
- `index.html` with `{{DASHBOARD_DATA}}` placeholder
- `styles/` directory for CSS
- `scripts/` directory for JavaScript

---

# Test Generation Profiles

Profiles customize how AI generates test cases. They're optional but recommended for consistent output.

## Create a Profile

### Interactive Mode (Recommended)

```bash
spectra init-profile
```

The wizard guides you through options:

```
SPECTRA Test Generation Profile Setup

[1/7] Detail Level
How detailed should test steps be?
  1. High-level - Brief steps, assumes tester knowledge
  2. Detailed - Comprehensive steps with expected results
  3. Very detailed - Granular steps, no assumptions
> 2

[2/7] Minimum Negative Scenarios
How many negative test cases per feature (minimum)?
> 3

...

Profile created: spectra.profile.md
```

### Non-Interactive Mode (CI/Scripts)

```bash
spectra init-profile \
  --non-interactive \
  --detail-level detailed \
  --min-negative 3 \
  --priority medium \
  --step-format numbered \
  --domains payments,authentication \
  --pii-sensitivity standard
```

## View Current Profile

```bash
# Show effective profile
spectra profile show

# Show as JSON
spectra profile show --json

# Show AI context that would be generated
spectra profile show --context

# Show profile for a specific suite
spectra profile show --suite tests/checkout
```

Example output:

```
Effective Profile
=================
Source: spectra.profile.md (repository)

Detail Level:       Detailed
Negative Scenarios: 3 minimum
Default Priority:   Medium

Formatting:
  Step Format:      Numbered
  Action Verbs:     Yes
  Screenshots:      No

Domains:
  - Payments
  - Authentication
  PII Sensitivity:  Standard
  Compliance Notes: Yes
```

## Suite-Level Overrides

Create a profile that only applies to a specific test suite:

```bash
# Create override for checkout suite
spectra init-profile --suite tests/checkout
```

This creates `tests/checkout/_profile.md` that inherits from and overrides the repository profile.

## Profile Options

| Option | Values | Description |
|--------|--------|-------------|
| `--detail-level` | `high_level`, `detailed`, `very_detailed` | How granular steps should be |
| `--min-negative` | `1-10` | Minimum negative scenarios per feature |
| `--priority` | `high`, `medium`, `low` | Default test priority |
| `--step-format` | `numbered`, `bullets`, `paragraphs` | How steps are formatted |
| `--action-verbs` | `true/false` | Start steps with action verbs |
| `--screenshots` | `true/false` | Include screenshot suggestions |
| `--domains` | `payments`, `authentication`, `pii_gdpr`, `healthcare`, `financial`, `accessibility` | Domain-specific considerations |
| `--pii-sensitivity` | `none`, `standard`, `strict` | PII handling level |
| `--exclusions` | `performance`, `security`, `edge_cases`, etc. | Categories to exclude |

## Edit Existing Profile

```bash
# Update specific options
spectra init-profile --edit --min-negative 5 --priority high

# Re-run full wizard
spectra init-profile --force
```

## How Profiles Affect Generation

When you run:

```bash
spectra ai generate checkout --count 10
```

The CLI:
1. Checks for `tests/checkout/_profile.md` (suite override)
2. Falls back to `spectra.profile.md` (repository profile)
3. Falls back to built-in defaults

The profile content is injected into the AI prompt context.

### Without Profile

```markdown
## TC-101: User Login

**Steps:**
1. Go to login page
2. Enter credentials
3. Click Login
```

### With Detailed Profile + Domains

```markdown
## TC-101: User Login

**Priority:** High
**Component:** Authentication

**Preconditions:**
- User account exists with valid credentials
- Browser cache cleared

**Steps:**
1. Navigate to the application login page at /login
2. Verify the login form displays username and password fields
3. Enter the test username "testuser@example.com" in the username field
4. Enter the test password in the password field
5. Click the "Sign In" button
6. Verify redirect to dashboard page within 3 seconds

**Expected Result:**
User is logged in and sees personalized dashboard.

**Compliance Notes:**
- Password field should mask input
- Failed login should not reveal whether username exists
```

---

## Running Locally (Debug Mode)

### Option 1: Run from Source (Recommended for Development)

#### MCP Server

```bash
# Run MCP server pointing to TestFixtures
dotnet run --project src/Spectra.MCP -- "tests/TestFixtures"

# Or from the project directory
cd src/Spectra.MCP
dotnet run -- "../../tests/TestFixtures"
```

The MCP server uses stdio transport. It expects JSON-RPC messages on stdin and writes responses to stdout.

#### CLI

```bash
# Run CLI commands
dotnet run --project src/Spectra.CLI -- validate --path tests/TestFixtures

# Generate dashboard
dotnet run --project src/Spectra.CLI -- dashboard --output ./site
```

### Option 2: Install as Global Tools

#### Build and Install MCP Server

```bash
# Pack the MCP server
dotnet pack src/Spectra.MCP/Spectra.MCP.csproj -c Release

# Install globally (uninstall first if exists)
dotnet tool uninstall -g Spectra.MCP 2>/dev/null
dotnet tool install -g --add-source src/Spectra.MCP/nupkg Spectra.MCP

# Run from anywhere
spectra-mcp /path/to/your/test/folder
```

#### Build and Install CLI

```bash
# Pack the CLI
dotnet pack src/Spectra.CLI/Spectra.CLI.csproj -c Release

# Install globally
dotnet tool uninstall -g Spectra.CLI 2>/dev/null
dotnet tool install -g --add-source src/Spectra.CLI/nupkg Spectra.CLI

# Run from anywhere
spectra validate
```

---

## Testing the MCP Server Locally

### Step 1: Set Up a Test Folder

Create a new folder with sample test data:

```bash
mkdir my-test-project
cd my-test-project

# Create structure
mkdir -p docs/features
mkdir -p tests/checkout
```

### Step 2: Add Sample Documentation

Create `docs/features/checkout.md`:

```markdown
# Checkout Feature

## Overview
Users can purchase items through the checkout flow.

## Steps
1. Add items to cart
2. Go to checkout
3. Enter shipping info
4. Enter payment info
5. Confirm order

## Payment Methods
- Credit card (Visa, Mastercard)
- PayPal

## Error Cases
- Invalid card number
- Expired card
- Insufficient funds
```

### Step 3: Add Sample Test Cases

Create `tests/checkout/TC-001.md`:

```markdown
---
id: TC-001
priority: high
tags: [smoke, checkout]
component: checkout
source_refs: [docs/features/checkout.md]
---

# Checkout with valid Visa card

## Preconditions
- User is logged in
- Cart has at least one item

## Steps
1. Navigate to checkout page
2. Enter valid shipping address
3. Enter valid Visa card (4111111111111111, exp: 12/2028)
4. Click "Place Order"

## Expected Result
- Order is placed successfully
- Confirmation page displays order number
- Confirmation email is sent
```

Create `tests/checkout/TC-002.md`:

```markdown
---
id: TC-002
priority: high
tags: [negative, checkout, payments]
component: checkout
depends_on: [TC-001]
source_refs: [docs/features/checkout.md]
---

# Checkout with expired card

## Preconditions
- User is logged in
- Cart has at least one item

## Steps
1. Navigate to checkout page
2. Enter valid shipping address
3. Enter expired card (4111111111111111, exp: 01/2020)
4. Click "Place Order"

## Expected Result
- Payment is rejected
- Error message: "Card has expired"
- User remains on payment page
```

### Step 4: Create the Index File

Create `tests/checkout/_index.json`:

```json
{
  "suite": "checkout",
  "generated_at": "2026-03-18T12:00:00Z",
  "test_count": 2,
  "tests": [
    {
      "id": "TC-001",
      "title": "Checkout with valid Visa card",
      "priority": "high",
      "file": "TC-001.md",
      "tags": ["smoke", "checkout"],
      "source_refs": ["docs/features/checkout.md"]
    },
    {
      "id": "TC-002",
      "title": "Checkout with expired card",
      "priority": "high",
      "file": "TC-002.md",
      "tags": ["negative", "checkout", "payments"],
      "depends_on": ["TC-001"],
      "source_refs": ["docs/features/checkout.md"]
    }
  ]
}
```

### Step 5: Run the MCP Server

```bash
# From source
dotnet run --project /path/to/Spectra/src/Spectra.MCP -- .

# Or if installed globally
spectra-mcp .
```

### Step 6: Test with an MCP Client

The MCP server communicates via JSON-RPC over stdio. You can test manually:

```bash
# Send a list_available_suites request
echo '{"jsonrpc":"2.0","id":1,"method":"tools/call","params":{"name":"list_available_suites","arguments":{}}}' | spectra-mcp .
```

---

## Configuring MCP Server in AI Assistants

### Claude Desktop

Add to your Claude Desktop config (`%APPDATA%\Claude\claude_desktop_config.json` on Windows or `~/Library/Application Support/Claude/claude_desktop_config.json` on macOS):

```json
{
  "mcpServers": {
    "spectra": {
      "command": "spectra-mcp",
      "args": ["C:/path/to/your/test/project"]
    }
  }
}
```

Or run from source:

```json
{
  "mcpServers": {
    "spectra": {
      "command": "dotnet",
      "args": [
        "run",
        "--project",
        "C:/SourceCode/Spectra/src/Spectra.MCP/Spectra.MCP.csproj",
        "--",
        "C:/path/to/your/test/project"
      ]
    }
  }
}
```

### VS Code with GitHub Copilot

MCP server configuration for VS Code Copilot (when supported) would go in `.vscode/mcp.json`:

```json
{
  "servers": {
    "spectra": {
      "command": "spectra-mcp",
      "args": ["${workspaceFolder}"]
    }
  }
}
```

### Claude Code (CLI)

Add to your project's `.mcp.json`:

```json
{
  "mcpServers": {
    "spectra": {
      "command": "spectra-mcp",
      "args": ["."]
    }
  }
}
```

---

## Example MCP Conversation

Once configured, you can interact with the MCP server through your AI assistant:

**User:** What test suites are available?

**Assistant:** (calls `list_available_suites`)
```
Available test suites:
- checkout (2 tests)
```

**User:** Start a test run for checkout

**Assistant:** (calls `start_execution_run`)
```
Started execution run for checkout suite.
Run ID: abc123...
Total tests: 2
First test: TC-001 - Checkout with valid Visa card
```

**User:** Show me the test

**Assistant:** (calls `get_test_case_details`)
```
## TC-001: Checkout with valid Visa card
Priority: high
Tags: smoke, checkout

### Preconditions
- User is logged in
- Cart has at least one item

### Steps
1. Navigate to checkout page
2. Enter valid shipping address
3. Enter valid Visa card (4111111111111111, exp: 12/2028)
4. Click "Place Order"

### Expected Result
- Order is placed successfully
- Confirmation page displays order number
- Confirmation email is sent
```

**User:** The test passed

**Assistant:** (calls `advance_test_case`)
```
Recorded: TC-001 PASSED
Next test: TC-002 - Checkout with expired card
Progress: 1/2
```

---

## Troubleshooting

### "Suite not found"
Ensure your test folder has:
1. A `tests/` subdirectory
2. Suite folders inside `tests/` (e.g., `tests/checkout/`)
3. Valid `_index.json` in each suite folder

### "Index stale"
Regenerate the index:
```bash
spectra index --rebuild
```

### MCP server exits immediately
Check that:
1. The base path argument is valid
2. The `.execution/` directory can be created
3. No other process is using the SQLite database

### Build errors (CA1062 warnings)
Build with warnings suppressed:
```bash
dotnet build -p:NoWarn=CA1062
```

### VS Code / Copilot doesn't see the MCP server

1. **Verify the MCP config exists:**
   ```bash
   cat .vscode/mcp.json
   ```

2. **Verify spectra-mcp is in PATH:**
   ```bash
   which spectra-mcp   # Linux/macOS
   where spectra-mcp   # Windows
   ```

3. **Test the MCP server manually:**
   ```bash
   echo '{"jsonrpc":"2.0","id":1,"method":"tools/list","params":{}}' | spectra-mcp .
   ```

4. **Reload VS Code window** after changing MCP config

5. **Check Copilot Chat output** for MCP connection errors

### "No spectra.config.json found"

Run `spectra init` to create the configuration file:
```bash
spectra init
```

### AI provider not available

1. **For GitHub Copilot:** Ensure you're signed in to GitHub in VS Code
2. **For BYOK (OpenAI/Anthropic):** Set the API key environment variable:
   ```bash
   export ANTHROPIC_API_KEY="sk-..."
   # or
   export OPENAI_API_KEY="sk-..."
   ```

---

## Using TestFixtures for Development

The `tests/TestFixtures/` folder contains ready-to-use sample data:

```bash
# Run MCP server against fixtures
dotnet run --project src/Spectra.MCP -- tests/TestFixtures

# Available suites in fixtures:
# - auth (3 tests)
# - checkout (1 test)
```

This provides a quick way to test without setting up your own data.
