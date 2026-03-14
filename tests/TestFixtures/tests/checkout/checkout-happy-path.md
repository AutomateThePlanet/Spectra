---
id: TC-101
priority: high
tags: [smoke, payments]
component: checkout
source_refs: [docs/features/checkout/checkout-flow.md]
---

# Checkout with valid Visa card

## Preconditions

- User is logged in
- Cart contains at least one item

## Steps

1. Navigate to checkout
2. Enter valid shipping address
3. Select Visa as payment method
4. Enter valid card details
5. Click "Place Order"

## Expected Result

- Order is created successfully
- Order confirmation page is displayed
- Confirmation email is sent to user

## Test Data

- Card number: 4111 1111 1111 1111
- Expiry: 12/2028
- CVV: 123
