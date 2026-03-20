# Notification System

## Overview

The system sends notifications to citizens and internal users through multiple channels. Notifications are triggered by application events, system events, and scheduled reminders. Citizens control their notification preferences.

## Channels

### Email
- Sender: no-reply@egovportal.bg
- HTML emails with plain-text fallback
- Maximum email size including attachments: 25 MB
- Attachments: only system-generated PDFs (receipts, certificates). Never citizen-uploaded documents.
- All emails include an unsubscribe link for optional notifications. Mandatory notifications (e.g., password reset, application status) cannot be unsubscribed.

### SMS
- Maximum length: 160 characters (Latin) or 70 characters (Cyrillic)
- If the message exceeds the limit, it is truncated with "..." and a link to the full notification in the portal
- SMS is sent through the contracted provider API (currently Nexmo/Vonage)
- Delivery reports are tracked: SENT, DELIVERED, FAILED, EXPIRED

### In-App Notification
- Displayed in the notification bell icon in the portal header
- Badge shows unread count (max displayed: 99+)
- Notifications are stored for 90 days, then archived
- Clicking a notification marks it as read and navigates to the relevant page
- "Mark all as read" button available
- Real-time delivery via WebSocket connection. If WebSocket is disconnected, polling every 30 seconds as fallback.

### Push Notification (Mobile App)
- Available for citizens who have installed the mobile app and granted push permission
- Uses Firebase Cloud Messaging (FCM) for Android and Apple Push Notification Service (APNs) for iOS
- Push notifications include: title (max 50 chars), body (max 200 chars), and a deep link to the relevant screen

## Notification Types and Triggers

| Event | Channels | Priority | Template |
|---|---|---|---|
| Application submitted | Email + In-App | Normal | application_submitted |
| Application status change | Email + In-App + Push | Normal | application_status_changed |
| Payment received | Email + SMS + In-App | High | payment_received |
| Payment failed | Email + SMS + In-App | High | payment_failed |
| Document ready for download | Email + In-App + Push | Normal | document_ready |
| Identity verification reminder | Email + SMS | Normal | verification_reminder |
| Password reset | Email only | Critical | password_reset |
| Account locked | Email only | Critical | account_locked |
| Scheduled maintenance | Email + In-App + Push | Low | system_maintenance |
| Application deadline approaching | Email + SMS + In-App | High | deadline_warning |

## Notification Preferences

Citizens can configure preferences per channel:

- **Email**: On/Off per notification category (mandatory categories cannot be turned off)
- **SMS**: On/Off globally (when off, no SMS sent except for OTP verification codes)
- **Push**: On/Off globally + quiet hours (e.g., no push between 22:00 and 08:00)
- **In-App**: Always on, cannot be disabled

Default preferences for new accounts: all channels enabled, no quiet hours.

Preference changes take effect immediately. Notifications already queued before the preference change are still sent.

## Templates

Notification templates support:
- **Localization**: Bulgarian (default) and English. The citizen's language preference determines which version is sent.
- **Placeholders**: `{{citizen.firstName}}`, `{{application.referenceNumber}}`, `{{application.serviceName}}`, `{{application.status}}`, `{{payment.amount}}`, `{{system.portalUrl}}`
- **Conditional blocks**: `{{#if payment.isRefund}}Refund processed{{/if}}`
- **Date formatting**: `{{application.submittedAt | format:"DD.MM.YYYY HH:mm"}}`

Templates are versioned. The template version used is recorded with each sent notification for audit purposes.

If a placeholder value is missing at send time, the placeholder is replaced with an empty string and a warning is logged. The notification is still sent.

## Retry Logic

Failed notifications are retried with exponential backoff:

| Attempt | Delay | Notes |
|---|---|---|
| 1st retry | 1 minute | - |
| 2nd retry | 5 minutes | - |
| 3rd retry | 30 minutes | - |
| 4th retry | 2 hours | - |
| 5th retry | 12 hours | Final attempt |

After 5 failed retries:
- Email: Marked as FAILED, no further retries. The citizen's email is flagged for verification.
- SMS: Marked as FAILED. If 3 consecutive SMS to the same number fail, the number is flagged as invalid.
- Push: Marked as FAILED. If the push token is invalid (HTTP 410 from FCM/APNs), the token is removed.

## Rate Limiting

To prevent notification flooding:
- Maximum 10 emails per hour per citizen
- Maximum 5 SMS per hour per citizen
- Maximum 20 push notifications per hour per citizen
- Maximum 50 in-app notifications per day per citizen

When rate limits are reached, non-critical notifications are queued and delivered in the next available window. Critical notifications (password reset, account locked) bypass rate limits.

## Do Not Disturb

Citizens can enable "Do Not Disturb" mode:
- Suppresses all non-critical push and SMS notifications
- Email and in-app notifications are still delivered but without sound/vibration on mobile
- DND can be scheduled (e.g., weekdays 22:00-08:00, weekends all day)
- Critical notifications always bypass DND
