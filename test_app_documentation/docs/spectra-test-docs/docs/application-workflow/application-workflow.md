# Application Workflow

## Overview

Citizens submit applications for government services through the portal. Each application follows a defined workflow from submission to completion. This document describes the application lifecycle, assignment rules, SLA management, and escalation procedures.

## Application States

```
                                    ┌──────────────┐
                                    │   CANCELLED   │
                                    └──────────────┘
                                          ↑
                                          │ (citizen cancels OR
                                          │  payment timeout)
┌─────────┐    ┌──────────────┐    ┌──────────────┐    ┌──────────────┐
│  DRAFT   │───→│   SUBMITTED  │───→│   AWAITING   │───→│     IN       │
│          │    │              │    │   PAYMENT    │    │  PROCESSING  │
└─────────┘    └──────────────┘    └──────────────┘    └──────────────┘
                                                              │
                                          ┌───────────────────┤
                                          ↓                   ↓
                                   ┌──────────────┐    ┌──────────────┐
                                   │   REJECTED   │    │   APPROVED   │
                                   └──────────────┘    └──────────────┘
                                          │                   │
                                          ↓                   ↓
                                   ┌──────────────┐    ┌──────────────┐
                                   │   RETURNED    │    │  DOCUMENT    │
                                   │  FOR REVISION │    │  GENERATION  │
                                   └──────────────┘    └──────────────┘
                                          │                   │
                                          ↓                   ↓
                                   (citizen resubmits)  ┌──────────────┐
                                   → back to SUBMITTED  │  COMPLETED   │
                                                        └──────────────┘
```

### State Descriptions

| State | Description | Who can act |
|---|---|---|
| DRAFT | Citizen has started but not submitted. Saved automatically. | Citizen |
| SUBMITTED | Citizen has submitted. System validates completeness. | System → auto-assigns |
| AWAITING_PAYMENT | Fee required. Waiting for payment confirmation. | Citizen (pays), System (monitors timeout) |
| CANCELLED | Application cancelled. Terminal state. | Citizen (before processing), System (payment timeout) |
| IN_PROCESSING | Assigned clerk is reviewing the application. | Assigned clerk, Manager (reassign) |
| APPROVED | Clerk or manager has approved. Document generation begins. | System (auto-generates documents) |
| REJECTED | Application rejected with reason. | Clerk (with reason), Manager (override) |
| RETURNED_FOR_REVISION | Additional information needed from citizen. | Citizen (uploads/corrects), Clerk (reviews after resubmission) |
| DOCUMENT_GENERATION | System is generating the output document. | System (automatic) |
| COMPLETED | Service delivered. Document available for download. Terminal state. | Citizen (downloads) |

### State Transition Rules

- **DRAFT → SUBMITTED**: All required fields must be filled. Document attachments validated (format, size). EGN cross-validated.
- **SUBMITTED → AWAITING_PAYMENT**: Automatic, if fee > 0 BGN. If fee = 0 (waiver), skips directly to IN_PROCESSING.
- **AWAITING_PAYMENT → IN_PROCESSING**: Payment confirmed via gateway callback.
- **AWAITING_PAYMENT → CANCELLED**: Payment not received within 7 calendar days (bank transfer) or 48 hours (voucher). Card payments are immediate — if declined, stays in AWAITING_PAYMENT.
- **IN_PROCESSING → APPROVED**: Clerk approves. For high-value services (fee > 100 BGN), requires Manager co-approval.
- **IN_PROCESSING → REJECTED**: Clerk rejects with mandatory reason (free text, minimum 20 characters). Citizen is notified with the reason.
- **IN_PROCESSING → RETURNED_FOR_REVISION**: Clerk requests changes. Specific fields or documents that need revision are marked. Citizen receives notification with the list of requested changes.
- **RETURNED_FOR_REVISION → SUBMITTED**: Citizen addresses the requested changes and resubmits. The application re-enters the queue but retains its original submission date for SLA purposes.
- **APPROVED → DOCUMENT_GENERATION**: Automatic. System generates the output document from the appropriate template.
- **DOCUMENT_GENERATION → COMPLETED**: Automatic after document is generated, signed with the system's electronic seal, and stored.
- **Any state → CANCELLED**: Citizen can cancel at any time before APPROVED. After APPROVED, cancellation is not possible. Cancelled applications that were paid trigger a refund process.

## Assignment Rules

When an application enters SUBMITTED (or returns from RETURNED_FOR_REVISION), it is assigned to a clerk:

### Auto-Assignment Algorithm

