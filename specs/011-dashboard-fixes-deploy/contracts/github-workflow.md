# Contract: GitHub Actions Deployment Workflow

**Date**: 2026-03-21

## Workflow: `.github/workflows/deploy-dashboard.yml`

### Triggers

```yaml
on:
  push:
    branches: [main]
    paths:
      - 'tests/**'
      - '.execution/**'
      - 'docs/**'
      - 'spectra.config.json'
  workflow_dispatch:
```

### Required Secrets

| Secret | Description |
|--------|-------------|
| CLOUDFLARE_API_TOKEN | Cloudflare API token with "Edit Cloudflare Pages" permissions |
| CLOUDFLARE_ACCOUNT_ID | Cloudflare account ID |

### Steps

1. `actions/checkout@v4`
2. `actions/setup-dotnet@v4` with dotnet-version 8.0.x
3. Install spectra: `dotnet tool install --global Spectra.CLI` (continue-on-error)
4. Run coverage: `spectra ai analyze --coverage --auto-link` (continue-on-error: true)
5. Generate dashboard: `spectra dashboard --output ./site`
6. Deploy: `cloudflare/wrangler-action@v3` with:
   - `apiToken: ${{ secrets.CLOUDFLARE_API_TOKEN }}`
   - `accountId: ${{ secrets.CLOUDFLARE_ACCOUNT_ID }}`
   - `command: pages deploy ./site --project-name=$PROJECT_NAME`

### Project Name Resolution

The workflow reads `cloudflare_project_name` from `spectra.config.json` dashboard section. Falls back to `"spectra-dashboard"` if not configured.
