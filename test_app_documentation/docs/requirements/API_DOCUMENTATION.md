# City Services Portal - API Documentation

## Base Information

### Base URL
```
Development: http://localhost:3001/api
Staging: https://staging-api.cityservices.gov/api
Production: https://api.cityservices.gov/api
```

### API Version
Current Version: `v1`

### Content Type
All requests and responses use `application/json` unless specified otherwise.

## Authentication

### Overview
The API uses JWT (JSON Web Tokens) for authentication. Tokens must be included in the Authorization header for protected endpoints.

### Token Format
```
Authorization: Bearer <token>
```

### Token Lifetime
- Access Token: 30 minutes
- Refresh Token: 7 days
- Token includes: userId, email, role, iat, exp

## Common Response Format

### Success Response
```json
{
  "success": true,
  "data": {
    // Response data
  },
  "correlationId": "req_1234567890_abc",
  "timestamp": "2025-01-15T10:30:00Z"
}
```

### Error Response
```json
{
  "success": false,
  "error": {
    "code": "VALIDATION_ERROR",
    "message": "Invalid input data",
    "details": [
      {
        "field": "email",
        "message": "Invalid email format"
      }
    ]
  },
  "correlationId": "req_1234567890_abc",
  "timestamp": "2025-01-15T10:30:00Z"
}
```

### Pagination Response
```json
{
  "success": true,
  "data": [],
  "pagination": {
    "page": 1,
    "limit": 20,
    "total": 150,
    "totalPages": 8,
    "hasNext": true,
    "hasPrev": false
  },
  "correlationId": "req_1234567890_abc"
}
```

## Error Codes

| Code | HTTP Status | Description |
|------|------------|-------------|
| UNAUTHORIZED | 401 | Missing or invalid authentication |
| FORBIDDEN | 403 | Insufficient permissions |
| NOT_FOUND | 404 | Resource not found |
| VALIDATION_ERROR | 400 | Invalid input data |
| DUPLICATE_ENTRY | 409 | Resource already exists |
| RATE_LIMIT_EXCEEDED | 429 | Too many requests |
| INTERNAL_ERROR | 500 | Server error |
| SERVICE_UNAVAILABLE | 503 | Service temporarily unavailable |

## API Endpoints

### Authentication Endpoints

#### POST /api/auth/register
Register a new citizen user account.

**Request Body:**
```json
{
  "email": "user@example.com",
  "password": "SecurePass123!",
  "firstName": "John",
  "lastName": "Doe",
  "phone": "+1234567890"
}
```

**Response:** `201 Created`
```json
{
  "success": true,
  "data": {
    "user": {
      "id": "usr_123",
      "email": "user@example.com",
      "firstName": "John",
      "lastName": "Doe",
      "role": "CITIZEN"
    },
    "token": "eyJhbGciOiJIUzI1NiIs..."
  }
}
```

**Validation Rules:**
- Email: Valid email format, unique
- Password: Min 8 chars, 1 uppercase, 1 lowercase, 1 number, 1 special char
- Phone: Optional, valid phone format

---

#### POST /api/auth/login
Authenticate user and receive access token.

**Request Body:**
```json
{
  "email": "user@example.com",
  "password": "SecurePass123!"
}
```

**Response:** `200 OK`
```json
{
  "success": true,
  "data": {
    "user": {
      "id": "usr_123",
      "email": "user@example.com",
      "firstName": "John",
      "lastName": "Doe",
      "role": "CITIZEN"
    },
    "token": "eyJhbGciOiJIUzI1NiIs...",
    "refreshToken": "eyJhbGciOiJIUzI1NiIs..."
  }
}
```

**Error Responses:**
- `401`: Invalid credentials
- `423`: Account locked (too many attempts)

---

#### POST /api/auth/refresh
Refresh access token using refresh token.

**Request Body:**
```json
{
  "refreshToken": "eyJhbGciOiJIUzI1NiIs..."
}
```

