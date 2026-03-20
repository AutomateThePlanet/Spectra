# Reporting and Analytics

## Overview

The system provides operational and analytical reports for administrators, managers, and auditors. Reports are available through an interactive dashboard and as scheduled exports.

## Dashboard

The main dashboard shows real-time operational metrics:

### KPI Cards (top row)
- **Applications today**: Count of applications submitted in the last 24 hours, with % change vs. previous day
- **Pending processing**: Count of applications in IN_PROCESSING status older than SLA threshold
- **Average processing time**: Mean time from submission to completion, in business days, for the current month
- **Revenue today**: Total payments received in the last 24 hours, in BGN

KPI cards are color-coded:
- Green: metric is within acceptable range
- Yellow: metric is approaching the warning threshold (configurable per KPI)
- Red: metric has exceeded the critical threshold

### Charts

1. **Applications by status** (donut chart): Shows distribution across all statuses. Clicking a segment filters the applications list below.
2. **Applications over time** (line chart): Daily count for the last 30 days. Toggle between submitted/completed/rejected. Hover shows exact count and date.
3. **Top services** (horizontal bar chart): Top 10 services by application count in the selected period.
4. **Processing time distribution** (histogram): Buckets of 1 day. Shows how many applications were processed in 0-1 days, 1-2 days, etc. Red line shows SLA target.
5. **Revenue by payment method** (stacked bar chart): Monthly breakdown by card/transfer/cash/voucher for the last 12 months.

### Filters

All dashboard components respond to global filters:
- Date range (preset: today, this week, this month, this quarter, this year, custom range)
- Service type (multi-select)
- Region (multi-select, based on citizen's address)
- Clerk/processor (multi-select, for managers)

Filters are preserved in the URL query string so that dashboard views can be bookmarked and shared.

## Standard Reports

### Application Status Report
- Lists all applications with current status, submission date, assigned processor, and SLA compliance
- Groupable by: service type, status, region, processor
- Exportable as: CSV, XLSX, PDF
- Maximum rows in export: 100,000 (for larger datasets, the user must narrow the date range)

### SLA Compliance Report
- For each service type: total applications, % completed within SLA, average processing time, min/max processing time
- Drill-down: clicking a service shows individual applications that missed SLA
- SLA thresholds are configurable per service (default: 14 business days)
- Business days exclude Bulgarian public holidays (loaded from a configurable calendar)

### Financial Report
- Revenue by period (daily, weekly, monthly)
- Breakdown by service type and payment method
- Refund summary: count, total amount, average refund processing time
- Reconciliation: system totals vs. bank statement import (CSV upload)
- Discrepancies are highlighted and flagged for manual review

### User Activity Report
- For administrators: login history, actions performed, documents accessed
- Filterable by user, action type, date range
- Exportable as CSV only (no PDF due to potentially large size)
- Data retained for 2 years

### Citizen Statistics Report
- New registrations per period
- Active vs. inactive citizens (active = at least one login in the last 90 days)
- Registration completion rate (started vs. completed wizard)
- Demographics: age distribution, region distribution (anonymized, no individual data)

## Scheduled Reports

Administrators can configure scheduled report delivery:

- **Frequency**: Daily (at 07:00), Weekly (Monday 07:00), Monthly (1st of month 07:00), Quarterly
- **Format**: PDF or XLSX
- **Delivery**: Email to specified recipients (up to 10 email addresses)
- **Filters**: Same as interactive filters, configured at schedule creation
- Reports larger than 25 MB are not emailed — instead, a download link is sent (valid for 7 days)

Scheduled reports run in a background queue. If a report fails to generate (e.g., database timeout), it retries once after 30 minutes. If it fails again, an alert email is sent to the system administrator.

## Access Control

| Role | Dashboard | Standard Reports | Financial Report | User Activity | Scheduled Reports |
|---|---|---|---|---|---|
| Clerk | Own workload only | Own applications | No | No | No |
| Manager | Department-wide | Department-wide | Department-wide | Department-wide | Can create |
| Director | All regions | All | All | All | Can create |
| Auditor | Read-only, all | Read-only, all | Read-only, all | Read-only, all | No |
| System Admin | All | All | All | All | Can create + manage all |

Attempting to access a report beyond one's role returns HTTP 403 and logs the attempt.

## Performance Requirements

- Dashboard must load within 3 seconds for the default date range (current month)
- Reports with up to 10,000 rows must generate within 10 seconds
- Reports with 10,000-100,000 rows must generate within 60 seconds
- Scheduled reports can take up to 5 minutes without triggering timeout
- All queries use pre-aggregated materialized views, refreshed every 15 minutes
