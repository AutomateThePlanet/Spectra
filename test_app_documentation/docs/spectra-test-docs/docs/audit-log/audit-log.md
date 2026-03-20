# Audit Log

## Overview

The audit log is an immutable record of all significant actions in the system. It serves regulatory compliance, security investigations, and operational transparency. Audit records cannot be modified or deleted by any user, including System Administrators.

## What Is Logged

### User Actions
- Login / Logout / Failed login attempts
- Application creation, modification, status changes
- Document upload, download, deletion, signing
- Payment initiation, confirmation, refund
- Citizen record creation, modification, deactivation
- User account creation, role changes, deactivation
- Permission changes
- Report generation and export
- Search queries (for security-sensitive searches: EGN lookups, financial queries)
- Configuration changes

### System Events
- Scheduled job execution (start, end, success/failure)
- Notification delivery (sent, failed, retried)
- Integration events (payment gateway callbacks, SSO authentication)
- Data purge execution
- Index rebuild
- Backup execution
- Error events (HTTP 500, unhandled exceptions, timeout errors)

### Data Access Events
- Every read access to citizen personal data (name, EGN, address, phone, email) is logged
- Every export of data containing personal information
- Every print action on documents containing personal data
- Database queries that return more than 1,000 rows of personal data

## Audit Record Structure

Each audit record contains:

| Field | Type | Description |
|---|---|---|
| `id` | UUID | Unique record identifier |
| `timestamp` | DateTime (UTC) | When the event occurred, millisecond precision |
| `actor_id` | UUID | The user who performed the action (null for system events) |
| `actor_type` | Enum | INTERNAL_USER, CITIZEN, SYSTEM, EXTERNAL_SYSTEM |
| `actor_ip` | String | Source IP address |
| `actor_user_agent` | String | Browser/client user agent |
| `action` | String | Action code (e.g., APPLICATION_CREATED, DOCUMENT_SIGNED) |
| `category` | Enum | USER_ACTION, SYSTEM_EVENT, DATA_ACCESS, SECURITY |
| `resource_type` | String | Type of resource affected (Application, Document, User, etc.) |
| `resource_id` | UUID | ID of the affected resource |
| `description` | String | Human-readable description of the event |
| `changes` | JSON | Before/after values for modification events |
| `metadata` | JSON | Additional context (session ID, request ID, correlation ID) |
| `severity` | Enum | INFO, WARNING, CRITICAL |
| `hash` | String | SHA-256 hash of the record content + previous record's hash (chain) |

## Immutability

### Hash Chain

Each audit record includes a SHA-256 hash computed from:
- The record's own content (all fields except `hash`)
- The `hash` value of the immediately preceding record

This creates a blockchain-like chain. If any record is modified or deleted, the chain breaks and the system detects tampering.

### Tamper Detection

A nightly job verifies the hash chain:
1. Reads all records for the previous day
2. Recomputes hashes sequentially
3. Compares computed hashes with stored hashes
4. If any mismatch: sends CRITICAL alert to all System Administrators and writes to a separate tamper log stored on an independent server

Additionally, System Admins can trigger manual verification for any date range.

### Storage Protection

- Audit records are stored in a separate database schema with restricted access
- The application's database user has INSERT and SELECT permissions only — no UPDATE or DELETE
- Database triggers prevent UPDATE and DELETE operations on the audit table
- Database backups include the audit log and are stored for 10 years on separate storage

## Retention

| Category | Retention Period |
|---|---|
| Security events (login, failed access, permission changes) | 5 years |
| Data access events (personal data reads) | 3 years |
| User actions (application processing, document operations) | 10 years |
| System events (jobs, integrations, errors) | 2 years |
| Financial events (payments, refunds) | 10 years |

After the retention period, records are exported to cold storage (encrypted archive) and removed from the active database. The export process:
1. Generates a signed archive with all records for the retention period
2. Verifies the hash chain of the exported records
3. Stores the archive in encrypted form on immutable storage (WORM — Write Once Read Many)
4. Only after successful verification and storage, removes records from the active database
5. A record of the export itself is added to the audit log

## Viewing Audit Logs

### Audit Log Viewer

Available to: Auditors, Directors, System Admins

Features:
- Search by: actor, action, resource, date range, severity, category
- Filters combine with AND logic
- Results are sorted by timestamp descending (newest first)
- Each record expandable to show full details including `changes` JSON
- For modification events, a diff view shows before/after values side by side
- Color-coded severity: blue=INFO, yellow=WARNING, red=CRITICAL

### Timeline View

For a specific resource (e.g., an application), a timeline view shows all audit events:
- Vertical timeline with events as cards
- Each card shows: timestamp, actor, action, brief description
- Useful for investigating the full history of an application or citizen record

### Correlation View

For investigating incidents, auditors can follow a correlation ID:
- A single user request can generate multiple audit records (e.g., approve application → change status → send notification → generate document)
- All records share the same `correlation_id`
- Correlation view shows all related records in sequence

## Export

Audit logs can be exported for external review or legal proceedings:

- Formats: CSV, JSON, PDF (for legal: PDF with digital signature)
- Maximum export: 500,000 records per export
- Exports are themselves logged in the audit log (who exported, date range, filter criteria)
- PDF exports include a cover page with: export date, requestor, filter criteria, record count, and a digital signature from the system's qualified electronic seal
- Exported files are encrypted with AES-256 and require a password set by the requestor

## Compliance Notes

The audit log is designed to comply with:
- **GDPR Article 30**: Records of processing activities
- **Bulgarian Personal Data Protection Act**: Logging access to personal data
- **eIDAS Regulation**: Audit trail for electronic trust services
- **ISO 27001 A.12.4**: Event logging requirements

Data Protection Officers have read-only access to audit logs containing personal data access events, without requiring System Admin privileges.