**Response:** `200 OK`
```json
{
  "success": true,
  "data": {
    "token": "eyJhbGciOiJIUzI1NiIs...",
    "refreshToken": "eyJhbGciOiJIUzI1NiIs..."
  }
}
```

---

#### POST /api/auth/logout
Logout user and invalidate tokens.

**Headers:**
```
Authorization: Bearer <token>
```

**Response:** `200 OK`
```json
{
  "success": true,
  "message": "Logged out successfully"
}
```

---

#### POST /api/auth/forgot-password
Request password reset email.

**Request Body:**
```json
{
  "email": "user@example.com"
}
```

**Response:** `200 OK`
```json
{
  "success": true,
  "message": "Password reset email sent"
}
```

---

#### POST /api/auth/reset-password
Reset password using token from email.

**Request Body:**
```json
{
  "token": "reset_token_from_email",
  "newPassword": "NewSecurePass123!"
}
```

**Response:** `200 OK`
```json
{
  "success": true,
  "message": "Password reset successfully"
}
```

### Service Request Endpoints

#### GET /api/requests
Get list of service requests (filtered by user role).

**Headers:**
```
Authorization: Bearer <token>
```

**Query Parameters:**
| Parameter | Type | Description | Default |
|-----------|------|-------------|---------|
| page | number | Page number | 1 |
| limit | number | Items per page | 20 |
| status | string | Filter by status | all |
| category | string | Filter by category | all |
| priority | string | Filter by priority | all |
| search | string | Search in title/description | - |
| sortBy | string | Sort field | createdAt |
| sortOrder | string | asc or desc | desc |
| dateFrom | string | Start date (ISO) | - |
| dateTo | string | End date (ISO) | - |

**Response:** `200 OK`
```json
{
  "success": true,
  "data": [
    {
      "id": "req_123",
      "requestId": "REQ-2024-0001",
      "title": "Pothole on Main Street",
      "description": "Large pothole causing damage",
      "category": "ROADS",
      "priority": "HIGH",
      "status": "IN_PROGRESS",
      "location": {
        "address": "123 Main St",
        "coordinates": {
          "lat": 40.7128,
          "lng": -74.0060
        }
      },
      "reporter": {
        "id": "usr_456",
        "name": "John Doe",
        "email": "john@example.com"
      },
      "assignedTo": {
        "id": "usr_789",
        "name": "Agent Smith"
      },
      "createdAt": "2024-01-15T10:00:00Z",
      "updatedAt": "2024-01-16T14:30:00Z"
    }
  ],
  "pagination": {
    "page": 1,
    "limit": 20,
    "total": 150,
    "totalPages": 8
  }
}
```

**Role-Based Filtering:**
- CITIZEN: Only their own requests
- CLERK: All requests
- FIELD_AGENT: Assigned requests
- SUPERVISOR: Department requests
- ADMIN: All requests

---

#### GET /api/requests/:id
Get single service request details.

**Headers:**
```
Authorization: Bearer <token>
```

