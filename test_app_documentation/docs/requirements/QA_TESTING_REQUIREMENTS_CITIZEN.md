# QA Testing Requirements - Citizen Portal

## Overview
This document outlines comprehensive testing requirements specifically for the Citizen user interface and functionality of the City Services Portal. The Citizen portal is the primary public-facing interface where residents interact with municipal services.

## Citizen User Journey Map

### Primary User Flows
1. **First-Time User Flow**
   - Landing page → Registration → Email confirmation → Login → First request submission
   
2. **Returning User Flow**
   - Login → My Requests page → View existing requests → Submit new request
   
3. **Public Browse Flow**
   - Public board → View all public requests → Registration prompt

4. **Request Lifecycle Flow**
   - Create new request → Track status → Add comments → View resolution

## Functional Testing Requirements

### 1. Registration and Account Management

#### TC-CITIZEN-001: New User Registration
**Priority**: Critical  
**Test Scenarios**:

1. **Successful Registration**
   - Valid email format
   - Strong password (8+ chars, uppercase, lowercase, number, special)
   - First name and last name provided
   - Phone number (optional)
   - Terms and conditions accepted

2. **Registration Validation**
   - Duplicate email detection
   - Password strength requirements enforced
   - Real-time field validation with debouncing (300ms)
   - Required field validation
   - Email format validation

3. **Error Handling**
   - Missing required fields (email, password, first name, last name)
   - Invalid email formats
   - Weak passwords (missing requirements)
   - Existing email address
   - Server errors (500)

**Expected Results**:
- Account created successfully
- Confirmation message displayed
- User redirected to email confirmation page
- Cannot login until email confirmed

#### TC-CITIZEN-002: Email Confirmation
**Priority**: High  
**Test Scenarios**:

1. **Email Confirmation Page**
   - Displays confirmation message
   - Shows email address where confirmation was sent
   - Provides option to resend email
   - Link to login page

2. **Email Confirmation Process**
   - Click confirmation link in email
   - Token validation
   - Account activation

**Expected Results**:
- Clear message about email confirmation requirement
- User can request resend
- After confirmation, can login successfully

#### TC-CITIZEN-003: Login and Authentication
**Priority**: Critical  
**Test Scenarios**:

1. **Successful Login**
   - Valid email and password
   - Remember me option
   - Redirect to citizen requests page

2. **Failed Login**
   - Invalid credentials
   - Account not confirmed
   - Account locked (after 5 failed attempts)
   - Session timeout (30 minutes)

3. **Password Reset**
   - Forgot password link
   - Email with reset token
   - Reset password form
   - Password requirements validation

**Expected Results**:
- JWT token stored
- User redirected to appropriate dashboard
- Session management working
- Proper error messages for failures

### 2. Service Request Submission

#### TC-CITIZEN-004: Multi-Step Request Form
**Priority**: Critical  
**Test Scenarios**:

**Step 1: Basic Information**
- Title field (required, min 10 characters)
- Description field (required, min 50 characters)
- Category selection dropdown (14 categories)
- Priority selection (LOW, MEDIUM, HIGH, URGENT)
- Character count display with color indicators
- Real-time validation with debouncing

**Step 2: Location Information**
- Address input field
- City field
- State/Province field
- Postal code field
- Country field (defaults to USA)
- Manual text entry (no map widget)

**Step 3: Contact Services**
- Phone number field (optional)
- Email field (pre-populated from profile)
- Preferred contact method selection
- Best time to contact

**Step 4: Additional Information**
- Additional notes text area (optional)
- Reference number field (optional)
- Urgency explanation (if HIGH/URGENT priority)

**Step 5: Review & Submit**
- Summary of all entered information
- Edit capability for each section
- Terms and conditions checkbox
- Submit button with rate limiting (5 attempts per minute)
- Loading indicator during submission

**Expected Results**:
- Form data persisted in localStorage
- Auto-save every 30 seconds
- Navigation between steps without data loss
- Validation prevents progression with errors
- Request created with unique ID
- Success message displayed

#### TC-CITIZEN-005: Form Validation and Error Handling
**Priority**: High  
**Test Scenarios**:

1. **Field Validation**
   - Title: Min 10 characters, max 200 characters
   - Description: Min 50 characters, max 2000 characters
   - Required fields cannot be empty
   - Email format validation
   - Phone number format (if provided)

2. **Real-time Validation**
   - Debounced validation (300ms delay)
   - Inline error messages below fields
   - Error icon and red border on invalid fields
   - Character count with color coding (green/yellow/red)
   - ARIA labels for accessibility

3. **Rate Limiting**
   - Maximum 5 submission attempts per minute
   - Clear error message when rate limited
   - Countdown timer until next attempt allowed
   - Persists across page refreshes

