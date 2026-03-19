# 🚀 City Services Portal API Documentation

## 📋 Overview

The City Services Portal API provides comprehensive endpoints for managing municipal service requests, user authentication, file uploads, and administrative operations. This API is designed for testing and demonstration purposes with comprehensive validation and security features.

**Base URL**: `http://localhost:3001`  
**API Version**: `v1`  
**Documentation**: `http://localhost:3001/api-docs`

## 🔐 Authentication

All protected endpoints require a JWT token in the Authorization header:
```
Authorization: Bearer <your-jwt-token>
```

### Authentication Endpoints

#### POST `/api/v1/auth/register`
Register a new user account (defaults to CITIZEN role).

**Request Body:**
```json
{
  "name": "John Doe",
  "email": "john.doe@example.com",
  "password": "securepassword123"
}
```

**Response (201):**
```json
{
  "accessToken": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
  "user": {
    "id": "123e4567-e89b-12d3-a456-426614174000",
    "name": "John Doe",
    "email": "john.doe@example.com",
    "role": "CITIZEN"
  },
  "correlationId": "req_1234567890_abc123"
}
```

#### POST `/api/v1/auth/login`
Authenticate user and receive JWT token.

**Request Body:**
```json
{
  "email": "john.doe@example.com",
  "password": "securepassword123"
}
```

**Response (200):**
```json
{
  "accessToken": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
  "user": {
    "id": "123e4567-e89b-12d3-a456-426614174000",
    "name": "John Doe",
    "email": "john.doe@example.com",
    "role": "CITIZEN"
  },
  "correlationId": "req_1234567890_abc123"
}
```

#### GET `/api/v1/auth/me`
Get current user profile (requires authentication).

**Response (200):**
```json
{
  "user": {
    "id": "123e4567-e89b-12d3-a456-426614174000",
    "name": "John Doe",
    "email": "john.doe@example.com",
    "role": "CITIZEN",
    "firstName": "John",
    "lastName": "Doe",
    "phone": "+1234567890",
    "streetAddress": "123 Main St",
    "city": "Anytown",
    "postalCode": "12345",
    "country": "USA",
    "preferredLanguage": "EN",
    "emailNotifications": true,
    "smsNotifications": false
  },
  "correlationId": "req_1234567890_abc123"
}
```

## 📋 Service Requests

### Service Request Endpoints

#### GET `/api/v1/requests`
List service requests with filtering, sorting, and pagination.

**Query Parameters:**
- `status` - Filter by status (SUBMITTED, TRIAGED, IN_PROGRESS, etc.)
- `category` - Filter by category
- `priority` - Filter by priority (LOW, MEDIUM, HIGH, URGENT)
- `department` - Filter by department
- `assignedTo` - Filter by assigned user ID
- `text` - Search in title and description
- `page` - Page number (default: 1)
- `size` - Page size (default: 20, max: 100)
- `sort` - Sort field:direction (e.g., "createdAt:desc")

**Response (200):**
```json
{
  "data": [
    {
      "id": "789e1234-e89b-12d3-a456-426614174000",
      "code": "REQ-2024-123456",
      "title": "Pothole on Main Street",
      "description": "Large pothole causing vehicle damage",
      "category": "Roads & Infrastructure",
      "priority": "HIGH",
      "status": "SUBMITTED",
      "locationText": "Main Street near City Hall",
      "creator": {
        "id": "123e4567-e89b-12d3-a456-426614174000",
        "name": "John Doe",
        "email": "john.doe@example.com"
      },
      "upvotes": 5,
      "comments": 3,
      "createdAt": "2024-01-15T10:30:00Z",
      "updatedAt": "2024-01-15T10:30:00Z"
    }
  ],
  "pagination": {
    "page": 1,
    "pageSize": 20,
    "totalCount": 150,
    "totalPages": 8
  },
  "correlationId": "req_1234567890_abc123"
}
```

#### POST `/api/v1/requests`
Create a new service request.

**Headers:**
- `Idempotency-Key` - Optional unique key to prevent duplicates

