# City Services Portal - Complete Technical Documentation

## System Overview

The City Services Portal is a comprehensive municipal service management system designed as a production-ready testing playground for AI-powered QA tools. The application simulates real-world municipal operations while providing extensive hooks for testing automation, manual testing scenarios, and AI-driven quality assurance workflows.

### Architecture
- **Pattern**: Full-stack monorepo with microservice-ready separation
- **Backend**: Node.js + Express + TypeScript + Prisma ORM + SQLite
- **Frontend**: React 18 + TypeScript + Vite + Material-UI v5
- **Authentication**: JWT-based with comprehensive RBAC
- **Database**: Prisma ORM with SQLite (production-ready for PostgreSQL/MySQL)

## Technology Stack Deep Dive

### Backend Technologies
```yaml
Runtime & Framework:
  - Node.js: JavaScript runtime for server-side execution
  - Express.js: Web application framework with middleware support
  - TypeScript: Static type checking for enhanced developer experience

Database & ORM:
  - Prisma ORM v5.7.1: Type-safe database client with migrations
  - SQLite: Development database (easily switchable to PostgreSQL/MySQL)
  - Database migrations: Version-controlled schema changes

Security & Authentication:
  - JWT (jsonwebtoken): Stateless authentication tokens
  - bcryptjs: Password hashing with configurable salt rounds
  - Helmet: Security headers and protection middleware
  - CORS: Cross-origin resource sharing with environment-specific configs

Validation & Processing:
  - Zod: Runtime type validation and schema enforcement
  - Multer: Multipart form data handling for file uploads
  - Rate Limiting: Express-rate-limit with configurable windows
  - Correlation IDs: Request tracing across the application

API Documentation:
  - Swagger/OpenAPI: Automated API documentation generation
  - Interactive API testing interface at /api-docs
```

### Frontend Technologies
```yaml
Core Framework:
  - React 18: Latest React with concurrent features
  - TypeScript: Full type safety across components
  - Vite: Fast build tool with HMR and optimized bundling

UI Components & Styling:
  - Material-UI v5.15.3: Comprehensive component library
  - MUI X DataGrid: Advanced data table with server-side operations
  - React Hook Form: Performant forms with minimal re-renders
  - React Leaflet: Interactive maps with OpenStreetMap integration

Routing & Navigation:
  - React Router DOM v6: Declarative routing with protected routes
  - Role-based route protection
  - Deep linking support for all application states

HTTP & State Management:
  - Axios: HTTP client with interceptors and error handling
  - React Context: Global state for authentication, features, language
  - Custom hooks: Reusable logic for API calls and form management

Development Tools:
  - ESLint: Code quality and consistency enforcement
  - Vite plugins: React, TypeScript, and development optimizations
```

## Database Architecture

### Schema Design Principles
- **Domain-Driven Design**: Entities reflect real municipal service operations
- **Audit Trail**: Complete event logging for all state changes
- **Optimistic Locking**: Version control for concurrent updates
- **Soft Deletes**: Data preservation for audit requirements
- **Flexible Relationships**: Support for complex organizational structures

### Core Entities

#### User Management System
```typescript
model User {
  // Primary Identity
  id: String @id @default(uuid())
  email: String @unique
  passwordHash: String
  role: Role @default(CITIZEN)
  
  // Personal Information
  name: String
  firstName: String?
  lastName: String?
  phone: String?
  alternatePhone: String?
  
  // Address Information
  streetAddress: String?
  city: String?
  state: String?
  postalCode: String?
  country: String? @default("Bulgaria")
  
  // Communication Preferences
  preferredLanguage: String @default("EN")
  communicationMethod: CommunicationMethod @default(EMAIL)
  emailNotifications: Boolean @default(true)
  smsNotifications: Boolean @default(false)
  marketingEmails: Boolean @default(false)
  serviceUpdates: Boolean @default(true)
  
  // Security Features
  twoFactorEnabled: Boolean @default(false)
  securityQuestion: String?
  securityAnswer: String?
  
  // Organizational
  departmentId: String?
  department: Department? @relation(fields: [departmentId], references: [id])
  
  // Audit Fields
  createdAt: DateTime @default(now())
  updatedAt: DateTime @updatedAt
  
  // Relations
  createdRequests: ServiceRequest[] @relation("RequestCreator")
  assignedRequests: ServiceRequest[] @relation("RequestAssignee")
  comments: Comment[]
  upvotes: Upvote[]
  assignments: Assignment[]
}

enum Role {
  CITIZEN
  CLERK
  FIELD_AGENT
  SUPERVISOR
  ADMIN
}
```

#### Service Request Lifecycle
```typescript
model ServiceRequest {
  // Core Identity
  id: String @id @default(uuid())
  code: String @unique // Format: REQ-YYYY-NNNN
  
  // Request Information
  title: String // 5-120 characters
  description: String // Minimum 30 characters
  category: String // 14 predefined categories
  priority: Priority @default(MEDIUM)
  status: RequestStatus @default(SUBMITTED)
  
  // Temporal Data
  dateOfRequest: DateTime
  preferredDate: DateTime?
  preferredTime: String?
  
  // Location Information
  streetAddress: String?
  city: String?
  postalCode: String?
  locationText: String // Required - human readable location
  landmark: String?
  accessInstructions: String?
  lat: Float? // Geographic coordinates
  lng: Float?
  
  // Contact Details
  contactMethod: CommunicationMethod @default(EMAIL)
  email: String?
  phone: String?
  alternatePhone: String?
  bestTimeToContact: String?
  
  // Mailing Address (Separate from service location)
  mailingStreetAddress: String?
  mailingCity: String?
  mailingPostalCode: String?
  
  // Issue Classification
  issueType: String? // MAINTENANCE, REPAIR, INSTALLATION, etc.
  severity: Int? // 1-10 scale
  isRecurring: Boolean @default(false)
  isEmergency: Boolean @default(false)
  hasPermits: Boolean @default(false)
  
  // Service Impact
  affectedServices: String? // JSON array stored as string
  estimatedValue: Float?
  additionalContacts: String? // JSON array stored as string
  
  // User Experience
  satisfactionRating: Int? // 1-5 scale
  formComments: String?
  
  // Legal & Preferences
  agreesToTerms: Boolean @default(true)
  wantsUpdates: Boolean @default(true)
  
  // System Management
  version: Int @default(1) // Optimistic locking
  slaDueAt: DateTime?
  closedAt: DateTime?
  reopenUntil: DateTime?
  
  // Relations
  createdBy: String
  creator: User @relation("RequestCreator", fields: [createdBy], references: [id])
  assignedTo: String?
  assignee: User? @relation("RequestAssignee", fields: [assignedTo], references: [id])
  departmentId: String?
  department: Department? @relation(fields: [departmentId], references: [id])
  
  // Child Relations
  attachments: Attachment[]
  comments: Comment[]
  upvotes: Upvote[]
  assignments: Assignment[]
  eventLogs: EventLog[]
  
  // Audit
  createdAt: DateTime @default(now())
  updatedAt: DateTime @updatedAt
}

enum RequestStatus {
  DRAFT
  SUBMITTED
  TRIAGED
  IN_PROGRESS
  WAITING_ON_CITIZEN
  RESOLVED
  CLOSED
  REJECTED
  REOPENED
}

enum Priority {
  LOW
  MEDIUM
  HIGH
  URGENT
}
```

