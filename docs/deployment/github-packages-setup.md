---
title: GitHub Packages Setup
parent: Deployment
nav_order: 2
---

# GitHub Packages Setup for Spectra CLI

This guide explains how to configure cross-repo access to the Spectra CLI NuGet package hosted on GitHub Packages. This is needed when a project (e.g., Spectra_Demo) uses a GitHub Actions workflow to install Spectra CLI from the AutomateThePlanet/Spectra private feed.

## Why is this needed?

Spectra CLI is published as a private NuGet package on GitHub Packages under the `AutomateThePlanet/Spectra` repository. The default `GITHUB_TOKEN` in GitHub Actions only has access to packages in the **current** repository — it cannot read packages from other repos. A Personal Access Token (PAT) with `read:packages` scope is required for cross-repo access.

## Step 1: Generate a Personal Access Token (PAT)

1. Go to [GitHub Token Settings](https://github.com/settings/tokens)
2. Click **"Generate new token (classic)"** (the second option)
3. Configure:
   - **Note**: `Spectra packages read`
   - **Expiration**: `90 days` (or `No expiration` for CI)
   - **Scopes**: check **only** `read:packages`
4. Click **"Generate token"**
5. **Copy the token immediately** — it is shown only once

## Step 2: Add the Token as a Repository Secret

1. Go to your project's repository (e.g., `AutomateThePlanet/Spectra_Demo`)
2. Navigate to **Settings** → **Secrets and variables** → **Actions**
3. Click **"New repository secret"**
4. Fill in:
   - **Name**: `GH_PACKAGES_TOKEN`
   - **Value**: the token you copied in Step 1
5. Click **"Add secret"**

## Step 3: Use in GitHub Actions Workflow

The `deploy-dashboard.yml` workflow (created by `spectra init`) already references this secret:

```yaml
- name: Install SPECTRA CLI
  run: |
    dotnet nuget add source "https://nuget.pkg.github.com/AutomateThePlanet/index.json" \
      --name github-packages \
      --username AutomateThePlanet \
      --password ${{ secrets.GH_PACKAGES_TOKEN }} \
      --store-password-in-clear-text
    dotnet tool install -g Spectra.CLI
```

After completing these steps, the GitHub Action will be able to install Spectra CLI from the private GitHub Packages feed.

## Local Development

To install Spectra CLI locally from GitHub Packages:

```bash
dotnet nuget add source "https://nuget.pkg.github.com/AutomateThePlanet/index.json" \
  --name github-packages \
  --username YOUR_GITHUB_USERNAME \
  --password YOUR_PAT_TOKEN \
  --store-password-in-clear-text

dotnet tool install -g Spectra.CLI
```

## Troubleshooting

| Error | Cause | Fix |
|-------|-------|-----|
| `spectra-cli is not found in NuGet feeds` | GitHub Packages source not configured | Add the source as shown above |
| `401 Unauthorized` | Token expired or missing `read:packages` scope | Regenerate the PAT with correct scope |
| `403 Forbidden` | Token doesn't have access to the org | Ensure the token owner is a member of `AutomateThePlanet` |