**Request Body:**
```json
{
  "title": "Pothole on Main Street",
  "description": "Large pothole causing vehicle damage near intersection",
  "category": "Roads & Infrastructure",
  "priority": "HIGH",
  "locationText": "Main Street near City Hall intersection",
  "streetAddress": "123 Main Street",
  "city": "Anytown",
  "postalCode": "12345",
  "contactMethod": "EMAIL",
  "email": "john.doe@example.com",
  "phone": "+1234567890",
  "severity": 7,
  "isEmergency": false,
  "isRecurring": false,
  "estimatedValue": 1500.50
}
```

**Response (201):**
```json
{
  "data": {
    "id": "789e1234-e89b-12d3-a456-426614174000",
    "code": "REQ-2024-123456",
    "title": "Pothole on Main Street",
    "description": "Large pothole causing vehicle damage near intersection",
    "category": "Roads & Infrastructure",
    "priority": "HIGH",
    "status": "SUBMITTED",
    "locationText": "Main Street near City Hall intersection",
    "creator": {
      "id": "123e4567-e89b-12d3-a456-426614174000",
      "name": "John Doe",
      "email": "john.doe@example.com"
    },
    "createdAt": "2024-01-15T10:30:00Z",
    "updatedAt": "2024-01-15T10:30:00Z"
  },
  "correlationId": "req_1234567890_abc123"
}
```

#### GET `/api/v1/requests/:id`
Get a specific service request by ID.

**Response (200):**
```json
{
  "data": {
    "id": "789e1234-e89b-12d3-a456-426614174000",
    "code": "REQ-2024-123456",
    "title": "Pothole on Main Street",
    "description": "Large pothole causing vehicle damage",
    "category": "Roads & Infrastructure",
    "priority": "HIGH",
    "status": "SUBMITTED",
    "locationText": "Main Street near City Hall",
    "streetAddress": "123 Main Street",
    "city": "Anytown",
    "postalCode": "12345",
    "contactMethod": "EMAIL",
    "email": "john.doe@example.com",
    "phone": "+1234567890",
    "severity": 7,
    "isEmergency": false,
    "isRecurring": false,
    "hasPermits": false,
    "estimatedValue": 1500.50,
    "satisfactionRating": null,
    "upvotes": 5,
    "hasUserUpvoted": false,
    "comments": [
      {
        "id": "comment-123e4567-e89b-12d3",
        "body": "This issue has been resolved successfully.",
        "visibility": "PUBLIC",
        "author": {
          "id": "123e4567-e89b-12d3-a456-426614174000",
          "name": "John Doe"
        },
        "createdAt": "2024-01-15T11:00:00Z"
      }
    ],
    "attachments": [
      {
        "id": "attach-123e4567-e89b-12d3",
        "filename": "pothole-photo.jpg",
        "mime": "image/jpeg",
        "size": 1024000,
        "uploadedBy": {
          "id": "123e4567-e89b-12d3-a456-426614174000",
          "name": "John Doe"
        },
        "createdAt": "2024-01-15T10:35:00Z"
      }
    ],
    "creator": {
      "id": "123e4567-e89b-12d3-a456-426614174000",
      "name": "John Doe",
      "email": "john.doe@example.com"
    },
    "assignee": null,
    "department": null,
    "version": 1,
    "createdAt": "2024-01-15T10:30:00Z",
    "updatedAt": "2024-01-15T10:30:00Z"
  },
  "correlationId": "req_1234567890_abc123"
}
```

#### PATCH `/api/v1/requests/:id`
Update a service request (requires optimistic locking).

**Headers:**
- `If-Match` - Current version number for optimistic locking

**Request Body:**
```json
{
  "title": "Updated Pothole on Main Street",
  "description": "Updated description of the pothole issue",
  "priority": "URGENT"
}
```

**Response (200):**
```json
{
  "data": {
    "id": "789e1234-e89b-12d3-a456-426614174000",
    "code": "REQ-2024-123456",
    "title": "Updated Pothole on Main Street",
    "description": "Updated description of the pothole issue",
    "priority": "URGENT",
    "version": 2,
    "updatedAt": "2024-01-15T12:00:00Z"
  },
  "correlationId": "req_1234567890_abc123"
}
```

#### POST `/api/v1/requests/:id/status`
Change the status of a service request.

**Request Body:**
```json
{
  "action": "triage",
  "reason": "Issue has been reviewed and assigned to appropriate department"
}
```