**Response:** `200 OK`
```json
{
  "success": true,
  "data": {
    "id": "req_123",
    "requestId": "REQ-2024-0001",
    "title": "Pothole on Main Street",
    "description": "Large pothole near intersection causing damage to vehicles",
    "category": "ROADS",
    "priority": "HIGH",
    "status": "IN_PROGRESS",
    "location": {
      "address": "123 Main St",
      "coordinates": {
        "lat": 40.7128,
        "lng": -74.0060
      }
    },
    "reporter": {
      "id": "usr_456",
      "name": "John Doe",
      "email": "john@example.com",
      "phone": "+1234567890"
    },
    "assignedTo": {
      "id": "usr_789",
      "name": "Agent Smith",
      "department": "Public Works"
    },
    "attachments": [
      {
        "id": "att_001",
        "filename": "pothole-photo.jpg",
        "url": "/api/files/att_001",
        "size": 524288,
        "mimeType": "image/jpeg",
        "uploadedAt": "2024-01-15T10:05:00Z"
      }
    ],
    "comments": [
      {
        "id": "com_001",
        "text": "Crew scheduled for tomorrow",
        "author": {
          "id": "usr_789",
          "name": "Agent Smith",
          "role": "FIELD_AGENT"
        },
        "createdAt": "2024-01-16T09:00:00Z"
      }
    ],
    "history": [
      {
        "status": "SUBMITTED",
        "timestamp": "2024-01-15T10:00:00Z",
        "user": "John Doe"
      },
      {
        "status": "IN_REVIEW",
        "timestamp": "2024-01-15T10:30:00Z",
        "user": "Mary Clerk"
      },
      {
        "status": "ASSIGNED",
        "timestamp": "2024-01-15T11:00:00Z",
        "user": "Mary Clerk"
      },
      {
        "status": "IN_PROGRESS",
        "timestamp": "2024-01-16T08:00:00Z",
        "user": "Agent Smith"
      }
    ],
    "metadata": {
      "internalNotes": "Check for additional damage",
      "estimatedCompletion": "2024-01-18T17:00:00Z",
      "actualCost": 1500.00,
      "tags": ["urgent", "safety"]
    },
    "createdAt": "2024-01-15T10:00:00Z",
    "updatedAt": "2024-01-16T14:30:00Z"
  }
}
```

---

#### POST /api/requests
Create new service request.

**Headers:**
```
Authorization: Bearer <token>
Content-Type: application/json
```

**Request Body:**
```json
{
  "title": "Broken streetlight",
  "description": "Streetlight not working at night, creating safety hazard",
  "category": "PUBLIC_SAFETY",
  "priority": "HIGH",
  "location": {
    "address": "456 Oak Avenue",
    "coordinates": {
      "lat": 40.7128,
      "lng": -74.0060
    }
  },
  "contactInfo": {
    "phone": "+1234567890",
    "preferredContact": "EMAIL"
  }
}
```

**Response:** `201 Created`
```json
{
  "success": true,
  "data": {
    "id": "req_124",
    "requestId": "REQ-2024-0002",
    "status": "SUBMITTED",
    "title": "Broken streetlight",
    "createdAt": "2024-01-17T10:00:00Z"
  }
}
```

**Validation:**
- Title: Required, 10-200 chars
- Description: Required, 50-2000 chars
- Category: Required, valid enum
- Location: Required

---

#### PUT /api/requests/:id
Update service request.

**Headers:**
```
Authorization: Bearer <token>
Content-Type: application/json
```

**Request Body (Partial Update):**
```json
{
  "status": "IN_PROGRESS",
  "priority": "URGENT",
  "assignedTo": "usr_789",
  "internalNotes": "Requires specialized equipment"
}
```

**Response:** `200 OK`
```json
{
  "success": true,
  "data": {
    "id": "req_123",
    "status": "IN_PROGRESS",
    "priority": "URGENT",
    "updatedAt": "2024-01-17T11:00:00Z"
  }
}
```

**Permissions:**
- CITIZEN: Can only update own draft requests
- CLERK: Can update status, priority, assignment
- FIELD_AGENT: Can update status, add notes
- SUPERVISOR: Full update access
- ADMIN: Full update access

---

#### DELETE /api/requests/:id
Delete service request (draft only).

**Headers:**
```
Authorization: Bearer <token>
```

**Response:** `200 OK`
```json
{
  "success": true,
  "message": "Request deleted successfully"
}
```

**Restrictions:**
- Only DRAFT status can be deleted
- Only by creator or ADMIN

---

#### POST /api/requests/:id/comments
Add comment to service request.

**Headers:**
```
Authorization: Bearer <token>
Content-Type: application/json
```

**Request Body:**
```json
{
  "text": "Work has been completed. Please verify.",
  "internal": false
}
```

