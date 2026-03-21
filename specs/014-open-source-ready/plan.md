# Implementation Plan: Open Source Ready

**Branch**: `014-open-source-ready` | **Date**: 2026-03-21 | **Spec**: [spec.md](spec.md)
**Input**: Feature specification from `/specs/014-open-source-ready/spec.md`

## Summary

Make SPECTRA open-source ready: README redesign with banner/badges/features, CI pipeline (build+test on PR), NuGet publish pipeline (tag-triggered), fix all failing tests, documentation cleanup, issue/PR templates, Dependabot config.

**Key finding from research**: Most infrastructure already exists — LICENSE (MIT), .editorconfig, CONTRIBUTING.md, docs/ (17 guides), NuGet packaging in .csproj files. The work is primarily: README visual redesign, two new GitHub Actions workflows, test fixes, and community templates.

## Technical Context

**Language/Version**: C# 12 / .NET 8+
**Primary Dependencies**: GitHub Actions (CI/CD), NuGet (package publishing), dotnet CLI (build/test/pack)
**Storage**: N/A — repo infrastructure only
**Testing**: xUnit (fix existing failures)
**Target Platform**: Cross-platform (.NET 8, runs on ubuntu-latest in CI)
**Project Type**: CLI tool + library
**Performance Goals**: CI pipeline under 10 minutes (SC-002)
**Constraints**: No secrets in logs. Fork PRs run CI without secrets. Cross-platform test compatibility.
**Scale/Scope**: 2 workflow files, 1 README rewrite, ~20 test fixes, 5 template files, 1 Dependabot config

## Constitution Check

| Principle | Status | Notes |
|-----------|--------|-------|
| I. GitHub as Source of Truth | PASS | All changes are repo files — workflows, README, templates, docs |
| II. Deterministic Execution | N/A | Infrastructure only, no execution state changes |
| III. Orchestrator-Agnostic | N/A | No orchestrator integration |
| IV. CLI-First Interface | PASS | NuGet publishing makes CLI installable via `dotnet tool install` |
| V. Simplicity (YAGNI) | PASS | Standard GitHub Actions patterns, no custom tooling |

## Project Structure

### Documentation (this feature)

```text
specs/014-open-source-ready/
├── plan.md              # This file
├── spec.md              # Feature specification
├── research.md          # Phase 0 research findings
├── quickstart.md        # Phase 1 quickstart
├── checklists/
│   └── requirements.md  # Spec quality checklist
└── tasks.md             # Phase 2 tasks (created by /speckit.tasks)
```

### Source Code (repository root)

```text
# New files
.github/
├── workflows/
│   ├── ci.yml                        # New: CI pipeline (build + test)
│   └── publish.yml                   # New: NuGet publish on tag
├── ISSUE_TEMPLATE/
│   ├── bug_report.md                 # New: Bug report template
│   └── feature_request.md           # New: Feature request template
├── pull_request_template.md          # New: PR checklist template
└── dependabot.yml                    # New: NuGet dependency updates

README.md                             # Rewrite: full visual redesign
assets/
└── spectra_github_readme_banner.png  # New: banner image (placeholder)

# Existing files (update)
CONTRIBUTING.md                       # Update: verify build/test/PR instructions
docs/                                 # Update: verify all links resolve

# Existing files (verify, no changes expected)
LICENSE                               # Verify: MIT with correct copyright
.editorconfig                         # Verify: already configured

# Test fixes
tests/Spectra.CLI.Tests/              # Fix: failing tests
```

**Structure Decision**: All changes are repo-level files. No new C# projects or library code. Test fixes are modifications to existing test files.

## Implementation Phases

### Phase A: Test Fixes (P1 — US4)

**Goal**: Achieve 100% green test suite on clean clone.

**Current state**: 20 failing tests in Spectra.CLI.Tests (CriticFactory auth tests, Quickstart workflow tests, GenerateCommand tests). Core and MCP tests pass 100%.

**Approach**:
1. Run `dotnet test` to get full failure report
2. Categorize failures: missing config, path issues, auth dependencies, flaky
3. Fix each category:
   - Missing config → add test fixtures with default config
   - Auth/external deps → mock or stub the dependency
   - Path issues → use temp directories or relative paths
   - Flaky → stabilize or document skip reason
4. Verify 100% pass on clean state

### Phase B: CI Pipeline (P1 — US2)

**Goal**: Automated build + test on every push/PR.

**File**: `.github/workflows/ci.yml`

**Steps**:
1. Trigger on push to main + PR to main
2. Checkout, setup .NET 8
3. `dotnet restore`
4. `dotnet build --configuration Release --no-restore`
5. `dotnet test --configuration Release --no-restore --logger trx --results-directory ./test-results`
6. Upload test-results as artifact (always, even on failure)

### Phase C: README Redesign (P1 — US1)

**Goal**: Professional README matching Testimize style.

**Sections** (in order):
1. Banner image (centered, full width)
2. Badge row (NuGet CLI, NuGet MCP, CI, License, .NET)
3. One-liner tagline
4. "Why SPECTRA?" — 6 value props with emoji icons
5. "Key Features" — 6 detailed feature blocks with emoji headings
6. "Quick Start" — concise, copy-paste installation + first generation
7. Architecture diagram (keep existing)
8. Ecosystem table (BELLATRIX + Testimize + SPECTRA)
9. Documentation links table (already exists, update if needed)
10. Project Status (keep existing phase list)
11. Contributing (link to CONTRIBUTING.md)
12. License (MIT badge + link)

**Banner**: Create `assets/` directory with placeholder image reference. Actual design is out of scope.

### Phase D: NuGet Publishing (P2 — US3)

**Goal**: Tag-triggered NuGet package publishing.

**File**: `.github/workflows/publish.yml`

**Steps**:
1. Trigger on push of tags `v*`
2. Checkout, setup .NET 8
3. Extract version from tag (`${GITHUB_REF#refs/tags/v}`)
4. `dotnet restore && dotnet build --configuration Release`
5. `dotnet test --configuration Release` (gate — don't publish if tests fail)
6. `dotnet pack` both Spectra.CLI and Spectra.MCP with `/p:Version=$VERSION`
7. `dotnet nuget push` with `--skip-duplicate`
8. Requires `NUGET_API_KEY` secret

### Phase E: License & Docs Verification (P2 — US5, US6)

**Goal**: Verify LICENSE is correct, all doc links resolve.

**Changes**:
1. Verify LICENSE file — already MIT with correct copyright (confirmed by research)
2. Walk all links in README.md and docs/ files — fix any broken ones
3. Ensure docs/ has all expected files (confirmed: 17 guides exist)
4. Update CONTRIBUTING.md if needed

### Phase F: Community Templates (P3 — US7)

**Goal**: Issue templates, PR template, Dependabot.

**Files**:
1. `.github/ISSUE_TEMPLATE/bug_report.md` — structured bug report
2. `.github/ISSUE_TEMPLATE/feature_request.md` — feature request template
3. `.github/pull_request_template.md` — PR checklist
4. `.github/dependabot.yml` — weekly NuGet updates

## Constitution Check — Post-Design

| Principle | Status | Notes |
|-----------|--------|-------|
| I. GitHub as Source of Truth | PASS | All changes are repo files |
| II. Deterministic Execution | N/A | Infrastructure only |
| III. Orchestrator-Agnostic | N/A | No orchestrator changes |
| IV. CLI-First Interface | PASS | NuGet enables `dotnet tool install` |
| V. Simplicity (YAGNI) | PASS | Standard GitHub Actions, no custom tooling |

## Complexity Tracking

No constitution violations. Standard open-source infrastructure patterns.