#### Supporting Entities
```typescript
model Department {
  id: String @id @default(uuid())
  name: String @unique
  slug: String @unique
  description: String?
  isActive: Boolean @default(true)
  
  users: User[]
  serviceRequests: ServiceRequest[]
  createdAt: DateTime @default(now())
  updatedAt: DateTime @updatedAt
}

model Comment {
  id: String @id @default(uuid())
  content: String
  visibility: CommentVisibility @default(PUBLIC)
  
  requestId: String
  request: ServiceRequest @relation(fields: [requestId], references: [id], onDelete: Cascade)
  authorId: String
  author: User @relation(fields: [authorId], references: [id])
  
  createdAt: DateTime @default(now())
  updatedAt: DateTime @updatedAt
}

model Attachment {
  id: String @id @default(uuid())
  filename: String
  originalName: String
  mime: String
  size: Int
  data: Bytes // Binary file data
  
  requestId: String
  request: ServiceRequest @relation(fields: [requestId], references: [id], onDelete: Cascade)
  
  createdAt: DateTime @default(now())
}

model EventLog {
  id: String @id @default(uuid())
  eventType: String // STATUS_CHANGE, ASSIGNMENT, COMMENT, etc.
  payload: String // JSON serialized event data
  
  requestId: String
  request: ServiceRequest @relation(fields: [requestId], references: [id], onDelete: Cascade)
  performedBy: String
  performer: User @relation(fields: [performedBy], references: [id])
  
  createdAt: DateTime @default(now())
}

model FeatureFlag {
  id: String @id @default(uuid())
  key: String @unique
  enabled: Boolean @default(false)
  description: String?
  
  createdAt: DateTime @default(now())
  updatedAt: DateTime @updatedAt
}
```

## API Architecture & Endpoints

### RESTful Design Principles
The API follows REST conventions with consistent patterns across all endpoints:

- **Resource-based URLs**: `/api/v1/resource` format
- **HTTP method semantics**: GET (read), POST (create), PATCH (update), DELETE (remove)
- **Consistent status codes**: Semantic HTTP status codes with descriptive error messages
- **Standardized responses**: All responses follow `{ data: T, correlationId: string }` pattern
- **Pagination support**: Cursor-based pagination with configurable page sizes
- **Filtering & sorting**: Query parameter-based with multiple criteria support

### Authentication & Authorization

#### Authentication Endpoints
```typescript
POST   /api/v1/auth/login
Request: { email: string, password: string }
Response: { user: User, token: string }

POST   /api/v1/auth/register
Request: { email: string, password: string, name: string, role?: Role }
Response: { user: User, token: string }

POST   /api/v1/auth/token
Request: { email: string, password: string }
Response: { token: string } // Simplified for API testing

GET    /api/v1/auth/me
Headers: { Authorization: "Bearer <token>" }
Response: { user: User }

PATCH  /api/v1/auth/profile
Request: { name?: string, phone?: string, ... }
Response: { user: User }

POST   /api/v1/auth/change-password
Request: { currentPassword: string, newPassword: string }
Response: { success: boolean }
```

#### Role-Based Access Control Matrix
| Operation | CITIZEN | CLERK | SUPERVISOR | FIELD_AGENT | ADMIN |
|-----------|---------|-------|------------|-------------|-------|
| Create Request | ✅ | ✅ | ✅ | ❌ | ✅ |
| View Own Requests | ✅ | N/A | N/A | N/A | ✅ |
| View Department Requests | ❌ | ✅ | ✅ | ✅ | ✅ |
| View All Requests | Public Only | ❌ | Department | ❌ | ✅ |
| Change Status | ❌ | ✅ | ✅ | ✅ | ✅ |
| Assign Requests | ❌ | Limited | ✅ | ❌ | ✅ |
| Internal Comments | ❌ | ✅ | ✅ | ✅ | ✅ |
| Feature Flags | ❌ | ❌ | ❌ | ❌ | ✅ |

### Service Request Management

#### Core Operations
```typescript
GET    /api/v1/requests
Query Parameters:
  - page?: number (default: 1)
  - size?: number (5-50, default: 20)
  - sort?: string (format: "field:direction")
  - status?: RequestStatus
  - priority?: Priority
  - category?: string
  - assignedTo?: string
  - department?: string
  - text?: string (search across title, description, code)
  - showAll?: boolean (admin/supervisor only)

POST   /api/v1/requests
Headers: { "Idempotency-Key": string } // Optional
Request: CreateServiceRequestData
Response: { data: ServiceRequest }

GET    /api/v1/requests/:id
Response: { data: ServiceRequest } // Full object with relations

PATCH  /api/v1/requests/:id
Headers: { "If-Match": version } // Optimistic locking
Request: Partial<ServiceRequest>
Response: { data: ServiceRequest }

POST   /api/v1/requests/:id/status
Request: { action: StatusAction, reason?: string }
Response: { data: ServiceRequest }
```

#### Status Workflow Management
```typescript
// Valid status transitions enforced server-side
const STATUS_TRANSITIONS: Record<RequestStatus, RequestStatus[]> = {
  DRAFT: ['SUBMITTED'],
  SUBMITTED: ['TRIAGED', 'REJECTED'],
  TRIAGED: ['IN_PROGRESS', 'REJECTED'],
  IN_PROGRESS: ['WAITING_ON_CITIZEN', 'RESOLVED', 'REJECTED'],
  WAITING_ON_CITIZEN: ['IN_PROGRESS', 'RESOLVED', 'CLOSED'],
  RESOLVED: ['CLOSED', 'REOPENED'],
  CLOSED: ['REOPENED'],
  REJECTED: ['REOPENED'],
  REOPENED: ['TRIAGED']
};

// Status actions map to transitions
const STATUS_ACTIONS = {
  'submit': 'SUBMITTED',
  'triage': 'TRIAGED',
  'start': 'IN_PROGRESS',
  'resolve': 'RESOLVED',
  'close': 'CLOSED',
  'reject': 'REJECTED',
  'reopen': 'REOPENED',
  'wait_for_citizen': 'WAITING_ON_CITIZEN'
};
```

#### Extended Features
```typescript
POST   /api/v1/requests/:id/comments
Request: { content: string, visibility: CommentVisibility }
Response: { data: Comment }

GET    /api/v1/requests/:id/comments
Response: { data: Comment[] } // Filtered by user role

POST   /api/v1/requests/:id/upvote
Response: { data: { upvoteCount: number } }

DELETE /api/v1/requests/:id/upvote
Response: { data: { upvoteCount: number } }

POST   /api/v1/requests/:id/assign
Request: { assigneeId: string }
Response: { data: ServiceRequest }

POST   /api/v1/requests/:id/attachments
Content-Type: multipart/form-data
Request: { files: File[] } // Max 5 files, 5MB each
Response: { data: Attachment[] }

GET    /api/v1/attachments/:id/image
Headers: { Authorization: "Bearer <token>" }
Response: Binary image data with appropriate Content-Type
```

#### Bulk Operations
```typescript
POST   /api/v1/requests/bulk
Request: { requests: CreateServiceRequestData[] } // Max 100
Response: { data: { created: ServiceRequest[], errors: any[] } }

DELETE /api/v1/requests/bulk
Request: { ids: string[] } // Admin only
Response: { data: { deleted: number } }
```

### Administrative Features

