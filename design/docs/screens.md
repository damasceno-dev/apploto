# Lotero — Screen Catalog (semantic)

> **Status:** Draft (Design M0, Phase 3). Template + five screens defined; remaining stubbed (item 0.3.3).
> **What this is:** *what every screen contains and does* — no colors, no layout. The visual look is decided in M1 (Visual Direction); the visual layout in M4 (Screen Blueprints). This catalog is the layer both build on.
> **Platform:** one catalog for both — mobile clones web (see [`product.md`](product.md) §3). Notes call out small-screen responsive adaptation where a screen is dense.

## Per-screen template

Every screen is defined with this fixed shape (fields marked *(where relevant)* are skipped when they don't apply, e.g. Default view on a pure form):

- **Purpose** — one line: why this screen exists.
- **Primary job** — the one thing the user must finish *fast* here; it drives what should lead.
- **Access** — auth tier (identity / branch-member / manager-admin), role, data scope (own-operator vs whole-branch).
- **Permission fallback** *(member-scoped screens)* — what a Member sees with no linked operator or empty account scope (usually the §6.10 empty short-circuit / a 403).
- **Default view / filter** *(list & report screens)* — the default date / account / operator / status, and *why* that default fits the job.
- **Data shown** — each field tagged by source: `[DTO]` direct from a response DTO · `[derived]` computed in the UI from DTO fields · `[composed]` needs >1 endpoint joined · `[gap]` not in the contract yet (see gaps section).
- **Actions** — what the user can do, each → the endpoint it calls.
- **Audit / lock context** *(transactional / close screens)* — who submitted/approved/rejected + timestamps; `LockDate`; whether the user can still edit/act.
- **States** — loading · empty · error · success (+ meaningful variants, e.g. read-only-after-finalize).
- **Navigation** — entry points + deep-link *targets* (which screen an alert/blocker opens).

**Conventions.** Errors render `ResponseErrorJson.ErrorMessages` verbatim (pt-BR from the backend). Branch-scoped screens require an active branch session. Lists paginate where the endpoint paginates. **Out of scope here (M4, not M0):** visual hierarchy / what's pixel-first, and deep-link *state preservation* (filter + return-context mechanics) — M0 records *which* screen a link targets, not *how* state persists across the jump.

---

## Screen list (by area)

Legend: **✓ defined** · ▫ stub (to define in 0.3.3)

**Auth & session**
- ▫ Login · ▫ Branch picker / session

**Manager work queue & approvals** *(Manager/Admin)*
- **✓ Manager Work Queue** · **✓ Daily-close approval**

**Transactions ledger**
- **✓ Transaction — Create (fast entry)** · **✓ Transaction — Edit (correction / audit)** · ▫ Transactions list · ▫ Installment (cheque) plan

**Reports** *(Manager/Admin)*
- ▫ Daily ledger · ▫ Fiado balance · ▫ Fiado aging · ▫ Open-cheque aging · ▫ Cash-variance summary · ▫ Monthly reconciliation + lock

**Operator self-service** *(Member)*
- ▫ My transaction summary · ▫ My time-entry balance

**Operator day flow** *(Member)*
- **✓ Operator Day Cockpit** · ▫ Open day · ▫ Close day · ▫ Fix & resubmit *(these are sub-flows the cockpit orchestrates)*

**Time clock & management**
- ▫ Clock in / out · ▫ Time-entry management

**Admin & configuration** *(Manager/Admin)*
- ▫ Operators · ▫ Account assignment · ▫ Accounts · ▫ Clients · ▫ Categories & Transaction Types · ▫ Products · ▫ Holidays · ▫ Settings · ▫ Branch members

---

## ✓ Manager Work Queue

- **Purpose** — the manager's home: an **exception-first work queue** of what needs action on the branch — not a KPI page.
- **Primary job** — clear today's approvals and act on exceptions (variances, non-submitters, blockers) fast.
- **Access** — manager-admin; whole-branch.
- **Default view / filter** — most recent business day (branch-local); the queue defaults to *needs-action* items (Submitted + Rejected). Managers open this to act, not to browse.
- **Data shown** *(a queue grouped by exception type, most-urgent first; the close, variance, and not-submitted groups all come from **one call** — `GET /report/dashboard?date=`)*
  - **Pending approvals** — closes with `Status = Submitted`, plus `PendingApprovalCount`. `[DTO: ResponseDashboardJson.Closes / ResponseDashboardCloseJson]` (AccountName, SubmittedByOperatorName, Status, SubmittedAt).
  - **Cash-variance exceptions** — biggest `|variance|` first. `[DTO: ResponseDashboardCloseJson.VarianceValue]` — joined server-side by `(Date, AccountId)`, no client-side cross-join. The biggest-first ordering is `[derived]`: the endpoint returns close rows ordered by account name.
  - **Day variance aggregates** — `TotalVariance` / `MeanVariance` for the selected date. `[DTO: ResponseDashboardJson]`.
  - **Not-submitted accounts** — expected terminal accounts with no submitted-or-later close. `[DTO: ResponseDashboardNotSubmittedJson]`; when an open Draft exists the row carries `DailyCloseId` + `Status` for the deep-link.
  - **Rejected / fix-needed closes** — closes with `Status = Rejected`. `[DTO: ResponseDashboardCloseJson]`.
  - **Draft transactions blocking month lock** — count of `Status = Draft` transactions in the open period. `[composed]` transaction list filtered to Draft (also surfaced as a reconciliation blocker).
  - **Month-end reconciliation blockers** — `[DTO: ResponseMonthlyReconciliationJson.Blockers + LockReady]` (`[composed]`: a call to `/report/monthly-reconciliation/{year}/{month}`).
- **Actions** — open a close → Daily-close approval; open a report; jump to a blocker's source (deep-link target → Monthly reconciliation, or Transactions filtered to Draft); change day.
- **Audit / lock context** — shows `LockDate` (from Setting) and reconciliation `LockReady`; signals whether the month can be locked yet.
- **States**
  - *loading* — queue skeleton.
  - *empty* — nothing needs action → an "all clear" state with the last close's summary.
  - *error* — `ResponseErrorJson` banner; retry.
  - *success* — grouped queue.
- **Navigation** — entered after branch session; deep-links to Daily-close approval, Monthly reconciliation, Transactions (Draft filter), Reports.
- **Note** — server M7.5 shipped `GET /report/dashboard` for this screen (gaps §3, resolved): the close, variance, and not-submitted groups need no client-side joins. Only the Draft-transaction count and the reconciliation blockers remain `[composed]`.

## ✓ Daily-close approval

- **Purpose** — review one submitted close *as a comparison* and approve it or reject it with a reason.
- **Primary job** — decide approve/reject quickly, with the variance and its cause visible.
- **Access** — manager-admin; whole-branch.
- **Default view / filter** — the single close passed in; day transactions filtered to its `(account, date)`.
- **Data shown** *(comparison: opening → closing → variance, with source clearly marked)*
  - **Close header** — account, date, status, submitted-by operator, submitted-at, rejection reason (if any). `[DTO: ResponseDailyCloseReviewJson]` — **one call**, `GET /dailyclose/{id}/review`, serves the header and every item row below.
  - **Closing snapshot — operator-entered** — per-product `ProductName` + `ClosingValue`. `[DTO: ResponseDailyCloseReviewItemJson]`. **Mark as operator-entered.**
  - **Opening values — system-derived** — per product, `OpeningValue` from the most recent prior close (§6.5), derived server-side. `[DTO: ResponseDailyCloseReviewItemJson]` (gaps §2, resolved). `null` on the variance row by design.
  - **Cash variance (Diferença Caixa) — system-calculated** — the item flagged `IsCashVarianceProduct = true` — **do not name-match the product string**. `[DTO: ResponseDailyCloseReviewItemJson]`; **mark system-calculated (§6.5/§6.12), not operator-entered.**
  - **Day's transactions** — context for the count. `[composed]` transaction list filtered to `(account, date)`.
- **Actions** — **Approve** → `POST /dailyclose/{id}/approve`; **Reject with reason** → `POST /dailyclose/{id}/reject` (`RequestRejectDailyCloseJson`; reason required).
- **Audit / lock context** — submitted-by/at, approved-by/at, rejection reason; if already `Approved`/`Rejected`, the screen is read-only.
- **States**
  - *loading* — close + items skeleton.
  - *not-found / cross-branch* — 404.
  - *already finalized* — read-only with the outcome (no approve/reject).
  - *error* — reject without a reason → inline 400 (`ResponseErrorJson`); other failures → banner.
  - *success* — status flips; return to the Work Queue, pending count decremented.
- **Navigation** — from Manager Work Queue (pending list); returns to the queue.

## ✓ Transaction — Create (fast entry)

- **Purpose** — record a money movement **fast** (operator at the counter).
- **Primary job** — enter a transaction in as few taps as possible.
- **Access** — branch-member with linked operator + account scope, or manager-admin (same scope as `POST /transaction`).
- **Permission fallback** — Member without a linked operator → **400** `TRANSACTION_MEMBER_REQUIRES_OPERATOR_LINK` (the `RecordedByOperator` resolver runs before the account-scope guard, so it surfaces as a validation error, not a 403); the screen explains a link is needed. Account out of scope → **403** `TRANSACTION_MEMBER_ACCOUNT_OUT_OF_SCOPE`. (Contrast the *read* path §6.10, where no-linked-operator is 403.)
- **Default view / filter** — today's date, the operator's linked/primary account, current time pre-filled.
- **Data shown**
  - **Form (full set)** — `[DTO: RequestCreateTransactionJson]`: `TransactionTypeId` (drives required fields), `Value`, `Date`, `TransactionTime`, `AccountId`, `ClientId` (fiado/Tab), `DueDate` (cheque/card), `Description`, `RecordedByOperatorId`, `SaveAsDraft`.
  - **Lookups** — transaction types & categories, accounts, clients. `[composed]` admin list endpoints.
  - **Impact preview** *(optional on the fast path)* — `[DTO: ResponseCreateTransactionPreviewJson.Impact]` Receivable / Fiado / CashVariance + `Warnings`. Fast entry may skip it; the field exists for confirmation when the operator wants it.
- **Actions** — **Save** (Active) → `POST /transaction`; **Save as draft** → `POST /transaction` with `SaveAsDraft = true`; **Preview** → `POST /transaction/preview`.
- **Audit / lock context** — blocked when `Date ≤ LockDate` (explain the period is locked); `RecordedByOperatorId` stamped.
- **States** — *loading* lookups · *validating* (400 inline) · *lock-date blocked* · *preview shown* · *success* (return to Day Cockpit / list) · *error* (row **not** assumed saved).
- **Navigation** — from Operator Day Cockpit (operator) or Transactions list (manager); returns there on save.

## ✓ Transaction — Edit (correction / audit)

- **Purpose** — correct an existing transaction's limited fields, with full impact preview — a **manager-control / audit** tool, not fast entry.
- **Primary job** — fix a mistake on a recorded row, seeing the downstream impact before committing.
- **Access** — per the §6.11 mutation contract: Member within scope, Manager/Admin elevated.
- **Permission fallback** — Member with the transaction's account out of scope → 403 `TRANSACTION_MEMBER_ACCOUNT_OUT_OF_SCOPE`; missing/cross-branch id → 404 `TRANSACTION_NOT_FOUND`.
- **Data shown**
  - **Loaded transaction (read context)** — `[DTO: ResponseTransactionJson]`: type, value, account, date, status, audit fields.
  - **Editable fields (restricted)** — `[DTO: RequestUpdateTransactionJson]`: `Description`, `DueDate`, `PaidAt`, `ClientId`, `TransactionTime` only. **`Value`, account, type, and `Date` are read-only** — cancel + re-create to change them.
  - **Impact preview** — `[DTO: ResponseEditTransactionPreviewJson.Impact]` Receivable / Fiado / CashVariance + `Warnings` + `TransactionId`.
- **Actions** — **Preview edit** → `POST /transaction/{id}/edit-preview`; **Save** → `PUT /transaction/{id}`; **Finalize draft** → `POST /transaction/{id}/finalize` (promote Draft → Active); **Cancel** → `POST /transaction/{id}/cancel` (required reason, `RequestCancelTransactionJson`).
- **Audit / lock context** — `UpdatedAt`/`UpdatedByUserId` stamped; blocked when `Date ≤ LockDate`; shows `CancelledAt`/`CancelledByUserId`/reason when cancelled; the current `Status` gates which actions are available (e.g. Finalize only on Draft).
- **States** — *loading* · *validating* (400 inline) · *lock-date blocked* · *preview shown* · *read-only* (Cancelled / locked) · *success* · *error*.
- **Navigation** — from Transactions list (manager) or a drafts list (Day Cockpit); returns there.

## ✓ Operator Day Cockpit

- **Purpose** — the operator's single "today" home: shows the day's state and the **next action**, so operators don't navigate modules.
- **Primary job** — know and do the next step of the day (open → record → close → resubmit).
- **Access** — branch-member (operator); own operator + linked accounts.
- **Permission fallback** — Member without a linked operator → a **setup-needed** state ("no operator linked — ask your manager"). Read/list areas may empty-short-circuit per their own endpoints; **write** actions (open day, record, close) are disabled or surface their own permission/validation errors (e.g. create's 400 `TRANSACTION_MEMBER_REQUIRES_OPERATOR_LINK`). §6.10 governs transaction-list *reads* specifically — not the whole screen.
- **Default view / filter** — today (branch-local), the operator's primary account.
- **Data shown** *(next-action oriented)*
  - **Today's close state** — is there an open `Draft` close? its status? `[composed]` `/dailyclose` filtered to `(account, today)`.
  - **Today's transactions + draft count** — `[composed]` `/transaction` (`Mine = true` / account, today); count of `Draft` rows.
  - **Clock status** — clocked in/out, live-running. `[composed]` `/timeentry` (today).
  - **Next-action banner** — `[derived]`: no close → "Open day"; open → "Record / Close day"; submitted → "Awaiting approval"; rejected → "Fix & resubmit".
- **Actions** — **Open day** → `POST /dailyclose`; **Record** → Transaction Create; **Close day** → close flow (`PUT /dailyclose/{id}/items` + `POST /dailyclose/{id}/submit`); **Fix & resubmit** → edit a rejected close and resubmit; **Clock in/out** → `/timeentry`.
- **Audit / lock context** — a rejected close shows its rejection reason; a submitted close shows submitted-at; `LockDate` rarely bites for "today".
- **States** — *loading* · *empty* (no operator link → guidance) · *error* · *success* (state + next action). Variants: *rejected* (show reason + resubmit), *submitted* (awaiting approval, read-only).
- **Navigation** — operator home after branch session; links to Transaction Create, Close day, Clock, My summaries.

---

## Remaining screens — stubs (define in 0.3.3)

Each will be filled with the template above, every data field tagged by source, mapped to its controller(s) and DTOs:

- **Login** — authenticate → identity token.
- **Branch picker / session** — list `my-branches`, open a branch session.
- **Transactions list** — paginated, filterable ledger (`ResponseListTransactionsJson`).
- **Installment (cheque) plan** — build + preview a pre-dated cheque plan and its downstream impact (incl. open-cheque aging impact).
- **Daily ledger** · **Fiado balance** · **Fiado aging** · **Open-cheque aging** · **Cash-variance summary** · **Monthly reconciliation + lock** — the Report endpoints; reconciliation also advances `LockDate` via `PUT /setting`.
- **My transaction summary** · **My time-entry balance** — operator-self report endpoints.
- **Open day** · **Close day** · **Fix & resubmit** — the day-flow sub-screens the Operator Day Cockpit orchestrates (§7.1, over DailyClose + Transaction). Close day reads opening values from `GET /dailyclose/{id}/review` (§6.14 — a Member within account scope may call it); do not re-derive them from the prior close.
- **Clock in / out** — TimeEntry upsert with live-running in-progress.
- **Time-entry management** — Manager/Admin list + edit + deactivate TimeEntry.
- **Operators · Account assignment · Accounts · Clients · Categories & Transaction Types · Products · Holidays · Settings · Branch members** — admin CRUD over the respective controllers.

---

## Backend contract gaps — resolved by server M7.5

Surfaced while mapping screens to the pre-M7.5 contract; **all resolved** by [server Milestone 7.5 — Frontend UX Contract Gaps](../../server/docs/milestones.md), shipped at spec `v31` (contracts in `loto-specs.md` §6.14). The per-field tags above have been updated to the shipped DTOs. Outcomes:

1. **No variance on the daily-close list item.** Resolved as a **documented deferral** (M7.5 Phase 3): `ResponseListDailyCloseItemJson` is intentionally unchanged. The cross-join this gap described is no longer needed anywhere — the Work Queue reads inline variance from the dashboard's close rows and the approval screen from the review context. The gate reopens only if a future multi-date list/history screen needs inline variance per row.
2. **Opening values are not returned.** Resolved (M7.5 Phase 2): `GET /dailyclose/{dailyCloseId}/review` returns `ResponseDailyCloseReviewJson` — the close header plus per-item `OpeningValue?` (§6.5 server-derived; `null` on the variance row), `ClosingValue`, and `IsCashVarianceProduct`. Same read scope as reading the close itself, so the operator's Close-day form may use it too.
3. **No dashboard aggregation endpoint.** Resolved (M7.5 Phase 1): `GET /report/dashboard?date=` returns `ResponseDashboardJson { Date, TotalVariance, MeanVariance, PendingApprovalCount, Closes, NotSubmitted }` — one call, no client-side joins. Close rows are ordered by account name (exception-first ordering is a UI concern); Draft closes surface only in `NotSubmitted`, carrying their close id for the deep-link.
4. **Draft finalize** (`POST /transaction/{id}/finalize`) — **already implemented**; a UI note, not a gap, so no milestone was needed (the UI just builds an explicit "Finalize" action).
