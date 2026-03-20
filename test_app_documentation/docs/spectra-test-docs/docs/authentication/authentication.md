# Authentication System

## Overview

The system supports multiple authentication methods for internal employees and external citizens accessing the public portal. All authentication events are logged to the audit trail.

## Login Methods

### Username and Password

Users authenticate with their email address and a password. Passwords must meet the following policy:

- Minimum 12 characters
- At least one uppercase letter, one lowercase letter, one digit, and one special character (!@#$%^&*)
- Cannot reuse the last 5 passwords
- Passwords expire every 90 days for internal users; never for citizens
- Temporary passwords issued by administrators expire after 24 hours and must be changed on first login

### Multi-Factor Authentication (MFA)

MFA is mandatory for all internal users and optional for citizens.

Supported MFA methods:
- **TOTP** (Time-based One-Time Password): Google Authenticator, Microsoft Authenticator
- **SMS OTP**: 6-digit code sent to registered phone number, valid for 5 minutes
- **Email OTP**: 6-digit code sent to registered email, valid for 10 minutes

If the user has not enrolled in MFA and it is required, the system forces enrollment at next login before granting access.

MFA codes can be entered incorrectly up to 3 times. After 3 failed attempts, the MFA session is invalidated and the user must restart the login process from the beginning.

### Single Sign-On (SSO)

Internal users can authenticate via the organization's Azure AD tenant using SAML 2.0. When SSO is enabled for a user's domain, the username/password login form is hidden and the user is redirected to the IdP automatically.

If SSO authentication succeeds but the user does not exist in the local system, a new account is provisioned automatically with the "Employee" role and the department from the SAML assertion.

## Account Lockout

After 5 consecutive failed login attempts, the account is locked for 30 minutes. During lockout:
- The user sees a generic message: "Your account has been temporarily locked. Please try again later or contact support."
- The system does NOT reveal whether the account exists
- An administrator can manually unlock the account before the 30-minute window expires
- Each failed attempt during lockout resets the 30-minute timer

## Session Management

- Session timeout: 30 minutes of inactivity for internal users, 60 minutes for citizens
- Maximum concurrent sessions: 3 per user
- When a 4th session is initiated, the oldest session is terminated automatically
- Sessions are bound to the user's IP address. If the IP changes mid-session, the session is invalidated and the user must re-authenticate
- "Remember me" option extends the session to 14 days but still requires MFA on each new browser

## Password Reset

### Self-Service Reset

1. User clicks "Forgot Password" on the login page
2. User enters their email address
3. System sends a password reset link to the email (valid for 1 hour)
4. The link can be used exactly once
5. User sets a new password that meets the password policy
6. All active sessions for the user are terminated upon password change

The system always displays "If an account exists with this email, you will receive a reset link" regardless of whether the email exists in the system.

### Administrator Reset

Administrators can issue a temporary password for any user. The temporary password:
- Is auto-generated (16 characters, meets password policy)
- Must be changed on first login
- Expires after 24 hours if not used
- Is sent to the user's registered email, never displayed to the administrator

## Rate Limiting

- Login attempts: maximum 10 per minute per IP address
- Password reset requests: maximum 3 per hour per email address
- MFA code requests (SMS/Email): maximum 5 per hour per user
- After exceeding rate limits, the IP or user is blocked for 1 hour with HTTP 429 response

## Audit Requirements

Every authentication event must be logged with:
- Timestamp (UTC)
- User identifier (or "unknown" for failed attempts with invalid usernames)
- Event type (LOGIN_SUCCESS, LOGIN_FAILED, LOGOUT, MFA_SUCCESS, MFA_FAILED, PASSWORD_CHANGED, PASSWORD_RESET_REQUESTED, ACCOUNT_LOCKED, ACCOUNT_UNLOCKED, SESSION_EXPIRED, SSO_PROVISIONED)
- Source IP address
- User agent string
- Result (success/failure) and failure reason if applicable
