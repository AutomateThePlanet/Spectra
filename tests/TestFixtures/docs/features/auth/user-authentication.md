# User Authentication

## Overview

The authentication system handles user login, registration, and session management.

## Login Flow

1. User navigates to login page
2. User enters email and password
3. System validates credentials
4. On success: redirect to dashboard
5. On failure: show error message

## Registration Flow

1. User navigates to registration page
2. User enters email, password, and confirmation
3. System validates input:
   - Email must be valid format
   - Password must be at least 8 characters
   - Password must contain uppercase, lowercase, and number
4. System sends verification email
5. User clicks verification link
6. Account is activated

## Password Requirements

- Minimum 8 characters
- At least one uppercase letter
- At least one lowercase letter
- At least one number
- Optional: special characters

## Session Management

- Sessions expire after 24 hours of inactivity
- Users can have multiple active sessions
- "Remember me" extends session to 30 days

## Security Features

- Rate limiting: 5 failed attempts triggers 15-minute lockout
- Two-factor authentication (optional)
- Password reset via email link

## Error Messages

| Code | Message |
|------|---------|
| AUTH001 | Invalid email or password |
| AUTH002 | Account locked due to too many failed attempts |
| AUTH003 | Email not verified |
| AUTH004 | Session expired |

## API Endpoints

- `POST /api/auth/login` - Authenticate user
- `POST /api/auth/register` - Create new account
- `POST /api/auth/logout` - End session
- `POST /api/auth/reset-password` - Request password reset
