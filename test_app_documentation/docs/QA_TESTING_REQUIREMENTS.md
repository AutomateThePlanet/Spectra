# City Services Portal - QA Testing Requirements Documentation

## Table of Contents
1. [System Overview](#system-overview)
2. [User Roles and Permissions](#user-roles-and-permissions)
3. [Authentication Requirements](#authentication-requirements)
4. [Field Validations](#field-validations)
5. [Service Request Workflow](#service-request-workflow)
6. [Status Transitions](#status-transitions)
7. [Form Validations](#form-validations)
8. [File Upload Requirements](#file-upload-requirements)
9. [Search and Filter Requirements](#search-and-filter-requirements)
10. [API Endpoints and Expected Responses](#api-endpoints-and-expected-responses)
11. [Error Codes and Messages](#error-codes-and-messages)
12. [Performance Requirements](#performance-requirements)
13. [Security Requirements](#security-requirements)
14. [Accessibility Requirements](#accessibility-requirements)
15. [Test Data and Scenarios](#test-data-and-scenarios)

---

## 1. System Overview

### Application Purpose
Municipal service request management system for citizens to report issues and city staff to manage and resolve them.

### Access Points
- **Frontend URL**: http://localhost:5173
- **API Base URL**: http://localhost:3001
- **API Documentation**: http://localhost:3001/api-docs
- **Database Studio**: Run `npm run db:studio` in `/api` directory

### Browser Support
- Chrome 90+
- Firefox 88+
- Safari 14+
- Edge 90+

### Mobile Responsiveness
- Breakpoints: 320px, 768px, 1024px, 1440px
- Touch-friendly interface required for all interactive elements
- Minimum touch target: 44x44px

---

## 2. User Roles and Permissions

### Role Matrix

| Role | Code | Permissions | Test Account | Password |
|------|------|-------------|--------------|----------|
| **Citizen** | `CITIZEN` | • Create service requests<br>• View own requests<br>• Add comments to own requests<br>• Upload attachments<br>• Track request status<br>• Upvote other requests | john@example.com | password123 |
| **Clerk** | `CLERK` | • View all requests in department<br>• Triage requests<br>• Update request status<br>• Assign to field agents<br>• Add internal comments<br>• View split-screen interface | mary.clerk@city.gov | password123 |
| **Field Agent** | `FIELD_AGENT` | • View assigned tasks<br>• Update task progress<br>• Upload field photos<br>• Complete work orders<br>• Update location status<br>• Time tracking | agent1@city.gov | password123 |
| **Supervisor** | `SUPERVISOR` | • View department metrics<br>• Assign workload<br>• Review staff performance<br>• Quality reviews<br>• Approve escalations<br>• Generate reports | supervisor1@city.gov | password123 |
| **Admin** | `ADMIN` | • Full system access<br>• User management<br>• Feature flags control<br>• System configuration<br>• Database operations<br>• View all departments | admin@city.gov | password123 |

### Permission Rules

#### Citizens
- ✅ Can view all public service requests
- ✅ Can create new service requests
- ✅ Can edit own requests within 10 minutes of creation
- ✅ Can add comments to own requests
- ✅ Can upvote other citizens' requests (not own)
- ❌ Cannot view internal comments
- ❌ Cannot change request status
- ❌ Cannot access admin features
- ❌ Cannot export data

#### Staff (Clerk, Field Agent, Supervisor)
- ✅ Can view all requests in their department
- ✅ Can add internal comments
- ✅ Can change request status (following workflow rules)
- ✅ Can export data (CSV, JSON)
- ✅ Can view citizen information
- ✅ Can upload files without size restrictions
- ❌ Cannot delete requests
- ❌ Cannot modify other departments' requests

#### Admin
- ✅ Full access to all features
- ✅ Can delete requests (bulk operations)
- ✅ Can change user status
- ✅ Can access all departments
- ✅ Can toggle feature flags
- ✅ Can reset database
- ✅ Can create and manage staff accounts
- ✅ Can assign and modify user roles
- ✅ Can edit role permissions

---

## 3. Staff & Role Management

### Staff Account Management

#### Create Staff Account
| Field | Validation | Required | Error Messages |
|-------|-----------|----------|----------------|
| **Email** | Valid email format, unique | Yes | "Email already exists" |
| **First Name** | 1-50 characters | Yes | "First name is required" |
| **Last Name** | 1-50 characters | Yes | "Last name is required" |
| **Role** | CLERK, FIELD_AGENT, SUPERVISOR, ADMIN | Yes | "Invalid role" |
| **Department** | Valid department ID | Conditional* | "Department required for this role" |
| **Employee ID** | Alphanumeric, unique | No | "Employee ID already exists" |
| **Phone** | Valid phone format | No | "Invalid phone number" |

*Department is required for CLERK, FIELD_AGENT, and SUPERVISOR roles

#### Staff Account Features
- **Temporary Password**: Auto-generated 12-character password
- **Invitation Email**: Optional email with login credentials
- **Password Reset Required**: New staff must reset password on first login
- **Bulk Creation**: Upload CSV or create multiple accounts at once

### Role Management System

#### System Roles
| Role | Hierarchy | Permissions | Editable |
|------|-----------|-------------|----------|
| **CITIZEN** | 1 | Basic permissions (view own, create requests) | No |
| **CLERK** | 2 | Department-scoped request management | Yes |
| **FIELD_AGENT** | 2 | Assigned task management | Yes |
| **SUPERVISOR** | 3 | Department management, reports | Yes |
| **ADMIN** | 4 | Full system access | No |

#### Permission Structure
| Resource | Actions | Scopes |
|----------|---------|--------|
| **service_requests** | view, create, edit, delete, assign, resolve | own, department, assigned, all |
| **users** | view, create, edit, delete, manage | own, department, all |
| **departments** | view, create, edit, delete, manage | own, all |
| **reports** | view, create, export | department, all |
| **system** | manage, audit | all |

#### Role Assignment Rules
1. **Hierarchy Enforcement**: Users can only assign roles at or below their hierarchy level
2. **Department Requirement**: Non-citizen roles require department assignment
3. **Audit Trail**: All role changes are logged with performer, reason, and timestamp
4. **Immediate Effect**: Role changes take effect immediately
5. **Session Update**: Active sessions reflect new permissions after refresh

### Permission Management

#### Permission Matrix Features
- **Visual Grid**: Checkbox grid showing role vs permission mapping
- **Edit Mode**: Toggle editing to modify permissions
- **Protected Roles**: ADMIN and CITIZEN permissions cannot be modified
- **Batch Updates**: Update all permissions for a role at once
- **Individual Toggle**: Toggle single permissions in real-time

#### Permission Validation
- **Resource Access**: Check user has permission for resource + action + scope
- **Scope Hierarchy**: 'all' scope includes 'department' and 'own'
- **Role Inheritance**: Higher hierarchy roles inherit lower role permissions
- **Override Support**: User-specific permission overrides (future feature)

### Testing Scenarios

#### Staff Account Creation
1. **Valid Creation**
   - Create account with all required fields
   - Verify temporary password generation
   - Check email uniqueness validation
   - Confirm role assignment

2. **Bulk Creation**
   - Upload 10+ accounts
   - Verify success/failure reporting
   - Check duplicate email handling
   - Validate role distribution

3. **Edge Cases**
   - Create account with existing email (should fail)
   - Create without required department (should fail)
   - Create with invalid role (should fail)
   - Create with special characters in name

#### Role Management
1. **Role Assignment**
   - Assign role to existing user
   - Change user from CITIZEN to CLERK
   - Bulk assign roles to multiple users
   - Verify department requirement

2. **Permission Editing**
   - Enable/disable permissions for CLERK
   - Modify SUPERVISOR permissions
   - Attempt to edit ADMIN permissions (should fail)
   - Save and verify persistence

3. **Audit Trail**
   - View role change history
   - Verify all changes logged
   - Check performer identification
   - Validate reason capture

#### API Testing
```bash
# Create Staff Account
curl -X POST http://localhost:3001/api/v1/admin/staff \
  -H "Authorization: Bearer ADMIN_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "email": "newstaff@city.gov",
    "firstName": "John",
    "lastName": "Staff",
    "role": "CLERK",
    "departmentId": "dept-id"
  }'

# Assign Role
curl -X PATCH http://localhost:3001/api/v1/admin/users/USER_ID/role \
  -H "Authorization: Bearer ADMIN_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "role": "SUPERVISOR",
    "reason": "Promotion"
  }'

# Toggle Permission
curl -X POST http://localhost:3001/api/v1/admin/roles/ROLE_ID/permission \
  -H "Authorization: Bearer ADMIN_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "permissionId": "PERM_ID",
    "granted": true
  }'
```

---

## 4. Authentication Requirements

### User Registration

#### Required Fields
| Field | Validation Rules | Error Messages |
|-------|-----------------|----------------|
| **First Name** | • 2-50 characters<br>• Letters, spaces, hyphens, apostrophes<br>• Supports Cyrillic and Latin<br>• No consecutive spaces<br>• Trimmed | • "First name must be at least 2 characters"<br>• "First name cannot exceed 50 characters"<br>• "First name contains invalid characters" |
| **Last Name** | • Same as First Name | • Same as First Name |
| **Email** | • Valid email format (RFC 5322)<br>• 5-254 characters<br>• Lowercase normalized<br>• Unique in system<br>• Trimmed | • "Please enter a valid email address"<br>• "Email already exists"<br>• "Email cannot exceed 254 characters" |
| **Password** | • Minimum 12 characters<br>• Maximum 128 characters<br>• At least 1 uppercase<br>• At least 1 lowercase<br>• At least 1 number<br>• At least 1 special character<br>• No more than 2 consecutive identical<br>• No more than 3 total repeated<br>• Not a common password<br>• No sequential characters (abc, 123) | • "Password must be at least 12 characters"<br>• "Password must contain uppercase letter"<br>• "Password must contain lowercase letter"<br>• "Password must contain number"<br>• "Password must contain special character"<br>• "Password is too common"<br>• "Cannot have sequential characters" |
| **Phone** (Optional) | • E.164 format<br>• Bulgarian: (+359\|0)[87-9]xxxxxxxx<br>• International: flexible format<br>• Removes spaces, dashes, parentheses | • "Please enter a valid phone number"<br>• "Invalid phone format" |

#### Special Characters Allowed in Password
`@ $ ! % * ? & # ^ ( ) _ + = - { } [ ] | \ : ; " ' < > , . ? / ` ~`

#### Common Passwords List (Blocked)
- password, 12345678, 123456789, qwerty, abc123, password123
- admin, letmein, welcome, monkey, 1234567890, qwertyuiop
- password1, password12, Passw0rd, Password1, Welcome123
- Admin123, Root123, Test123, Demo123

### User Login

#### Fields
| Field | Validation | Error Messages |
|-------|-----------|----------------|
| **Email** | • Required<br>• Valid email format<br>• Case-insensitive | • "Email is required"<br>• "Invalid email format" |
| **Password** | • Required<br>• No client-side validation of content | • "Password is required" |

#### Login Rules
- 5 login attempts allowed per 15 minutes
- Account locked after 5 failed attempts
- Session timeout: 24 hours
- Refresh token valid for 7 days
- Remember me option extends session to 30 days

### User Status Lifecycle

| Status | Code | Description | Allowed Actions |
|--------|------|-------------|-----------------|
| **Pending Email Verification** | `PENDING_EMAIL_VERIFICATION` | New user, email not verified | • Can request verification resend<br>• Cannot login |
| **Active** | `ACTIVE` | Normal active user | • Full access based on role |
| **Inactive** | `INACTIVE` | Temporarily disabled | • Cannot login<br>• Can reset password |
| **Password Reset Required** | `PASSWORD_RESET_REQUIRED` | Must reset password | • Can only access password reset<br>• Cannot use other features |
| **Suspended** | `SUSPENDED` | Admin suspended | • Cannot login<br>• Shows suspension message |
| **Archived** | `ARCHIVED` | Permanently disabled | • No access<br>• Data retained for audit |

#### Status Transition Rules
```
PENDING_EMAIL_VERIFICATION → ACTIVE, SUSPENDED, ARCHIVED
ACTIVE → INACTIVE, SUSPENDED, PASSWORD_RESET_REQUIRED, ARCHIVED
INACTIVE → ACTIVE, SUSPENDED, ARCHIVED
PASSWORD_RESET_REQUIRED → ACTIVE, SUSPENDED, ARCHIVED
SUSPENDED → ACTIVE, INACTIVE, ARCHIVED
ARCHIVED → (No transitions allowed)
```

### Email Verification
- Token valid for 24 hours
- Clickable link in console output (demo mode)
- Format: `http://localhost:5173/verify-email?token=xxxxx`
- One-time use token
- Automatic status change to ACTIVE upon verification

### Password Reset
- Token valid for 1 hour
- Requires email to initiate
- Console displays reset link (demo mode)
- Format: `http://localhost:5173/reset-password?token=xxxxx`
- One-time use token
- Must meet all password requirements for new password

---

## 4. Field Validations

### Text Fields

#### Name Fields (First Name, Last Name, etc.)
- **Min Length**: 2 characters (after trim)
- **Max Length**: 50 characters
- **Allowed Characters**: 
  - Latin: A-Z, a-z
  - Cyrillic: А-Я, а-я, Ё, ё
  - Extended Latin: À-ÿ, Ā-ž
  - Special: spaces, hyphens (-), apostrophes (')
- **Not Allowed**: 
  - Numbers
  - Special characters except hyphen and apostrophe
  - Consecutive spaces
  - Leading/trailing spaces (auto-trimmed)

#### Email Field
- **Format**: standard@example.com
- **Min Length**: 5 characters
- **Max Length**: 254 characters
- **Case**: Auto-converted to lowercase
- **Uniqueness**: Must be unique in system
- **Special Rules**: 
  - Must have @ symbol
  - Must have domain with TLD
  - No spaces allowed

#### Phone Fields
- **Bulgarian Format**: 
  - (+359|0)[87-9]xxxxxxxx
  - Examples: +359888123456, 0888123456
- **International Format**: 
  - E.164 standard
  - 1-15 digits after country code
  - Auto-removes: spaces, dashes, parentheses
- **Optional**: Not required for registration

### Address Fields

#### Street Address
- **Min Length**: 5 characters
- **Max Length**: 200 characters
- **Allowed**: Unicode letters, numbers, spaces, commas, periods, hyphens, #, &, /
- **Languages**: Supports all Unicode scripts

#### City
- **Min Length**: 2 characters
- **Max Length**: 100 characters
- **Allowed**: Letters (any script), spaces, hyphens

#### Postal Code
- **Bulgaria**: 4 digits (e.g., 1000, 4000)
- **USA**: 5 digits or 5+4 (e.g., 12345, 12345-6789)
- **UK**: Format like SW1A 1AA
- **Canada**: Format like K1A 0B1
- **Germany/France**: 5 digits
- **Default**: 3-10 alphanumeric characters

### Numeric Fields

#### Severity (1-10 scale)
- **Min**: 1
- **Max**: 10
- **Type**: Integer only
- **Required**: No

#### Satisfaction Rating (1-5 scale)
- **Min**: 1
- **Max**: 5
- **Type**: Integer only
- **Required**: No

#### Estimated Value
- **Min**: 0
- **Max**: 999999999.99
- **Decimals**: Up to 2
- **Format**: Numbers only, optional decimal point

---

## 5. Service Request Workflow

### Request Creation Process

#### Step 1: Basic Information
| Field | Required | Validation |
|-------|----------|------------|
| Title | Yes | 5-120 characters |
| Description | Yes | Minimum 30 characters |
| Category | Yes | Select from predefined list |
| Priority | No | LOW, MEDIUM, HIGH, URGENT (default: MEDIUM) |

#### Step 2: Location Details
| Field | Required | Validation |
|-------|----------|------------|
| Location Description | Yes | Minimum 1 character |
| Street Address | No | 5-200 characters if provided |
| City | No | 2-100 characters if provided |
| Postal Code | No | Country-specific format |
| Landmark | No | Free text |
| Access Instructions | No | Free text |
| GPS Coordinates | No | Valid lat/lng if provided |

#### Step 3: Contact Information
| Field | Required | Validation |
|-------|----------|------------|
| Contact Method | No | EMAIL, PHONE, SMS |
| Email | No | Valid email format |
| Phone | No | Valid phone format |
| Alternate Phone | No | Valid phone format |
| Best Time to Contact | No | Free text |

#### Step 4: Additional Details
| Field | Required | Validation |
|-------|----------|------------|
| Issue Type | No | Predefined list based on category |
| Severity | No | 1-10 scale |
| Is Recurring | No | Boolean checkbox |
| Is Emergency | No | Boolean checkbox |
| Has Permits | No | Boolean checkbox |
| Affected Services | No | Multi-select list |
| Estimated Value | No | Numeric, >= 0 |

#### Step 5: Review and Submit
- All entered data displayed for review
- Edit capability for each section
- Terms acceptance required (checkbox)
- Updates preference (checkbox)
- File attachments (optional, up to 5 files)

### Request Code Generation
- **Format**: REQ-YYYY-XXXXXX
- **Example**: REQ-2024-123456
- **Uniqueness**: System ensures unique codes
- **Retry Logic**: Up to 10 attempts if collision

---

## 6. Status Transitions

### Service Request Statuses

| Status | Code | Description | Valid Transitions |
|--------|------|-------------|-------------------|
| **Submitted** | `SUBMITTED` | Initial status after creation | → Triaged, Rejected |
| **Triaged** | `TRIAGED` | Reviewed and categorized | → In Progress, Rejected |
| **In Progress** | `IN_PROGRESS` | Being actively worked on | → Waiting on Citizen, Resolved, Rejected |
| **Waiting on Citizen** | `WAITING_ON_CITIZEN` | Needs citizen input | → In Progress, Resolved, Closed |
| **Resolved** | `RESOLVED` | Work completed | → Closed, Reopened |
| **Closed** | `CLOSED` | Issue closed | → Reopened (within 30 days) |
| **Rejected** | `REJECTED` | Request rejected | (Terminal state) |
| **Reopened** | `REOPENED` | Previously closed, opened again | → In Progress |

### Status Change Rules

#### Who Can Change Status
| Role | Allowed Status Changes |
|------|----------------------|
| Citizen | Cannot change status |
| Clerk | All transitions except Reopened |
| Field Agent | In Progress → Resolved, Waiting on Citizen |
| Supervisor | All transitions |
| Admin | All transitions |

#### Status Actions API
```javascript
POST /api/v1/requests/:id/status
{
  "action": "triage|start|wait_for_citizen|resolve|close|reject|reopen",
  "reason": "Optional reason text",
  "assignedTo": "Optional user ID for assignment"
}
```

### Validation Rules for Status Changes
1. Cannot skip required intermediate states
2. Rejected status is terminal (no further changes)
3. Reopening only allowed within 30 days of closing
4. Must provide reason for rejection
5. Assignment required when moving to In Progress

---

## 7. Form Validations

### Multi-Step Request Form

#### Navigation Rules
- Can navigate to any previously completed step
- Cannot skip ahead without completing current step
- Progress saved automatically
- Browser back button supported
- Data persists during navigation

#### Field Dependencies
1. **If Emergency = true**:
   - Priority auto-set to URGENT
   - Additional emergency contact fields appear
   
2. **If Has Permits = true**:
   - Permit number field appears (optional)
   - Permit type field appears (optional)

3. **If Is Recurring = true**:
   - Frequency field appears (required)
   - First occurrence date field appears

4. **If Contact Method = PHONE**:
   - Phone becomes required
   - Best time to contact becomes visible

### Dynamic Form Arrays

#### Additional Contacts
- **Add Button**: Creates new contact entry
- **Remove Button**: Deletes contact entry
- **Maximum**: 5 additional contacts
- **Fields per Contact**:
  - Name (required, 2-50 chars)
  - Phone (required, valid format)
  - Relationship (required, free text)

#### File Attachments
- **Maximum Files**: 5 per request
- **File Size**: 10MB per file
- **Allowed Types**: 
  - Images: jpg, jpeg, png, gif, webp
  - Documents: pdf, doc, docx, txt
- **Drag & Drop**: Supported
- **Preview**: Available for images
- **Validation Messages**:
  - "File too large (max 10MB)"
  - "File type not supported"
  - "Maximum 5 files allowed"

### Real-time Validation

#### Validation Timing
- **On Blur**: Field validated when focus lost
- **On Change**: After first blur, validates on each change
- **Debounce**: 300ms delay for text fields
- **Submit**: All fields validated before submission

#### Error Display
- Red border on invalid fields
- Error message below field
- Error icon in field
- Summary at top of form on submit attempt
- Focus moves to first error field

---

## 8. File Upload Requirements

### Upload Specifications

| Aspect | Requirement |
|--------|------------|
| **Max File Size** | 10MB per file |
| **Max Files per Request** | 5 files |
| **Total Max Size** | 50MB combined |
| **Supported Formats** | • Images: JPG, JPEG, PNG, GIF, WEBP<br>• Documents: PDF, DOC, DOCX, TXT<br>• Spreadsheets: XLS, XLSX, CSV |
| **File Name Length** | Maximum 255 characters |
| **Special Characters** | Automatically sanitized |

### Upload Process
1. Select files via button or drag & drop
2. Files validated on selection
3. Progress bar during upload
4. Thumbnail preview for images
5. Delete option before submission
6. Files stored in `/uploads` directory

### Upload Validation Messages
- "File size exceeds 10MB limit"
- "Unsupported file type"
- "Maximum 5 files allowed"
- "Upload failed, please try again"
- "File name too long"

### Security Rules
- File type verified by content, not extension
- Executable files blocked
- Files scanned for malicious content
- Unique file names generated server-side
- Original names preserved in database

---

## 9. Search and Filter Requirements

### Search Endpoints

#### GET Search (Simple)
- **URL**: `/api/v1/service-requests/search`
- **Max URL Length**: 2000 characters
- **Cacheable**: Yes (5 minutes)
- **Use Case**: Bookmarkable searches

#### POST Search (Complex)
- **URL**: `/api/v1/service-requests/search`
- **No URL limit**: Body-based
- **Use Case**: Advanced filtering

### Search Parameters

| Parameter | Type | Values | Example |
|-----------|------|--------|---------|
| **status** | String/Array | SUBMITTED, TRIAGED, IN_PROGRESS, etc. | status=SUBMITTED,TRIAGED |
| **priority** | String/Array | LOW, MEDIUM, HIGH, URGENT | priority=HIGH,URGENT |
| **category** | String/Array | Any category | category=roads,water |
| **department** | String/Array | Department slugs | department=water-dept |
| **keyword** | String | Free text | keyword=pothole |
| **assignedTo** | String | User ID | assignedTo=user-123 |
| **createdFrom** | DateTime | ISO 8601 | createdFrom=2024-01-01T00:00:00Z |
| **createdTo** | DateTime | ISO 8601 | createdTo=2024-12-31T23:59:59Z |
| **location** | String | Location text | location=downtown |
| **page** | Number | 1+ | page=1 |
| **limit** | Number | 1-100 | limit=20 |
| **sortBy** | String | Field name | sortBy=createdAt |
| **sortOrder** | String | asc, desc | sortOrder=desc |

### Advanced Filters (POST only)

#### Geolocation Search
```json
{
  "filters": {
    "geoLocation": {
      "latitude": 42.6977,
      "longitude": 23.3219,
      "radiusKm": 5
    }
  }
}
```

#### Complex Date Ranges
```json
{
  "filters": {
    "complexDateRanges": [
      {
        "field": "createdAt",
        "from": "2024-01-01T00:00:00Z",
        "to": "2024-12-31T23:59:59Z",
        "operator": "AND"
      }
    ]
  }
}
```

#### Reporting Filters
```json
{
  "filters": {
    "reportingFilters": {
      "minUpvotes": 5,
      "maxUpvotes": 100,
      "hasAttachments": true,
      "isEmergency": true
    }
  }
}
```

### Search Response Format
```json
{
  "data": [...],
  "pagination": {
    "page": 1,
    "limit": 20,
    "totalCount": 150,
    "totalPages": 8
  },
  "filters": {...},
  "aggregations": {
    "byStatus": {...},
    "byPriority": {...}
  },
  "metadata": {
    "searchDuration": 45,
    "cached": false,
    "queryComplexity": "simple"
  },
  "correlationId": "req_123_abc"
}
```

### Export Functionality

| Format | Endpoint | Who Can Export |
|--------|----------|----------------|
| CSV | POST /api/v1/service-requests/search/export | Staff only |
| JSON | POST /api/v1/service-requests/search/export | Staff only |
| XLSX | POST /api/v1/service-requests/search/export | Staff only (planned) |

---

## 10. API Endpoints and Expected Responses

### Authentication Endpoints

| Method | Endpoint | Request Body | Success Response | Error Codes |
|--------|----------|--------------|------------------|-------------|
| POST | /auth/register | `{email, password, firstName, lastName}` | 201: `{accessToken, user}` | 400, 409 |
| POST | /auth/login | `{email, password}` | 200: `{accessToken, refreshToken, user}` | 400, 401, 403 |
| POST | /auth/logout | - | 200: `{message}` | 401 |
| POST | /auth/refresh | `{refreshToken}` | 200: `{accessToken, refreshToken}` | 401, 403 |
| GET | /auth/verify-email | `?token=xxx` | 200: `{message}` | 400, 404, 410 |
| POST | /auth/forgot-password | `{email}` | 200: `{message}` | 400, 404 |
| POST | /auth/reset-password | `{token, password}` | 200: `{message}` | 400, 404, 410 |

### Service Request Endpoints

| Method | Endpoint | Success Response | Error Codes |
|--------|----------|------------------|-------------|
| GET | /requests | 200: `{data[], pagination}` | 400, 401 |
| POST | /requests | 201: `{data}` | 400, 401, 403 |
| GET | /requests/:id | 200: `{data}` | 401, 404 |
| PATCH | /requests/:id | 200: `{data}` | 400, 401, 403, 404, 409 |
| POST | /requests/:id/status | 200: `{data}` | 400, 401, 403, 404, 422 |
| POST | /requests/:id/comments | 201: `{data}` | 400, 401, 403, 404 |
| POST | /requests/:id/upvote | 201: `{hasUpvoted, upvoteCount}` | 401, 403, 404 |
| POST | /requests/:id/attachments | 201: `{data}` | 400, 401, 403, 404, 413 |

### Response Headers

| Header | Description | Example |
|--------|-------------|---------|
| X-Total-Count | Total items for pagination | 150 |
| X-Request-Id | Unique request identifier | req_123_abc |
| X-RateLimit-Limit | Rate limit maximum | 100 |
| X-RateLimit-Remaining | Remaining requests | 95 |
| X-RateLimit-Reset | Reset timestamp | 1234567890 |

---

## 11. Error Codes and Messages

### HTTP Status Codes

| Code | Meaning | When Used |
|------|---------|-----------|
| 200 | OK | Successful GET, PATCH, DELETE |
| 201 | Created | Successful POST creating resource |
| 400 | Bad Request | Invalid input, validation errors |
| 401 | Unauthorized | Missing or invalid token |
| 403 | Forbidden | Insufficient permissions |
| 404 | Not Found | Resource doesn't exist |
| 409 | Conflict | Resource conflict (duplicate email, version mismatch) |
| 410 | Gone | Expired token |
| 413 | Payload Too Large | File upload exceeds limit |
| 414 | URI Too Long | GET search URL exceeds 2000 chars |
| 422 | Unprocessable Entity | Business logic violation |
| 429 | Too Many Requests | Rate limit exceeded |
| 500 | Internal Server Error | Unexpected server error |

### Application Error Codes

#### Authentication Errors (AUTH)
| Code | Message | Context |
|------|---------|---------|
| AUTH001 | Invalid credentials | Wrong email/password |
| AUTH002 | Token expired | JWT expired |
| AUTH003 | Token invalid | Malformed JWT |
| AUTH004 | Account locked | Too many login attempts |
| AUTH005 | Email not verified | Pending verification |
| AUTH006 | Account suspended | Admin suspended |

#### Validation Errors (VAL)
| Code | Message | Context |
|------|---------|---------|
| VAL001 | Validation failed | General validation error |
| VAL002 | Required field missing | Missing required input |
| VAL003 | Invalid format | Wrong data format |
| VAL004 | Value out of range | Number outside limits |
| VAL005 | String too short | Below minimum length |
| VAL006 | String too long | Exceeds maximum length |

#### Business Logic Errors (BUS)
| Code | Message | Context |
|------|---------|---------|
| BUS001 | Invalid status transition | Workflow violation |
| BUS002 | Cannot edit after 10 minutes | Edit time expired |
| BUS003 | Cannot upvote own request | Self-upvote attempt |
| BUS004 | Maximum files exceeded | Too many attachments |
| BUS005 | Duplicate entry | Unique constraint violation |

---

## 12. Performance Requirements

### Response Time Targets

| Operation | Target | Maximum |
|-----------|--------|---------|
| Page Load | < 2s | 5s |
| API Response | < 200ms | 1s |
| Search | < 500ms | 2s |
| File Upload (10MB) | < 5s | 10s |
| Login | < 300ms | 1s |
| Form Submission | < 500ms | 2s |

### Concurrent Users
- Minimum: 100 concurrent users
- Target: 500 concurrent users
- Peak: 1000 concurrent users

### Rate Limits

| Endpoint Type | Limit | Window |
|---------------|-------|--------|
| Authentication | 5 requests | 15 minutes |
| General API | 100 requests | 1 minute |
| Search | 30 requests | 1 minute |
| File Upload | 10 uploads | 5 minutes |
| Password Reset | 3 requests | 1 hour |

### Caching Strategy
- Search results: 5 minutes
- User profile: 10 minutes
- Static assets: 1 hour
- Department data: 30 minutes

---

## 13. Security Requirements

### Authentication Security
- JWT tokens with 24-hour expiry
- Refresh tokens with 7-day expiry
- Secure HttpOnly cookies for tokens
- CSRF protection on state-changing operations

### Password Security
- Bcrypt hashing with cost factor 12
- Password history (last 5 passwords)
- Account lockout after 5 failed attempts
- Password reset tokens expire in 1 hour

### Input Sanitization
- XSS prevention on all text inputs
- SQL injection prevention via parameterized queries
- Path traversal prevention for file uploads
- HTML encoding for user-generated content

### Data Protection
- PII encryption at rest
- TLS 1.2+ for data in transit
- Secure session management
- Audit logging for sensitive operations

### CORS Policy
```javascript
{
  origin: ['http://localhost:5173'],
  credentials: true,
  methods: ['GET', 'POST', 'PATCH', 'DELETE'],
  allowedHeaders: ['Content-Type', 'Authorization']
}
```

---

## 14. Accessibility Requirements

### WCAG 2.1 Level AA Compliance

#### Keyboard Navigation
- All interactive elements keyboard accessible
- Tab order follows logical flow
- Skip links provided
- No keyboard traps
- Focus indicators visible

#### Screen Reader Support
- Semantic HTML structure
- ARIA labels for icons
- Form field descriptions
- Error announcements
- Status change notifications

#### Visual Requirements
- Minimum contrast ratio 4.5:1 (normal text)
- Minimum contrast ratio 3:1 (large text)
- Color not sole indicator
- Resizable text up to 200%
- No horizontal scrolling at 320px

#### Interactive Elements
- Minimum touch target 44x44px
- Clear focus indicators
- Error identification
- Input assistance
- Consistent navigation

### Required ARIA Attributes

```html
<!-- Form Fields -->
<input aria-label="Email address" 
       aria-required="true" 
       aria-invalid="false"
       aria-describedby="email-error">

<!-- Status Messages -->
<div role="alert" aria-live="polite">
  Form submitted successfully
</div>

<!-- Navigation -->
<nav aria-label="Main navigation">
  <ul role="menubar">
    <li role="menuitem">Home</li>
  </ul>
</nav>
```

---

## 15. Test Data and Scenarios

### Test User Accounts

| Email | Password | Role | Status | Special Conditions |
|-------|----------|------|--------|-------------------|
| john@example.com | password123 | CITIZEN | ACTIVE | Has 5 submitted requests |
| jane.smith@example.com | password123 | CITIZEN | PENDING_EMAIL_VERIFICATION | Email not verified |
| mary.clerk@city.gov | password123 | CLERK | ACTIVE | Roads department |
| agent1@city.gov | password123 | FIELD_AGENT | ACTIVE | 3 assigned tasks |
| supervisor1@city.gov | password123 | SUPERVISOR | ACTIVE | Manages 5 agents |
| admin@city.gov | password123 | ADMIN | ACTIVE | Full system access |
| suspended@example.com | password123 | CITIZEN | SUSPENDED | Account suspended |
| inactive@example.com | password123 | CITIZEN | INACTIVE | Inactive account |

### Test Service Requests

| Code | Title | Status | Priority | Category | Test Purpose |
|------|-------|--------|----------|----------|--------------|
| REQ-2024-00001 | Pothole on Main Street | SUBMITTED | HIGH | roads | New request flow |
| REQ-2024-00002 | Streetlight out | TRIAGED | MEDIUM | utilities | Assignment flow |
| REQ-2024-00003 | Park maintenance | IN_PROGRESS | LOW | parks | Progress tracking |
| REQ-2024-00004 | Water leak | RESOLVED | URGENT | water | Resolution flow |
| REQ-2024-00005 | Garbage missed | WAITING_ON_CITIZEN | MEDIUM | waste | Citizen interaction |

### Critical Test Scenarios

#### 1. Registration Flow
1. Navigate to registration page
2. Fill all required fields with valid data
3. Submit form
4. Check console for verification email
5. Click verification link
6. Verify account activated
7. Login with new credentials

#### 2. Service Request Creation
1. Login as citizen
2. Click "New Request"
3. Complete all 5 steps
4. Upload 2 image files
5. Submit request
6. Verify request appears in list
7. Check request code generated

#### 3. Status Workflow
1. Login as clerk
2. Find submitted request
3. Change status to TRIAGED
4. Assign to field agent
5. Login as field agent
6. Change status to IN_PROGRESS
7. Add internal comment
8. Change status to RESOLVED

#### 4. Search and Filter
1. Use GET search with status filter
2. Use POST search with complex filters
3. Test pagination (next/previous)
4. Test sorting (ascending/descending)
5. Export results as CSV
6. Verify export contains filtered data

#### 5. Error Handling
1. Try login with wrong password 6 times
2. Verify account locked after 5 attempts
3. Submit form with invalid email
4. Upload file larger than 10MB
5. Try to access admin page as citizen
6. Submit request with description < 30 chars

#### 6. Performance Testing
1. Load page and measure time < 2s
2. Search with 1000+ results
3. Upload 5 files simultaneously
4. Open application in 3 tabs
5. Perform rapid form submissions

#### 7. Security Testing
1. Try SQL injection in search field
2. Try XSS in comment field
3. Try to upload executable file
4. Try to access other user's data
5. Try to bypass authentication
6. Test CSRF token validation

#### 8. Accessibility Testing
1. Navigate entire app with keyboard only
2. Test with screen reader (NVDA/JAWS)
3. Verify all images have alt text
4. Check color contrast ratios
5. Test at 200% zoom level
6. Verify focus indicators visible

### Feature Flag Testing

| Flag | Effect | Test Scenario |
|------|--------|---------------|
| API_Random500 | 5% random errors | Submit 20 requests, verify ~1 fails |
| UI_WrongDefaultSort | Wrong sort order | Check default sort is by title, not date |
| API_SlowRequests | 10% slow responses | Make 10 API calls, ~1 should be slow |
| API_UploadIntermittentFail | Upload failures | Upload 10 files, some should fail |

### Browser Testing Matrix

| Browser | Versions | OS | Priority |
|---------|----------|-----|----------|
| Chrome | 90+ | Windows, Mac, Linux | High |
| Firefox | 88+ | Windows, Mac, Linux | High |
| Safari | 14+ | Mac, iOS | High |
| Edge | 90+ | Windows | Medium |
| Mobile Chrome | Latest | Android | High |
| Mobile Safari | Latest | iOS | High |

### Regression Test Suite

#### Priority 1 (Critical - Run Always)
- User registration and login
- Service request creation
- Status changes
- File uploads
- Search functionality
- Role-based access control

#### Priority 2 (High - Run Daily)
- Profile updates
- Comment system
- Pagination
- Sorting
- Export functionality
- Email verification

#### Priority 3 (Medium - Run Weekly)
- Password reset
- Bulk operations
- Advanced search
- Performance metrics
- Accessibility compliance
- Cross-browser compatibility

---

## Appendix A: Test Data SQL

```sql
-- Reset all test data
DELETE FROM service_requests WHERE created_by IN (
  SELECT id FROM users WHERE email LIKE '%example.com'
);

-- Create test requests with various statuses
INSERT INTO service_requests (code, title, status, priority) VALUES
  ('REQ-TEST-001', 'Test Submitted', 'SUBMITTED', 'HIGH'),
  ('REQ-TEST-002', 'Test In Progress', 'IN_PROGRESS', 'MEDIUM'),
  ('REQ-TEST-003', 'Test Resolved', 'RESOLVED', 'LOW');
```

## Appendix B: API Testing with cURL

```bash
# Login
curl -X POST http://localhost:3001/api/v1/auth/login \
  -H "Content-Type: application/json" \
  -d '{"email":"john@example.com","password":"password123"}'

# Create Service Request
curl -X POST http://localhost:3001/api/v1/requests \
  -H "Authorization: Bearer YOUR_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "title":"Test Request",
    "description":"This is a test request with minimum 30 characters",
    "category":"roads",
    "locationText":"Test Location"
  }'

# Search Requests
curl -X GET "http://localhost:3001/api/v1/service-requests/search?status=SUBMITTED&priority=HIGH" \
  -H "Authorization: Bearer YOUR_TOKEN"
```

## Appendix C: Common Validation Regex Patterns

```javascript
// Email validation
/^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}$/

// Name validation (with Cyrillic)
/^[a-zA-ZÀ-ÿĀ-žА-яЁё\u0100-\u017F\u0400-\u04FF\s'-]+$/

// Bulgarian phone
/^(\+359|0)[87-9]\d{8}$/

// Password strength
/^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[@$!%*?&#])[A-Za-z\d@$!%*?&#]{12,}$/
```

---

## Document Version
- **Version**: 1.0.0
- **Last Updated**: December 2024
- **Author**: QA Team
- **Review Status**: Final

## Change Log
| Version | Date | Changes | Author |
|---------|------|---------|--------|
| 1.0.0 | Dec 2024 | Initial comprehensive documentation | QA Team |

---

**Note**: This document should be reviewed and updated with each sprint to reflect any changes in requirements, validations, or business rules.