**Available Actions:**
- `triage` - SUBMITTED → TRIAGED
- `start` - TRIAGED → IN_PROGRESS
- `wait_for_citizen` - IN_PROGRESS → WAITING_ON_CITIZEN
- `resolve` - IN_PROGRESS → RESOLVED
- `close` - RESOLVED → CLOSED
- `reject` - Any status → REJECTED
- `reopen` - CLOSED → IN_PROGRESS

**Response (200):**
```json
{
  "data": {
    "id": "789e1234-e89b-12d3-a456-426614174000",
    "status": "TRIAGED",
    "version": 2,
    "updatedAt": "2024-01-15T12:00:00Z"
  },
  "correlationId": "req_1234567890_abc123"
}
```

#### POST `/api/v1/requests/:id/comments`
Add a comment to a service request.

**Request Body:**
```json
{
  "content": "This issue has been resolved successfully.",
  "visibility": "PUBLIC"
}
```

**Response (201):**
```json
{
  "data": {
    "id": "comment-123e4567-e89b-12d3",
    "requestId": "789e1234-e89b-12d3-a456-426614174000",
    "body": "This issue has been resolved successfully.",
    "visibility": "PUBLIC",
    "author": {
      "id": "123e4567-e89b-12d3-a456-426614174000",
      "name": "John Doe"
    },
    "createdAt": "2024-01-15T12:00:00Z"
  },
  "correlationId": "req_1234567890_abc123"
}
```

#### POST `/api/v1/requests/:id/upvote`
Upvote a service request.

**Response (200):**
```json
{
  "data": {
    "id": "789e1234-e89b-12d3-a456-426614174000",
    "upvotes": 6,
    "hasUserUpvoted": true
  },
  "correlationId": "req_1234567890_abc123"
}
```

#### DELETE `/api/v1/requests/:id/upvote`
Remove upvote from a service request.

**Response (200):**
```json
{
  "data": {
    "id": "789e1234-e89b-12d3-a456-426614174000",
    "upvotes": 5,
    "hasUserUpvoted": false
  },
  "correlationId": "req_1234567890_abc123"
}
```

## 👥 User Management

### User Endpoints (Admin Only)

#### GET `/api/v1/admin/users`
List all users with filtering and pagination.

**Query Parameters:**
- `role` - Filter by role
- `department` - Filter by department slug
- `isActive` - Filter by active status
- `isTestUser` - Filter by test user flag
- `search` - Search in name, email, firstName, lastName
- `page` - Page number (default: 1)
- `size` - Page size (default: 20, max: 100)
- `sort` - Sort field:direction (e.g., "createdAt:desc")

**Response (200):**
```json
{
  "data": [
    {
      "id": "123e4567-e89b-12d3-a456-426614174000",
      "name": "John Doe",
      "email": "john.doe@example.com",
      "role": "CITIZEN",
      "firstName": "John",
      "lastName": "Doe",
      "phone": "+1234567890",
      "isActive": true,
      "isTestUser": false,
      "department": {
        "id": "456e7890-e89b-12d3-a456-426614174000",
        "name": "Public Works",
        "slug": "public-works"
      },
      "createdAt": "2024-01-01T00:00:00Z",
      "updatedAt": "2024-01-15T10:30:00Z"
    }
  ],
  "pagination": {
    "page": 1,
    "pageSize": 20,
    "totalCount": 50,
    "totalPages": 3
  },
  "correlationId": "req_1234567890_abc123"
}
```

#### POST `/api/v1/admin/users`
Create a new user (Admin only).

**Request Body:**
```json
{
  "name": "Jane Smith",
  "email": "jane.smith@city.gov",
  "password": "securepassword123",
  "role": "CLERK",
  "departmentId": "456e7890-e89b-12d3-a456-426614174000",
  "firstName": "Jane",
  "lastName": "Smith",
  "phone": "+1234567890",
  "isTestUser": true
}
```

**Response (201):**
```json
{
  "data": {
    "id": "789e1234-e89b-12d3-a456-426614174000",
    "name": "Jane Smith",
    "email": "jane.smith@city.gov",
    "role": "CLERK",
    "firstName": "Jane",
    "lastName": "Smith",
    "phone": "+1234567890",
    "isActive": true,
    "isTestUser": true,
    "createdAt": "2024-01-15T12:00:00Z",
    "updatedAt": "2024-01-15T12:00:00Z"
  },
  "correlationId": "req_1234567890_abc123"
}
```

