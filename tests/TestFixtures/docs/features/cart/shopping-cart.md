# Shopping Cart

## Overview

The shopping cart allows users to collect items before checkout.

## Adding Items

1. User clicks "Add to Cart" on product page
2. System validates stock availability
3. Item is added with quantity 1
4. Cart icon updates with item count
5. Mini-cart dropdown shows confirmation

## Cart Operations

### Update Quantity
- Minimum quantity: 1
- Maximum quantity: 99 or available stock
- Quantity 0 removes item

### Remove Item
- Click "Remove" link
- Confirm removal (optional setting)
- Item is removed immediately

### Save for Later
- Move item to "Saved Items" list
- Item remains until explicitly removed
- Can move back to cart

## Cart Persistence

- Logged-in users: Cart synced to account
- Guest users: Cart stored in session/localStorage
- Carts expire after 30 days of inactivity

## Price Updates

- Prices update in real-time
- Stock availability refreshes on page load
- Out-of-stock items flagged with warning

## Promotions

- Coupon codes apply at cart level
- Automatic discounts apply when conditions met
- Only one coupon code per order
- Discount shown as separate line item

## Cart Summary

- Subtotal (before tax/shipping)
- Estimated tax
- Estimated shipping (based on default address)
- Total

## Error Handling

- Item out of stock: Show warning, allow removal
- Price changed: Show notification with old/new price
- Maximum quantity exceeded: Cap at maximum
- Cart session expired: Attempt recovery from account
