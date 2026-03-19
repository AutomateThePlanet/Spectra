# Seed Data and Feature Flags Documentation

## Overview
This document describes the test data seeded in the City Services Portal database and the available feature flags for testing various scenarios, including bug simulation and performance testing.

## Seeded User Accounts

### Authentication Credentials
All test accounts use the password: `password123`

### User Profiles

#### 1. Citizen Users

**John Doe** - `john@example.com`
- Role: CITIZEN
- Profile: Active citizen with multiple service requests
- Location: Downtown area
- Test Scenarios: Standard citizen workflows, request tracking

**Jane Smith** - `jane@example.com`  
- Role: CITIZEN
- Profile: New user with single request
- Location: Suburban area
- Test Scenarios: First-time user experience, onboarding

**Bob Wilson** - `bob@example.com`
- Role: CITIZEN
- Profile: Frequent reporter with diverse request types
- Location: Multiple locations
- Test Scenarios: Power user workflows, bulk operations

#### 2. City Staff

**Mary Johnson** - `mary.clerk@city.gov`
- Role: CLERK
- Department: Customer Service
- Profile: Experienced clerk, handles high volume
- Test Scenarios: Request processing, split-view interface

**Sarah Brown** - `sarah.clerk@city.gov`
- Role: CLERK
- Department: Customer Service
- Profile: New clerk in training
- Test Scenarios: Training mode, error handling

#### 3. Field Agents

**Agent One** - `agent1@city.gov`
- Role: FIELD_AGENT
- Department: Public Works
- Specialization: Roads and infrastructure
- Test Scenarios: Mobile workflows, field updates

**Agent Two** - `agent2@city.gov`
- Role: FIELD_AGENT
- Department: Parks & Recreation
- Specialization: Parks and facilities
- Test Scenarios: Photo uploads, completion reports

**Agent Three** - `agent3@city.gov`
- Role: FIELD_AGENT
- Department: Utilities
- Specialization: Water and sewer
- Test Scenarios: Emergency responses, priority handling

#### 4. Supervisors

**Supervisor One** - `supervisor1@city.gov`
- Role: SUPERVISOR
- Department: Public Works
- Team Size: 5 agents
- Test Scenarios: Performance monitoring, quality reviews

**Supervisor Two** - `supervisor2@city.gov`
- Role: SUPERVISOR
- Department: All Departments
- Authority: Cross-department oversight
- Test Scenarios: Report generation, resource allocation

#### 5. System Administrator

**Admin User** - `admin@city.gov`
- Role: ADMIN
- Access: Full system access
- Test Scenarios: System configuration, user management, feature flags

## Seeded Service Requests

### Request Distribution

Total seeded requests: **50+**

#### By Status
- DRAFT: 3 requests
- SUBMITTED: 8 requests
- IN_REVIEW: 7 requests
- ASSIGNED: 10 requests
- IN_PROGRESS: 12 requests
- COMPLETED: 8 requests
- CLOSED: 5 requests

#### By Category
- **Roads & Transportation**: 15 requests
  - Pothole repairs (5)
  - Street light issues (4)
  - Traffic signal problems (3)
  - Road marking (3)

- **Parks & Recreation**: 12 requests
  - Park maintenance (4)
  - Playground repairs (3)
  - Graffiti removal (3)
  - Tree trimming (2)

- **Utilities**: 10 requests
  - Water leaks (4)
  - Sewer issues (3)
  - Storm drain problems (3)

- **Waste Management**: 8 requests
  - Missed collection (3)
  - Bulk item pickup (2)
  - Recycling issues (2)
  - Illegal dumping (1)

- **Public Safety**: 5 requests
  - Abandoned vehicles (2)
  - Street lighting (2)
  - Noise complaints (1)

#### By Priority
- URGENT: 5 requests
- HIGH: 12 requests
- MEDIUM: 20 requests
- LOW: 13 requests

### Sample Request Details

#### REQ-2024-001: Major Pothole on Main Street
- Status: IN_PROGRESS
- Category: Roads & Transportation
- Priority: HIGH
- Reporter: John Doe
- Assigned: Agent One
- Description: "Large pothole causing damage to vehicles"
- Attachments: 2 photos
- Comments: 3 updates

#### REQ-2024-002: Broken Playground Equipment
- Status: ASSIGNED
- Category: Parks & Recreation
- Priority: URGENT
- Reporter: Jane Smith
- Assigned: Agent Two
- Description: "Swing set has broken chain, safety hazard"
- Attachments: 1 photo
- Comments: 1 update