**Response:** `201 Created`
```json
{
  "success": true,
  "data": {
    "id": "com_002",
    "text": "Work has been completed. Please verify.",
    "author": {
      "id": "usr_789",
      "name": "Agent Smith"
    },
    "createdAt": "2024-01-17T15:00:00Z"
  }
}
```

---

#### POST /api/requests/:id/attachments
Upload attachment to service request.

**Headers:**
```
Authorization: Bearer <token>
Content-Type: multipart/form-data
```

**Request Body (Form Data):**
- file: Binary file data
- description: Optional description

**Response:** `201 Created`
```json
{
  "success": true,
  "data": {
    "id": "att_002",
    "filename": "completion-photo.jpg",
    "url": "/api/files/att_002",
    "size": 1048576,
    "mimeType": "image/jpeg",
    "uploadedAt": "2024-01-17T15:30:00Z"
  }
}
```

**Restrictions:**
- Max file size: 10MB
- Allowed types: jpg, png, pdf, docx
- Max 5 files per request

### User Management Endpoints

#### GET /api/users
Get list of users (Admin only).

**Headers:**
```
Authorization: Bearer <token>
```

**Query Parameters:**
| Parameter | Type | Description |
|-----------|------|-------------|
| page | number | Page number |
| limit | number | Items per page |
| role | string | Filter by role |
| search | string | Search in name/email |

**Response:** `200 OK`
```json
{
  "success": true,
  "data": [
    {
      "id": "usr_123",
      "email": "john@example.com",
      "firstName": "John",
      "lastName": "Doe",
      "role": "CITIZEN",
      "status": "ACTIVE",
      "createdAt": "2024-01-01T00:00:00Z"
    }
  ],
  "pagination": {
    "page": 1,
    "limit": 20,
    "total": 50
  }
}
```

---

#### GET /api/users/:id
Get user details.

**Headers:**
```
Authorization: Bearer <token>
```

**Response:** `200 OK`
```json
{
  "success": true,
  "data": {
    "id": "usr_123",
    "email": "john@example.com",
    "firstName": "John",
    "lastName": "Doe",
    "role": "CITIZEN",
    "phone": "+1234567890",
    "department": null,
    "status": "ACTIVE",
    "lastLogin": "2024-01-17T09:00:00Z",
    "createdAt": "2024-01-01T00:00:00Z"
  }
}
```

---

#### PUT /api/users/:id
Update user information.

**Headers:**
```
Authorization: Bearer <token>
Content-Type: application/json
```

**Request Body:**
```json
{
  "firstName": "John",
  "lastName": "Smith",
  "phone": "+9876543210",
  "role": "CLERK",
  "department": "Customer Service"
}
```

**Response:** `200 OK`
```json
{
  "success": true,
  "data": {
    "id": "usr_123",
    "email": "john@example.com",
    "firstName": "John",
    "lastName": "Smith",
    "role": "CLERK",
    "updatedAt": "2024-01-17T10:00:00Z"
  }
}
```

**Permissions:**
- Users can update own profile (except role)
- ADMIN can update any user including role

---

#### DELETE /api/users/:id
Deactivate user account (Admin only).

**Headers:**
```
Authorization: Bearer <token>
```

**Response:** `200 OK`
```json
{
  "success": true,
  "message": "User deactivated successfully"
}
```

### Reports Endpoints

#### GET /api/reports/dashboard
Get dashboard statistics.

**Headers:**
```
Authorization: Bearer <token>
```

**Query Parameters:**
| Parameter | Type | Description |
|-----------|------|-------------|
| dateFrom | string | Start date |
| dateTo | string | End date |
| department | string | Filter by department |