#### Feature Flag Management
```typescript
GET    /api/v1/admin/flags
Response: { data: FeatureFlag[] }

PATCH  /api/v1/admin/flags/:key
Request: { enabled: boolean }
Response: { data: FeatureFlag }
```

#### System Operations
```typescript
GET    /api/v1/admin/stats
Response: {
  data: {
    totalRequests: number,
    requestsByStatus: Record<RequestStatus, number>,
    requestsByPriority: Record<Priority, number>,
    requestsByDepartment: Record<string, number>,
    averageResolutionTime: number,
    slaCompliance: number
  }
}

POST   /api/v1/admin/seed
Request: { resetData?: boolean }
Response: { data: { message: string } }

DELETE /api/v1/admin/test-data
Response: { data: { deletedCount: number } }

GET    /api/v1/admin/test-data/validate
Response: { data: { isValid: boolean, issues: string[] } }
```

## Frontend Architecture

### Application Structure

#### Component Hierarchy
```
src/
├── components/
│   ├── common/                    # Reusable UI components
│   │   ├── DataTable.tsx          # Server-side data grid
│   │   ├── ImageUpload.tsx        # Drag-and-drop file upload
│   │   ├── ProtectedRoute.tsx     # Role-based route protection
│   │   └── StepperWizard.tsx      # Multi-step form navigation
│   ├── forms/                     # Form-specific components
│   │   ├── LocationStep.tsx       # Geographic input with maps
│   │   ├── BasicInfoStep.tsx      # Core request information
│   │   └── ReviewStep.tsx         # Final review before submission
│   └── request-detail/            # Request viewing components
│       ├── RequestHeader.tsx      # Status, priority, actions
│       ├── RequestComments.tsx    # Comment thread
│       └── AuthenticatedImage.tsx # Secure image display
├── pages/                         # Route-level page components
│   ├── citizen/                   # Citizen-specific views
│   │   ├── NewRequestPage.tsx     # Request creation wizard
│   │   ├── CitizenRequestsPage.tsx # Personal request management
│   │   └── PublicBoardPage.tsx    # Community request visibility
│   ├── clerk/                     # Staff processing views
│   │   ├── ClerkInboxPage.tsx     # Split-pane request processing
│   │   └── ClerkTasksPage.tsx     # Personal task management
│   ├── admin/                     # Administrative interfaces
│   │   ├── FeatureFlagsPage.tsx   # Feature toggle management
│   │   └── SystemStatsPage.tsx    # Analytics dashboard
│   └── shared/                    # Cross-role components
│       ├── RequestDetailPage.tsx  # Full request view
│       └── LoginPage.tsx          # Authentication
├── contexts/                      # Global state management
│   ├── AuthProvider.tsx           # JWT token management
│   ├── LanguageProvider.tsx       # i18n support (EN/BG)
│   └── FeatureFlagsProvider.tsx   # Runtime feature toggles
├── hooks/                         # Custom React hooks
│   ├── useServiceRequests.ts      # API data fetching
│   ├── useAuth.ts                 # Authentication state
│   └── useFeatureFlags.ts         # Feature flag access
└── lib/                          # Utility libraries
    ├── api.ts                    # Axios HTTP client
    ├── validation.ts             # Zod schemas
    └── constants.ts              # Application constants
```

### Key UI Components

#### Multi-Step Request Creation Wizard
```typescript
interface StepperWizardProps {
  steps: {
    label: string;
    component: React.ComponentType<StepProps>;
    validation?: ZodSchema;
  }[];
  onSubmit: (data: ServiceRequestData) => Promise<void>;
  onStepChange?: (step: number) => void;
  initialData?: Partial<ServiceRequestData>;
}

// Implementation includes:
// - Form data persistence across steps
// - Step validation before navigation
// - Progress indicator with click navigation
// - Mobile-responsive stepper
// - Error handling and recovery
```

#### Advanced Data Grid
```typescript
interface DataTableProps<T> {
  columns: GridColDef[];
  rows: T[];
  loading: boolean;
  paginationMode: 'server' | 'client';
  sortingMode: 'server' | 'client';
  filterMode: 'server' | 'client';
  totalRows?: number;
  pageSize?: number;
  onPaginationModelChange?: (model: GridPaginationModel) => void;
  onSortModelChange?: (model: GridSortModel) => void;
  onFilterModelChange?: (model: GridFilterModel) => void;
}

// Features:
// - Server-side pagination, sorting, filtering
// - Custom column definitions with formatters
// - Row selection with bulk operations
// - Export functionality (CSV, Excel)
// - Mobile-responsive with column hiding
```

#### Geographic Location Input
```typescript
interface LocationStepProps {
  value: LocationData;
  onChange: (location: LocationData) => void;
  onValidate: () => Promise<boolean>;
}

// Capabilities:
// - Interactive map with draggable markers
// - Address geocoding with multiple providers
// - Current location detection (GPS)
// - Manual coordinate input
// - Address validation and formatting
// - Offline map caching preparation
```

#### File Upload System
```typescript
interface ImageUploadProps {
  maxImages: number;
  maxSizePerImage: number;
  acceptedTypes: string[];
  onImagesChange: (images: File[]) => void;
  uploadProgress?: Record<string, number>;
  existingImages?: AttachmentData[];
}

// Features:
// - Drag-and-drop interface
// - Progress tracking per file
// - Image preview with thumbnails
// - Client-side validation (type, size)
// - Batch upload optimization
// - Error handling and retry
```

### Form Validation Framework

#### Comprehensive Validation Schemas
```typescript
// Shared validation between frontend and backend
const serviceRequestSchema = z.object({
  // Basic Information (Step 1)
  title: z.string()
    .min(5, "Title must be at least 5 characters")
    .max(120, "Title cannot exceed 120 characters")
    .refine(title => !profanityCheck(title), "Title contains inappropriate language"),
  
  description: z.string()
    .min(30, "Description must be at least 30 characters")
    .refine(desc => !profanityCheck(desc), "Description contains inappropriate language"),
  
  category: z.string().min(1, "Category is required"),
  priority: z.enum(['LOW', 'MEDIUM', 'HIGH', 'URGENT']).default('MEDIUM'),
  
  // Location Information (Step 2)
  locationText: z.string().min(5, "Location description is required"),
  streetAddress: z.string().optional(),
  city: z.string().optional(),
  postalCode: z.string()
    .regex(/^\d{4}$/, "Postal code must be 4 digits")
    .optional(),
  latitude: z.number().min(-90).max(90).optional(),
  longitude: z.number().min(-180).max(180).optional(),
  
  // Contact Information (Step 3)
  contactMethod: z.enum(['EMAIL', 'PHONE', 'SMS']).default('EMAIL'),
  email: z.string().email("Invalid email format").optional(),
  phone: z.string()
    .regex(/^[\+]?[\d\s\-\(\)]{7,15}$/, "Invalid phone number format")
    .optional(),
  
  // Cross-field validation
}).refine((data) => {
  // Contact method validation
  if (data.contactMethod === 'EMAIL' && !data.email) {
    return false;
  }
  if (['PHONE', 'SMS'].includes(data.contactMethod) && !data.phone) {
    return false;
  }
  return true;
}, {
  message: "Contact information must match selected contact method",
  path: ["contactMethod"]
}).refine((data) => {
  // Geographic validation
  if (data.latitude && !data.longitude) return false;
  if (data.longitude && !data.latitude) return false;
  return true;
}, {
  message: "Both latitude and longitude are required for coordinates",
  path: ["latitude"]
});
```

