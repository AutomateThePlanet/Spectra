# Checkout Flow

## Overview

Users can complete purchases through our checkout flow.

## Happy Path

1. User adds items to cart
2. User clicks "Checkout"
3. User enters shipping address
4. User selects payment method
5. User reviews order
6. User clicks "Place Order"
7. Order confirmation displayed

## Payment Methods

- Visa, Mastercard, Amex
- PayPal
- Apple Pay

## Error Handling

- Invalid card: Show "Card declined" message
- Expired card: Show "Card expired" message
- Insufficient funds: Show "Payment failed" message

## Edge Cases

- Empty cart: Disable checkout button
- Address validation failure: Show address suggestions
- Network timeout: Retry with exponential backoff