**Response:** `200 OK`
```json
{
  "success": true,
  "data": {
    "summary": {
      "totalRequests": 1250,
      "openRequests": 145,
      "closedRequests": 1105,
      "averageResolutionTime": 72.5,
      "satisfactionScore": 4.2
    },
    "byStatus": {
      "SUBMITTED": 45,
      "IN_REVIEW": 30,
      "ASSIGNED": 25,
      "IN_PROGRESS": 40,
      "COMPLETED": 5,
      "CLOSED": 1105
    },
    "byCategory": {
      "ROADS": 450,
      "PARKS": 300,
      "UTILITIES": 250,
      "WASTE": 150,
      "PUBLIC_SAFETY": 100
    },
    "byPriority": {
      "URGENT": 50,
      "HIGH": 200,
      "MEDIUM": 600,
      "LOW": 400
    },
    "trends": {
      "daily": [
        {
          "date": "2024-01-17",
          "submitted": 15,
          "completed": 12
        }
      ]
    }
  }
}
```

---

#### GET /api/reports/performance
Get performance metrics report.

**Headers:**
```
Authorization: Bearer <token>
```

**Query Parameters:**
| Parameter | Type | Description |
|-----------|------|-------------|
| dateFrom | string | Start date |
| dateTo | string | End date |
| groupBy | string | agent/department/category |

**Response:** `200 OK`
```json
{
  "success": true,
  "data": {
    "agents": [
      {
        "id": "usr_789",
        "name": "Agent Smith",
        "metrics": {
          "completed": 45,
          "averageTime": 48.5,
          "satisfactionScore": 4.5,
          "onTimeRate": 0.92
        }
      }
    ],
    "departments": [
      {
        "name": "Public Works",
        "metrics": {
          "totalRequests": 450,
          "completed": 420,
          "pending": 30,
          "averageTime": 72.0
        }
      }
    ]
  }
}
```

---

#### POST /api/reports/export
Export report data.

**Headers:**
```
Authorization: Bearer <token>
Content-Type: application/json
```

**Request Body:**
```json
{
  "type": "requests",
  "format": "csv",
  "filters": {
    "dateFrom": "2024-01-01",
    "dateTo": "2024-01-31",
    "status": ["COMPLETED", "CLOSED"]
  }
}
```

**Response:** `200 OK`
```json
{
  "success": true,
  "data": {
    "url": "/api/exports/exp_123",
    "expiresAt": "2024-01-18T00:00:00Z"
  }
}
```

### Admin Endpoints

#### GET /api/admin/feature-flags
Get all feature flags.

**Headers:**
```
Authorization: Bearer <token>
```

**Response:** `200 OK`
```json
{
  "success": true,
  "data": [
    {
      "id": "flag_001",
      "name": "API_Random500",
      "description": "Randomly return 500 errors",
      "enabled": false,
      "type": "BOOLEAN",
      "value": false
    },
    {
      "id": "flag_002",
      "name": "FEATURE_AdvancedSearch",
      "description": "Enable advanced search",
      "enabled": true,
      "type": "BOOLEAN",
      "value": true
    }
  ]
}
```

---

#### PUT /api/admin/feature-flags/:name
Update feature flag.

**Headers:**
```
Authorization: Bearer <token>
Content-Type: application/json
```

**Request Body:**
```json
{
  "enabled": true,
  "value": true
}
```

**Response:** `200 OK`
```json
{
  "success": true,
  "data": {
    "name": "API_Random500",
    "enabled": true,
    "value": true,
    "updatedAt": "2024-01-17T10:00:00Z"
  }
}
```

---

#### GET /api/admin/audit-logs
Get system audit logs.

**Headers:**
```
Authorization: Bearer <token>
```

**Query Parameters:**
| Parameter | Type | Description |
|-----------|------|-------------|
| page | number | Page number |
| limit | number | Items per page |
| action | string | Filter by action |
| userId | string | Filter by user |
| dateFrom | string | Start date |
| dateTo | string | End date |

