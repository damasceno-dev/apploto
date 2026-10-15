# Lotero — Product Framing (frontend)

> **Draft started at** 2026-06-10
> **Planned start:** 2026-06-30
> **Scope:** the *frontend* framing only. Domain truth (entities, business rules, flows) lives in `docs/loto-specs.md` — this file points to it and never restates it. Per-screen detail lives in [`screens.md`](screens.md).

---

## 1. What this is, for the frontend

Lotero is a multi-tenant management app for Brazilian lottery houses (*lotéricas*); each **Branch** is one tenant. Operators record the day's money movements and close their day; managers review closes, watch cash variance and receivables, and lock the month. Two subsystems run in parallel: the **money system** (transactions, daily close, fiado, cheques, cards) and the **people system** (time tracking).

> Domain reference (do not duplicate): `loto-specs.md` §1 (domain overview), §2 (multi-tenancy & user model), §3 (entities), §4 (enums), §5 (seed data), §6 (business rules).

## 2. Roles → UI gating

Three backend concepts the UI must respect (detail in `loto-specs.md` §2): **User** (login identity) → **BranchUser** (role in a branch: `Admin` / `Manager` / `Member`) → **Operator** (employee/history identity, optionally linked to a login, including a Manager/Admin's). The branch role grants permission; the operator link supplies own-work context. **Gestão** and **Meu turno** are navigation modes, never roles or impersonation.

**Two-stage authentication, in UI terms:**
1. `POST /user/login` → `ResponseUserLoginJson {Name, Email, ResponseToken}`; `ResponseToken` carries `Token` and `RefreshToken`. Identity authority opens **Branch picker / session**. Profile is a pending M8 / design 0.4.6 surface (gap N), not a shipped identity-tier promise.
2. `POST /branch/session {BranchId}` → `ResponseCreateBranchSessionJson {Token, Branch}`; `Branch.Role` sets the initial home and UI gating; the server rechecks current membership and permission on every request, and `GET /branch/current` returns the live role for UI reconciliation. A branch token gates branch screens, each still subject to its role/account/operator rules.
3. `POST /user/renew-token {Value}` → `ResponseTokenJson {Token, RefreshToken}`. The renewed token is **global identity authority**, not a renewed branch session: recreate the intended branch session and recheck permission before resuming. Login and renewal replace the user’s sole active refresh token, so another-device login prevents renewal here; web tabs sharing credentials must coordinate renewal and replacement. Failures, re-login, and scoped recovery follow [the cross-screen patterns](screens.md#cross-screen-patterns). Every API `ErrorMessages` entry renders verbatim.

**Three gating tiers:**

| Tier          | Backend filter                               | UI sees                                                    |
|---------------|----------------------------------------------|------------------------------------------------------------|
| Identity      | `[TokenAuthenticate]`                        | branch picker / session; Profile deferred (gap N)           |
| Branch member | `[TokenAuthenticateBranch]`                  | member-scoped views — scope is screen-specific (see below) |
| Manager/Admin | `[TokenAuthorize(Role.Manager, Role.Admin)]` | whole-branch financials, approvals, admin, lock            |

**Member scope is screen-specific:** transaction reads use **linked accounts** (§6.10 — a shared account shows *both* operators' rows; `Mine=true` narrows to the caller's own operator); mutation ownership and current-day checks are separate. Operator/time-entry reports resolve the **own operator**, with each endpoint's empty-scope fallback. Clients are shared counter work for any branch role except deactivation. The decided Client statement access (gap H, pending delivery) requires an explicit Tab grant and shows complete client debt across the branch, not a partial per-drawer debt. Managers/Admins have whole-branch access subject to each command's rules.

**Navigation follows the existing IA:** Members start in Meu turno; Manager/Admin start in Gestão and, when linked to an active Operator, may switch to Meu turno. An unlinked Manager/Admin retains Open day, Close day, Fix & resubmit, approval and recall access in Gestão; only own-shift mode needs the link. Manager/Admin always use the **snapshot** time-entry contract in Time-entry management, even for their own hours; the Member tap shape never follows a mode switch. The snapshot reconciles segment ids; omission does not delete a segment and existing `ClockIn` changes use the granular route. See the authoritative [route table and screen entries](screens.md#navigation--information-architecture).

## 3. Platform rule — mobile clones web

**Web and mobile ship the same screen set and semantics.** “Mobile clones web” means the same jobs, data meaning, permission gates, states, validation, financial confirmation, and return destinations. Dense tables may become readable grouped rows; browser history, keyboards, native back, touch, focus, and screen-reader interaction follow each platform. No required action depends on hover, a desktop-only gesture, or hidden columns. There is **one** catalog and one cross-screen behavior contract.

Each app owns its UI, transport, token storage, durable local-input storage, cache and navigation. `design/` remains non-runtime; `shared/api` supplies generated API types and `shared/core` pure/injected formatting and helpers. Neither shared package owns a browser/native session or persistence adapter. MVP financial writes are explicitly **online-only**; saved-on-device input is not an offline money queue or a server save.

## 4. Screen index

One-line purpose per screen; full semantic definition (access, data, actions, states, navigation) lives in [`screens.md`](screens.md).

**Auth & session**
- **Login** — authenticate, get identity token.
- **Branch picker / session** — choose a branch, open a branch session.

**Manager work queue & approvals** *(Manager/Admin)*
- **Manager Work Queue** — exception-first home: pending approvals, not-submitted accounts, biggest unapproved variances, an earlier-days backlog banner, rejected/fix-needed closes, draft transactions blocking lock, reconciliation blockers.
- **Daily-close approval** — review a submitted close as a comparison (opening → closing → variance), approve or reject with reason.

**Transactions ledger** *(Member: reads + entry scoped to linked accounts, §6.10/§6.11; Manager/Admin: whole branch — same screens on both platforms)*
- **Transactions list** — filter and browse the ledger.
- **Transaction — Create (fast entry)** — operator-speed, type-driven entry with an automatic, non-blocking impact preview.
- **Transaction — Edit (correction / audit)** — restricted-field correction with full impact preview (manager-control).
- **Installment (pre-dated cheque) plan** — build and preview a cheque plan and its downstream impact.
- **Client statement (Extrato de fiado)** — one client's fiado debt and history (*quanto deve e desde quando*); entry to Receber pagamento.
- **Receber pagamento** — settle a client's fiado debt, partially or in full.

**Reports** *(Manager/Admin)*
- **Daily ledger** · **Fiado balance** · **Fiado aging** · **Open-cheque aging** · **Cash-variance summary** · **Monthly reconciliation + lock** (advance `LockDate`).

**Operator self-service** *(Member; also linked Manager/Admin in the catalog's explicit Meu turno mode; also web)*
- **My transaction summary** · **My time-entry balance**.

**Day close flows** *(dual-mode: the Cockpit orchestrates them in Meu turno — Member by default, linked Manager/Admin through the explicit Gestão ↔ Meu turno switch — and Gestão handles their exceptions; mobile-first)*
- **Operator Day Cockpit** — the operator's "today" home; surfaces the next action across the steps below.
- **Open day** · record transactions via **Transaction — Create (fast entry)** · **Close day** (snapshot + variance) · **Fix & resubmit** on rejection.

**Time clock & management**
- **Clock in / out** *(Member only)* · **Time-entry management** *(Manager/Admin, including own hours)*.

**Admin & configuration** *(Manager/Admin)*
- **Operators** · **Account assignment** · **Accounts** · **Clients** *(exception: also counter — any branch role except deactivate)* · **Categories & Transaction Types** · **Products** · **Holidays** · **Settings** · **Branch members**.

## 5. Promises and delivery boundaries

The screen entries and their authoritative gap → screen map distinguish shipped contracts from decided behavior awaiting delivery. A prototype is a visual reference, not API evidence. In particular, fiado settlement/linked movements/statement (H), paired-Tab routing and deactivation (L/S), review events (X), whole-plan finalization (Y), and the transaction-type filter (Z) remain gated. Their fields or endpoints must not be invented to fill a mockup. The current queue remains `GET /report/dashboard` with the shipped `Backlog` and `UnlockedDraftTransactionCount`; no replacement endpoint or second backlog composition is needed here.

**Auth, Profile and platform ownership:** server **M8** owns invitations, recovery/change password, Profile, logout with refresh-token revocation, Google sign-in, operated onboarding and its platform authority. Design **0.4.6** catalogs those screens after the contracts land. The existing avatar menu owns the mode switch only; its existence does not deliver Profile or logout. There is no public Register or self-service Create-branch journey. Today's server still exposes `POST /user/register` and `POST /branch/create`; their lockdown/replacement is M8 work, not evidence of a product entry point. The internal **Plataforma** onboarding area belongs to the Lotero team, with server platform authority; a branch Admin is not automatically a platform operator. The future owner setup checklist and guided Add/Offboard employee journeys remain 0.4.6 work; “under a minute” on Operators is a goal for that guided journey, not a delivered three-screen guarantee.

**MVP reporting:** the cataloged Daily ledger, Fiado balance/aging, Open-cheque aging, Cash-variance summary and Monthly reconciliation + lock serve operational work; own-operator summaries remain available under their distinct scopes. Monthly reconciliation is a readiness/lock surface, not the consolidated monthly financial report. CEF borderô comparison is manual in MVP, with no attestation/integration promise. The upcoming card/cheque settlements view, consolidated monthly report (variance + bolão + hours), KPI page and planned queue rename belong to **server M11 / gap Q**, after its contracts and catalog entries. Work Queue stays exception-first. Fiado reporting still carries H's pending semantics; product framing does not certify FIFO delivery.

## 6. Flows

Do not restate — follow `loto-specs.md` §7: §7.1 operator daily flow, §7.2 manager daily flow, §7.3 fiado lifecycle. `screens.md` references these flows per screen.

---

> **Resolved (was open):** mobile parity → mobile clones web, same screens (§3). The dashboard hero-number and transaction-entry-scope questions are resolved per-screen in `screens.md`, not here. Release-version planning is deferred to the deployment milestone (server M9) — no version labels in this doc.