#### GET `/api/v1/admin/users/:id`
Get a specific user by ID.

**Response (200):**
```json
{
  "data": {
    "id": "123e4567-e89b-12d3-a456-426614174000",
    "name": "John Doe",
    "email": "john.doe@example.com",
    "role": "CITIZEN",
    "firstName": "John",
    "lastName": "Doe",
    "phone": "+1234567890",
    "alternatePhone": "+0987654321",
    "streetAddress": "123 Main St",
    "city": "Anytown",
    "state": "CA",
    "postalCode": "12345",
    "country": "USA",
    "preferredLanguage": "EN",
    "communicationMethod": "EMAIL",
    "emailNotifications": true,
    "smsNotifications": false,
    "marketingEmails": false,
    "serviceUpdates": true,
    "twoFactorEnabled": false,
    "isActive": true,
    "isTestUser": false,
    "department": {
      "id": "456e7890-e89b-12d3-a456-426614174000",
      "name": "Public Works",
      "slug": "public-works"
    },
    "createdAt": "2024-01-01T00:00:00Z",
    "updatedAt": "2024-01-15T10:30:00Z"
  },
  "correlationId": "req_1234567890_abc123"
}
```

#### PATCH `/api/v1/admin/users/:id`
Update a user.

**Request Body:**
```json
{
  "name": "John Smith",
  "role": "SUPERVISOR",
  "departmentId": "456e7890-e89b-12d3-a456-426614174000",
  "isActive": true
}
```

**Response (200):**
```json
{
  "data": {
    "id": "123e4567-e89b-12d3-a456-426614174000",
    "name": "John Smith",
    "role": "SUPERVISOR",
    "isActive": true,
    "updatedAt": "2024-01-15T12:00:00Z"
  },
  "correlationId": "req_1234567890_abc123"
}
```

#### DELETE `/api/v1/admin/users/:id`
Delete a user (soft delete).

**Response (200):**
```json
{
  "data": {
    "message": "User deleted successfully",
    "userId": "123e4567-e89b-12d3-a456-426614174000"
  },
  "correlationId": "req_1234567890_abc123"
}
```

#### POST `/api/v1/admin/users/bulk`
Create multiple users in bulk.

**Request Body:**
```json
{
  "users": [
    {
      "name": "User 1",
      "email": "user1@test.com",
      "role": "CITIZEN",
      "isTestUser": true
    },
    {
      "name": "User 2",
      "email": "user2@test.com",
      "role": "CLERK",
      "isTestUser": true
    }
  ],
  "defaultPassword": "password123"
}
```

**Response (201):**
```json
{
  "data": {
    "created": 2,
    "users": [
      {
        "id": "123e4567-e89b-12d3-a456-426614174000",
        "name": "User 1",
        "email": "user1@test.com",
        "role": "CITIZEN"
      },
      {
        "id": "789e1234-e89b-12d3-a456-426614174000",
        "name": "User 2",
        "email": "user2@test.com",
        "role": "CLERK"
      }
    ]
  },
  "correlationId": "req_1234567890_abc123"
}
```

#### DELETE `/api/v1/admin/users/bulk`
Delete multiple test users.

**Request Body:**
```json
{
  "userIds": [
    "123e4567-e89b-12d3-a456-426614174000",
    "789e1234-e89b-12d3-a456-426614174000"
  ]
}
```

**Response (200):**
```json
{
  "data": {
    "deleted": 2,
    "message": "Users deleted successfully"
  },
  "correlationId": "req_1234567890_abc123"
}
```

## 🔧 Admin Operations

### Admin Endpoints

#### GET `/api/v1/admin/flags`
Get all feature flags.

**Response (200):**
```json
{
  "data": {
    "API_Random500": false,
    "UI_WrongDefaultSort": false,
    "API_SlowRequests": false,
    "API_UploadIntermittentFail": false
  },
  "correlationId": "req_1234567890_abc123"
}
```

#### PATCH `/api/v1/admin/flags/:key`
Update a specific feature flag.

**Request Body:**
```json
{
  "value": true
}
```