4. **XSS Prevention**
   - Script tags sanitized
   - HTML entities escaped
   - SQL injection attempts blocked
   - Special characters properly handled

**Expected Results**:
- Validation messages clear and helpful
- No submission with invalid data
- Rate limiting prevents spam
- Security measures effective

#### TC-CITIZEN-006: Local Storage and Form Persistence
**Priority**: Medium  
**Test Scenarios**:

1. **Auto-Save to localStorage**
   - Saves form data every 30 seconds
   - Data persists across browser refreshes
   - Storage key: 'new-request-form-data'
   - Handles browser storage quota

2. **Data Recovery**
   - Restore form data after browser crash
   - Restore after accidental navigation
   - Clear option for stored data
   - Prompt to restore previous session

3. **Storage Management**
   - Clear form data after successful submission
   - Handle localStorage disabled scenarios
   - Fallback to session storage
   - Privacy mode handling

**Expected Results**:
- No data loss during form filling
- Seamless recovery after interruptions
- Clear user communication about saved data
- Proper cleanup after submission

### 3. Request Tracking and Management

#### TC-CITIZEN-007: My Requests Page
**Priority**: High  
**Test Scenarios**:

1. **Page Layout**
   - Page title "My Requests"
   - "Create New Request" button (top right)
   - Filter panel with collapsible card
   - Data grid with requests
   - Pagination controls

2. **Request List Grid**
   - Columns: Code, Title, Category, Priority, Status, Date, Upvotes, Comments
   - Default sort by ID descending
   - Pagination (10 items per page default)
   - Row click navigates to detail page
   - Responsive column sizing

3. **Filter Panel**
   - Search text field with icon
   - Status dropdown (All, SUBMITTED, TRIAGED, IN_PROGRESS, etc.)
   - Category dropdown (14 categories)
   - Priority dropdown (LOW, MEDIUM, HIGH, URGENT)
   - Resolved status dropdown
   - Date range pickers (From/To)
   - Clear filters button

**Expected Results**:
- Page loads within 3 seconds
- Filters apply without page refresh
- Grid updates immediately
- All filters can be combined

#### TC-CITIZEN-008: Request Detail View
**Priority**: High  
**Test Scenarios**:

1. **Information Display**
   - Request ID and code
   - Title and description
   - Current status badge
   - Priority indicator
   - Category label
   - Submission date
   - Last updated timestamp
   - Location information

2. **Comments Section**
   - View all comments
   - Add new comment (text field + submit button)
   - Comment author name
   - Comment timestamp
   - Comments sorted by date

3. **Action Options**
   - Back to list button
   - Upvote button (if enabled)
   - Status history timeline
   - Assigned staff information (if visible)

**Expected Results**:
- All request data displayed correctly
- Comments load and display properly
- Can add new comments successfully
- Navigation works smoothly

#### TC-CITIZEN-009: Request Status Management
**Priority**: Critical  
**Test Scenarios**:

1. **Status Values**
   - SUBMITTED - Initial submission
   - TRIAGED - Reviewed by clerk
   - IN_PROGRESS - Being worked on
   - WAITING_ON_CITIZEN - Needs citizen input
   - RESOLVED - Work completed
   - CLOSED - Verified and closed
   - REJECTED - Request denied

2. **Status Display**
   - Color-coded status badges
   - Status text in user's language
   - Status column in grid
   - Status filter in search

3. **Status Updates**
   - Only staff can change status
   - Citizens see current status
   - Status history visible in details
   - Last update timestamp shown

**Expected Results**:
- Status clearly visible
- Correct color coding
- Proper translations
- Accurate status filtering

### 4. Search and Filtering

#### TC-CITIZEN-010: Search and Filter Functionality
**Priority**: Medium  
**Test Scenarios**:

1. **Text Search**
   - Search field in filter panel
   - Searches in title and description
   - Case-insensitive search
   - Partial word matching
   - Clear search with X button

2. **Filter Options**
   - Status dropdown filter
   - Category dropdown filter (14 options)
   - Priority dropdown filter
   - Resolved status filter (All/Resolved/Not Resolved)
   - Date range filters (From/To date pickers)

3. **Filter Behavior**
   - Filters apply immediately
   - Multiple filters work together (AND logic)
   - Clear all filters button
   - Filter state persists during session
   - Filter count indicator

**Expected Results**:
- Instant filter application
- Accurate filtering results
- Clear filter indicators
- Smooth user experience

#### TC-CITIZEN-011: Public Board Access
**Priority**: Low  
**Test Scenarios**:

1. **Public Board Page**
   - Accessible without login (/public)
   - Shows all public requests
   - Read-only view
   - Basic filtering available
   - No personal information visible

