# Payment Processing

## Overview

Citizens pay fees for government services through the portal. The system integrates with the national payment gateway (ePay.bg) and supports multiple payment methods. All financial transactions are immutable and fully audited.

## Fee Structure

Each service has a configured fee schedule:

| Service Type | Standard Fee | Expedited Fee | Waiver Conditions |
|---|---|---|---|
| Certificate of Birth | 5.00 BGN | 15.00 BGN | Free for citizens over 70 |
| Certificate of Marital Status | 5.00 BGN | 15.00 BGN | None |
| Address Registration | 3.00 BGN | 10.00 BGN | Free for disabled persons (TELK ≥ 50%) |
| Criminal Record Certificate | 5.00 BGN | 20.00 BGN | Free for job applications at state institutions |
| Building Permit Application | 50.00 BGN + 2.00 BGN/m² | Not available | None |

Fee amounts are configured by administrators and can change. When a fee changes, applications already submitted retain their original fee. New applications use the new fee from the moment of publication.

## Payment Methods

### Bank Card (Visa/Mastercard)
- Processed through ePay.bg payment gateway
- 3D Secure is mandatory for all transactions
- Minimum transaction: 0.50 BGN
- Maximum single transaction: 10,000 BGN
- The system stores only the last 4 digits of the card number and the transaction reference — never the full card number

### Bank Transfer
- The system generates a unique payment reference (PR-YYYYMMDD-XXXXXX) for each transaction
- Payment is matched automatically when the bank transfer arrives with the correct reference
- If the transfer amount does not match exactly, the payment is flagged for manual review
- Bank transfers must be received within 7 calendar days or the application is cancelled
- Partial payments via bank transfer are not accepted — the full amount must be paid in one transfer

### Cash Payment at Government Office
- Available only for in-person services
- The clerk enters the payment confirmation manually
- A receipt number from the fiscal device is required
- Cash payments above 5,000 BGN are not accepted (anti-money laundering regulation)

### ePay / EasyPay Voucher
- The system generates a payment code valid for 48 hours
- The citizen pays at any EasyPay terminal or through ePay.bg
- Notification of payment is received via callback within 1-5 minutes

## Payment Flow

1. Citizen submits a service application
2. System calculates the fee (base + extras if applicable)
3. If a fee waiver applies, the fee is automatically set to 0.00 BGN and the citizen proceeds without payment. The waiver reason is logged.
4. Citizen selects payment method
5. For card payments: redirect to ePay.bg → 3D Secure → callback to system
6. For bank transfer: payment reference displayed with bank details, email sent with same info
7. For voucher: payment code displayed and emailed
8. Upon successful payment confirmation:
   - Payment status changes to PAID
   - Application status changes to IN_PROCESSING
   - Receipt is generated (PDF) and emailed to the citizen
   - Receipt is also available for download from the citizen's dashboard

## Refunds

Refunds are processed when:
- An application is rejected by the administration
- A service is cancelled before processing begins (citizen-initiated, within 24 hours of payment)
- A duplicate payment was made

Refund rules:
- Card payments: Refund is processed back to the original card. Processing time: 5-10 business days.
- Bank transfer: Refund is processed to the citizen's bank account (IBAN must be provided). Processing time: 3-5 business days.
- Cash: Refund is only available in person at the government office. The citizen must present their ID and the original receipt.
- Voucher payments: Refund is issued as a credit note valid for 12 months, usable for any future service.

Partial refunds are supported (e.g., if only the expedited fee portion is refunded but the base fee is retained).

The refund amount can never exceed the original payment amount. If the fee was reduced between payment and refund, the original paid amount is refunded.

## Receipts

Every payment generates a receipt containing:
- Receipt number (unique, sequential per calendar year: R-2026-000001)
- Date and time of payment
- Citizen name and EGN (masked: first 6 digits shown, last 4 replaced with ****)
- Service name and application reference number
- Fee breakdown (base fee, expedited surcharge, waiver discount)
- Payment method (card ending in XXXX / bank transfer ref / cash receipt / voucher code)
- Total amount paid
- Digital signature of the system (qualified electronic seal)

Receipts are generated as PDF/A-2b for long-term archival. They are stored for 10 years.

## Currency

All amounts are in Bulgarian Lev (BGN). The system does not support multi-currency. Input fields for amounts accept exactly 2 decimal places. Amounts are displayed with the Bulgarian locale format: 1 234,56 лв.

## Timeout and Error Handling

- If the ePay.bg gateway does not respond within 30 seconds, the transaction is marked as PENDING and the citizen is shown: "Payment is being processed. You will receive a confirmation email within 15 minutes."
- If the callback is not received within 15 minutes, the system queries ePay.bg for status
- If ePay.bg confirms failure, the application returns to "Awaiting Payment" status
- The citizen can retry payment up to 3 times for the same application
- After 3 failed payment attempts, the application is locked for 1 hour with a message: "Multiple payment attempts detected. Please try again later or contact support."