#### Real-time Validation Implementation
```typescript
const useFormValidation = (schema: ZodSchema, mode: 'onChange' | 'onBlur' = 'onBlur') => {
  const form = useForm({
    resolver: zodResolver(schema),
    mode,
    reValidateMode: 'onChange',
    defaultValues: getDefaultValues(schema)
  });
  
  // Debounced validation for expensive operations
  const debouncedValidation = useMemo(
    () => debounce(async (fieldName: string) => {
      await form.trigger(fieldName);
    }, 300),
    [form]
  );
  
  return { ...form, debouncedValidation };
};
```

### State Management & Data Flow

#### Context-Based Architecture
```typescript
// Authentication Context
interface AuthContextType {
  user: User | null;
  token: string | null;
  login: (email: string, password: string) => Promise<void>;
  logout: () => void;
  updateProfile: (data: Partial<User>) => Promise<void>;
  hasRole: (role: Role | Role[]) => boolean;
  hasPermission: (permission: string) => boolean;
  isLoading: boolean;
}

// Feature Flags Context
interface FeatureFlagsContextType {
  flags: Record<string, boolean>;
  isEnabled: (key: string) => boolean;
  refresh: () => Promise<void>;
  isLoading: boolean;
}

// Language Context
interface LanguageContextType {
  language: 'EN' | 'BG';
  setLanguage: (lang: 'EN' | 'BG') => void;
  t: (key: string, params?: Record<string, any>) => string;
}
```

#### Custom Hooks for Data Management
```typescript
// Service Request Hook
const useServiceRequests = (params: ServiceRequestParams) => {
  const [data, setData] = useState<ServiceRequest[]>([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [totalCount, setTotalCount] = useState(0);
  
  const fetchData = useCallback(async () => {
    // Implementation with error handling, caching, optimistic updates
  }, [params]);
  
  return { data, loading, error, totalCount, refetch: fetchData };
};

// Request Creation Hook
const useCreateServiceRequest = () => {
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  
  const createRequest = useCallback(async (
    data: CreateServiceRequestData,
    idempotencyKey?: string
  ) => {
    // Implementation with validation, error handling, progress tracking
  }, []);
  
  return { createRequest, loading, error };
};
```

## Security & Validation Framework

### Multi-Layer Security Architecture

#### Authentication Security
```typescript
// JWT Configuration
const JWT_CONFIG = {
  expiresIn: '24h',
  algorithm: 'HS256',
  issuer: 'city-services-portal',
  audience: 'city-services-users'
};

// Password Security
const PASSWORD_REQUIREMENTS = {
  minLength: 8,
  requireUppercase: true,
  requireLowercase: true,
  requireNumbers: true,
  requireSpecialChars: false,
  maxAttempts: 5,
  lockoutDuration: '15m'
};

// Session Management
const SESSION_CONFIG = {
  tokenRefreshThreshold: '2h', // Refresh when 2h remaining
  maxConcurrentSessions: 3,
  deviceTracking: true,
  ipValidation: false // Disabled for development
};
```

#### Input Validation & Sanitization
```typescript
// XSS Prevention
const sanitizeInput = (input: string): string => {
  return input
    .replace(/[<>]/g, '') // Remove angle brackets
    .replace(/javascript:/gi, '') // Remove javascript: URLs
    .replace(/on\w+\s*=/gi, '') // Remove event handlers
    .trim();
};

// SQL Injection Prevention (Prisma handles this, but additional checks)
const validateSQLInput = (input: string): boolean => {
  const sqlInjectionPatterns = [
    /(\b(SELECT|INSERT|UPDATE|DELETE|DROP|CREATE|ALTER|EXEC)\b)/i,
    /(\b(UNION|OR|AND)\b.*\b(SELECT|INSERT|UPDATE|DELETE)\b)/i,
    /(--|\/\*|\*\/)/
  ];
  
  return !sqlInjectionPatterns.some(pattern => pattern.test(input));
};

// File Upload Security
const validateFileUpload = (file: Express.Multer.File): ValidationResult => {
  const allowedMimeTypes = ['image/jpeg', 'image/png', 'image/gif'];
  const maxFileSize = 5 * 1024 * 1024; // 5MB
  
  const errors: string[] = [];
  
  if (!allowedMimeTypes.includes(file.mimetype)) {
    errors.push('Invalid file type');
  }
  
  if (file.size > maxFileSize) {
    errors.push('File too large');
  }
  
  // Additional checks: magic number validation, virus scanning preparation
  if (!validateMagicNumbers(file.buffer, file.mimetype)) {
    errors.push('File content does not match declared type');
  }
  
  return { isValid: errors.length === 0, errors };
};
```

#### Rate Limiting & DDoS Protection
```typescript
// API Rate Limiting Configuration
const RATE_LIMITS = {
  global: {
    windowMs: 15 * 60 * 1000, // 15 minutes
    maxRequests: 10000, // Per IP
    message: 'Too many requests from this IP'
  },
  authentication: {
    windowMs: 15 * 60 * 1000,
    maxRequests: 10,
    skipSuccessfulRequests: true
  },
  requestCreation: {
    windowMs: 5 * 60 * 1000, // 5 minutes
    maxRequests: 5, // Prevent spam
    keyGenerator: (req) => req.user?.id || req.ip
  },
  fileUpload: {
    windowMs: 10 * 60 * 1000, // 10 minutes
    maxRequests: 20,
    keyGenerator: (req) => req.user?.id || req.ip
  }
};
```

### Role-Based Access Control Implementation

#### Permission Matrix
```typescript
interface Permission {
  resource: string;
  action: 'create' | 'read' | 'update' | 'delete';
  condition?: (user: User, resource: any) => boolean;
}

const ROLE_PERMISSIONS: Record<Role, Permission[]> = {
  CITIZEN: [
    { resource: 'serviceRequest', action: 'create' },
    { resource: 'serviceRequest', action: 'read', condition: (user, request) => 
        request.createdBy === user.id || request.isPublic },
    { resource: 'serviceRequest', action: 'update', condition: (user, request) => 
        request.createdBy === user.id && isWithinEditWindow(request) },
    { resource: 'comment', action: 'create', condition: (user, comment) => 
        comment.visibility === 'PUBLIC' },
    { resource: 'upvote', action: 'create' },
    { resource: 'profile', action: 'update', condition: (user, profile) => 
        profile.id === user.id }
  ],
  
  CLERK: [
    { resource: 'serviceRequest', action: 'read', condition: (user, request) =>
        request.departmentId === user.departmentId || request.assignedTo === user.id },
    { resource: 'serviceRequest', action: 'update', condition: (user, request) =>
        request.assignedTo === user.id },
    { resource: 'serviceRequest', action: 'create' }, // Can create on behalf of citizens
    { resource: 'comment', action: 'create' },
    { resource: 'assignment', action: 'update', condition: (user, assignment) =>
        assignment.assignedTo === user.id },
    { resource: 'status', action: 'update', condition: (user, request) =>
        isValidStatusTransition(request.status, user.role) }
  ],
  
  SUPERVISOR: [
    { resource: 'serviceRequest', action: 'read', condition: (user, request) =>
        request.departmentId === user.departmentId },
    { resource: 'serviceRequest', action: 'update', condition: (user, request) =>
        request.departmentId === user.departmentId },
    { resource: 'assignment', action: 'create' },
    { resource: 'assignment', action: 'update' },
    { resource: 'comment', action: 'create' },
    { resource: 'user', action: 'read', condition: (user, targetUser) =>
        targetUser.departmentId === user.departmentId },
    { resource: 'statistics', action: 'read', condition: (user) =>
        user.departmentId !== null }
  ],
  
  FIELD_AGENT: [
    { resource: 'serviceRequest', action: 'read', condition: (user, request) =>
        request.assignedTo === user.id },
    { resource: 'serviceRequest', action: 'update', condition: (user, request) =>
        request.assignedTo === user.id },
    { resource: 'comment', action: 'create' },
    { resource: 'status', action: 'update', condition: (user, request) =>
        request.assignedTo === user.id && isFieldAgentStatusTransition(request.status) }
  ],
  
  ADMIN: [
    { resource: '*', action: '*' } // Full access
  ]
};
```