#### REQ-2024-003: Water Leak on Oak Avenue
- Status: COMPLETED
- Category: Utilities
- Priority: HIGH
- Reporter: Bob Wilson
- Assigned: Agent Three
- Description: "Water bubbling up from street"
- Attachments: 3 photos
- Resolution: "Main line repaired"

## Feature Flags System

### Overview
Feature flags allow dynamic enabling/disabling of features and behaviors for testing purposes. They can be managed through the Admin interface at `/admin/flags`.

### Available Feature Flags

#### 1. Bug Simulation Flags

**API_Random500**
- Type: Boolean
- Default: false
- Description: Randomly returns HTTP 500 errors on 5% of API calls
- Use Case: Testing error handling and retry logic
- Impact: Affects all API endpoints randomly

**UI_WrongDefaultSort**
- Type: Boolean
- Default: false
- Description: Applies incorrect default sorting to request lists
- Use Case: Testing sort functionality and user corrections
- Impact: Lists show oldest first instead of newest

**API_SlowRequests**
- Type: Boolean
- Default: false
- Description: Adds 3-5 second delay to 10% of API requests
- Use Case: Testing loading states and timeout handling
- Impact: Random API slowdown

**API_UploadIntermittentFail**
- Type: Boolean
- Default: false
- Description: Causes 20% of file uploads to fail
- Use Case: Testing upload retry and error messaging
- Impact: File upload endpoints only

**DB_ConnectionTimeout**
- Type: Boolean
- Default: false
- Description: Simulates database connection timeouts (2% of queries)
- Use Case: Testing database error handling
- Impact: All database operations

**Auth_SessionExpireEarly**
- Type: Boolean
- Default: false
- Description: Expires user sessions after 5 minutes instead of 30
- Use Case: Testing session renewal and re-authentication
- Impact: All authenticated users

#### 2. Feature Toggle Flags

**FEATURE_AdvancedSearch**
- Type: Boolean
- Default: true
- Description: Enables advanced search functionality
- Use Case: A/B testing search features
- Impact: Search interface and API

**FEATURE_BulkOperations**
- Type: Boolean
- Default: false
- Description: Enables bulk request operations
- Use Case: Testing mass update functionality
- Impact: Request list interface

**FEATURE_AIAssistant**
- Type: Boolean
- Default: false
- Description: Enables AI-powered request categorization
- Use Case: Testing ML integration
- Impact: Request submission flow

**FEATURE_DarkMode**
- Type: Boolean
- Default: true
- Description: Enables dark mode toggle
- Use Case: Testing theme switching
- Impact: All UI components

**FEATURE_EmailDigest**
- Type: Boolean
- Default: false
- Description: Enables daily email digest for staff
- Use Case: Testing email batching
- Impact: Notification system

**FEATURE_MobileOptimized**
- Type: Boolean
- Default: true
- Description: Enables mobile-optimized views
- Use Case: Testing responsive design
- Impact: Mobile browsers

#### 3. Performance Testing Flags

**PERF_DisableCache**
- Type: Boolean
- Default: false
- Description: Disables all caching mechanisms
- Use Case: Testing worst-case performance
- Impact: System-wide performance

**PERF_VerboseLogging**
- Type: Boolean
- Default: false
- Description: Enables detailed performance logging
- Use Case: Performance profiling
- Impact: Log volume increase

**PERF_SimulateHighLoad**
- Type: Boolean
- Default: false
- Description: Simulates high system load
- Use Case: Stress testing
- Impact: Artificial CPU/memory usage

**PERF_LimitConcurrency**
- Type: Number
- Default: 100
- Description: Limits concurrent request processing
- Use Case: Testing queue behavior
- Impact: Request throughput

#### 4. Testing Mode Flags

**TEST_MockEmails**
- Type: Boolean
- Default: true (in QA environment)
- Description: Prevents actual email sending, logs instead
- Use Case: Testing without spamming inboxes
- Impact: Email service

**TEST_FastAnimations**
- Type: Boolean
- Default: false
- Description: Speeds up all animations 10x
- Use Case: Faster E2E test execution
- Impact: UI animations

**TEST_ShowTestData**
- Type: Boolean
- Default: false
- Description: Adds visual indicators for test data
- Use Case: Distinguishing test vs real data
- Impact: UI display

**TEST_BypassValidation**
- Type: Boolean
- Default: false
- Description: Bypasses client-side validation
- Use Case: Testing server-side validation
- Impact: Form submissions

### Feature Flag Management

#### Via Admin UI
1. Navigate to `/admin/flags`
2. Login with admin credentials
3. Toggle flags using the interface
4. Changes take effect immediately

