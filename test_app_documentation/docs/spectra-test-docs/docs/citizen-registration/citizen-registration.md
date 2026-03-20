# Citizen Registration

## Overview

Citizens register through a public-facing portal to access government services. Registration is a multi-step wizard that collects personal information, verifies identity, and creates an account. The process must be completable on mobile devices.

## Registration Wizard Steps

### Step 1: Personal Information

Required fields:
- **First Name**: 2-50 characters, letters and hyphens only, Unicode supported (e.g., José, Müller, Иванов)
- **Last Name**: 2-50 characters, same rules as first name
- **Date of Birth**: Must be 18 years or older at the time of registration. Calendar picker with manual entry option. Format: DD/MM/YYYY for Bulgarian locale, MM/DD/YYYY for English locale
- **Personal Identification Number (EGN/ЕГН)**: Exactly 10 digits, validated against the Bulgarian EGN checksum algorithm. The system extracts date of birth and gender from the EGN and cross-validates against the manually entered date of birth
- **Gender**: Male / Female / Other / Prefer not to say — if extracted from EGN, pre-filled but editable
- **Nationality**: Dropdown with search, defaults to "Bulgarian"

Validation happens on blur for each field and again on "Next" button click. If validation fails, the field is highlighted in red with an error message below it. The "Next" button is always enabled (no progressive disclosure) but shows all validation errors on click.

### Step 2: Contact Information

Required fields:
- **Email**: Standard email format validation. A confirmation email is sent with a 6-digit code that must be entered before proceeding to Step 3. The code expires after 15 minutes. The user can request a new code up to 3 times per hour.
- **Mobile Phone**: International format with country code. Bulgarian numbers (+359) are validated for correct length (9 digits after country code). An SMS verification code is sent — same rules as email verification.

Optional fields:
- **Landline Phone**: No verification required
- **Preferred Contact Method**: Email (default) / SMS / Phone Call

Both email and phone must be verified before proceeding. If the user leaves the wizard and returns within 24 hours, verified statuses are preserved.

### Step 3: Address

The system supports two address entry modes:

**Structured Address** (default for Bulgarian addresses):
- Region (Област): Dropdown, 28 regions
- Municipality (Община): Filtered dropdown based on selected region
- City/Village (Населено място): Filtered dropdown based on selected municipality, with search
- Postal Code: Auto-filled from city selection, editable
- Street: Free text, 3-100 characters
- Street Number: Free text, 1-10 characters
- Building / Entrance / Floor / Apartment: All optional, free text

**Free-form Address** (for foreign addresses):
- Full address text area, 10-500 characters
- Country: Dropdown, required

If nationality is "Bulgarian", structured address is required for the permanent address. A separate correspondence address can be added optionally (checkbox: "Correspondence address is different from permanent address").

### Step 4: Identity Verification

Citizens must verify their identity through one of the following methods:

1. **Qualified Electronic Signature (QES/КЕП)**: The citizen signs a verification challenge using their Bulgarian eID or qualified certificate. Verification is real-time.

2. **Document Upload**: The citizen uploads a scan or photo of their Bulgarian ID card (лична карта) — front and back. Requirements:
   - Accepted formats: JPEG, PNG, PDF
   - Maximum file size: 10 MB per file
   - Minimum resolution: 300 DPI or 1000x600 pixels
   - The system performs OCR to extract document number, expiry date, and name
   - OCR results are shown to the citizen for confirmation
   - If the document is expired, registration is blocked with a message directing the citizen to renew their ID
   - If OCR fails (blurry image, wrong document type), the citizen is asked to re-upload

3. **In-person Verification**: The citizen can complete registration online but choose to verify in person at a government office within 30 days. The account is created in "Pending Verification" status and has limited access (can view services but cannot submit applications).

### Step 5: Account Setup

- **Password**: Must meet the password policy (see Authentication document)
- **MFA Enrollment**: Optional for citizens. If chosen, TOTP or SMS MFA is configured
- **Terms and Conditions**: Checkbox, mandatory. Links to current version of Terms of Service and Privacy Policy. The version number and acceptance timestamp are stored.
- **Marketing Consent**: Checkbox, optional. "I agree to receive information about new services and updates via email."

### Step 6: Confirmation

Summary of all entered information displayed for review. The citizen can go back to any step to edit. Upon confirmation:
1. Account is created
2. Welcome email is sent with account details (no password included)
3. If identity verification is pending (method 3), a reminder email is sent after 7, 14, and 21 days
4. The citizen is redirected to their dashboard

## Duplicate Detection

Before account creation, the system checks for existing accounts with the same EGN. If found:
- If the existing account is active: "An account with this identification number already exists. Please use the password reset function if you cannot access your account."
- If the existing account is deactivated: The citizen is offered to reactivate it. Reactivation restores the old account but requires new identity verification.

The system also checks for duplicate email and phone number. These are warnings, not blockers — multiple citizens can share a phone number (e.g., family members) but not an email address. If the email is already registered, the citizen must use a different email.

## Wizard Persistence

- Progress is saved to local storage after each step completion
- If the browser is closed, the citizen can resume from the last completed step within 24 hours
- After 24 hours, incomplete registrations are purged and the citizen must start over
- Verified email/phone tokens are preserved during the 24-hour window

## Accessibility Requirements

- All form fields must have associated labels
- Error messages must be announced by screen readers
- The wizard must be navigable by keyboard alone
- Color is not the only indicator of validation state (icons and text are also used)
- Minimum contrast ratio: 4.5:1 for normal text, 3:1 for large text
