# Selected Claude reference set — 2026-10-20

**Owner-selected Design M1 direction.** Use the [implementation handoff](../implementation-handoff.md) and [decision](../../../docs/decisions.md) before building. This folder contains static references and measurements only; the executable prototype stays outside the checkout.

The prototype was run unchanged and captured across **all 33 screen paths**. There are **62 screenshots**: default views plus selected light/dark, phone, dialog, focus, existing-plan, full-review and simulated-state examples. Not every state on every screen was exercised.

## Start with these views

| Pattern | Light | Dark / adaptation |
|---|---|---|
| Manager queue | [Desktop](queue-desktop-light.jpg) | [Dark](queue-desktop-dark.jpg) |
| Ledger and filters | [Desktop](transactions-desktop-light.jpg) | [Dark](transactions-desktop-dark.jpg), [phone](transactions-mobile-dark.jpg) |
| Operator next action | [Desktop](cockpit-desktop-light.jpg) | [Dark](cockpit-desktop-dark.jpg), [phone defect](cockpit-mobile-dark.jpg) |
| Recording-Member count | [Header/preview](close-day-member-desktop-light.jpg), [focused table](count-focus-desktop-light.jpg) | [Dark table](count-focus-desktop-dark.jpg), [phone](count-focus-mobile-light.jpg) |
| Entry form | [Light](transaction-create-desktop-light.jpg) | [Dark](transaction-create-desktop-dark.jpg), [phone](transaction-create-mobile-light.jpg) |
| Navigation | [Session menu](session-menu-desktop-light.jpg) | [Phone drawer](navigation-mobile-light.jpg) |
| Dialog | [New client](client-dialog-desktop-light.jpg) | No form submitted |
| Loading / empty / error | [Loading](queue-loading.jpg), [empty](queue-empty.jpg), [error](queue-error.jpg) | Statebar simulations only |
| Extended detail | [Approval](approval-complete-light.jpg), [correction](correction-history-complete-light.jpg), [existing cheque plan](installment-existing-desktop-light.jpg) | Full-page capture requests |

## Scale and evidence

Requested desktop viewport: 1440 × 1000. Requested phone viewport: 375 × 812. Returned raster sizes are recorded individually; the browser scrollbar reduces the captured area. Default desktop preference is **150%**, not authored 100%. [Compare the 100% queue](queue-desktop-100pct.jpg). On a phone the prototype clamps zoom to 100% while retaining the 150% label. The handoff explains why production must replace that mechanism.

The fake business date is 24/09/2026. These are not the October 19 real API captures. The fixed statebar, demo identity UI, source/gap annotations and artificial clock are prototype chrome and must not appear in production. Some labels/behaviors predate shipped contracts. The mobile Cockpit defect, header crowding and contrast failures are adaptations, not selected patterns to reproduce.

## Screen coverage

| Screen | Reference | Available captures |
|---|---|---|
| account-assignment | [View](account-assignment-desktop-light.jpg) | 1 |
| accounts | [View](accounts-desktop-light.jpg) | 1 |
| branch-members | [View](branch-members-desktop-light.jpg) | 2 |
| branch-picker | [View](branch-picker-desktop-light.jpg) | 1 |
| cash-variance-summary | [View](cash-variance-summary-desktop-light.jpg) | 1 |
| categories-types | [View](categories-types-desktop-light.jpg) | 1 |
| client-statement | [View](client-statement-desktop-light.jpg) | 1 |
| clients | [View](clients-desktop-light.jpg) | 2 |
| clock | [View](clock-desktop-light.jpg) | 2 |
| close-approval | [View](close-approval-desktop-light.jpg) | 2 |
| close-day | [View](close-day-member-desktop-light.jpg) | 7 |
| cockpit | [View](cockpit-desktop-light.jpg) | 5 |
| daily-ledger | [View](daily-ledger-desktop-light.jpg) | 1 |
| fiado-aging | [View](fiado-aging-desktop-light.jpg) | 1 |
| fiado-balance | [View](fiado-balance-desktop-light.jpg) | 1 |
| fix-resubmit | [View](fix-resubmit-desktop-light.jpg) | 2 |
| holidays | [View](holidays-desktop-light.jpg) | 1 |
| installment-plan | [View](installment-plan-desktop-light.jpg) | 2 |
| login | [View](login-desktop-light.jpg) | 1 |
| monthly-reconciliation | [View](monthly-reconciliation-desktop-light.jpg) | 1 |
| my-time-entry-balance | [View](my-time-entry-balance-desktop-light.jpg) | 1 |
| my-transaction-summary | [View](my-transaction-summary-desktop-light.jpg) | 1 |
| open-cheque-aging | [View](open-cheque-aging-desktop-light.jpg) | 1 |
| open-day | [View](open-day-desktop-light.jpg) | 1 |
| operators | [View](operators-desktop-light.jpg) | 1 |
| products | [View](products-desktop-light.jpg) | 1 |
| receber-pagamento | [View](receber-pagamento-desktop-light.jpg) | 1 |
| settings | [View](settings-desktop-light.jpg) | 1 |
| time-entry-management | [View](time-entry-management-desktop-light.jpg) | 1 |
| transaction-create | [View](transaction-create-desktop-light.jpg) | 3 |
| transaction-edit | [View](transaction-edit-desktop-light.jpg) | 1 |
| transactions-list | [View](transactions-desktop-light.jpg) | 3 |
| work-queue | [View](queue-desktop-light.jpg) | 10 |

## Supporting records

- [Manifest](manifest.json): source build and 111 source hashes, exact screenshot hashes/dimensions/context and 33-screen coverage.
- [Style inventory](style-inventory.json): observed light/dark custom properties, authored media rules and limited contrast calculations. These are not M2 tokens.
- [Browser observations](browser-observations.json): sampled computed styles, headings, table dimensions and state-control inventory. Rendered rectangle sizes and authored CSS values differ when zoom is applied.

No source file, financial count or transaction was changed. An isolated browser origin kept this study separate from the owner's prototype data. No independent review, native-device acceptance or complete accessibility/functional certification is claimed.