#### Via API
```http
PUT /api/admin/feature-flags
Authorization: Bearer {admin_token}
Content-Type: application/json

{
  "flag": "API_Random500",
  "enabled": true
}
```

#### Via Database
Flags are stored in the `FeatureFlag` table:
```sql
UPDATE FeatureFlag 
SET enabled = true 
WHERE name = 'API_Random500';
```

### Testing Scenarios with Flags

#### Scenario 1: Chaos Testing
Enable these flags together:
- API_Random500 ✓
- API_SlowRequests ✓
- API_UploadIntermittentFail ✓
- DB_ConnectionTimeout ✓

Expected behavior: System should remain usable despite errors

#### Scenario 2: Performance Testing
Enable these flags:
- PERF_DisableCache ✓
- PERF_VerboseLogging ✓
- PERF_SimulateHighLoad ✓

Monitor: Response times, error rates, resource usage

#### Scenario 3: New User Experience
Disable these flags:
- FEATURE_AdvancedSearch ✗
- FEATURE_BulkOperations ✗
- FEATURE_AIAssistant ✗

Test: Simplified interface for new users

#### Scenario 4: Mobile Testing
Configure:
- FEATURE_MobileOptimized ✓
- TEST_FastAnimations ✓
- UI_WrongDefaultSort ✓

Test: Mobile experience with intentional bugs

## Test Data Patterns

### Predictable Test Data

#### Request IDs
Format: `REQ-YYYY-XXXX`
- YYYY: Year
- XXXX: Sequential number

#### Timestamps
- Requests created between 30 days ago and today
- Updates occur every 2-5 days
- Completed requests: 5-10 days old

#### File Attachments
Standard test files available:
- `test-image-1.jpg` (500KB)
- `test-image-2.png` (1MB)
- `test-document.pdf` (2MB)
- `large-file.pdf` (9MB)
- `invalid-file.exe` (Should be rejected)

### Data Relationships

#### User-Request Associations
- John Doe: 10-15 requests (various statuses)
- Jane Smith: 1-3 requests (mostly new)
- Bob Wilson: 20+ requests (power user)

#### Agent Workload
- Agent One: 5-8 active assignments
- Agent Two: 3-5 active assignments
- Agent Three: 10+ assignments (busy)

#### Department Distribution
- Public Works: 40% of requests
- Parks & Recreation: 25% of requests
- Utilities: 20% of requests
- Other: 15% of requests

## Database Reset and Seeding

### Reset Commands

#### Full Reset (Caution: Deletes all data)
```bash
cd city-services-portal/api
npm run db:reset
```

#### Seed Only (Preserves structure)
```bash
cd city-services-portal/api
npm run db:seed
```

#### Selective Seeding
```bash
# Seed only users
npm run db:seed -- --users

# Seed only requests
npm run db:seed -- --requests

# Seed specific number of requests
npm run db:seed -- --count=100
```

### Custom Test Data Generation

#### Via API (Admin only)
```http
POST /api/admin/generate-test-data
Authorization: Bearer {admin_token}
Content-Type: application/json

{
  "type": "requests",
  "count": 50,
  "options": {
    "category": "Roads & Transportation",
    "priority": "HIGH",
    "status": "SUBMITTED"
  }
}
```

#### Via Prisma Studio
```bash
npm run db:studio
# Opens browser interface for direct data manipulation
```

## Testing Best Practices

### Using Feature Flags
1. Document which flags are enabled for each test
2. Reset flags to defaults after testing
3. Test both enabled and disabled states
4. Verify flag changes don't affect other tests

### Working with Seed Data
1. Use consistent test users for scenarios
2. Don't modify core seed data
3. Create test-specific data when needed
4. Clean up test data after test runs

### Bug Simulation Testing
1. Enable one bug flag at a time initially
2. Combine flags for chaos testing
3. Monitor system behavior and recovery
4. Document unexpected interactions

### Performance Testing Setup
1. Start with baseline (no flags)
2. Enable performance flags incrementally
3. Measure impact of each flag
4. Run tests during different load conditions

## Troubleshooting

### Common Issues

**Issue**: Feature flag not taking effect
- Solution: Check cache, may need to clear browser cache
- API cache TTL: 60 seconds

**Issue**: Seed data missing
- Solution: Run `npm run db:seed`
- Check for migration issues

**Issue**: Conflicting flags
- Example: API_Random500 + TEST_BypassValidation
- Solution: Test flags independently first

**Issue**: Test user cannot login
- Check password: Should be `password123`
- Verify account not locked (5 failed attempts)
- Check feature flag: Auth_SessionExpireEarly

---
*Document Version: 1.0*
*Last Updated: 2025*
*For Workshop and Testing Use*