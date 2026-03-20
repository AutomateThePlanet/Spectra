# GDPR and Data Privacy Compliance

## Overview

The system processes personal data of Bulgarian and EU citizens. It must comply with the General Data Protection Regulation (GDPR), the Bulgarian Personal Data Protection Act (ЗЗЛД), and the eGovernment Act (ЗЕУ). This document defines the technical implementation of privacy requirements.

## Data Classification

All data fields are classified:

| Classification | Description | Examples | Handling |
|---|---|---|---|
| **Public** | Information that is publicly available | Service descriptions, fee schedules, office addresses | No restrictions |
| **Internal** | Operational data not containing personal information | Application counts, processing statistics, system configuration | Access restricted to authenticated internal users |
| **Personal** | Data that identifies or can identify a natural person | Name, EGN, address, email, phone number | GDPR rules apply, access logged |
| **Sensitive** | Special categories under GDPR Article 9 | Health data (disability certificates for fee waivers), criminal records | Additional safeguards, explicit consent required, access strictly logged |

## Lawful Basis for Processing

| Processing Activity | Lawful Basis | Notes |
|---|---|---|
| Application processing | Legal obligation (eGovernment Act) | No consent needed |
| Identity verification | Legal obligation | No consent needed |
| Payment processing | Contract performance | Necessary to fulfill service request |
| Sending mandatory notifications | Legal obligation | Cannot be opted out |
| Sending marketing/info notifications | Consent | Can be withdrawn at any time |
| Analytics and statistics | Legitimate interest (anonymized) | No personal data in reports |
| Audit logging | Legal obligation (ЗЗЛД) | No consent needed, cannot be opted out |

## Data Subject Rights

### Right of Access (Article 15)

Citizens can request a copy of all personal data the system holds about them:

1. Citizen clicks "My Data" in the portal settings
2. System compiles: profile data, application history, documents, payment history, notification history, audit log entries about the citizen, consent records
3. Data is packaged as a structured JSON file + a human-readable PDF summary
4. Available for download within 30 seconds for datasets up to 100 MB
5. For larger datasets, the citizen receives an email notification when the export is ready (maximum processing time: 24 hours)
6. The export file is available for download for 7 days, then automatically deleted
7. Export is encrypted with a password that the citizen sets before starting the export

The export does NOT include:
- Internal notes by clerks about the citizen's application (these are considered internal operational data)
- Audit log entries about system events not directly related to the citizen
- Data that has been anonymized and cannot be re-identified

### Right to Rectification (Article 16)

Citizens can update their personal information through the portal:

- **Self-service changes**: Name (requires supporting document upload), email (requires re-verification), phone (requires re-verification), address, contact preferences
- **Clerk-assisted changes**: EGN correction (requires official document from civil registry), date of birth correction
- Changes to verified identity data (name, EGN, date of birth) require administrator approval and are logged as a separate audit event
- Previous values are retained in the version history for audit purposes — rectification does not delete historical records

### Right to Erasure (Article 17)

Citizens can request account deletion:

1. Citizen submits deletion request through the portal or by email to the Data Protection Officer
2. System checks for active applications. If any exist: "Your account cannot be deleted while you have active applications. Please wait for them to be completed or cancel them first."
3. If no active applications:
   - Personal data is anonymized (not deleted — replaced with hashed pseudonyms): name → "Citizen_A3F7", EGN → hash, email → deleted, phone → deleted, address → deleted
   - Application records are retained with anonymized citizen data (legal obligation to maintain administrative records)
   - Documents uploaded by the citizen are permanently deleted
   - System-generated documents (certificates, decisions) are retained with anonymized citizen reference
   - Payment records are retained with anonymized citizen data (tax/accounting obligation: 10 years)
   - Audit log entries are retained with anonymized actor data
   - The login account is permanently disabled and the email is freed for re-registration
4. Processing time: within 30 days (GDPR requirement)
5. Citizen receives confirmation email to an alternative email address (since the account email is deleted)

Data that CANNOT be erased:
- Audit log entries (legal obligation)
- Financial records within the mandatory retention period
- Application decisions (administrative law requirement)
- Data subject to ongoing legal proceedings or investigations

### Right to Data Portability (Article 20)

Citizens can export their data in a machine-readable format:

