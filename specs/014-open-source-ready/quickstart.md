# Quickstart: Open-Source Readiness

**Feature**: 014-open-source-ready
**Date**: 2026-03-21

## Implementation Order

Execute phases in this order (dependencies flow downward):

```
Phase A: Test Health ──→ Phase B: CI/CD Workflows
                    ──→ Phase C: README Redesign
                    ──→ Phase D: Contributor Templates
                    ──→ Phase E: Dependabot
                         ↓
                    Phase F: Verification
```

Phase A must complete first (CI can't pass if tests fail). Phases B-E are independent and can run in parallel. Phase F verifies everything.

## Phase A: Fix Failing Tests

```bash
# 1. Run tests and capture output
dotnet test --configuration Release 2>&1 | tee test-results-before.txt

# 2. Fix failures (common patterns):
#    - Path separators: use Path.Combine() instead of string concatenation
#    - Missing fixtures: add test data or use temp directories
#    - External deps: mock or skip with reason

# 3. Verify all green
dotnet test --configuration Release
```

## Phase B: CI/CD Workflows

### ci.yml
```yaml
# .github/workflows/ci.yml
name: CI
on:
  push:
    branches: [main]
  pull_request:
    branches: [main]
jobs:
  build-and-test:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '8.0.x'
      - run: dotnet restore
      - run: dotnet build --configuration Release --no-restore
      - run: dotnet test --configuration Release --no-restore --logger trx --results-directory ./test-results
      - uses: actions/upload-artifact@v4
        if: always()
        with:
          name: test-results
          path: ./test-results
```

### publish.yml
```yaml
# .github/workflows/publish.yml
name: Publish NuGet
on:
  push:
    tags: ['v*']
jobs:
  publish:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '8.0.x'
      - run: dotnet restore
      - run: dotnet build --configuration Release
      - run: dotnet test --configuration Release
      - name: Extract version from tag
        id: version
        run: echo "VERSION=${GITHUB_REF_NAME#v}" >> $GITHUB_OUTPUT
      - run: dotnet pack src/Spectra.CLI/Spectra.CLI.csproj -c Release -o ./nupkg /p:Version=${{ steps.version.outputs.VERSION }}
      - run: dotnet pack src/Spectra.MCP/Spectra.MCP.csproj -c Release -o ./nupkg /p:Version=${{ steps.version.outputs.VERSION }}
      - run: dotnet nuget push ./nupkg/*.nupkg --api-key ${{ secrets.NUGET_API_KEY }} --source https://api.nuget.org/v3/index.json --skip-duplicate
```

## Phase C: README Structure

```markdown
<!-- Banner -->
<p align="center">
  <img src="assets/spectra_github_readme_banner.png" alt="SPECTRA" width="100%">
</p>

<!-- Badges -->
<p align="center">
  [NuGet CLI] [NuGet MCP] [CI Status] [License] [.NET 8.0+]
</p>

<!-- Tagline -->
<p align="center">
  <strong>AI-native test generation and execution framework.</strong>
</p>

## Why SPECTRA?
🔍 Reads docs | 🧠 AI guardrails | 📋 Markdown tests | ⚡ Deterministic execution | 📊 Coverage | 🔗 No migration

## Key Features
### 🤖 AI Test Generation
### ✅ Grounding Verification
### 📈 Coverage Analysis
### 🖥️ Visual Dashboard
### 🎯 MCP Execution Engine
### 🔧 Generation Profiles

## Quick Start
## Architecture
## Ecosystem (BELLATRIX + Testimize + SPECTRA table)
## Documentation (links table)
## Project Status
## Contributing
## License
```

## Phase D: Contributor Templates

Create 4 files:
1. `.github/ISSUE_TEMPLATE/bug_report.md` — with name, about, labels frontmatter
2. `.github/ISSUE_TEMPLATE/feature_request.md` — with name, about, labels frontmatter
3. `.github/PULL_REQUEST_TEMPLATE.md` — checklist (tests, docs, breaking changes)
4. Update `CONTRIBUTING.md` — add build/test/style/PR sections

## Phase E: Dependabot

```yaml
# .github/dependabot.yml
version: 2
updates:
  - package-ecosystem: nuget
    directory: /
    schedule:
      interval: weekly
```

## Phase F: Verification Checklist

```bash
# Build
dotnet build --configuration Release

# Test
dotnet test --configuration Release

# Pack
dotnet pack src/Spectra.CLI/Spectra.CLI.csproj -c Release -o ./nupkg
dotnet pack src/Spectra.MCP/Spectra.MCP.csproj -c Release -o ./nupkg

# Verify README links (manual check or script)
# Verify CI YAML syntax (push to branch, check Actions tab)
```
