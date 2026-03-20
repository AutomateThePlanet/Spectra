# User Management

## Overview

The system manages two user populations: **internal users** (government employees) and **citizens**. This document covers internal user management. For citizen registration, see the Citizen Registration document.

## Roles and Permissions

### Built-in Roles

| Role | Description | Can be modified |
|---|---|---|
| System Admin | Full system access, user management, configuration | No |
| Director | All operations for all regions, reporting, approval of high-value applications | No |
| Manager | Department operations, assign work, approve standard applications, reporting | No |
| Senior Clerk | Process applications, approve simple cases, generate documents | No |
| Clerk | Process assigned applications, enter data, upload documents | No |
| Auditor | Read-only access to all data, reports, and audit logs | No |
| Viewer | Read-only access to own department's applications | No |

### Custom Roles

System administrators can create custom roles by combining granular permissions:

**Permission categories:**
- `applications.view`, `applications.create`, `applications.process`, `applications.approve`, `applications.reject`, `applications.cancel`
- `citizens.view`, `citizens.edit`, `citizens.deactivate`
- `documents.view`, `documents.upload`, `documents.sign`, `documents.delete`
- `payments.view`, `payments.refund`, `payments.reconcile`
- `reports.view`, `reports.export`, `reports.schedule`
- `users.view`, `users.create`, `users.edit`, `users.deactivate`, `users.assign_role`
- `config.view`, `config.edit`
- `audit.view`

Custom roles cannot exceed the permissions of the built-in Director role. The system prevents creating a custom role with `config.edit` unless the creator is a System Admin.

### Permission Inheritance

Permissions are evaluated in the following order:
1. User's explicit permissions (if any)
2. User's role permissions
3. Department-level restrictions

If a user has multiple roles, permissions are **additive** (union of all role permissions). There is no "deny" permission — to restrict access, remove the role or permission rather than adding a deny rule.

## Department Hierarchy

The system supports a tree structure of departments:

```
Ministry of Regional Development
├── Directorate "Administrative Services"
│   ├── Department "Civil Status"
│   └── Department "Address Registration"
├── Directorate "Urban Planning"
│   ├── Department "Building Permits"
│   └── Department "Spatial Planning"
└── Regional Office Sofia
    ├── Front Office
    └── Back Office
```

Rules:
- A user belongs to exactly one department
- Managers see data for their department and all sub-departments
- Directors see data for the entire directorate (their department + all descendants)
- Cross-department data access requires explicit permission grant by a System Admin
- Department hierarchy changes (restructuring) do not affect existing application assignments — they remain with the originally assigned user

## User Lifecycle

### Creation

System Admins or Managers (with `users.create` permission) can create internal users:

Required fields:
- First Name, Last Name
- Email (must be from an approved domain: @mrrb.government.bg, @sofia.bg)
- Department
- Role (or custom role)
- Start date (user cannot login before this date)

Optional fields:
- Employee ID
- Phone number
- End date (for temporary/contract employees — account auto-deactivates on this date)
- Supervisor (for delegation chain)

Upon creation:
1. Welcome email sent with a temporary password
2. User must change password and enroll MFA on first login
3. Account is in "Active" status

### Modification

Changes to user accounts:
- Role changes take effect immediately upon save. Active sessions are NOT terminated — new permissions apply on the next request.
- Department transfers: The user's active application assignments are reviewed. The system shows a warning: "This user has X active assignments. Transfer assignments to another user or keep with current user?"
- Email changes require re-verification of the new email address

### Deactivation

Deactivated accounts:
- Cannot log in
- All active sessions are terminated immediately
- Application assignments remain visible but the user is shown as "(Deactivated)" in assignment lists
- The user's data is retained for audit purposes
- Deactivated accounts can be reactivated by a System Admin. Upon reactivation, the user must set a new password and re-enroll MFA.

Accounts are NOT deleted — only deactivated. This ensures audit trail integrity.

### Auto-deactivation triggers:
- End date reached (contract expiry)
- No login for 90 consecutive days (with 7-day and 1-day warning emails)
- Domain removed from approved list (bulk deactivation)

## Delegation

Users can delegate their responsibilities to another user in the same department:

- Delegation has a start date and end date (maximum 30 days)
- The delegate receives all application assignments and can process them
- The delegate does NOT receive the delegator's role — they act with their own permissions plus the delegator's application access
- Both the original user and the delegate can act on the applications during the delegation period
- When delegation expires, unprocessed applications return to the original user's queue
- A user can have at most one active delegation at a time
- Managers must approve delegations for their department members

## Bulk Operations

For organizational changes, System Admins can perform bulk operations:

- **Bulk role change**: Select multiple users → assign new role → confirm → applied immediately
- **Bulk department transfer**: Select users → new department → optional: reassign applications → confirm
- **Bulk deactivation**: Select users → confirm → all deactivated, sessions terminated

Bulk operations are logged as a single audit event with a list of affected users. A confirmation dialog shows the count and lists the first 10 users with a "show all" option.

Maximum users per bulk operation: 500. For larger changes, the operation must be split.
