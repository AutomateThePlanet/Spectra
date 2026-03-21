# Contract: OAuth Middleware (Cloudflare Pages Functions)

**Date**: 2026-03-21

## Environment Variables

| Variable | Required | Description |
|----------|----------|-------------|
| AUTH_ENABLED | No | Set to "true" to enable authentication (default: disabled) |
| GITHUB_CLIENT_ID | Yes (if auth enabled) | GitHub OAuth App client ID |
| GITHUB_CLIENT_SECRET | Yes (if auth enabled) | GitHub OAuth App client secret |
| SESSION_SECRET | Yes (if auth enabled) | Random 32+ char string for HMAC-SHA256 signing |
| ALLOWED_REPOS | No | Comma-separated `org/repo` values for access control |

## Authentication Flow

```
GET /dashboard
  → No session cookie? → Redirect to GitHub OAuth (/login)
  → Valid session cookie? → Serve page
  → Expired session? → Redirect to GitHub OAuth

GET /auth/callback?code=XXX&state=YYY
  → Exchange code for access token
  → Fetch user info from GitHub API
  → If ALLOWED_REPOS set: check user has read access to at least one repo
  → If access granted: set signed session cookie (24h), redirect to original URL
  → If access denied: redirect to /access-denied.html?error=no_repo_access

GET /auth/logout
  → Clear session cookie
  → Redirect to /
```

## Session Cookie

| Property | Value |
|----------|-------|
| Name | `__spectra_session` |
| HttpOnly | true |
| Secure | true |
| SameSite | Lax |
| Max-Age | 86400 (24 hours) |
| Format | `base64(payload).base64(hmac-sha256-signature)` |

## Unauthenticated Paths (bypass)

- Static assets: `.css`, `.js`, `.png`, `.jpg`, `.svg`, `.ico`, `.woff`, `.woff2`
- `/access-denied.html`
- `/auth/*` paths
- `/favicon.ico`
- `/robots.txt`

## ALLOWED_REPOS Change (from ALLOWED_REPO)

**Before**: Single repo string `"org/repo"`
**After**: Comma-separated string `"org/repo1,org/repo2"`
**Logic**: Grant access if user has read access to ANY listed repo. If not set, any authenticated GitHub user can access.