1. Identify the service type and determine the responsible department
2. Get the list of active clerks in that department with `applications.process` permission
3. Filter out clerks who are on leave or have active delegation
4. Sort by current workload (number of IN_PROCESSING applications, ascending)
5. Assign to the clerk with the lowest workload
6. If multiple clerks have equal workload, assign to the one who most recently completed an application of the same service type (expertise-based tiebreaker)
7. If no clerks are available (all on leave), the application enters a department queue and the Manager is notified

### Manual Reassignment

Managers can reassign applications:
- Drag-and-drop in the department workload view
- Or click "Reassign" on the application and select a clerk from a dropdown
- Reassignment reason is required (free text)
- The original clerk is notified of the reassignment
- Reassignment resets the clerk's processing timer but NOT the overall SLA timer

## SLA Management

### SLA Definition

Each service type has a defined SLA:

| Service Type | SLA (business days) | Warning at | Critical at |
|---|---|---|---|
| Certificate of Birth | 7 | Day 5 | Day 7 |
| Certificate of Marital Status | 7 | Day 5 | Day 7 |
| Address Registration | 3 | Day 2 | Day 3 |
| Criminal Record Certificate | 14 | Day 10 | Day 14 |
| Building Permit Application | 30 | Day 20 | Day 28 |

Business days: Monday through Friday, excluding Bulgarian public holidays. The holiday calendar is maintained by System Admin and loaded annually.

### SLA Calculation

- SLA clock starts when the application enters IN_PROCESSING
- SLA clock pauses when the application is in RETURNED_FOR_REVISION (waiting for citizen)
- SLA clock resumes when the citizen resubmits
- SLA is measured against the COMPLETED state (document delivered to citizen)
- Expedited applications have SLA = standard SLA / 2 (rounded up)

### SLA Breach Handling

When an application approaches or exceeds its SLA:

1. **Warning** (configurable, default: 2 days before breach): Application highlighted in yellow in the clerk's queue. Manager receives daily summary email of approaching SLAs.
2. **Critical** (on the SLA day): Application highlighted in red. Manager receives immediate notification. Director is added to the notification.
3. **Breached** (SLA exceeded): Application flagged as SLA_BREACHED in metadata. Automatically escalated to Manager if still with a clerk. The breach is recorded in the SLA Compliance Report and cannot be unflagged.

## Escalation Rules

### Automatic Escalation

| Condition | Action |
|---|---|
| Application unassigned for > 4 hours | Notify Manager |
| Clerk has not opened the application within 24 hours of assignment | Notify Manager + option to reassign |
| SLA warning threshold reached | Notify Manager |
| SLA breached | Escalate to Manager (reassign), Notify Director |
| Application returned for revision 3 times | Escalate to Manager for review |
| Citizen complaint filed about application | Escalate to Manager immediately |

### Manual Escalation

Clerks can escalate an application to their Manager with a reason:
- "Need guidance on processing"
- "Outside my competence area"
- "Citizen is unresponsive to revision requests"
- Custom reason (free text)

Escalated applications appear in the Manager's priority queue with the escalation reason.

## Application History

Every state change is recorded:

```json
{
  "application_id": "APP-2026-000142",
  "history": [
    {
      "from_state": "SUBMITTED",
      "to_state": "AWAITING_PAYMENT",
      "timestamp": "2026-03-15T10:30:00Z",
      "actor": "SYSTEM",
      "note": "Fee: 15.00 BGN (expedited certificate of birth)"
    },
    {
      "from_state": "AWAITING_PAYMENT",
      "to_state": "IN_PROCESSING",
      "timestamp": "2026-03-15T10:35:00Z",
      "actor": "SYSTEM",
      "note": "Payment confirmed: card ending 4532, ref: TXN-2026-A8F3"
    },
    {
      "from_state": "IN_PROCESSING",
      "to_state": "RETURNED_FOR_REVISION",
      "timestamp": "2026-03-16T14:20:00Z",
      "actor": "clerk.ivanova@mrrb.government.bg",
      "note": "Missing: birth certificate of parent. Please upload a certified copy."
    }
  ]
}
```

History is visible to:
- The assigned clerk and their manager
- The citizen (filtered view: no internal notes, only status changes and requests directed at them)
- Auditors (full view)

## Batch Operations

Managers can perform batch operations on multiple applications:

- **Batch approve**: Select multiple applications → confirm → all approved (each generates separate audit record)
- **Batch reassign**: Select applications → choose new clerk → confirm
- **Batch return for revision**: Select applications → enter common revision message → confirm

Maximum applications per batch operation: 50.

Applications in different states cannot be batch-operated together (e.g., cannot batch approve an application that is in RETURNED_FOR_REVISION state).