**Response (200):**
```json
{
  "data": {
    "key": "API_Random500",
    "value": true,
    "allFlags": {
      "API_Random500": true,
      "UI_WrongDefaultSort": false,
      "API_SlowRequests": false,
      "API_UploadIntermittentFail": false
    }
  },
  "correlationId": "req_1234567890_abc123"
}
```

#### POST `/api/v1/admin/seed`
Re-seed the database with test data.

**Response (200):**
```json
{
  "data": {
    "message": "Database seeding initiated",
    "status": "success"
  },
  "correlationId": "req_1234567890_abc123"
}
```

#### POST `/api/v1/admin/reset`
Reset the database.

**Response (200):**
```json
{
  "data": {
    "message": "Database reset initiated",
    "status": "success"
  },
  "correlationId": "req_1234567890_abc123"
}
```

## 📎 File Attachments

### Attachment Endpoints

#### POST `/api/v1/requests/:id/attachments`
Upload a file attachment to a service request.

**Content-Type:** `multipart/form-data`

**Form Data:**
- `file` - File to upload (JPG, PNG, PDF, TXT, max 5MB)

**Response (201):**
```json
{
  "data": {
    "id": "attach-123e4567-e89b-12d3",
    "requestId": "789e1234-e89b-12d3-a456-426614174000",
    "filename": "pothole-photo.jpg",
    "mime": "image/jpeg",
    "size": 1024000,
    "uploadedBy": {
      "id": "123e4567-e89b-12d3-a456-426614174000",
      "name": "John Doe"
    },
    "createdAt": "2024-01-15T12:00:00Z"
  },
  "correlationId": "req_1234567890_abc123"
}
```

#### GET `/api/v1/requests/:id/attachments`
Get all attachments for a service request.

**Response (200):**
```json
{
  "data": [
    {
      "id": "attach-123e4567-e89b-12d3",
      "requestId": "789e1234-e89b-12d3-a456-426614174000",
      "filename": "pothole-photo.jpg",
      "mime": "image/jpeg",
      "size": 1024000,
      "uploadedBy": {
        "id": "123e4567-e89b-12d3-a456-426614174000",
        "name": "John Doe"
      },
      "createdAt": "2024-01-15T12:00:00Z"
    }
  ],
  "correlationId": "req_1234567890_abc123"
}
```

#### GET `/api/v1/attachments/:id`
Download a specific attachment.

**Response (200):**
Binary file data with appropriate Content-Type header.

#### DELETE `/api/v1/attachments/:id`
Delete an attachment.

**Response (200):**
```json
{
  "data": {
    "message": "Attachment deleted successfully",
    "attachmentId": "attach-123e4567-e89b-12d3"
  },
  "correlationId": "req_1234567890_abc123"
}
```

## 🏆 Rankings

### Ranking Endpoints

#### GET `/api/v1/rankings/users`
Get user rankings and statistics.

**Query Parameters:**
- `period` - Time period (week, month, year, all)
- `limit` - Number of results (default: 10, max: 100)

**Response (200):**
```json
{
  "data": [
    {
      "id": "123e4567-e89b-12d3-a456-426614174000",
      "name": "John Doe",
      "email": "john.doe@example.com",
      "rank": 1,
      "badge": "gold",
      "approvedRequestsCount": 15,
      "totalRequestsCount": 18,
      "approvalRate": 83.3,
      "averageRating": 4.2,
      "joinedDate": "2024-01-01T00:00:00Z",
      "lastRequestDate": "2024-01-15T10:30:00Z"
    }
  ],
  "correlationId": "req_1234567890_abc123"
}
```

#### GET `/api/v1/rankings/departments`
Get department performance rankings.

**Response (200):**
```json
{
  "data": [
    {
      "id": "456e7890-e89b-12d3-a456-426614174000",
      "name": "Public Works",
      "slug": "public-works",
      "rank": 1,
      "totalRequests": 150,
      "resolvedRequests": 120,
      "averageResolutionTime": 3.5,
      "averageRating": 4.1
    }
  ],
  "correlationId": "req_1234567890_abc123"
}
```

## 🧪 Test Data Management

### Test Data Endpoints

#### GET `/api/v1/admin/test-data/status`
Get test data status and statistics.