2. **Public Request View**
   - Request title and description
   - Category and status
   - Submission date
   - No citizen names or contact info
   - Upvote counts visible

**Expected Results**:
- Public can view requests
- Privacy maintained
- Encourages registration
- Community awareness

### 5. Navigation and UI Elements

#### TC-CITIZEN-012: Navigation and Layout
**Priority**: High  
**Test Scenarios**:

1. **Main Navigation**
   - App header with logo
   - User menu (profile icon)
   - Language selector
   - Logout option
   - Responsive hamburger menu (mobile)

2. **Citizen Menu Items**
   - My Requests
   - All Requests
   - Create New Request
   - Public Board
   - Community Ranking

3. **Page Layout**
   - Consistent header/footer
   - Breadcrumb navigation
   - Loading indicators
   - Error boundaries
   - Responsive design

**Expected Results**:
- Navigation intuitive
- All links functional
- Consistent layout
- Mobile-responsive

#### TC-CITIZEN-013: Comment System
**Priority**: Medium  
**Test Scenarios**:

1. **Adding Comments**
   - Comment text field in request details
   - Submit button
   - Empty comment validation
   - Maximum length validation
   - XSS prevention

2. **Viewing Comments**
   - Comments list in chronological order
   - Author name displayed
   - Timestamp shown
   - Comment count in grid
   - Pagination for many comments

**Expected Results**:
- Comments post successfully
- Display updates immediately
- Proper sanitization
- Clear attribution

### 6. Internationalization (i18n)

#### TC-CITIZEN-014: Multi-Language Support
**Priority**: High  
**Test Scenarios**:

1. **Language Selection**
   - Language selector in header
   - Available languages: English, Spanish, Bulgarian
   - Language persists in session
   - Default to browser language
   - Flag icons for languages

2. **Translation Coverage**
   - All UI labels translated
   - Status names translated
   - Category names translated
   - Error messages translated
   - Date/time formats localized

3. **RTL Support Preparation**
   - Text alignment correct
   - Form layouts proper
   - No text truncation
   - Proper character encoding

**Expected Results**:
- Seamless language switching
- Complete translation coverage
- No mixed languages
- Proper formatting

### 7. Data Grid Features

#### TC-CITIZEN-015: Data Grid Functionality
**Priority**: High  
**Test Scenarios**:

1. **Grid Operations**
   - Column sorting (click header)
   - Column resizing
   - Column reordering
   - Row selection
   - Row click navigation
   - Pagination controls

2. **Grid Display**
   - Proper data formatting
   - Status badges with colors
   - Priority indicators
   - Date formatting
   - Number formatting
   - Empty state message

3. **Grid Performance**
   - Handle 100+ rows
   - Smooth scrolling
   - Fast sorting
   - Quick page changes
   - Responsive updates

**Expected Results**:
- Grid fully functional
- Good performance
- Clear data presentation
- Intuitive interactions

### 8. Error Handling and Edge Cases

#### TC-CITIZEN-016: Error Scenarios
**Priority**: High  
**Test Scenarios**:

1. **API Errors**
   - 500 server errors (with feature flag)
   - 404 not found
   - 401 unauthorized
   - Network timeout
   - Slow responses (with feature flag)

2. **Form Errors**
   - Validation failures
   - Rate limit exceeded
   - Session timeout
   - Concurrent edit conflicts
   - Storage quota exceeded

3. **Error Recovery**
   - Retry mechanisms
   - Error boundaries
   - Fallback UI
   - Clear error messages
   - Recovery instructions

**Expected Results**:
- Graceful error handling
- No data loss
- Clear user guidance
- System stability maintained

## Test Data Requirements

### User Accounts
```
john@example.com / password123 - Standard citizen with multiple requests
jane@example.com / password123 - New citizen with single request
bob@example.com / password123 - Frequent user with 20+ requests
```

### Test Categories
- roads-transportation
- street-lighting
- waste-management
- water-sewer
- parks-recreation
- public-safety
- building-permits
- snow-removal
- traffic-signals
- sidewalk-maintenance
- tree-services
- noise-complaints
- animal-control
- other

### Test Priorities
- LOW - Routine maintenance
- MEDIUM - Standard requests
- HIGH - Important issues
- URGENT - Safety concerns

### Test Statuses
- SUBMITTED - New request
- TRIAGED - Reviewed by clerk
- IN_PROGRESS - Being worked on
- WAITING_ON_CITIZEN - Needs input
- RESOLVED - Work completed
- CLOSED - Verified and closed
- REJECTED - Request denied

---
*Document Version: 2.0*
*Last Updated: 2025*
*Focus: Actual Implementation Testing*
*For Workshop Training Use*