- Format: JSON (structured) or CSV (tabular)
- Includes: profile data, application history, documents metadata (not document files themselves), payment history
- Same mechanism as Right of Access but with machine-readable format selected
- The export follows a published JSON schema that is versioned and documented

### Right to Object (Article 21)

Citizens can object to:
- Marketing communications: Instant opt-out, takes effect within 24 hours
- Profiling or automated decision-making: The system does not perform automated decision-making, so this is not applicable. If added in the future, an objection mechanism must be implemented before the feature goes live.

## Consent Management

### Consent Types

| Consent | Required | Withdrawable | Default |
|---|---|---|---|
| Terms of Service | Yes (for registration) | Yes (results in account deactivation) | Not accepted |
| Privacy Policy | Yes (for registration) | Yes (results in account deactivation) | Not accepted |
| Marketing Communications | No | Yes (at any time) | Not accepted |
| Analytics Cookies | No | Yes (at any time) | Not accepted |
| Third-party Service Integration | No | Yes (at any time) | Not accepted |

### Consent Records

Every consent action is recorded:
- Consent type
- Version of the document consented to (Terms v2.3, Privacy Policy v1.8)
- Timestamp of consent/withdrawal
- Method (checkbox click, banner acceptance, API call)
- IP address and user agent at the time of consent

Consent records are retained for 5 years after withdrawal, as proof of processing lawfulness during the consent period.

### Cookie Banner

On first visit (no existing consent), the portal displays a cookie banner:
- **Strictly Necessary Cookies**: Always active, cannot be disabled. Includes session cookies and CSRF tokens.
- **Analytics Cookies**: Off by default. Google Analytics or Matomo, anonymized IP.
- **Functional Cookies**: Off by default. Language preference, saved filters.

The banner includes "Accept All", "Reject All", and "Customize" buttons. "Customize" opens a detailed panel where each cookie category can be toggled individually.

Cookie preferences are stored in a first-party cookie (not in the user's profile) and in the consent management database if the user is authenticated.

## Data Processing Agreements

The system integrates with third-party processors:

| Processor | Data Shared | Purpose | DPA Status |
|---|---|---|---|
| ePay.bg | Transaction amount, citizen reference (not EGN) | Payment processing | Required before go-live |
| Nexmo/Vonage | Phone number, message content | SMS notifications | Required before go-live |
| Azure (Microsoft) | All data (hosting) | Infrastructure | Required before go-live |
| OCR Service Provider | Document images | Identity document extraction | Required before go-live |

Data shared with processors is minimized to what is strictly necessary. The citizen's EGN is never shared with payment processors.

## Data Breach Notification

If a personal data breach is detected:

1. **Detection**: Automated monitoring or manual report by staff
2. **Assessment** (within 4 hours): Scope (how many citizens affected), type (confidentiality/integrity/availability), risk level (low/medium/high/critical)
3. **Internal notification** (within 8 hours): DPO, System Admin, and management are notified
4. **Authority notification** (within 72 hours): If risk to citizens is medium or higher, notify the Commission for Personal Data Protection (КЗЛД) via their online portal
5. **Citizen notification** (without undue delay): If risk is high or critical, affected citizens are notified directly via email with: description of breach, likely consequences, measures taken, contact details of DPO
6. **Remediation**: Technical measures to prevent recurrence
7. **Documentation**: Full incident report stored for 5 years

The system maintains a breach register (accessible only to DPO and System Admin) with all detected breaches, whether or not they were reported to the authority.

## Privacy by Design

Technical measures implemented:

- **Data minimization**: Forms collect only necessary fields. Optional fields are clearly marked.
- **Pseudonymization**: Internal processing uses citizen reference IDs instead of EGN where possible
- **Encryption at rest**: AES-256 for all personal data in the database. Encryption keys stored in Azure Key Vault.
- **Encryption in transit**: TLS 1.2+ for all connections. HSTS enabled.
- **Access logging**: Every access to personal data is logged (see Audit Log document)
- **Automatic anonymization**: Test environments use anonymized data generated from production schemas, never copies of production data
- **Data masking**: EGN is displayed as ******XXXX in all UI components except the citizen's own profile. Full EGN visible only to users with explicit `citizens.view_full_egn` permission.
