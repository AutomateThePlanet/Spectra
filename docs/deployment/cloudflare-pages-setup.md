---
title: Cloudflare Pages Setup
parent: Deployment
nav_order: 1
---

# Cloudflare Pages Dashboard Deployment Setup

This guide walks you through deploying the SPECTRA dashboard to Cloudflare Pages with optional GitHub OAuth authentication.

## Prerequisites

- A GitHub repository with SPECTRA configured (`spectra.config.json` exists)
- A [Cloudflare account](https://dash.cloudflare.com/sign-up) (free tier works)
- Admin access to your GitHub repository (for adding secrets)

## Step 1: Create a Cloudflare Pages Project

1. Go to [Cloudflare Dashboard](https://dash.cloudflare.com) > **Workers & Pages** > **Create**
2. Select **Pages** > **Direct Upload**
3. Name your project (e.g., `spectra-dashboard`) — note this name, you'll need it later
4. Upload any placeholder file to complete creation
5. Note your **Account ID** from the right sidebar of the dashboard

## Step 2: Create a Cloudflare API Token

1. Go to [API Tokens](https://dash.cloudflare.com/profile/api-tokens)
2. Click **Create Token**
3. Use the **Edit Cloudflare Pages** template
4. Under **Account Resources**, select your account
5. Click **Continue to summary** > **Create Token**
6. Copy the token — you won't see it again

## Step 3: Create a GitHub OAuth App (for authentication)

> Skip this step if you don't need authentication.

1. Go to [GitHub Developer Settings](https://github.com/settings/developers)
2. Click **New OAuth App**
3. Fill in:
   - **Application name**: `SPECTRA Dashboard`
   - **Homepage URL**: `https://<your-project>.pages.dev`
   - **Authorization callback URL**: `https://<your-project>.pages.dev/auth/callback`
4. Click **Register application**
5. Note the **Client ID**
6. Click **Generate a new client secret** and copy it

## Step 4: Configure GitHub Repository Secrets

Go to your repository > **Settings** > **Secrets and variables** > **Actions** > **New repository secret**:

| Secret Name | Value |
|---|---|
| `CLOUDFLARE_API_TOKEN` | The API token from Step 2 |
| `CLOUDFLARE_ACCOUNT_ID` | Your Cloudflare account ID from Step 1 |

## Step 5: Configure Cloudflare Pages Environment Variables

> Skip this step if you don't need authentication.

Go to your Cloudflare Pages project > **Settings** > **Environment variables** > **Production**:

| Variable | Value | Encrypted |
|---|---|---|
| `AUTH_ENABLED` | `true` | No |
| `GITHUB_CLIENT_ID` | Client ID from Step 3 | No |
| `GITHUB_CLIENT_SECRET` | Client secret from Step 3 | Yes |
| `ALLOWED_REPOS` | `owner/repo` (comma-separated for multiple) | No |
| `SESSION_SECRET` | A random string (generate with `openssl rand -hex 32`) | Yes |

## Step 6: Update Project Configuration

In your `spectra.config.json`, set the project name to match your Cloudflare Pages project:

```json
{
  "dashboard": {
    "cloudflare_project_name": "spectra-dashboard"
  }
}
```

## Step 7: First Deployment

Option A — **Push to main**: Make any change to a file in `tests/`, `docs/`, or `.execution/` and push to `main`. The workflow triggers automatically.

Option B — **Manual trigger**: Go to **Actions** > **Deploy SPECTRA Dashboard** > **Run workflow**.

Verify the deployment:
1. Check the Actions tab for workflow completion
2. Visit `https://<your-project>.pages.dev`
3. If auth is enabled, you'll be redirected to GitHub login

## Custom Domain (Optional)

1. Go to Cloudflare Pages project > **Custom domains**
2. Add your domain
3. Update DNS records as instructed
4. Update the GitHub OAuth app URLs to use your custom domain

## Troubleshooting

### Access Denied — "no_repo_access"
The authenticated user doesn't have read access to any repository in `ALLOWED_REPOS`. Verify:
- The `ALLOWED_REPOS` value uses `owner/repo` format
- The user has at least read access to the repository
- Multiple repos are comma-separated: `org/repo1,org/repo2`

### OAuth Callback Error
The callback URL in your GitHub OAuth app doesn't match your deployment URL. Verify:
- Callback URL is `https://<your-project>.pages.dev/auth/callback`
- If using a custom domain, update the callback URL accordingly

### Deploy Fails — Token Permissions
The Cloudflare API token doesn't have sufficient permissions. Verify:
- Token was created with the "Edit Cloudflare Pages" template
- Token is scoped to the correct account

### Empty Dashboard
SPECTRA commands failed during the workflow. Check:
- `spectra.config.json` exists and is valid
- Test files exist in the `tests/` directory
- Run `spectra dashboard --output ./site` locally to verify

### Session Issues After SECRET Rotation
Rotating `SESSION_SECRET` invalidates all existing sessions. Users will need to re-authenticate — this is expected behavior.

## Security Notes

- OAuth checks that authenticated users have read access to at least one allowed repository
- Session cookies are signed with HMAC-SHA256 and expire after 24 hours
- Static assets (CSS, JS, images) are served without authentication
- For enterprise requirements, consider [Cloudflare Access](https://www.cloudflare.com/products/zero-trust/) as an additional layer