**Response (200):**
```json
{
  "data": {
    "users": {
      "total": 50,
      "testUsers": 25,
      "activeUsers": 45
    },
    "requests": {
      "total": 150,
      "testRequests": 75,
      "activeRequests": 100
    },
    "attachments": {
      "total": 300,
      "testAttachments": 150
    },
    "lastCleanup": "2024-01-15T00:00:00Z"
  },
  "correlationId": "req_1234567890_abc123"
}
```

#### POST `/api/v1/admin/test-data/cleanup`
Clean up test data.

**Request Body:**
```json
{
  "cleanupUsers": true,
  "cleanupRequests": true,
  "cleanupAttachments": true,
  "keepLastDays": 7
}
```

**Response (200):**
```json
{
  "data": {
    "cleanedUsers": 25,
    "cleanedRequests": 75,
    "cleanedAttachments": 150,
    "message": "Test data cleanup completed"
  },
  "correlationId": "req_1234567890_abc123"
}
```

#### POST `/api/v1/admin/test-data/generate`
Generate test data.

**Request Body:**
```json
{
  "users": 10,
  "requests": 50,
  "attachments": 100,
  "categories": ["Roads", "Lighting", "Waste"]
}
```

**Response (200):**
```json
{
  "data": {
    "generatedUsers": 10,
    "generatedRequests": 50,
    "generatedAttachments": 100,
    "message": "Test data generation completed"
  },
  "correlationId": "req_1234567890_abc123"
}
```

## 🔍 Utility Endpoints

### Utility Endpoints

#### GET `/health`
Health check endpoint.

**Response (200):**
```json
{
  "status": "healthy",
  "timestamp": "2024-01-15T12:00:00Z",
  "service": "city-services-portal-api"
}
```

## 📊 Error Responses

All endpoints return consistent error responses:

### Validation Error (400)
```json
{
  "error": {
    "code": "VALIDATION_ERROR",
    "message": "Invalid input data",
    "details": [
      {
        "field": "email",
        "message": "Invalid email format"
      }
    ],
    "correlationId": "req_1234567890_abc123"
  }
}
```

### Unauthorized (401)
```json
{
  "error": {
    "code": "UNAUTHORIZED",
    "message": "Missing or invalid authentication token",
    "correlationId": "req_1234567890_abc123"
  }
}
```

### Forbidden (403)
```json
{
  "error": {
    "code": "FORBIDDEN",
    "message": "Insufficient permissions for this operation",
    "correlationId": "req_1234567890_abc123"
  }
}
```

### Not Found (404)
```json
{
  "error": {
    "code": "NOT_FOUND",
    "message": "Resource not found",
    "correlationId": "req_1234567890_abc123"
  }
}
```

### Conflict (409)
```json
{
  "error": {
    "code": "VERSION_CONFLICT",
    "message": "Resource has been modified by another request",
    "correlationId": "req_1234567890_abc123"
  }
}
```

### Internal Server Error (500)
```json
{
  "error": {
    "code": "INTERNAL_SERVER_ERROR",
    "message": "An unexpected error occurred",
    "correlationId": "req_1234567890_abc123"
  }
}
```

## 🔐 Security Features

### Authentication & Authorization
- JWT-based authentication with 1-hour expiration
- Role-based access control (CITIZEN, CLERK, FIELD_AGENT, SUPERVISOR, ADMIN)
- Account lockout after 5 failed login attempts (30-minute lockout)

### Input Validation
- Comprehensive Zod schema validation for all inputs
- XSS prevention with input sanitization
- File upload security (type, size, content validation)

### Rate Limiting
- 10,000 requests per 15 minutes per IP
- Form submission rate limiting (5 attempts per minute)

### Audit Trail
- All operations logged with correlation IDs
- Event logging for status changes and important actions
- User activity tracking

## 🧪 Testing Features

### Feature Flags
- `API_Random500` - 5% random server errors
- `UI_WrongDefaultSort` - Wrong default sorting behavior
- `API_SlowRequests` - 10% slow API responses (2.5s delay)
- `API_UploadIntermittentFail` - Random upload failures

### Test Data Management
- Bulk user creation and deletion
- Test data cleanup and generation
- Test user and request marking for easy identification

### Stable Test Selectors
All endpoints support consistent error responses and correlation IDs for reliable testing.

---

**🎯 This API is designed for comprehensive testing and demonstration purposes, with full CRUD operations, user management, and test data preparation capabilities.**
