# LottoGest — Product Framing (frontend)

> **Draft started at** 2026-06-10
> **Planned start:** 2026-06-30
> **Scope:** the *frontend* framing only. Domain truth (entities, business rules, flows) lives in `server/docs/loto-specs.md` — this file points to it and never restates it. Per-screen detail lives in [`screens.md`](screens.md).

---

## 1. What this is, for the frontend

LottoGest is a multi-tenant management app for Brazilian lottery houses (*lotéricas*); each **Branch** is one tenant. Operators record the day's money movements and close their day; managers review closes, watch cash variance and receivables, and lock the month. Two subsystems run in parallel: the **money system** (transactions, daily close, fiado, cheques, cards) and the **people system** (time tracking).

> Domain reference (do not duplicate): `loto-specs.md` §1 (domain overview), §2 (multi-tenancy & user model), §3 (entities), §4 (enums), §5 (seed data), §6 (business rules).

## 2. Roles → UI gating

Three backend concepts the UI must respect (detail in `loto-specs.md` §2): **User** (login identity) → **BranchUser** (role in a branch: `Admin` / `Manager` / `Member`) → **Operator** (the employee a Member is linked to, which scopes their data).

**Two-stage authentication, in UI terms:**
1. `POST /user/login` → an **identity token** that only unlocks the branch picker and profile.
2. `POST /branch/session {BranchId}` → a **branch token** that unlocks everything branch-scoped. The UI gates all branch screens behind an active branch session.
3. Token refresh via `POST /user/renew-token`. Every error is `ResponseErrorJson { ErrorMessages: string[] }`, rendered verbatim.

**Three gating tiers:**

| Tier          | Backend filter                               | UI sees                                                    |
|---------------|----------------------------------------------|------------------------------------------------------------|
| Identity      | `[TokenAuthenticate]`                        | branch picker, profile                                     |
| Branch member | `[TokenAuthenticateBranch]`                  | member-scoped views — scope is screen-specific (see below) |
| Manager/Admin | `[TokenAuthorize(Role.Manager, Role.Admin)]` | whole-branch financials, approvals, admin, lock            |

**Member scope is screen-specific:** transaction reads are scoped to the member's **linked accounts** (§6.10 — a shared account shows *both* operators' rows; `Mine=true` narrows to the caller's own operator), while operator/time-entry reports are scoped to the member's **own operator**. Managers/Admins see the whole branch.

## 3. Platform rule — mobile clones web

**Web and mobile ship the same screen set.** The screen catalog in `screens.md` is platform-agnostic; **mobile is a port/clone of web**, not an independent design. Dense screens (report tables) adapt responsively on small screens, but the screen *set* and *semantics* are identical across platforms. There’s therefore **one** screen catalog, not two.

## 4. Screen index

One-line purpose per screen; full semantic definition (access, data, actions, states, navigation) lives in [`screens.md`](screens.md).

**Auth & session**
- **Login** — authenticate, get identity token.
- **Branch picker / session** — choose a branch, open a branch session.

**Manager work queue & approvals** *(Manager/Admin)*
- **Manager Work Queue** — exception-first home: pending approvals, not-submitted accounts, biggest variances, rejected/fix-needed closes, draft transactions blocking lock, reconciliation blockers.
- **Daily-close approval** — review a submitted close as a comparison (opening → closing → variance), approve or reject with reason.

**Transactions ledger** *(view Manager/Admin; entry also Member on mobile)*
- **Transactions list** — filter and browse the ledger.
- **Transaction — Create (fast entry)** — operator-speed, type-driven entry with optional impact preview.
- **Transaction — Edit (correction / audit)** — restricted-field correction with full impact preview (manager-control).
- **Installment (pre-dated cheque) plan** — build and preview a cheque plan and its downstream impact.

**Reports** *(Manager/Admin)*
- **Daily ledger** · **Fiado balance** · **Fiado aging** · **Open-cheque aging** · **Cash-variance summary** · **Monthly reconciliation + lock** (advance `LockDate`).

**Operator self-service** *(Member; also web)*
- **My transaction summary** · **My time-entry balance**.

**Operator day flow** *(Member, mobile-first)*
- **Operator Day Cockpit** — the operator's "today" home; surfaces the next action across the steps below.
- **Open day** · **Record transactions** · **Close day** (snapshot + variance) · **Fix & resubmit** on rejection.

**Time clock & management**
- **Clock in / out** *(Member)* · **Time-entry management** *(Manager/Admin)*.

**Admin & configuration** *(Manager/Admin)*
- **Operators** · **Account assignment** · **Accounts** · **Clients** · **Categories & Transaction Types** · **Products** · **Holidays** · **Settings** · **Branch members**.

## 5. Flows

Do not restate — follow `loto-specs.md` §7: §7.1 operator daily flow, §7.2 manager daily flow, §7.3 fiado lifecycle. `screens.md` references these flows per screen.

---

> **Resolved (was open):** mobile parity → mobile clones web, same screens (§3). The dashboard hero-number and transaction-entry-scope questions are resolved per-screen in `screens.md`, not here. Release-version planning is deferred to the deployment milestone (server M9) — no version labels in this doc.