#### Authorization Middleware
```typescript
const requirePermission = (resource: string, action: string) => {
  return async (req: Request, res: Response, next: NextFunction) => {
    try {
      const user = req.user; // Set by authentication middleware
      const targetResource = req.body || req.params;
      
      if (!hasPermission(user, resource, action, targetResource)) {
        return res.status(403).json({
          error: {
            code: 'INSUFFICIENT_PERMISSIONS',
            message: `User does not have ${action} permission on ${resource}`
          },
          correlationId: req.correlationId
        });
      }
      
      next();
    } catch (error) {
      next(error);
    }
  };
};

// Usage
app.patch('/api/v1/requests/:id', 
  authenticateToken,
  requirePermission('serviceRequest', 'update'),
  updateServiceRequest
);
```

## Supervisor Mode & Staff Management

### Supervisor Dashboard Overview
The supervisor mode provides comprehensive staff performance management, quality assurance, and workload optimization capabilities for departmental supervisors.

#### Core Supervisor Functionalities

##### 1. Department Performance Metrics
```typescript
// Real-time department analytics
- Average Resolution Time: Time from request creation to resolution
- SLA Compliance Rate: Percentage of requests resolved within SLA
- First Call Resolution: Requests resolved without escalation
- Customer Satisfaction Score: Average rating from citizens
- Request Volume Trends: Daily/weekly/monthly request patterns
- Escalation Rate: Percentage of requests requiring supervisor intervention
```

##### 2. Quality Review System
```typescript
interface QualityReview {
  requestId: string;
  qualityScore: number; // 1-10 overall quality rating
  communicationScore: number; // 1-10 communication effectiveness
  technicalAccuracyScore: number; // 1-10 technical solution accuracy
  timelinessScore: number; // 1-10 response time rating
  citizenSatisfactionScore: number; // 1-10 citizen feedback score
  improvementSuggestions?: string; // Coaching feedback
  followUpRequired: boolean; // Flag for additional training needs
  calibrationSession?: string; // Link to team calibration
  reviewStatus: 'PENDING' | 'IN_PROGRESS' | 'COMPLETED';
}
```

##### 3. Workload Assignment & Balancing
```typescript
interface WorkloadAssignment {
  requestId: string;
  assignedTo: string; // Target staff member
  assignedFrom?: string; // Previous assignee if reassignment
  assignedBy: string; // Supervisor making assignment
  assignmentReason?: string; // Justification for assignment
  estimatedEffort?: number; // Hours estimated for completion
  skillsRequired?: string[]; // Required competencies
  priorityWeight?: number; // 0-100 priority scoring
  isActive: boolean; // Current assignment status
}
```

##### 4. Performance Goal Management
```typescript
interface PerformanceGoal {
  userId: string; // Staff member
  supervisorId: string; // Goal creator
  title: string; // Goal title
  description: string; // Detailed goal description
  targetValue?: number; // Quantitative target
  currentValue: number; // Current progress
  unit: string; // Measurement unit
  dueDate: Date; // Goal deadline
  status: 'ACTIVE' | 'ACHIEVED' | 'MISSED' | 'PAUSED';
  priority: 'LOW' | 'MEDIUM' | 'HIGH';
}
```

##### 5. Staff Performance Tracking
```typescript
interface StaffPerformance {
  userId: string;
  departmentId: string;
  performancePeriod: string; // e.g., "2024-Q1"
  
  // Productivity Metrics
  completedRequests: number;
  averageHandlingTime: number; // Minutes
  firstCallResolution: number; // Percentage
  
  // Quality Metrics
  qualityScore: number; // Average from reviews
  citizenSatisfactionRating: number; // Average rating
  escalationRate: number; // Percentage escalated
  
  // Behavioral Metrics
  attendanceScore: number;
  collaborationScore: number;
  initiativeScore: number;
  
  // Calculated Scores
  productivityScore: number; // Weighted calculation
  overallPerformanceRating: number; // 1-10 scale
}
```

### Supervisor API Endpoints

#### Dashboard & Analytics
- `GET /api/v1/supervisor/dashboard-summary` - Comprehensive dashboard data
- `GET /api/v1/supervisor/department-metrics` - Department performance metrics
- `GET /api/v1/dashboard/overview` - Overview statistics for supervisors
- `GET /api/v1/dashboard/charts/request-trends` - Request trend visualizations
- `GET /api/v1/dashboard/charts/resolution-times` - Resolution time analytics
- `GET /api/v1/dashboard/charts/staff-performance` - Staff performance comparisons

#### Quality Management
- `GET /api/v1/supervisor/quality-reviews` - List quality reviews
- `POST /api/v1/supervisor/quality-reviews` - Create quality review
- `PUT /api/v1/supervisor/quality-reviews/:id` - Update quality review
- `DELETE /api/v1/supervisor/quality-reviews/:id` - Delete quality review

#### Workload Management
- `POST /api/v1/supervisor/workload-assignments` - Create workload assignment
- `GET /api/v1/supervisor/workload-assignments` - List workload assignments
- `PUT /api/v1/supervisor/workload-assignments/:id` - Update assignment
- `GET /api/v1/metrics/workloads` - Get workload analytics

#### Performance Management
- `GET /api/v1/supervisor/staff-performance` - Get staff performance data
- `PUT /api/v1/supervisor/staff-performance/:id/skills` - Update skills assessment
- `GET /api/v1/supervisor/performance-goals` - List performance goals
- `POST /api/v1/supervisor/performance-goals` - Create performance goal
- `PUT /api/v1/supervisor/performance-goals/:id` - Update performance goal
- `DELETE /api/v1/supervisor/performance-goals/:id` - Delete performance goal

### Supervisor UI Components

#### Dashboard Page (`SupervisorDashboardPage.tsx`)
```typescript
// Key Features:
- Real-time department metrics cards
- Staff performance leaderboard
- Pending quality reviews list
- Upcoming goal deadlines
- Request distribution charts
- SLA compliance indicators
- Workload balance visualization
```

#### Assignment Page (`SupervisorAssignPage.tsx`)
```typescript
// Key Features:
- Drag-and-drop request assignment
- Staff availability calendar
- Skills matching matrix
- Workload distribution view
- Auto-assignment suggestions
- Bulk assignment operations
```

### Role-Based Permissions for Supervisors

