# Lotero — Product Framing (frontend)

<!--
Synced against loto-specs: v54
-->

> **Status:** Reviewed baseline (Design M0 0.4.3). This is the frontend brief for M1 and later blueprints; pending features retain their owning contract gates in the screen catalog. The auth/account addendum (0.4.6) and deferred reports are not delivered by this baseline.
> **M8 v54 reconciliation accepted (2026-10-18):** independent repeat review accepted the technical implementation and semantics of server Phases 1–5 and 6–7, including the N/U/V account and invitation contracts, operated onboarding and verified Google identity described below. This aligns the catalog with canonical v54; it does not approve production providers, the 0.4.6 screen set, owner visual direction or launch readiness.
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
1. `POST /user/login` or verified `POST /user/google/sign-in` → `ResponseUserLoginJson {UserId, Name, Email, ResponseToken}`; `ResponseToken` carries `Token` and `RefreshToken`. Google sign-in first obtains a single-use state/nonce challenge, and an unknown Google identity creates an account only with a matching invitation. Identity authority opens **Branch picker / session**. The required caller `UserId` supports own-row and self-recorded markers without decoding tokens (gap U backend resolved). Identity-only Profile/name, password change and logout now have server contracts (gap N backend resolved); `ResponseUserProfileJson` also reads `IsPlatformOperator` and `HasPassword` without allowing either to be edited. These screens remain uncataloged design 0.4.6 work.
2. `POST /branch/session {BranchId}` → `ResponseCreateBranchSessionJson {Token, Branch}`; `Branch.Role` sets the initial home and UI gating; the server rechecks current membership and permission on every request, and `GET /branch/current` returns the live role for UI reconciliation. A branch token gates branch screens, each still subject to its role/account/operator rules.
3. `POST /user/renew-token {Value}` → `ResponseTokenJson {Token, RefreshToken}`. The renewed token is **global identity authority**, not a renewed branch session: recreate the intended branch session and recheck permission before resuming. Login replaces active refresh authority with a new session; renewal rotates its value inside the same stable server session, so another-device login prevents renewal here; web tabs sharing credentials must coordinate renewal and replacement. Failures, re-login, and scoped recovery follow [the cross-screen patterns](screens.md#cross-screen-patterns). Every API `ErrorMessages` entry renders verbatim.

**Three gating tiers:**

| Tier          | Backend filter                               | UI sees                                                    |
|---------------|----------------------------------------------|------------------------------------------------------------|
| Identity      | `[TokenAuthenticate]`                        | branch picker / session; Profile/password/logout API (screens: 0.4.6)           |
| Platform identity | `[TokenPlatformOperator]`                 | operated onboarding API after a fresh stored flag check; no branch data access (Plataforma screen: 0.4.6) |
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

**M9.5 reconciliation for canonical v55 — independent acceptance pending.** Under the adopted working Q3/Q9 assumptions, the fresh close catalog excludes Tarifa Bolão and Operador: operators record each actual daily amount through Transaction — Create on the Terminal, as Receita/In and Entradas/In, both SameDay with no Tab/client requirement. Cash stays in the physical count. Operador’s editable label awaits shop question 9; no automatic fee, rate, receipt or required daily row is implied. Ordinary cash sales are count-only under Q5 (stock down, cash up, no transaction), pending question 5. These are prospective assumptions, not evidence of shop practice or historical Access parity. Retired saved fee values remain visible; AA still gates their save/preview recovery, and the migration refuses retirement on unlocked retained counts. Locked history and prior opening values remain intact, so the first transition is not certified reconciled. Existing roles, locks, statuses and navigation are unchanged; `IsCashVarianceProduct` identifies the system row in responses. Q8 keeps existing fiado linked legs with no mandatory product selector. M10 opening/import and M11 report classification/reads remain pending. The v54 sync marker stays until independent semantic review accepts this reconciliation.


The screen entries and their authoritative gap → screen map distinguish shipped contracts from decided behavior awaiting delivery. A prototype is a visual reference, not API evidence. In particular, retained-retired-product count editing/preview recovery (AA) remains gated. Fiado settlement/linked movements/statement (H) and paired-Tab routing data (L) shipped in server M7.7 Phase 5 (accepted), deactivation unpairing (S) in Phase 5.3, close review events (X) in Phase 7.6.1, whole-plan finalization (Y) in Phase 7.6.2, and the transaction-type filter (Z) in Phase 7.6.3. A fiado repayment method must be configured per branch (`FiadoPaymentKind`) before Receber pagamento can be used; none is seeded. Their fields or endpoints must not be invented to fill a mockup. The current queue is `GET /report/manager-queue` (renamed from `GET /report/dashboard` by server M7.7 Phase 7.8, fields unchanged) with the shipped `Backlog` and `UnlockedDraftTransactionCount`; no replacement endpoint or second backlog composition is needed here.

**Auth, Profile and platform ownership:** server **M8 Phases 1–7** deliver invitations (atomic new-account acceptance and optional Member pre-link), profile/name, confirmed password change, recovery and stable-session logout. Password change/reset revoke all active refresh sessions; JWTs expire naturally. Recovery uses enumeration-safe 202 requests, hashed single-use expiring capabilities and configured trusted links; local SMTP capture proves local delivery only. Member operator-link requests feed the bounded Manager queue and grant no account implicitly. The platform-only `POST /platform/onboarding` and dev/staging CLI share one seed-and-owner-invite use case; an empty migrated environment first uses a gated CLI command to email the initial passwordless platform operator a single-use setup link. Password login has the same invalid-credentials message for missing, inactive, wrong-password and passwordless accounts; throttling remains a Phase 8 gate. The platform operator flag is read-only in profile and never grants branch data access. Public `/user/register`, `/branch/create` and direct `POST /branch/users` are removed; people enter through invitations. Google sign-in verifies a signed provider token and state/nonce, links a local account only with safe proof and creates a new account only with a matching invitation; a Google identity token is never branch authority. Design **0.4.6** still owns the uncataloged auth/account/invitation/Plataforma screens and owner setup checklist. Production SMTP and Google project/consent acceptance, guided employee UI and launch readiness remain open.

**MVP reporting:** the cataloged Daily ledger, Fiado balance/aging, Open-cheque aging, Cash-variance summary and Monthly reconciliation + lock serve operational work; own-operator summaries remain available under their distinct scopes. Monthly reconciliation is a readiness/lock surface, not the consolidated monthly financial report. CEF borderô comparison is manual in MVP, with no attestation/integration promise. The upcoming card/cheque settlements view, consolidated monthly report (variance + bolão + hours) and the **Dashboard** — a separate read-only overview of every manager report's latest facts with links to each (owner decision 2026-10-16) — belong to **server M11 / gap Q**, after its contracts and catalog entries; the queue rename already shipped in server M7.7 Phase 7.8. Work Queue stays the exception-first home. Fiado reporting reads the shipped FIFO semantics (gap H, server M7.7 Phase 5). The existing reports also carry whole-set totals and open deactivated accounts/operators for managers (server M11 Phases 2–3), the inputs the Dashboard will summarize.

## 6. Flows

Do not restate — follow `loto-specs.md` §7: §7.1 operator daily flow, §7.2 manager daily flow, §7.3 fiado lifecycle. `screens.md` references these flows per screen.

---

> **Resolved (was open):** mobile parity → mobile clones web, same screens (§3). The dashboard hero-number and transaction-entry-scope questions are resolved per-screen in `screens.md`, not here. Release-version planning is deferred to the deployment milestone (server M9) — no release-version labels in this doc; the sync marker records only which canonical specification this baseline was reviewed against.
