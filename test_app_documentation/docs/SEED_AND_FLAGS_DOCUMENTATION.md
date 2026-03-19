# City Services Portal - Test Data and Feature Flags Guide

## Table of Contents
- [Overview](#overview)
- [Getting Started](#getting-started)
- [Test Accounts](#test-accounts)
- [Feature Flag System](#feature-flag-system)
- [Complete List of Testing Flags](#complete-list-of-testing-flags)
- [How to Use Feature Flags](#how-to-use-feature-flags)
- [Test Data Overview](#test-data-overview)

## Overview

The City Services Portal is a comprehensive municipal service management system designed as a testing playground for QA tools. It includes 51 configurable bugs/issues that can be toggled on or off to test whether automated testing tools can detect various types of problems.

## Getting Started

### Accessing the Application

1. **Main Application**: http://localhost:5173
2. **API Documentation**: http://localhost:3001/api-docs
3. **Admin Panel**: Login as admin@city.gov and navigate to Admin section

### Quick Start Commands

```bash
# Start the application
cd city-services-portal
# Terminal 1 - Backend
cd api && npm install && npm run dev

# Terminal 2 - Frontend  
cd ui && npm install && npm run dev
```

## Test Accounts

All test accounts use the password: **`password123`**

| Role | Email | Purpose |
|------|-------|---------|
| **Admin** | admin@city.gov | Access admin panel, manage feature flags, view system statistics |
| **Citizen** | john@example.com | Submit service requests, track status, add comments |
| **Clerk** | mary.clerk@city.gov | Process service requests, use split-view interface, triage issues |
| **Supervisor** | supervisor@city.gov | Review staff performance, quality assessments, department metrics |
| **Field Agent** | field.agent@city.gov | Handle field work orders, update service status on-site |

## Feature Flag System

The application has two types of feature flags:

### 1. Standard Feature Flags
Located at: `/admin/flags` (requires admin login)

| Flag | What It Does When Enabled |
|------|---------------------------|
| **API_Random500** | 5% of API requests randomly fail with 500 errors |
| **API_SlowRequests** | 10% of requests are delayed by 3-5 seconds |
| **API_UploadIntermittentFail** | File uploads randomly fail |
| **UI_WrongDefaultSort** | Tables sort in wrong direction by default |
| **UI_MissingAria_Search** | Search fields lack accessibility labels |

### 2. Testing Feature Flags
Located at: `/admin/testing-flags` (requires admin login)

These 51 flags simulate various bugs and issues for QA testing purposes.

## Complete List of Testing Flags

### Master Control (1 flag)

| Flag | Impact | What Happens When Enabled |
|------|--------|---------------------------|
| **MASTER_TESTING_FLAGS_ENABLED** | HIGH | Acts as a global switch - must be ON for other flags to work. When OFF, all other testing flags are disabled regardless of their individual settings. |

### Authentication & User Management (10 flags)

| Flag | Impact | What Happens When Enabled |
|------|--------|---------------------------|
| **LOGIN_WRONG_PASSWORD_ACCEPTED** | HIGH | System accepts ANY password for ANY user account. Type anything in the password field and you'll be logged in. Critical security vulnerability. |
| **REGISTRATION_SKIP_EMAIL_VALIDATION** | HIGH | New accounts are created without requiring email verification. Users can register with fake emails and immediately access the system. |
| **PASSWORD_RESET_BROKEN_LINKS** | HIGH | When users request password reset, the email links lead to 404 error pages instead of the reset form. |
| **LOGIN_BUTTON_SHOWS_REGISTER** | MEDIUM | The login button text displays "Register Account" instead of "Login", confusing users about which action they're taking. |
| **PROFILE_UPDATE_FAILS_SILENTLY** | MEDIUM | When users update their profile, it shows "Saved successfully" but changes are lost when page refreshes. |
| **PASSWORD_FIELD_SHOWS_PLAIN_TEXT** | MEDIUM | Password input fields show typed characters instead of dots/asterisks, exposing passwords to shoulder surfers. |
| **USER_AVATAR_SHOWS_WRONG_INITIALS** | LOW | User avatars display random letters instead of the user's actual initials (e.g., "XY" for "John Doe"). |
| **EMAIL_CONFIRMATION_WRONG_SUCCESS_MESSAGE** | LOW | After confirming email, message says "Password reset successful" instead of "Email confirmed successfully". |
| **LOGIN_FORM_LOSES_EMAIL_ON_ERROR** | LOW | When login fails due to wrong password, the email field gets cleared, forcing users to retype it. |
| **REGISTRATION_SUCCESS_SHOWS_ERROR_STYLING** | LOW | Successful registration message appears in red error box instead of green success box. |

### Service Request Management (15 flags)

| Flag | Impact | What Happens When Enabled |
|------|--------|---------------------------|
| **SERVICE_REQUEST_CREATION_FAILS** | HIGH | All attempts to create new service requests fail with error messages. Submit button doesn't work. |
| **STATUS_CHANGES_DONT_SAVE** | HIGH | Request status appears to update but reverts to original status on refresh. Critical for workflow. |
| **CRITICAL_REQUESTS_MARKED_LOW_PRIORITY** | HIGH | Requests marked as "URGENT" are automatically saved as "LOW" priority in the system. |
| **FILE_UPLOADS_CORRUPT_IMAGES** | MEDIUM | Uploaded images appear broken/corrupted when viewing request details. Shows broken image icon. |
| **REQUEST_ASSIGNMENTS_IGNORED** | MEDIUM | Assigning requests to staff appears to work but assigned person never receives notification or sees it. |
| **SEARCH_RETURNS_WRONG_RESULTS** | MEDIUM | Searching for "water leak" returns results about "road repair" and other unrelated categories. |
| **COMMENTS_POST_TO_WRONG_REQUEST** | MEDIUM | Comments added to one request randomly appear on different requests instead. |
| **DUPLICATE_REQUEST_DETECTION_BROKEN** | MEDIUM | System allows identical requests to be submitted multiple times from same address. |
| **REQUEST_TIMESTAMPS_WRONG_TIMEZONE** | LOW | All timestamps display in UTC instead of user's local timezone (e.g., showing 3 AM for noon submissions). |
| **REQUEST_CODES_MISSING_YEAR** | LOW | Request IDs show as "REQ-000001" instead of "REQ-2024-000001", losing year information. |
| **STATUS_COLORS_REVERSED** | LOW | Red badge for "Resolved" status, green badge for "Urgent" - opposite of expected. |
| **REQUEST_CATEGORIES_UNSORTED** | LOW | Category dropdown lists items randomly instead of alphabetically. |
| **DESCRIPTION_TEXT_FORMATTING_BROKEN** | LOW | Line breaks in request descriptions are removed, showing everything as one long paragraph. |
| **LOCATION_MAP_POINTS_OFFSET** | LOW | Map markers appear approximately 100 meters away from actual reported location. |
| **REQUEST_COUNT_BADGE_WRONG** | LOW | Dashboard shows "5 open requests" when there are actually 12 open requests. |

### User Interface & UX (10 flags)

| Flag | Impact | What Happens When Enabled |
|------|--------|---------------------------|
| **NAVIGATION_MENU_DISAPPEARS** | MEDIUM | Main navigation menu randomly disappears, requiring page refresh to get it back. |
| **FORM_VALIDATION_MESSAGES_WRONG_LANGUAGE** | MEDIUM | Error messages appear in Bulgarian even when English is selected (e.g., "Полето е задължително"). |
| **BUTTONS_DOUBLE_CLICK_REQUIRED** | MEDIUM | All buttons require double-clicking to work. Single clicks do nothing. |
| **PAGE_TITLES_INCORRECT** | MEDIUM | Browser tab shows "Untitled Document" for all pages instead of proper page names. |
| **LOADING_SPINNERS_NEVER_STOP** | LOW | Loading animations continue spinning even after content has fully loaded. |
| **TOOLTIPS_SHOW_PLACEHOLDER_TEXT** | LOW | All hover tooltips display "Lorem ipsum dolor sit amet" instead of helpful text. |
| **DATE_PICKER_SHOWS_WRONG_YEAR** | LOW | Date picker calendar opens to year 2020 instead of current year. |
| **TABLE_SORTING_REVERSED** | LOW | Clicking "Sort A-Z" actually sorts Z-A and vice versa. |
| **PAGINATION_NEXT_BUTTON_DISABLED** | LOW | "Next page" button is grayed out even when more pages of results exist. |
| **BREADCRUMBS_SHOW_WRONG_PATH** | LOW | Breadcrumb navigation shows "Home > Settings > Profile" when you're actually in "Home > Requests > Details". |

### Search & Filtering (8 flags)

| Flag | Impact | What Happens When Enabled |
|------|--------|---------------------------|
| **SEARCH_IGNORES_FILTERS** | MEDIUM | Applied filters (status, priority, date) are ignored - search returns all results. |
| **EXPORT_DOWNLOADS_EMPTY_FILE** | MEDIUM | Export to CSV/JSON downloads a file with headers only, no data rows. |
| **ADVANCED_SEARCH_CRASHES_PAGE** | MEDIUM | Using advanced search causes white screen of death, requires browser refresh. |
| **SEARCH_RESULTS_RANDOM_ORDER** | LOW | Search results appear in random order each time, ignoring sort preferences. |
| **FILTER_COUNTERS_WRONG** | LOW | Filter shows "(23)" next to status but clicking it shows 8 results. |
| **SEARCH_AUTOCOMPLETE_SUGGESTS_NONSENSE** | LOW | Typing "water" suggests "banana", "telescope", and other unrelated terms. |
| **PAGINATION_SHOWS_WRONG_TOTAL** | LOW | Shows "Displaying 1-20 of 543 results" when only 50 total results exist. |
| **SAVED_SEARCHES_LOAD_DIFFERENT_CRITERIA** | LOW | Loading a saved search applies different filters than what was originally saved. |

### Notifications & Messaging (7 flags)

| Flag | Impact | What Happens When Enabled |
|------|--------|---------------------------|
| **SUCCESS_MESSAGES_SHOW_ERRORS** | MEDIUM | Success notifications appear with red X icon and error styling, confusing users. |
| **ERROR_MESSAGES_DISAPPEAR_TOO_QUICKLY** | MEDIUM | Error messages vanish after 1 second, not giving users time to read them. |
| **NOTIFICATION_BADGE_SHOWS_SMILEY_FACE** | LOW | Notification count badge shows "😊" emoji instead of number of notifications. |
| **TOAST_NOTIFICATIONS_APPEAR_UPSIDE_DOWN** | LOW | Pop-up notifications appear rotated 180 degrees (upside down text). |
| **SUCCESS_MESSAGES_SHOW_WRONG_ICONS** | LOW | Success messages show ⚠️ warning or ❌ error icons instead of ✅ checkmark. |
| **SYSTEM_MESSAGES_WRONG_LANGUAGE** | LOW | Random system messages appear in Bulgarian when English interface is selected. |
| **NOTIFICATION_PANEL_SHOWS_PLACEHOLDER_DATA** | LOW | Notifications list shows "Sample notification text here" for all notifications. |

## How to Use Feature Flags

### For QA Testing

1. **Login as Admin**: Use admin@city.gov / password123
2. **Navigate to Testing Flags**: Go to Admin → Testing Flags
3. **Enable Master Control**: Toggle "Master Testing Flags Control" to ON
4. **Select Bugs to Test**: 
   - Choose by category (Authentication, Service Requests, etc.)
   - Choose by impact level (HIGH, MEDIUM, LOW)
   - Or enable specific individual flags
5. **Test the Application**: Use other test accounts to verify if your testing tools detect the enabled issues

### Testing Scenarios

#### Scenario 1: Critical Security Test
Enable these HIGH impact flags:
- LOGIN_WRONG_PASSWORD_ACCEPTED
- REGISTRATION_SKIP_EMAIL_VALIDATION
- SERVICE_REQUEST_CREATION_FAILS

Expected: Your testing tools should immediately flag critical security and functionality issues.

#### Scenario 2: User Experience Test
Enable these MEDIUM/LOW impact flags:
- BUTTONS_DOUBLE_CLICK_REQUIRED
- LOADING_SPINNERS_NEVER_STOP
- TABLE_SORTING_REVERSED

Expected: Your testing tools should detect usability problems.

#### Scenario 3: Data Integrity Test
Enable these flags:
- STATUS_CHANGES_DONT_SAVE
- COMMENTS_POST_TO_WRONG_REQUEST
- PROFILE_UPDATE_FAILS_SILENTLY

Expected: Your testing tools should catch data persistence issues.

## Test Data Overview

The application comes pre-loaded with realistic test data:

### Service Requests (500 total)
- **Categories**: Waste Management, Roads & Infrastructure, Water & Utilities, Public Safety, Parks & Recreation
- **Statuses**: Submitted (66), In Progress (20), Resolved (71), Closed (63), and others
- **Priorities**: Urgent (125), High (122), Medium (125), Low (128)
- **Locations**: Real Bulgarian addresses from Sofia, Plovdiv, Varna, Burgas, Ruse

### Users (133 total)
- 54 Citizens (can submit requests)
- 31 Clerks (can process requests)
- 26 Field Agents (can handle field work)
- 21 Supervisors (can review performance)
- 1 Admin (can manage system)

### Engagement Data
- 300 comments across various requests
- 405 upvotes on requests
- 112 quality reviews with average score of 7.6/10
- 96 field work orders

### Bulgarian-Themed Data
All test data uses realistic Bulgarian names, addresses, and phone numbers for authenticity:
- Names like "Georgi Petrov", "Maria Dimitrova"
- Addresses like "ul. Vitosha 15, Sofia"
- Phone numbers with +359 country code

## Managing Test Data

### Reset Database
To start fresh with clean test data:
1. Login as admin
2. Go to Admin → Database Management
3. Click "Reset Database"
4. Confirm the action

### View Statistics
To see current data statistics:
1. Login as admin
2. Go to Admin → Database Management
3. View real-time statistics with auto-refresh option

### Important Notes

- **Testing Only**: These feature flags are designed for QA testing, not production use
- **Master Control**: Remember to enable the master flag first
- **Impact Levels**: Start with HIGH impact flags for critical issue detection
- **Combinations**: Some flags may interact - test both individually and in combinations
- **Performance**: With many flags enabled, the application may become very buggy (intentionally)

## Support

For issues or questions about the testing environment:
- Check the API documentation at http://localhost:3001/api-docs
- Review the database schema in Prisma Studio: `npm run db:studio`
- Report issues at: https://github.com/anthropics/claude-code/issues