```typescript
const SUPERVISOR_PERMISSIONS = {
  // Request Management
  viewDepartmentRequests: true,
  assignRequests: true,
  changeRequestStatus: true,
  createInternalComments: true,
  
  // Staff Management
  viewStaffPerformance: true,
  createQualityReviews: true,
  setPerformanceGoals: true,
  updateSkillsAssessment: true,
  
  // Analytics Access
  viewDepartmentMetrics: true,
  generateReports: true,
  exportData: true,
  
  // Restrictions
  viewOtherDepartments: false,
  modifySystemSettings: false,
  accessFeatureFlags: false
};
```

## Testing Infrastructure & QA Framework

### Testing Strategy Overview

The application is designed as a comprehensive testing playground with multiple layers of testing support:

#### Frontend Testing Architecture
```typescript
// Component Testing with React Testing Library
describe('ImageUpload Component', () => {
  const mockProps = {
    maxImages: 5,
    maxSizePerImage: 5 * 1024 * 1024,
    acceptedTypes: ['image/jpeg', 'image/png'],
    onImagesChange: jest.fn()
  };
  
  describe('File Selection', () => {
    it('should handle file selection via input', async () => {
      render(<ImageUpload {...mockProps} />);
      const fileInput = screen.getByTestId('cs-image-upload-input');
      const file = new File(['test'], 'test.jpg', { type: 'image/jpeg' });
      
      await user.upload(fileInput, file);
      
      expect(mockProps.onImagesChange).toHaveBeenCalledWith([file]);
    });
    
    it('should handle drag and drop', async () => {
      render(<ImageUpload {...mockProps} />);
      const dropzone = screen.getByTestId('cs-image-upload-dropzone');
      const file = new File(['test'], 'test.jpg', { type: 'image/jpeg' });
      
      await user.drop(dropzone, [file]);
      
      expect(mockProps.onImagesChange).toHaveBeenCalledWith([file]);
    });
  });
  
  describe('Validation', () => {
    it('should reject files exceeding size limit', async () => {
      const largeFile = new File(['x'.repeat(6 * 1024 * 1024)], 'large.jpg', 
        { type: 'image/jpeg' });
      
      render(<ImageUpload {...mockProps} />);
      const fileInput = screen.getByTestId('cs-image-upload-input');
      
      await user.upload(fileInput, largeFile);
      
      expect(screen.getByText(/file too large/i)).toBeInTheDocument();
      expect(mockProps.onImagesChange).not.toHaveBeenCalled();
    });
    
    it('should reject invalid file types', async () => {
      const textFile = new File(['test'], 'test.txt', { type: 'text/plain' });
      
      render(<ImageUpload {...mockProps} />);
      const fileInput = screen.getByTestId('cs-image-upload-input');
      
      await user.upload(fileInput, textFile);
      
      expect(screen.getByText(/invalid file type/i)).toBeInTheDocument();
      expect(mockProps.onImagesChange).not.toHaveBeenCalled();
    });
  });
});
```

#### Test ID Convention System
```typescript
// Consistent test ID patterns across the application
const TEST_ID_PATTERNS = {
  // Format: cs-component-action-element
  'cs-stepper-wizard-next': 'Next step button in stepper wizard',
  'cs-stepper-wizard-back': 'Previous step button in stepper wizard',
  'cs-stepper-wizard-step-{n}': 'Specific step in stepper (0-indexed)',
  
  'cs-data-grid-row-{id}': 'Specific row in data grid',
  'cs-data-grid-header-{column}': 'Column header in data grid',
  'cs-data-grid-pagination': 'Pagination controls',
  
  'cs-request-form-title': 'Request title input field',
  'cs-request-form-description': 'Request description textarea',
  'cs-request-form-category': 'Category selection dropdown',
  
  'cs-image-upload-dropzone': 'Drag and drop area for images',
  'cs-image-upload-input': 'File input for image selection',
  'cs-image-upload-preview-{index}': 'Image preview thumbnail',
  
  'cs-location-picker-map': 'Interactive map component',
  'cs-location-picker-address': 'Address input field',
  'cs-location-picker-coordinates': 'Latitude/longitude inputs',
  
  'cs-comment-thread': 'Comment display area',
  'cs-comment-input': 'New comment input field',
  'cs-comment-submit': 'Submit comment button'
};
```

#### Integration Testing Patterns
```typescript
// Full user workflow testing
describe('Service Request Creation Workflow', () => {
  beforeEach(() => {
    // Setup test user and authentication
    mockAuthProvider({ user: mockCitizen, token: 'valid-token' });
    
    // Setup API mocks
    mockApi.post('/api/v1/requests').mockResolvedValue({
      data: { data: mockServiceRequest }
    });
  });
  
  it('should complete full request creation workflow', async () => {
    render(<NewRequestPage />);
    
    // Step 1: Basic Information
    await user.type(screen.getByTestId('cs-request-form-title'), 
      'Broken streetlight on Main Street');
    await user.type(screen.getByTestId('cs-request-form-description'), 
      'The streetlight at the intersection of Main and Oak has been out for a week');
    await user.selectOptions(screen.getByTestId('cs-request-form-category'), 
      'Street Lighting');
    await user.click(screen.getByTestId('cs-stepper-wizard-next'));
    
    // Step 2: Location
    await user.type(screen.getByTestId('cs-location-picker-address'), 
      '123 Main Street');
    await user.click(screen.getByTestId('cs-stepper-wizard-next'));
    
    // Step 3: Contact
    await user.selectOptions(screen.getByTestId('cs-contact-method'), 'EMAIL');
    await user.click(screen.getByTestId('cs-stepper-wizard-next'));
    
    // Step 4: Additional Details
    await user.click(screen.getByTestId('cs-stepper-wizard-next'));
    
    // Step 5: Review and Submit
    expect(screen.getByText('Broken streetlight on Main Street')).toBeInTheDocument();
    await user.click(screen.getByTestId('cs-request-submit'));
    
    // Verify API call
    expect(mockApi.post).toHaveBeenCalledWith('/api/v1/requests', 
      expect.objectContaining({
        title: 'Broken streetlight on Main Street',
        description: expect.any(String),
        category: 'Street Lighting'
      })
    );
    
    // Verify redirect to success page
    expect(window.location.pathname).toBe('/requests/success');
  });
});
```

### Feature Flag Testing Framework

#### A/B Testing & Bug Simulation
```typescript
// Feature flags designed for QA scenarios
const QA_FEATURE_FLAGS = {
  'API_Random500': {
    enabled: false,
    description: 'Introduces random 500 errors (5% chance)',
    implementation: 'Middleware randomly throws errors',
    testScenarios: [
      'Error handling in UI components',
      'Retry mechanisms',
      'User feedback for server errors',
      'Graceful degradation'
    ]
  },
  
  'UI_WrongDefaultSort': {
    enabled: false,
    description: 'Sets incorrect default sorting in data grids',
    implementation: 'Changes default sort from createdAt:desc to title:asc',
    testScenarios: [
      'User expectation violation',
      'Sort control functionality',
      'Data presentation logic',
      'User preference persistence'
    ]
  },
  
  'API_SlowRequests': {
    enabled: false,
    description: 'Introduces artificial delays (10% of requests, 3s delay)',
    implementation: 'Middleware adds setTimeout before processing',
    testScenarios: [
      'Loading state management',
      'Timeout handling',
      'User experience during delays',
      'Race condition prevention'
    ]
  },
  
  'API_UploadIntermittentFail': {
    enabled: false,
    description: 'File uploads fail randomly (20% chance)',
    implementation: 'Multer middleware randomly rejects uploads',
    testScenarios: [
      'Upload retry mechanisms',
      'Progress indicator accuracy',
      'Error message clarity',
      'Partial upload handling'
    ]
  }
};
```