**Response:** `200 OK`
```json
{
  "success": true,
  "data": [
    {
      "id": "log_001",
      "action": "REQUEST_UPDATE",
      "userId": "usr_789",
      "userName": "Agent Smith",
      "resource": "requests",
      "resourceId": "req_123",
      "changes": {
        "status": {
          "from": "ASSIGNED",
          "to": "IN_PROGRESS"
        }
      },
      "ipAddress": "192.168.1.100",
      "userAgent": "Mozilla/5.0...",
      "timestamp": "2024-01-17T10:00:00Z"
    }
  ],
  "pagination": {
    "page": 1,
    "limit": 50,
    "total": 1000
  }
}
```

---

#### POST /api/admin/generate-test-data
Generate test data (QA environment only).

**Headers:**
```
Authorization: Bearer <token>
Content-Type: application/json
```

**Request Body:**
```json
{
  "type": "requests",
  "count": 100,
  "options": {
    "categories": ["ROADS", "PARKS"],
    "statuses": ["SUBMITTED", "IN_PROGRESS"],
    "dateRange": {
      "from": "2024-01-01",
      "to": "2024-01-31"
    }
  }
}
```

**Response:** `201 Created`
```json
{
  "success": true,
  "data": {
    "created": 100,
    "type": "requests",
    "summary": {
      "ROADS": 50,
      "PARKS": 50
    }
  }
}
```

### Search Endpoints

#### GET /api/search
Global search across requests.

**Headers:**
```
Authorization: Bearer <token>
```

**Query Parameters:**
| Parameter | Type | Description |
|-----------|------|-------------|
| q | string | Search query |
| type | string | requests/users/all |
| limit | number | Results limit |

**Response:** `200 OK`
```json
{
  "success": true,
  "data": {
    "requests": [
      {
        "id": "req_123",
        "title": "Pothole on Main Street",
        "matchedField": "title",
        "score": 0.95
      }
    ],
    "users": [
      {
        "id": "usr_456",
        "name": "John Doe",
        "email": "john@example.com",
        "matchedField": "name",
        "score": 0.85
      }
    ],
    "totalResults": 15
  }
}
```

### File Management Endpoints

#### GET /api/files/:id
Download file attachment.

**Headers:**
```
Authorization: Bearer <token>
```

**Response:** `200 OK`
- Content-Type: Based on file type
- Content-Disposition: attachment; filename="file.jpg"
- Binary file data

---

#### DELETE /api/files/:id
Delete file attachment.

**Headers:**
```
Authorization: Bearer <token>
```

**Response:** `200 OK`
```json
{
  "success": true,
  "message": "File deleted successfully"
}
```

### Health Check Endpoints

#### GET /api/health
System health check.

**Response:** `200 OK`
```json
{
  "status": "healthy",
  "timestamp": "2024-01-17T10:00:00Z",
  "version": "1.0.0",
  "services": {
    "database": "connected",
    "redis": "connected",
    "email": "operational"
  }
}
```

---

#### GET /api/health/detailed
Detailed health metrics (Admin only).

**Headers:**
```
Authorization: Bearer <token>
```

**Response:** `200 OK`
```json
{
  "status": "healthy",
  "timestamp": "2024-01-17T10:00:00Z",
  "version": "1.0.0",
  "uptime": 864000,
  "services": {
    "database": {
      "status": "connected",
      "latency": 5,
      "connections": 10
    },
    "redis": {
      "status": "connected",
      "memory": "45MB",
      "keys": 1250
    },
    "email": {
      "status": "operational",
      "queue": 5,
      "sent24h": 450
    }
  },
  "metrics": {
    "requestsPerMinute": 25,
    "activeUsers": 45,
    "cpuUsage": 35.5,
    "memoryUsage": 65.2
  }
}
```

## WebSocket Events

### Connection
```javascript
const socket = io('http://localhost:3001', {
  auth: {
    token: 'Bearer <jwt_token>'
  }
});
```

### Events

#### request:updated
Emitted when a request is updated.
```json
{
  "requestId": "req_123",
  "changes": {
    "status": "IN_PROGRESS",
    "assignedTo": "usr_789"
  },
  "timestamp": "2024-01-17T10:00:00Z"
}
```

