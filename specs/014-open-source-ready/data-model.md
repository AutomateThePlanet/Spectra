# Data Model: Open-Source Readiness

**Feature**: 014-open-source-ready
**Date**: 2026-03-21

## Overview

This feature has **no data model changes**. It is a repo configuration and content feature that creates/updates markdown files, YAML workflows, and GitHub templates. No entities, database schemas, or application models are affected.

## File Inventory

The "entities" in this feature are the files themselves:

### New Files (to create)

| File | Format | Purpose |
|------|--------|---------|
| `.github/workflows/ci.yml` | YAML | GitHub Actions CI workflow |
| `.github/workflows/publish.yml` | YAML | GitHub Actions NuGet publish workflow |
| `.github/ISSUE_TEMPLATE/bug_report.md` | Markdown (YAML frontmatter) | Bug report issue template |
| `.github/ISSUE_TEMPLATE/feature_request.md` | Markdown (YAML frontmatter) | Feature request issue template |
| `.github/PULL_REQUEST_TEMPLATE.md` | Markdown | PR checklist template |
| `.github/dependabot.yml` | YAML | Dependabot configuration |

### Updated Files (existing)

| File | Format | Changes |
|------|--------|---------|
| `README.md` | Markdown + HTML | Full redesign with Testimize-style layout |
| `CONTRIBUTING.md` | Markdown | Expand with build/test/style/PR sections |

### Verified Files (no changes expected)

| File | Format | Current State |
|------|--------|---------------|
| `LICENSE` | Plain text | MIT, correct copyright |
| `.editorconfig` | INI-like | Comprehensive C# 12 rules |
| `Directory.Build.props` | XML | Version 0.1.0, correct metadata |
| `docs/*` (16 files) | Markdown | All substantive, no stubs |

## GitHub Issue Template Schema

Issue templates use YAML frontmatter for GitHub's template chooser:

```yaml
---
name: "Template Display Name"
about: "Short description"
title: "[PREFIX] "
labels: ["label1", "label2"]
assignees: []
---
```

## GitHub Actions Workflow Schema

Workflows follow the GitHub Actions YAML schema:

```yaml
name: "Workflow Name"
on:
  push: { branches: [...] }      # or tags: [...]
  pull_request: { branches: [...] }
jobs:
  job-name:
    runs-on: ubuntu-latest
    steps:
      - uses: action@version
      - run: command
```

## NuGet Package Metadata

Already defined in `.csproj` files:

| Property | Spectra.CLI | Spectra.MCP |
|----------|-------------|-------------|
| PackageId | Spectra.CLI | Spectra.MCP |
| PackAsTool | true | true |
| ToolCommandName | spectra | spectra-mcp |
| Version (csproj) | 1.10.16 | 1.10.8 |
| Version (publish) | From git tag | From git tag |