#### Test Data Management
```typescript
// Comprehensive test data generation
const generateTestData = {
  serviceRequests: (count: number = 150) => {
    const categories = [
      'Street Lighting', 'Pothole Repair', 'Trash Collection',
      'Water/Sewer Issues', 'Park Maintenance', 'Noise Complaints',
      'Building Permits', 'Tree Services', 'Snow Removal',
      'Traffic Signals', 'Sidewalk Repair', 'Graffiti Removal',
      'Animal Control', 'Public Transportation'
    ];
    
    const statuses = Object.values(RequestStatus);
    const priorities = Object.values(Priority);
    
    return Array.from({ length: count }, (_, index) => ({
      id: `req-${index + 1}`,
      code: `REQ-2024-${String(index + 1).padStart(4, '0')}`,
      title: faker.lorem.sentence({ min: 3, max: 8 }),
      description: faker.lorem.paragraph({ min: 3, max: 6 }),
      category: faker.helpers.arrayElement(categories),
      priority: faker.helpers.arrayElement(priorities),
      status: faker.helpers.arrayElement(statuses),
      dateOfRequest: faker.date.recent({ days: 90 }),
      locationText: faker.location.streetAddress(),
      // ... additional realistic data
    }));
  },
  
  users: (roles: Role[]) => {
    return roles.map((role, index) => ({
      id: `user-${role}-${index}`,
      email: `${role.toLowerCase()}${index}@city.gov`,
      name: faker.person.fullName(),
      role,
      // ... role-specific data
    }));
  }
};
```

## Performance & Scalability Architecture

### Database Optimization Strategy

#### Indexing Strategy
```sql
-- Critical indexes for performance
CREATE INDEX idx_service_requests_status ON ServiceRequest(status);
CREATE INDEX idx_service_requests_priority ON ServiceRequest(priority);
CREATE INDEX idx_service_requests_created_by ON ServiceRequest(createdBy);
CREATE INDEX idx_service_requests_assigned_to ON ServiceRequest(assignedTo);
CREATE INDEX idx_service_requests_department ON ServiceRequest(departmentId);
CREATE INDEX idx_service_requests_created_at ON ServiceRequest(createdAt);
CREATE INDEX idx_service_requests_code ON ServiceRequest(code); -- Unique constraint provides index

-- Composite indexes for common query patterns
CREATE INDEX idx_service_requests_status_priority ON ServiceRequest(status, priority);
CREATE INDEX idx_service_requests_department_status ON ServiceRequest(departmentId, status);
CREATE INDEX idx_service_requests_assigned_status ON ServiceRequest(assignedTo, status);

-- Full-text search preparation
CREATE INDEX idx_service_requests_search ON ServiceRequest(title, description, locationText);

-- Geographic indexes for location-based queries
CREATE INDEX idx_service_requests_location ON ServiceRequest(lat, lng);
```

#### Query Optimization Patterns
```typescript
// Efficient pagination with cursor-based approach
const getPaginatedRequests = async (params: PaginationParams) => {
  const { page = 1, pageSize = 20, cursor, sort = 'createdAt:desc' } = params;
  
  // Use cursor-based pagination for large datasets
  const where: Prisma.ServiceRequestWhereInput = {
    // Filter conditions
    ...(params.status && { status: params.status }),
    ...(params.priority && { priority: params.priority }),
    ...(params.assignedTo && { assignedTo: params.assignedTo }),
    
    // Text search across multiple fields
    ...(params.text && {
      OR: [
        { title: { contains: params.text, mode: 'insensitive' } },
        { description: { contains: params.text, mode: 'insensitive' } },
        { code: { contains: params.text, mode: 'insensitive' } },
        { locationText: { contains: params.text, mode: 'insensitive' } }
      ]
    }),
    
    // Cursor-based pagination
    ...(cursor && {
      id: { lt: cursor } // Assuming ID-based cursor
    })
  };
  
  // Optimized query with selective field loading
  const requests = await prisma.serviceRequest.findMany({
    where,
    select: {
      // Only load essential fields for list view
      id: true,
      code: true,
      title: true,
      status: true,
      priority: true,
      category: true,
      createdAt: true,
      updatedAt: true,
      creator: { select: { name: true, email: true } },
      assignee: { select: { name: true } },
      department: { select: { name: true } },
      _count: { select: { comments: true, attachments: true, upvotes: true } }
    },
    orderBy: parseSort(sort),
    take: pageSize + 1, // Take one extra to determine if there are more pages
    skip: cursor ? 0 : (page - 1) * pageSize
  });
  
  const hasNextPage = requests.length > pageSize;
  const items = hasNextPage ? requests.slice(0, -1) : requests;
  const nextCursor = hasNextPage ? items[items.length - 1].id : null;
  
  return {
    items,
    pagination: {
      page,
      pageSize,
      hasNextPage,
      nextCursor,
      totalCount: await getTotalCount(where) // Cached for performance
    }
  };
};
```

### Frontend Performance Optimization

#### Code Splitting & Lazy Loading
```typescript
// Route-based code splitting
const CitizenRequestsPage = lazy(() => import('../pages/citizen/CitizenRequestsPage'));
const ClerkInboxPage = lazy(() => import('../pages/clerk/ClerkInboxPage'));
const AdminDashboard = lazy(() => import('../pages/admin/AdminDashboard'));

// Component-based code splitting for large components
const LocationPicker = lazy(() => import('../components/LocationPicker'));
const DataTable = lazy(() => import('../components/DataTable'));

// Usage with Suspense
<Suspense fallback={<PageSkeleton />}>
  <Route path="/requests" element={<CitizenRequestsPage />} />
</Suspense>
```

#### Memoization & Optimization Patterns
```typescript
// Expensive computation memoization
const ServiceRequestList = memo(({ requests, onStatusChange }) => {
  // Memoize filtered and sorted data
  const processedRequests = useMemo(() => {
    return requests
      .filter(request => request.status !== 'DRAFT')
      .sort((a, b) => new Date(b.createdAt) - new Date(a.createdAt))
      .map(request => ({
        ...request,
        formattedDate: formatDate(request.createdAt),
        statusColor: getStatusColor(request.status),
        priorityIcon: getPriorityIcon(request.priority)
      }));
  }, [requests]);
  
  // Memoize callback to prevent child re-renders
  const handleStatusChange = useCallback((requestId, newStatus) => {
    onStatusChange(requestId, newStatus);
  }, [onStatusChange]);
  
  return (
    <VirtualizedList
      items={processedRequests}
      onItemAction={handleStatusChange}
    />
  );
});
```

