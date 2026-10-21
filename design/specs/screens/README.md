# Lotero screen blueprints

> **Status:** Design M4, authored 2026-10-20 (D04) for every currently unblocked feature; independent review pending. Blueprints are written just ahead of the matching Web/Mobile feature milestone and mature with the apps; M4 stays open.

A blueprint turns a catalog entry into a layout: the order of regions, how they rearrange from a wide desktop to a 375px phone, which [components](../components/README.md) and [tokens](../../tokens/README.md) carry them, where focus goes, which origin each journey returns to, and how every named state looks. It never changes what a screen does — the [catalog](../../docs/screens.md) does that — and it never moves a calculation or a permission decision into the client.

Blueprints are grouped by coherent feature rather than one file per screen, because a journey such as the operator's day crosses four screens that share a layout language.

## Screen → blueprint index

| Screen (catalog entry) | Blueprint | App milestone | Status |
|---|---|---|---|
| Login | [shell-and-identity.md](shell-and-identity.md#login) | Web/Mobile M4 (Google: M11) | Ready |
| Branch picker / session | [shell-and-identity.md](shell-and-identity.md#branch-picker--session) | Web/Mobile M4 | Ready |
| Session menu | [shell-and-identity.md](shell-and-identity.md#session-menu) | Web/Mobile M4 (account items: M11) | Ready |
| Profile | [shell-and-identity.md](shell-and-identity.md#profile) | Web/Mobile M11 | Ready |
| Change password | [shell-and-identity.md](shell-and-identity.md#change-password) | Web/Mobile M11 | Ready |
| Forgot password | [shell-and-identity.md](shell-and-identity.md#forgot-password) | Web/Mobile M11 | Ready |
| Reset password | [shell-and-identity.md](shell-and-identity.md#reset-password) | Web/Mobile M11 | Ready |
| Invitation | [shell-and-identity.md](shell-and-identity.md#invitation) | Web/Mobile M11 | Ready |
| Plataforma — Nova lotérica | [onboarding-and-employees.md](onboarding-and-employees.md#plataforma--nova-lotérica) | Web/Mobile M11 | Partial — onboarding list (gap AB) |
| Manager Work Queue | [queue-and-review.md](queue-and-review.md#manager-work-queue) | Web/Mobile M7 (setup checklist: M11) | Ready |
| Daily-close approval | [queue-and-review.md](queue-and-review.md#daily-close-approval) | Web/Mobile M7 | Ready |
| Transaction — Create (fast entry) | [transactions.md](transactions.md#transaction--create-fast-entry) | Web/Mobile M6 | Ready |
| Transaction — Edit (correction / audit) | [transactions.md](transactions.md#transaction--edit-correction--audit) | Web/Mobile M6 | Ready |
| Transactions list | [transactions.md](transactions.md#transactions-list) | Web/Mobile M6 | Ready |
| Installment (pre-dated cheque) plan | [transactions.md](transactions.md#installment-pre-dated-cheque-plan) | Web/Mobile M6 | Ready |
| Client statement (Extrato de fiado) | [transactions.md](transactions.md#client-statement-extrato-de-fiado) | Web/Mobile M6 | Ready |
| Receber pagamento | [transactions.md](transactions.md#receber-pagamento) | Web/Mobile M6 | Ready |
| Daily ledger | [reports-and-month-lock.md](reports-and-month-lock.md#daily-ledger) | Web/Mobile M8 | Ready |
| Fiado balance | [reports-and-month-lock.md](reports-and-month-lock.md#fiado-balance) | Web/Mobile M8 | Ready |
| Fiado aging | [reports-and-month-lock.md](reports-and-month-lock.md#fiado-aging) | Web/Mobile M8 | Ready |
| Open-cheque aging | [reports-and-month-lock.md](reports-and-month-lock.md#open-cheque-aging) | Web/Mobile M8 | Ready |
| Cash-variance summary | [reports-and-month-lock.md](reports-and-month-lock.md#cash-variance-summary) | Web/Mobile M8 | Ready |
| Monthly reconciliation + lock | [reports-and-month-lock.md](reports-and-month-lock.md#monthly-reconciliation--lock) | Web/Mobile M8 | Ready |
| Dashboard | [reports-and-month-lock.md](reports-and-month-lock.md#dashboard) | Web/Mobile M8 | Not blueprinted — gap Q (M4 4.8.4) |
| Upcoming settlements | [reports-and-month-lock.md](reports-and-month-lock.md#upcoming-settlements) | Web/Mobile M8 | Not blueprinted — gap Q (M4 4.8.4) |
| Monthly financial report | [reports-and-month-lock.md](reports-and-month-lock.md#monthly-financial-report) | Web/Mobile M8 | Not blueprinted — gap Q (M4 4.8.4) |
| My transaction summary | [time-and-self-service.md](time-and-self-service.md#my-transaction-summary) | Web/Mobile M9 | Ready |
| My time-entry balance | [time-and-self-service.md](time-and-self-service.md#my-time-entry-balance) | Web/Mobile M9 | Ready |
| Operator Day Cockpit | [operator-day.md](operator-day.md#operator-day-cockpit) | Web/Mobile M5 | Ready |
| Open day | [operator-day.md](operator-day.md#open-day) | Web/Mobile M5 | Ready |
| Close day | [operator-day.md](operator-day.md#close-day) | Web/Mobile M5 | Partial — retained retired-product edit/preview/save (gap AA) |
| Fix & resubmit | [operator-day.md](operator-day.md#fix--resubmit) | Web/Mobile M5 | Partial — retained retired-product edit/preview/save (gap AA) |
| Clock in / out | [time-and-self-service.md](time-and-self-service.md#clock-in--out) | Web/Mobile M9 | Ready |
| Time-entry management | [time-and-self-service.md](time-and-self-service.md#time-entry-management) | Web/Mobile M9 | Ready |
| Operators (admin) | [administration.md](administration.md#operators-admin) | Web/Mobile M10 (link requests: M11) | Ready |
| Account assignment (admin) | [administration.md](administration.md#account-assignment-admin) | Web/Mobile M10 | Ready |
| Accounts (admin) | [administration.md](administration.md#accounts-admin) | Web/Mobile M10 | Ready |
| Clients (admin + counter) | [administration.md](administration.md#clients-admin--counter) | Web/Mobile M10 | Ready |
| Categories & Transaction Types (admin) | [administration.md](administration.md#categories--transaction-types-admin) | Web/Mobile M10 | Ready |
| Products (admin) | [administration.md](administration.md#products-admin) | Web/Mobile M10 | Partial — deactivation warning/recovery (gap AA) |
| Holidays (admin) | [administration.md](administration.md#holidays-admin) | Web/Mobile M10 | Ready |
| Settings (admin) | [administration.md](administration.md#settings-admin) | Web/Mobile M10 | Ready |
| Branch members (admin) | [administration.md](administration.md#branch-members-admin) | Web/Mobile M10 (invitations: M11) | Partial — withdrawing an invitation (gap AC) |
| Add employee (guided) | [onboarding-and-employees.md](onboarding-and-employees.md#add-employee-guided) | Web/Mobile M11 | Ready |
| Offboard employee (guided) | [onboarding-and-employees.md](onboarding-and-employees.md#offboard-employee-guided) | Web/Mobile M11 | Partial — withdrawing an invitation (gap AC) |

**Not blueprinted yet:** Dashboard, Upcoming settlements and the Monthly financial report now have catalog entries against shipped server M11 Phase 4–7 contracts (canonical v56), but those contracts await independent acceptance, so the M4 per-screen gate (gap Q) still holds. Their rows point at placeholder headings that say so; no layout, region order or component choice is defined for them until design M4 4.8.4.

## What every blueprint specifies

Region order and purpose; responsive arrangement at `breakpoint.wide`, `breakpoint.medium` and below, including 375px and enlarged text; components and the tokens that matter; focus order and the single primary action; exact origin and return; loading, empty, error, success, read-only, concurrent (stale) and uncertain states plus the catalog's named variants; and a **verification boundary** naming what can be checked statically and what waits for real web/native UI against the API.

`node scripts/check-design-docs.mjs` fails when a catalog entry has no row here, when a row points at a missing file or a file without a heading for that screen, or when a blueprint names a token that does not exist.