#### request:commented
Emitted when a comment is added.
```json
{
  "requestId": "req_123",
  "comment": {
    "id": "com_002",
    "text": "Work completed",
    "author": "Agent Smith"
  }
}
```

#### notification
Personal notifications for user.
```json
{
  "type": "REQUEST_ASSIGNED",
  "title": "New Assignment",
  "message": "Request REQ-2024-0001 assigned to you",
  "data": {
    "requestId": "req_123"
  }
}
```

## Rate Limiting

### Limits by Endpoint Type

| Endpoint Type | Limit | Window |
|--------------|-------|---------|
| Authentication | 5 requests | 1 minute |
| Read (GET) | 100 requests | 1 minute |
| Write (POST/PUT) | 50 requests | 1 minute |
| File Upload | 10 requests | 5 minutes |
| Reports | 10 requests | 1 minute |

### Rate Limit Headers
```
X-RateLimit-Limit: 100
X-RateLimit-Remaining: 95
X-RateLimit-Reset: 1642416000
```

### Rate Limit Exceeded Response
```json
{
  "success": false,
  "error": {
    "code": "RATE_LIMIT_EXCEEDED",
    "message": "Too many requests",
    "retryAfter": 60
  }
}
```

## Webhook Configuration

### Webhook Events
- request.created
- request.updated
- request.completed
- user.registered
- system.alert

### Webhook Payload
```json
{
  "event": "request.created",
  "timestamp": "2024-01-17T10:00:00Z",
  "data": {
    "requestId": "req_123",
    "title": "New service request"
  },
  "signature": "sha256=..."
}
```

### Webhook Security
- HMAC signature validation
- Retry logic with exponential backoff
- Dead letter queue for failed deliveries

## API Testing

### Swagger Documentation
Available at: `http://localhost:3001/api-docs`

### Postman Collection
Download: `http://localhost:3001/api/postman-collection`

### Example cURL Commands

**Login:**
```bash
curl -X POST http://localhost:3001/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"email":"john@example.com","password":"password123"}'
```

**Get Requests:**
```bash
curl -X GET http://localhost:3001/api/requests \
  -H "Authorization: Bearer <token>"
```

**Create Request:**
```bash
curl -X POST http://localhost:3001/api/requests \
  -H "Authorization: Bearer <token>" \
  -H "Content-Type: application/json" \
  -d '{
    "title":"Broken streetlight",
    "description":"Light not working",
    "category":"PUBLIC_SAFETY",
    "location":{"address":"123 Main St"}
  }'
```

## SDK Examples

### JavaScript/TypeScript
```typescript
import { CityServicesAPI } from '@cityservices/sdk';

const api = new CityServicesAPI({
  baseURL: 'http://localhost:3001/api',
  token: 'your-jwt-token'
});

// Get requests
const requests = await api.requests.list({
  status: 'SUBMITTED',
  limit: 10
});

// Create request
const newRequest = await api.requests.create({
  title: 'Pothole repair needed',
  description: 'Large pothole on main street',
  category: 'ROADS',
  location: { address: '123 Main St' }
});
```

### Python
```python
from cityservices import CityServicesClient

client = CityServicesClient(
    base_url="http://localhost:3001/api",
    token="your-jwt-token"
)

# Get requests
requests = client.requests.list(
    status="SUBMITTED",
    limit=10
)

# Create request
new_request = client.requests.create(
    title="Pothole repair needed",
    description="Large pothole on main street",
    category="ROADS",
    location={"address": "123 Main St"}
)
```

## Changelog

### Version 1.0.0 (Current)
- Initial API release
- Authentication system
- Request management
- User management
- Basic reporting
- File uploads
- WebSocket support

### Planned Features (v2.0)
- GraphQL endpoint
- Batch operations
- Advanced analytics
- AI-powered categorization
- Mobile push notifications
- Third-party integrations

---
*API Version: 1.0.0*
*Last Updated: 2025*
*For Workshop and Development Use*