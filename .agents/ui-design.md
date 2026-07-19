# UI Design Guide

## 1. Product Direction

Design the project as a practical logistics operations system, not a marketing-heavy website. The UI should feel clear, trustworthy, fast to scan and easy to operate for shop users, admin users, operators, shippers and guests using tracking pages.

Core qualities: clean, operational, structured, readable, lightweight, modern.

Primary UX goals: help users create shipments quickly, track orders clearly, manage COD and statuses reliably, and move between workflows with minimal friction.

## 2. Visual Style

Use a white and sea-blue visual system with restrained contrast and low visual noise. The interface should look like a logistics service platform with real operational purpose, not a decorative landing page or generic startup template.

General style rules: use flat or lightly elevated surfaces, calm shadows, clear borders, consistent spacing, concise section headers and minimal ornament.

For FullHD desktop layouts, prioritize stable composition and avoid unnecessary vertical or horizontal scrolling in the first screen whenever the workflow allows it.

## 3. Color System

Primary colors: `#FFFFFF` White, `#0077C8` Sea Blue, `#005B9F` Deep Sea Blue, `#EAF7FF` Ice Blue, `#063B63` Navy Text.

Supporting colors: `#F7FBFF` Light Background, `#DDECF7` Border, `#5F7285` Muted Text, `#16A34A` Success, `#F59E0B` Warning, `#DC2626` Danger.

Usage rules: white is the main surface color, sea blue is the main brand and action color, navy is for headings and high-emphasis text, muted blue-gray is for supporting text, and semantic colors are reserved for statuses, alerts and validation.

Avoid large areas of saturated color except for compact emphasis zones such as CTA buttons, badges, active tabs or summary panels.

## 4. Typography

Prefer a readable Vietnamese-friendly sans-serif system. Recommended pairing: `Be Vietnam Pro` for headings and primary labels, `Inter` for body, forms, tables and dense UI. If a single-family system is preferred, `Be Vietnam Pro` alone is acceptable.

Suggested sizes: page title `32px - 40px`, section title `24px - 32px`, card title `18px - 20px`, body text `14px - 16px`, helper text `12px - 13px`.

Typography rules: keep heading count low, use strong hierarchy, avoid excessive font-size variation, avoid decorative type, and optimize for scan speed over visual drama.

## 5. Layout Principles

Build layouts around clear workflow blocks: header, page title, action area, filters, content surface and supporting information. Prefer simple grid or split-column structures and keep content aligned to a stable container width.

Recommended spacing: page section spacing `48px - 72px`, card padding `20px - 24px`, panel padding `24px - 32px`, control height `40px - 52px`, border radius `12px - 16px`.

Use wide layouts carefully: on desktop, make space feel organized rather than empty; on tablet and mobile, stack content in a clear reading order.

## 6. Navigation

Global navigation should be simple and role-aware. Guests should only see key entry points such as home, tracking and authentication; authenticated users should see workflow-focused navigation grouped by business purpose.

Navigation rules: keep labels short, highlight the active item clearly, avoid deep nesting, and keep top-level actions discoverable within one glance.

For app-style pages, side navigation is appropriate. For public or guest-facing pages, use a compact top navigation.

## 7. Surfaces and Cards

Use cards and panels for grouped information, quick actions, summaries, service explanations, forms and filtered result blocks. Card style should use white background, light border, subtle shadow and clear internal spacing.

Cards should support dense information without becoming cramped. Favor short headings, one supporting sentence or metric, and one obvious action where relevant.

Avoid card overload, nested decorative cards and oversized empty cards.

## 8. Buttons and Actions

Primary buttons use sea blue fill with white text. Secondary buttons use white or transparent background with blue border and blue text. Tertiary actions can appear as text links where visual weight should stay low.

Action rules: each screen should have one obvious primary action, secondary actions should not compete with it, and destructive actions should be visually distinct and used sparingly.

Buttons must have stable dimensions and should not resize due to changing labels, counts or loading states.

## 9. Forms

Forms should be straightforward, sectioned by real business meaning and optimized for completion speed. Use clear labels, short helper text, inline validation and sensible grouping.

Typical shipment-related groups: sender information, receiver information, package details, COD and fee details, note or special instructions.

Form rules: avoid very wide single-column fields on desktop, keep related fields close together, show validation below the field, and reserve modal forms for short tasks only.

## 10. Tables and Lists

Operational screens should rely heavily on tables, filtered lists and status-oriented views. Tables must support search, status filters, date filters, pagination and row-level actions where needed.

Table rules: keep rows compact but readable, pin the most important columns conceptually, use badges for statuses, avoid exposing too many secondary columns at once, and make action placement predictable.

When content becomes too dense for cards, switch to table or list layouts rather than forcing more detail into visual tiles.

## 11. Status and Feedback

Status design must be consistent across the project. Use text labels with colored badges; never communicate state by color alone.

Recommended shipment status palette: `PendingPickup` Blue, `Assigned` Indigo, `PickingUp` Sky, `PickedUp` Cyan, `InTransit` Purple-Blue, `Delivering` Amber, `Delivered` Green, `DeliveryFailed` Red, `Returned` Slate, `Cancelled` Gray.

System feedback should be immediate and quiet: success confirmations, validation messages, loading states and empty states should be informative without becoming noisy.

## 12. Dashboard Patterns

Dashboard pages should feel operational first. Use summary cards, filter bars, status distributions, recent activity blocks, compact charts only when useful, and clear primary tasks for the current role.

Role emphasis:
- Shop dashboard focuses on shipment volume, pending work, COD and recent orders.
- Admin dashboard focuses on users, system activity, control and reporting.
- Operator dashboard focuses on assignment, routing, throughput and exception handling.
- Shipper dashboard focuses on assigned pickups, deliveries and completion flow.

## 13. Public Pages

Public-facing pages such as landing, login and tracking should still follow the same design language but stay lighter and more direct than dashboard screens.

Public page rules: bring core actions forward early, keep the first screen useful, avoid oversized hero sections, and use imagery only when it supports logistics context clearly.

Do not let public pages drift into a separate visual language that conflicts with the app UI.

## 14. Icons and Imagery

Use clean logistics-relevant icons and images: package, route, warehouse, vehicle, delivery, COD, dashboard, service point. Prefer consistent SVG icon sets and bright operational imagery over abstract decorative art.

Image rules: use images to support context, not to dominate layout; keep backgrounds bright; avoid dark cinematic visuals, low-quality stock photos and inconsistent icon styles.

## 15. Responsive Behavior

Design desktop first, then adapt to tablet and mobile by reducing columns, stacking sections and preserving action clarity.

Responsive rules: header collapses cleanly, filters wrap predictably, cards become one column when needed, tables degrade into stacked list patterns only when truly necessary, and primary actions remain visible without overlap.

Text, controls and cards must not overflow or collide at common laptop and mobile sizes.

## 16. Interaction Style

Animations should be minimal and purposeful. Use small transitions for hover, active, expand/collapse and loading states only when they improve clarity.

Avoid flashy motion, decorative parallax and unnecessary animated elements in operational screens.

## 17. Anti-patterns

Avoid cluttered screens, overly large hero areas, decorative gradients that reduce readability, too many competing CTA buttons, inconsistent spacing, low-contrast text, card-in-card layouts, logic-heavy UI components and direct data access from presentation components.

Do not design the product like a generic marketing site when the actual value is workflow efficiency.