#### Virtual Scrolling for Large Datasets
```typescript
// Virtual scrolling implementation for request lists
const VirtualizedRequestList = ({ requests, height = 600 }) => {
  const listRef = useRef();
  const [visibleRange, setVisibleRange] = useState({ start: 0, end: 20 });
  
  const itemHeight = 120; // Fixed height per request item
  const containerHeight = Math.min(requests.length * itemHeight, height);
  
  const handleScroll = useCallback(throttle((scrollTop) => {
    const start = Math.floor(scrollTop / itemHeight);
    const end = Math.min(start + Math.ceil(height / itemHeight) + 5, requests.length);
    
    setVisibleRange({ start, end });
  }, 16), [requests.length, itemHeight, height]);
  
  const visibleItems = requests.slice(visibleRange.start, visibleRange.end);
  
  return (
    <div
      ref={listRef}
      style={{ height: containerHeight, overflowY: 'auto' }}
      onScroll={(e) => handleScroll(e.target.scrollTop)}
    >
      <div style={{ height: visibleRange.start * itemHeight }} />
      {visibleItems.map((request, index) => (
        <RequestListItem
          key={request.id}
          request={request}
          style={{ height: itemHeight }}
        />
      ))}
      <div style={{ 
        height: (requests.length - visibleRange.end) * itemHeight 
      }} />
    </div>
  );
};
```

### Caching Strategy

#### Multi-Layer Caching Architecture
```typescript
// API Response Caching
class CacheManager {
  private memoryCache = new Map<string, CacheEntry>();
  private cacheConfig = {
    defaultTTL: 5 * 60 * 1000, // 5 minutes
    maxEntries: 1000,
    cleanupInterval: 60 * 1000 // 1 minute
  };
  
  set(key: string, data: any, ttl?: number): void {
    const expiry = Date.now() + (ttl || this.cacheConfig.defaultTTL);
    this.memoryCache.set(key, { data, expiry });
    
    // Cleanup old entries
    if (this.memoryCache.size > this.cacheConfig.maxEntries) {
      this.cleanup();
    }
  }
  
  get(key: string): any | null {
    const entry = this.memoryCache.get(key);
    
    if (!entry) return null;
    
    if (Date.now() > entry.expiry) {
      this.memoryCache.delete(key);
      return null;
    }
    
    return entry.data;
  }
  
  invalidatePattern(pattern: string): void {
    const regex = new RegExp(pattern);
    for (const key of this.memoryCache.keys()) {
      if (regex.test(key)) {
        this.memoryCache.delete(key);
      }
    }
  }
  
  private cleanup(): void {
    const now = Date.now();
    for (const [key, entry] of this.memoryCache.entries()) {
      if (now > entry.expiry) {
        this.memoryCache.delete(key);
      }
    }
  }
}

// Usage in API layer
const cache = new CacheManager();

const getServiceRequests = async (params: RequestParams) => {
  const cacheKey = `requests:${JSON.stringify(params)}`;
  const cached = cache.get(cacheKey);
  
  if (cached) {
    return cached;
  }
  
  const data = await fetchRequestsFromDatabase(params);
  cache.set(cacheKey, data, 2 * 60 * 1000); // 2 minutes TTL
  
  return data;
};
```

## Deployment & DevOps Configuration

### Development Environment Setup

#### Docker Configuration
```yaml
# docker-compose.yml
version: '3.8'

services:
  api:
    build:
      context: ./api
      dockerfile: Dockerfile.dev
    ports:
      - "3001:3001"
    environment:
      - NODE_ENV=development
      - DATABASE_URL=file:./dev.db
      - JWT_SECRET=development-secret-key
    volumes:
      - ./api:/app
      - /app/node_modules
    depends_on:
      - database
  
  ui:
    build:
      context: ./ui
      dockerfile: Dockerfile.dev
    ports:
      - "5173:5173"
    environment:
      - VITE_API_BASE_URL=http://localhost:3001
    volumes:
      - ./ui:/app
      - /app/node_modules
    depends_on:
      - api
  
  database:
    image: postgres:15-alpine
    environment:
      - POSTGRES_DB=city_services
      - POSTGRES_USER=development
      - POSTGRES_PASSWORD=development
    ports:
      - "5432:5432"
    volumes:
      - postgres_data:/var/lib/postgresql/data

volumes:
  postgres_data:
```

#### Environment Configuration
```bash
# .env.development
NODE_ENV=development
PORT=3001
DATABASE_URL="file:./dev.db"
JWT_SECRET="development-jwt-secret-key-change-in-production"

# Frontend environment
VITE_API_BASE_URL=http://localhost:3001
VITE_APP_NAME="City Services Portal"
VITE_ENABLE_DEBUG=true

# Feature flags
VITE_ENABLE_FEATURE_FLAGS=true
VITE_DEFAULT_LANGUAGE=EN

# Map configuration
VITE_MAP_DEFAULT_CENTER_LAT=42.6977
VITE_MAP_DEFAULT_CENTER_LNG=23.3219
VITE_MAP_DEFAULT_ZOOM=13
```

### Production Deployment Considerations

#### Security Hardening
```typescript
// Production security configuration
const PRODUCTION_CONFIG = {
  cors: {
    origin: process.env.ALLOWED_ORIGINS?.split(',') || 'https://city-services.gov',
    credentials: true,
    optionsSuccessStatus: 200
  },
  
  jwt: {
    secret: process.env.JWT_SECRET, // Must be cryptographically secure
    expiresIn: '24h',
    algorithm: 'RS256', // Use RSA in production
    issuer: 'city-services-portal',
    audience: 'city-services-users'
  },
  
  rateLimit: {
    windowMs: 15 * 60 * 1000,
    maxRequests: 1000, // Reduced for production
    message: 'Too many requests from this IP, please try again later.',
    standardHeaders: true,
    legacyHeaders: false
  },
  
  helmet: {
    contentSecurityPolicy: {
      directives: {
        defaultSrc: ["'self'"],
        styleSrc: ["'self'", "'unsafe-inline'", 'fonts.googleapis.com'],
        fontSrc: ["'self'", 'fonts.gstatic.com'],
        imgSrc: ["'self'", 'data:', 'https:'],
        scriptSrc: ["'self'"],
        connectSrc: ["'self'", process.env.API_BASE_URL]
      }
    },
    hsts: {
      maxAge: 31536000,
      includeSubDomains: true,
      preload: true
    }
  }
};
```

#### Monitoring & Logging
```typescript
// Structured logging configuration
const logger = pino({
  level: process.env.LOG_LEVEL || 'info',
  formatters: {
    level: (label) => ({ level: label.toUpperCase() })
  },
  serializers: {
    req: (request) => ({
      method: request.method,
      url: request.url,
      correlationId: request.correlationId,
      userAgent: request.headers['user-agent']
    }),
    res: (response) => ({
      statusCode: response.statusCode
    }),
    err: pino.stdSerializers.err
  },
  redact: {
    paths: [
      'req.headers.authorization',
      'req.body.password',
      'req.body.passwordHash'
    ],
    censor: '[REDACTED]'
  }
});

// Performance monitoring middleware
const performanceMiddleware = (req: Request, res: Response, next: NextFunction) => {
  const start = Date.now();
  
  res.on('finish', () => {
    const duration = Date.now() - start;
    
    logger.info({
      req,
      res,
      duration,
      correlationId: req.correlationId
    }, 'Request completed');
    
    // Track slow queries
    if (duration > 1000) {
      logger.warn({
        correlationId: req.correlationId,
        duration,
        endpoint: req.path
      }, 'Slow request detected');
    }
  });
  
  next();
};
```

This comprehensive technical documentation provides a complete overview of the City Services Portal architecture, implementation details, and all supporting systems. The application serves as an ideal testing playground for AI-powered QA tools while maintaining production-ready standards and realistic business logic.