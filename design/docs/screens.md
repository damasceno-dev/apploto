# Lotero — Screen Catalog (semantic)

> **Status:** Draft (Design M0, Phase 3 + items 0.4.1 and 0.4.5 complete). Template + all currently-cataloged screens and the semantic navigation/IA map are defined; the remaining Phase 4 cross-screen patterns and close-out turn this catalog into a **reviewed baseline** — not a final lock: every screen named in the gaps section's **gap → screen map** stays **implementation-gated** until its owning decision/contract lands and the entry is re-synced (inline pointers are mirrors, the map is authoritative). The 2026-07 catalog review added gaps **F–T**, whose current signed/pending/implemented states are recorded below.
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

**Writing rules.** (1) An entry states what the user sees and does. Server-side invariants — concurrency tokens, coordination, cascade and floor mechanics — live in `docs/loto-specs.md` and are referenced by section, not restated. (2) When an entry states a rule for one actor, one direction, or one origin, it says whether the counterpart is deliberately excluded or merely unstated, so an unstated counterpart is a visible gap rather than a silent one.

**Conventions.** Errors render `ResponseErrorJson.ErrorMessages` verbatim (pt-BR from the backend). Branch-scoped screens require an active branch session. Lists paginate where the endpoint paginates. **Out of scope here (M4, not M0):** visual hierarchy / what's pixel-first, and deep-link *state preservation* (filter + return-context mechanics) — M0 records *which* screen a link targets, not *how* state persists across the jump. What M0 does fix: **unsaved input is never lost** — returning to a form restores its unsaved input, kept on the device (the full pattern is design 0.4.2's); Close day additionally autosaves to the server on today's plain Draft (see that entry).

---

## Navigation & information architecture

This section is the authority for **semantic route ownership**: which authentication tier or work mode owns a destination, where a journey enters, and where it returns. It does not prescribe a sidebar, bottom tabs, item count, responsive placement, pixels, or any other visual layout; those decisions belong to M4. The route table's entry targets stay authoritative and exhaustive for semantic entry; M4 may add **navigation shortcuts** to any destination the caller can already reach (for example **Nova transação** in both modes, within the caller's account scope). A shortcut is not a new entry point: it inherits that journey's ownership and return rules unchanged.

### Session gate and home resolution

Authentication remains two-stage, and navigation never treats an identity token as permission to render branch data:

| Current authority                           | Reachable destination                | Home / gate resolution                                                                                                                                                                                                       |
|---------------------------------------------|--------------------------------------|------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| Anonymous, with no usable identity token    | Login only                           | A successful login receives an identity token and continues to Branch picker / session. A requested branch-scoped deep-link remains only an intent until a branch session exists; no resource is fetched before then.        |
| Identity token only                         | Branch picker / session only         | Selecting a branch calls `POST /branch/session`; identity alone never opens a branch screen. Profile, logout, recovery, invitation, and onboarding routes are deliberately absent until server M8 + design 0.4.6 (gaps C/N). |
| Active branch session, `Member`             | **Meu turno** → Operator Day Cockpit | Every plain Member lands on the cockpit, including an unlinked Member; the latter sees its cataloged setup-needed state rather than being routed into management.                                                            |
| Active branch session, `Manager` or `Admin` | **Gestão** → Manager Work Queue      | Every elevated role starts in management mode. After session creation, `GET /operator/self-context` determines whether an additional **Meu turno** mode is available; it does not alter the branch token or its role.        |

Every branch-scoped deep-link first passes the same gate. With a valid session for the intended branch and sufficient permission, it opens its target; a linked Manager/Admin enters the target's owning mode when that target is mode-specific. A shared destination such as Transactions list keeps the current mode. If the target is unavailable for the caller, navigation returns to that role's available home and explains the permission or setup requirement; it never probes another branch or invents a broader scope. Once an authorized deep-linked target is open and has no source screen to return to, closing it falls back to the owning mode's home: Manager Work Queue for a management route, Operator Day Cockpit for a shift route, and the active mode's home for a shared destination. **Route guard:** Open day, Close day, and Fix & resubmit are never gated behind an operator-link check for a Manager/Admin in Gestão — the operator-link requirement is a Member / Meu turno constraint only.

### Role and mode navigation

`Role` is the server-authorized `BranchUser.Role`; **mode** is only the active navigation/home context within that branch. Switching mode does not mint a token, impersonate a Member, remove Manager/Admin permissions, or change endpoint authorization.

| Caller in the active branch                   | Initial home                         | Primary semantic destinations                                                                                                                                      | Other mode                                                                                                                                                                                                                                                                                                     |
|-----------------------------------------------|--------------------------------------|--------------------------------------------------------------------------------------------------------------------------------------------------------------------|----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| Member                                        | **Meu turno** / Operator Day Cockpit | Cockpit; today's close flow; Transactions list and entry flows within linked-account scope; Clients; Clock in / out; My transaction summary; My time-entry balance | None                                                                                                                                                                                                                                                                                                           |
| Manager/Admin without an active Operator link | **Gestão** / Manager Work Queue      | Work Queue and approvals; whole-branch Transactions; Reports; Time-entry management; Operators, account setup, and the remaining admin/configuration screens       | None. An attempted Meu turno/Cockpit entry returns to Gestão with a full-page setup explanation (not a banner or toast) and points to Operators (then Account assignment if needed), instead of showing a broken shift mode.                                                                                   |
| Manager/Admin linked to an active Operator    | **Gestão** / Manager Work Queue      | The same management destinations as above                                                                                                                          | An explicit **Gestão ↔ Meu turno** switch exposes Operator Day Cockpit and the own-operator working set. Meu turno defaults transaction authorship to the caller's linked operator and uses `mine` on self reports and as the Transactions list default; management/on-behalf controls remain Gestão concerns. |

For a linked Manager/Admin, Meu turno is not a reduced-permission session: direct management deep-links still work and activate Gestão, while returning to Meu turno restores the cockpit as that mode's home. Actions remain role-shaped. In particular, the Member **Clock in / out** tap does not become available to Manager/Admin, because `PUT /timeentry` still requires the elevated snapshot shape for those roles; they record their own hours and corrections in Gestão → Time-entry management (Manager/Admin format, own operator). The **Gestão ↔ Meu turno** switch is its own control in the navigation shell and is one-shot: activating it changes mode and lands on that mode's home. It does not depend on the session menu (gap N), which may absorb it later. An active operator link with no usable account assignment still exposes Meu turno, but the cockpit shows its account setup-needed state and points back to Gestão → Account assignment.

### Route ownership, entry, and return targets

“Return to origin” below means the semantic caller recorded by the journey. Deep-link preservation mechanics are still deferred to M4; when no origin exists, the named canonical fallback applies.

| Journey / route family     | Semantic owner                                                                                                                       | Entry targets                                                                                                                                                                                                                                                                                                                                                                                                                                                     | Return targets                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                            |
|----------------------------|--------------------------------------------------------------------------------------------------------------------------------------|-------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| Login and branch selection | Auth/session, outside both work modes                                                                                                | Anonymous → Login → Branch picker; switch branch → Branch picker                                                                                                                                                                                                                                                                                                                                                                                                  | A new branch session resolves to the role home above, unless an authorized pending deep-link names a more specific target.                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                |
| Approval                   | Gestão                                                                                                                               | Manager Work Queue pending item, Cash-variance summary row, or Monthly reconciliation blocker → status-routed close (Draft → Close day, Rejected → Fix & resubmit, Approved → Daily-close approval); only `Submitted` → Daily-close approval                                                                                                                                                                                                                      | Approve, reject, or read-only review → the originating Work Queue, Cash-variance summary, or Monthly reconciliation view; without an origin → Manager Work Queue.                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                         |
| Close-day flow             | Meu turno for own work; Gestão when an elevated exception/blocker opens it                                                           | Operator Day Cockpit → Open day / Close day / Fix & resubmit. Work Queue or Monthly reconciliation may enter Open day (a missing expected close) or Close day / Fix & resubmit for the returned account, date, close, and status.                                                                                                                                                                                                                                 | Own-flow completion → Operator Day Cockpit. Elevated exception handling → the originating Work Queue or Monthly reconciliation view; without an origin → the active mode home.                                                                                                                                                                                                                                                                                                                                                                                                                                                                                            |
| Transaction flow           | Shared branch work, retaining the entry mode                                                                                         | Cockpit or Transactions list → Transaction — Create; Create may hand off to Installment plan. Transactions list, Daily ledger, Fiado aging, or Open-cheque aging → Transaction — Edit; a Draft blocker carries only a count, so it opens Transactions list filtered to Draft for its day, then Edit. Clients may be entered inline from Create. Clients, Transaction — Create (a fiado client selected), or Fiado balance → Client statement → Receber pagamento. | Create/save → Cockpit when entered there, otherwise Transactions list. Installment save/cancel → Transaction — Create or Transactions list, whichever opened it. Edit/cancel/finalize → the originating list/report/blocker; without an origin → Transactions list in the current mode. The three blocker origins — the Work Queue's Draft transactions group, a Monthly reconciliation blocker row, and Close day's Submit blockers — carry their origin, so finishing the drafts returns there; Close day restores its unsaved count. Client statement returns to its source; Receber pagamento returns to Client statement when opened there, otherwise to its source. |
| Management reports         | Gestão                                                                                                                               | Management navigation or a Work Queue exception → Daily ledger, Fiado balance/aging, Open-cheque aging, Cash-variance summary, or Monthly reconciliation + lock                                                                                                                                                                                                                                                                                                   | Report drill-downs return to the source report; closing the report returns to Manager Work Queue when no narrower management origin exists.                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                               |
| Operator self-service      | Meu turno for own context; Gestão for manager-selected operator review                                                               | Cockpit → My transaction summary / My time-entry balance using own context. Operators or Time-entry management → the same screens with an explicit operator context.                                                                                                                                                                                                                                                                                              | Own context → Operator Day Cockpit; manager-selected context → Operators or Time-entry management, whichever opened it.                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                   |
| Admin and configuration    | Gestão — except **Clients**, which is shared counter work (any branch role; deactivate stays Manager/Admin) and keeps the entry mode | Management navigation → Operators, Account assignment, Accounts, Clients, Categories & Transaction Types, Products, Holidays, Settings, or Branch members; contextual pairs follow the per-screen links.                                                                                                                                                                                                                                                          | A detail or paired setup journey returns to its source admin screen; otherwise the management fallback is Manager Work Queue. Clients returns to its source (Transaction — Create when entered inline); without an origin → the active mode's home.                                                                                                                                                                                                                                                                                                                                                                                                                       |

### Branch switching and branch-session loss

**Switch branch** is a global semantic action on branch-scoped screens, not a Profile screen, offered only when the identity has more than one branch (the visibility rule is on Branch picker). It exits Gestão or Meu turno, discards the branch token and all branch-scoped route/data state (including selected operator/account, filters, mode, and return origin), and returns to Branch picker / session under the identity token. The newly selected branch mints a new branch token and resolves its default home afresh: Member → Meu turno; Manager/Admin → Gestão. A previous branch's mode or resource deep-link is never replayed into the new tenant.

If the branch session is lost while the identity token remains usable, the same branch gate takes ownership and returns to Branch picker / session; if identity authority is also unavailable, Login takes ownership. After authentication/session is restored, an intended deep-link may resume only after branch and permission checks; otherwise navigation uses the applicable role home. Session-expiry retry mechanics and unsaved-state handling remain the cross-screen-pattern work of design 0.4.2.

---

## Screen list (by area)

All **currently-cataloged** screens are defined at **reviewed-baseline** level, ordered by area exactly as listed below; the five **spine screens** (0.3.2) are marked where they appear. Two screen sets are deferred by decision and not yet cataloged: the auth & account set — with the Plataforma onboarding area and the owner setup checklist (design 0.4.6, after server M8) — and the post-MVP reports, including the KPI page (gap Q / server M11). An entry named by the gaps section's **gap → screen map** is decision-blocked, not final.

**Auth & session**
- Login · Branch picker / session

**Manager work queue & approvals** *(Manager/Admin)*
- **Manager Work Queue** *(spine)* · **Daily-close approval** *(spine)*

**Transactions ledger**
- **Transaction — Create (fast entry)** *(spine)* · **Transaction — Edit (correction / audit)** *(spine)* · Transactions list · Installment (pre-dated cheque) plan · Client statement · Receber pagamento

**Reports** *(Manager/Admin)*
- Daily ledger · Fiado balance · Fiado aging · Open-cheque aging · Cash-variance summary · Monthly reconciliation + lock

**Operator self-service** *(Member; linked Manager/Admin may use own context in Meu turno)*
- My transaction summary · My time-entry balance

**Day close flows** *(dual-mode: orchestrated by the Cockpit in Meu turno; managed as exceptions from the Gestão Work Queue)*
- **Operator Day Cockpit** *(spine)* · Open day · Close day · Fix & resubmit *(the three sub-flows are dual-mode: own work in Meu turno, exceptions in Gestão)*

**Time clock & management**
- Clock in / out · Time-entry management

**Admin & configuration** *(Manager/Admin)*
- Operators · Account assignment · Accounts · Clients *(exception: also counter — any branch role except deactivate)* · Categories & Transaction Types · Products · Holidays · Settings · Branch members

---

## Login

- **Purpose** — authenticate an existing user and obtain the identity token that unlocks the branch picker.
- **Primary job** — get in fast: two fields, one button.
- **Access** — public (anonymous); everything else in the app sits behind this door.
- **Data shown**
  - **Form** — `Email`, `Password`. `[DTO: RequestUserLoginJson]`.
  - **Signed-in identity** *(after success)* — `Name`, `Email`, identity + refresh tokens. `[DTO: ResponseUserLoginJson + ResponseTokenJson]`.
- **Actions** — **Entrar** → `POST /user/login`; silent renewal thereafter → `POST /user/renew-token` (never a visible screen).
- **States** — *validating* (400 inline) · *invalid credentials* (401 `ResponseErrorJson`, verbatim) · *loading* · *success* (→ Branch picker).
- **Navigation** — app entry point; success always lands on Branch picker / session. There is no public sign-up: onboarding is operated and people arrive by invitation (gap C, decided in server M8). The forgot-password, invitation, and **Continuar com Google** entries join this screen via design 0.4.6 after server M8 (gaps C/N).

## Branch picker / session

- **Purpose** — choose which branch (tenant) to work in and open the branch session that unlocks everything branch-scoped.
- **Primary job** — get into the right branch in one tap; most users have exactly one.
- **Access** — identity tier (`[TokenAuthenticate]`).
- **Default view / filter** — the user's branches. Exactly one branch → auto-open its session and skip the screen; more than one → the selection; zero → the *empty* state. Switch branch is offered only when there is more than one branch, so a single-branch user never sees this screen after login. `[derived]`
- **Data shown**
  - **Branch options** — `Name`, `Cnpj?`, `Address?`, `Phone?`, and the caller's `Role` in that branch. `[DTO: ResponseListMyBranchesJson.Branches / ResponseBranchSummaryJson]`. The role tells the user what they'll see inside.
- **Session context** — after selection, `GET /branch/current` returns `BranchLocalDate` as a calendar-only `YYYY-MM-DD` value and `BranchLocalDateTime` as an offset-free branch-wall-clock value from one server clock capture. These are the authoritative business-day defaults; do not pass either through device-timezone conversion (`BranchLocalDate` is not a JavaScript instant). The device clock is display-only. `[DTO: ResponseGetCurrentBranchSummaryJson]`.
- **Actions** — select a branch → `POST /branch/session {BranchId}` → branch token `[DTO: ResponseCreateBranchSessionJson]`; then route by role: Manager/Admin → Gestão / Manager Work Queue, Member → Meu turno / Operator Day Cockpit. After an elevated session, `GET /operator/self-context` determines only whether the Gestão ↔ Meu turno switch is available. Session context re-read → `GET /branch/current`.
- **States** — *loading* · *empty* (zero branches → guidance to contact whoever manages the lotérica; lotéricas are created by operated onboarding, gap C; logout from this dead end arrives with the session menu, gap N) · *error* · *success*.
- **Navigation** — after Login; reachable later through the navigation shell's Switch branch action when the identity has more than one branch. Switching exits either mode and drops the old branch token and branch-scoped state. Profile and session-menu screens remain uncataloged under gaps C/N.

## Manager Work Queue

- **Purpose** — the manager's home: an **exception-first work queue** of what needs action on the branch — not a KPI page.
- **Primary job** — clear today's approvals and act on exceptions (variances, non-submitters, blockers) fast.
- **Access** — manager-admin; whole-branch.
- **Default view / filter** — branch-local today (`BranchLocalDate` from `GET /branch/current`); the queue never switches day by itself. It has no status filter: every group below is shown, and the needs-action groups (Pending approvals, Rejected) lead — managers open this to act, not to browse. There is no operating calendar: *today* is literal, and earlier pending days surface through the backlog banner. *No activity today* means today's dashboard returns no close rows and no `NotSubmitted` row carrying a `DailyCloseId`; the banner then offers the jump.
- **Data shown** *(a queue grouped by exception type in a fixed order — Pending approvals → Rejected / fix-needed → Not submitted → Cash-variance exceptions → Reconciliation blockers → Draft transactions → Operator-link requests; the close, variance, and not-submitted groups, the backlog banner, and the Draft count all come from **one call** — `GET /report/dashboard?date=`; the last two once gap W ships)*
  - **Pending backlog banner** — above the groups whenever an unlocked day before the selected date holds a `Submitted`, `Rejected`, or `Draft` close: counts per status and **Ver dia mais antigo**, which sets the selected date to the oldest such day. **Ver dia mais antigo** sets the selected date to `OldestPendingDate`. `[gap]` — gaps §0.3.3-W: `ResponseDashboardJson.Backlog { SubmittedCount, RejectedCount, DraftCount, OldestPendingDate? }` over unlocked days before the requested date (server M7.7 Phase 7.5). Never stitched from paginated `GET /dailyclose` pages: those cannot give exact per-status counts, and the list's order is not part of the contract.
  - **Seu turno** *(linked Manager/Admin only)* — one line linking into Meu turno. With a workstation already chosen today on this device: the terminal and today's close status (`[composed]`: `GET /operator/self-context` + `GET /dailyclose` for `(account, today)`). Several Terminals and none chosen yet: *"Escolher terminal"*. No usable Terminal assignment: *"Sem terminal atribuído"*, linking to Account assignment. Absent for an unlinked caller.
  - **Pending approvals** — closes with `Status = Submitted`, plus `PendingApprovalCount`. `[DTO: ResponseDashboardJson.Closes / ResponseDashboardCloseJson]` (AccountName, recorder user/operator, current submitter user/operator, Status, SubmittedAt?). Keep “who counted” separate from “who sent”.
  - **Rejected / fix-needed closes** — closes with `Status = Rejected`. `[DTO: ResponseDashboardCloseJson]`.
  - **Not-submitted accounts** — expected terminal accounts with no submitted-or-later close. `[DTO: ResponseDashboardNotSubmittedJson]`; when an open Draft exists the row carries `DailyCloseId` + `Status` for the deep-link.
  - **Cash-variance exceptions** — unapproved closes first, biggest `|variance|` first within each group; approved closes follow. `[DTO: ResponseDashboardCloseJson.VarianceValue?]` — joined server-side by `(Date, AccountId)`, no client-side cross-join. The biggest-first ordering is `[derived]`: the endpoint returns close rows ordered by account name.
  - **Day variance aggregates** — `TotalVariance` / `MeanVariance` for the selected date. `[DTO: ResponseDashboardJson]`.
  - **Month-end reconciliation blockers** — `[DTO: ResponseMonthlyReconciliationJson.Blockers + LockReady]` (`[composed]`: a call to `/report/monthly-reconciliation/{year}/{month}`). Blockers include unapproved closes, Draft transactions, and direct Terminal activity missing its expected close.
  - **Draft transactions blocking month lock** — count of `Status = Draft` transactions on every unlocked day up to branch-local today — not only the earliest unlocked month the reconciliation blockers cover. `[gap]` — gaps §0.3.3-W: `ResponseDashboardJson.UnlockedDraftTransactionCount` (server M7.7 Phase 7.5).
  - **Operator-link requests** — pending requests from Members with no operator link or no usable account assignment; each opens Operators to resolve it. `[gap]` — gaps §0.3.3-V (server M8).
- **Actions** — open a close **routed by its status** (only `Submitted` is approvable): `Submitted` → Daily-close approval · `Draft` (a `NotSubmitted` row carrying `DailyCloseId`) → Close day (Manager/Admin may edit items) · `Rejected` → Fix & resubmit · `Approved` → Daily-close approval, read-only; open a report; jump to a blocker's source (`UnapprovedClose` → status-routed close, `DraftTransactions` → Transactions list filtered to Draft/day, `MissingExpectedClose` → Open day for the returned account/day); change day.
- **Audit / lock context** — shows `LockDate` (`[composed]`: `GET /setting`) and reconciliation `LockReady`. `LockReady` is advisory for the viewed month; the lock action atomically rechecks the entire unlocked interval. If imported legacy state shows a current/future `LockDate`, route the manager to Monthly reconciliation: the explicit command is the recovery path and validates from the operational floor before replacing that impossible boundary.
- **States**
  - *loading* — queue skeleton.
  - *empty* — nothing needs action on the selected day → an all-clear state; the backlog banner still appears when earlier unlocked days hold pending closes. A recent-close summary, if shown, is `[composed]`: `GET /dailyclose` most-recent row.
  - *error* — `ResponseErrorJson` shown; retry.
  - *success* — grouped queue.
- **Navigation** — Gestão home after a Manager/Admin branch session; deep-links to Daily-close approval / Close day / Fix & resubmit (by close status), Open day (a missing expected close), Monthly reconciliation + lock, Transactions list (Draft filter), the report screens, and Operators (an operator-link request). A linked Manager/Admin may switch explicitly to Meu turno / Operator Day Cockpit without changing the branch token or role.
- **Note** — server M7.5 shipped `GET /report/dashboard` for this screen (gaps §3, resolved): the close, variance, and not-submitted groups need no client-side joins. M7.7 Phase 4 aligned reconciliation readiness with direct Terminal activity and shipped the atomic lock command. What remains `[composed]`: the reconciliation blockers, `LockDate` (`GET /setting`), and the optional recent-close summary on the all-clear state; the backlog banner and the Draft-transaction count join the dashboard response with gap W.

## Daily-close approval

- **Purpose** — review one submitted close *as a comparison* and approve it or reject it with a reason.
- **Primary job** — decide approve/reject quickly, with the variance and its cause visible.
- **Access** — manager-admin; whole-branch.
- **Default view / filter** — the single close passed in; day transactions filtered to its `(account, date)`.
- **Data shown** *(comparison: opening → closing → variance, with source clearly marked)*
  - **Close header** — account, date, status, `Version`, opener, immutable recorder user/operator, current submitter user/operator, `SubmittedAt?`, rejection reason (if any), and the recorder's `Notes?` explanation. `[DTO: ResponseDailyCloseReviewJson]` — **one call**, `GET /dailyclose/{id}/review`, serves the header and every item row below. Do not label the submitter as the person who counted. When `RecordedByUserId` is the current user, show the self-recorded marker "Você registrou este fechamento" beside the actions — self-approval is permitted (gap R). `[gap]` — gaps §0.3.3-U: the caller's user id arrives on the login response with server M8.
  - **Closing snapshot — operator-entered** — every active product ordered by `DisplayOrder`, carrying `ProductName` + nullable `ClosingValue?`; a null closing means the operator has not entered that row. `[DTO: ResponseDailyCloseReviewItemJson]`. **Mark non-variance values as operator-entered.**
  - **Opening values — system-derived** — per product, `OpeningValue?` from the most recent prior **counted** close (`ItemsFirstRecordedAt != null`, regardless of Draft/non-Draft status), derived server-side. `[DTO: ResponseDailyCloseReviewItemJson]`. `null` on the variance row by design.
  - **Cash variance (Diferença Caixa) — system-calculated** — the item flagged `IsCashVarianceProduct = true` — **do not name-match the product string**. `[DTO: ResponseDailyCloseReviewItemJson]`; **mark system-calculated (§6.5/§6.12), not operator-entered.**
  - **Day's transactions** — context for the count. `[composed]`: `GET /transaction?AccountId&DateFrom=DateTo=` the close's `(account, date)`.
- **Actions** — **Approve** → `POST /dailyclose/{id}/approve`; **Reject with reason** → `POST /dailyclose/{id}/reject` (`RequestRejectDailyCloseJson`; reason required); on an Approved close, Manager/Admin **Reopen for correction** → `POST /dailyclose/{id}/reopen` (returns to Draft and requires submit + approval again).
- **Audit / lock context** — Submitted metadata (SubmittedBy, SubmittedAt), reviewer metadata on finalized closes, and the submitted Notes snapshot. The ledger and notes are frozen at submit, so the displayed count and cash variance are an authoritative immutable snapshot. All actions (Approve, Reject, Reopen) are blocked at or before LockDate. Reopening an approved close reverts it to Draft and requires resubmission and approval.
- **States**
  - *loading* — close + items skeleton.
  - *not-found / cross-branch* — 404.
  - *already finalized* — read-only with the outcome: no Approve/Reject, only **Voltar** — plus **Reabrir para correção** on an Approved close for Manager/Admin before `LockDate`.
  - *self-recorded* — the caller recorded this close: the marker shows and Approve/Reject stay available.
  - *error* — reject without a reason → inline 400 (`ResponseErrorJson`); other failures → the standard error display.
  - *success* — status flips; return to the originating view (Manager Work Queue when none), its pending count decremented.
- **Navigation** — Gestão; from Manager Work Queue (pending item), a Cash-variance summary row, or a Monthly reconciliation blocker, each status-routed so only `Submitted` opens actionable; returns to the originating view, otherwise Manager Work Queue.

## Transaction — Create (fast entry)

- **Purpose** — record a money movement **fast** (operator at the counter).
- **Primary job** — enter a transaction in as few taps as possible.
- **Access** — branch-member with linked operator + account scope, or manager-admin (same scope as `POST /transaction`).
- **Permission fallback** — Member without a linked operator → **400** `TRANSACTION_MEMBER_REQUIRES_OPERATOR_LINK` (the `RecordedByOperator` resolver runs before the account-scope guard, so it surfaces as a validation error, not a 403); the screen explains a link is needed. Account out of scope → **403** `TRANSACTION_MEMBER_ACCOUNT_OUT_OF_SCOPE`. (Contrast the *read* path §6.10, where no-linked-operator is 403.)
- **Default view / filter** — today's date, the operator's linked/primary account, current time pre-filled.
- **Data shown**
  - **Form (full set)** — `[DTO: RequestCreateTransactionJson]`: `TransactionTypeId` (drives required fields), `Value`, `Date`, `TransactionTime?`, `AccountId`, `ClientId?` (fiado/Tab), `DueDate?` (cheque/card), `Description?`, `RecordedByOperatorId?`, `SaveAsDraft`. **Role-shaped:** a Member must **omit** `RecordedByOperatorId` — even their own id → 400 `TRANSACTION_MEMBER_CANNOT_OVERRIDE_RECORDED_BY_OPERATOR`; a Manager/Admin may supply it (acting on behalf of an operator) and **must** when they have no linked operator (400 `TRANSACTION_REQUIRES_RECORDED_BY_OPERATOR`). In Meu turno, a linked Manager/Admin omits it by default so the server resolves the caller's own Operator; choosing another operator is an elevated on-behalf task entered from Gestão.
  - **Lookups** — types & categories: `[composed]` `GET /transaction-type` + `GET /category` (any branch role); accounts: Member → own scope from `GET /operator/self-context`, Manager/Admin → `GET /account` (Manager/Admin-only); clients: `GET /client`; operators (elevated on-behalf entry): `GET /operator` (Manager/Admin-only). For a linked Manager/Admin entering from Meu turno, self-context supplies the initial own account/operator context; the whole-branch and on-behalf choices belong to Gestão without reducing the caller's server permission.
  - **Impact preview** *(automatic, never blocking)* — `[DTO: ResponseCreateTransactionPreviewJson {Impact, Warnings}]` — Receivable / Fiado / CashVariance impact; `Warnings` is a root-level sibling of `Impact`. It runs automatically inline (debounced) once the form holds a complete candidate and refreshes as fields change. It never blocks **Save**, and a failed preview shows nothing blocking.
- **Actions** — **Save** (Active) → `POST /transaction`; **Save as draft** → `POST /transaction` with `SaveAsDraft = true`; the automatic preview → `POST /transaction/preview`. Each real create sends a fresh client `Idempotency-Key`; an uncertain result retries the identical body with the same key and accepts the deterministic replay. A committed replay is resolved before DailyClose account coordination, so an in-flight Submit/Approve cannot turn that durable retry into a ledger-busy conflict.
- **Audit / lock context** — blocked when `Date ≤ LockDate` (explain the period is locked); `RecordedByOperatorId` stamped. A Terminal save first requires an active same-day DailyClose: none → `TRANSACTION_REQUIRES_OPEN_DAILY_CLOSE`, Draft → writable, non-Draft → `TRANSACTION_DAILY_CLOSE_LEDGER_FROZEN`. Tab and Bank writes have no DailyClose requirement. Impact preview remains hypothetical. The server accepts create-envelope replay for 24 hours and periodically purges expired envelopes; changing the body while reusing a live key is a verbatim 409 conflict.
- **States** — *loading* lookups · *validating* (400 inline) · *lock-date blocked* · *preview shown* · *success* (return to Day Cockpit / list) · *error* (row **not** assumed saved).
- **Navigation** — from Operator Day Cockpit in Meu turno, Transactions list in either mode, or the **Nova transação** navigation shortcut; returns to the semantic origin on save (Cockpit or list; a shortcut entry has no origin, so it returns to Transactions list). A selected fiado client links to its Client statement.

## Transaction — Edit (correction / audit)

- **Purpose** — correct an existing transaction's limited fields, with full impact preview — a **manager-control / audit** tool, not fast entry.
- **Primary job** — fix a mistake on a recorded row, seeing the downstream impact before committing.
- **Access** — per the §6.11 mutation contract: Manager/Admin elevated; a Member must additionally be the **recording operator** of the row *and* act on the **same branch-local day**. Shared-account lists expose other operators' rows to a Member — those open **read-only** (`[derived]` gate mirroring the guard).
- **Permission fallback** — Member without a linked operator → 403 `TRANSACTION_MEMBER_REQUIRES_OPERATOR_LINK` (mutation path); Member with the transaction's account out of scope → 403 `TRANSACTION_MEMBER_ACCOUNT_OUT_OF_SCOPE`; Member who is not the recording operator → 403 `TRANSACTION_MEMBER_NOT_RECORDING_OPERATOR`; Member editing a prior-day row → 403 `TRANSACTION_UPDATE_REQUIRES_SAME_DAY`; missing/cross-branch id → 404 `TRANSACTION_NOT_FOUND`.
- **Data shown**
  - **Loaded transaction (read context)** — `[DTO: ResponseTransactionJson]`: value, dates, status, audit fields, `Version` — but ids only for type/category/account/client; display labels are `[composed]` role-safely: `GET /transaction-type` + `GET /category` (any role); account name via `GET /operator/self-context` (Member) or `GET /account` (Manager/Admin); client via `GET /client/{id}`. Preserve the response `ETag` with the draft form.
  - **Editable fields (restricted)** — `[DTO: RequestUpdateTransactionJson]`: `Description?`, `DueDate`, `PaidAt?` (cheques and card settlements only — disabled on Tab/fiado rows, whose settlement is **Receber pagamento**, gap H), `ClientId?`, `TransactionTime?` only. **`Value`, account, type, and `Date` are read-only** — cancel + re-create to change them.
  - **Impact preview** *(Manager/Admin only — see Actions)* — `[DTO: ResponseEditTransactionPreviewJson {TransactionId, Impact, Warnings}]` — all three root-level siblings.
- **Actions** — **Preview edit** → `POST /transaction/{id}/edit-preview` — **Manager/Admin only** (the deliberate §6.14 exception); a permitted Member saves without the impact preview; **Save** → `PUT /transaction/{id}` with the loaded `If-Match`; **Finalize draft** → `POST /transaction/{id}/finalize` with the loaded `If-Match` (promote Draft → Active); **Cancel** → `POST /transaction/{id}/cancel` (required reason + loaded `If-Match`). For an installment row, offer **Cancel only this row** or **Cancel remaining plan installments**, behind a confirmation listing the affected cheques (executed per row from each sibling's list `Version`; enumeration and concurrency rules: loto-specs §6.10/§6.11).
- **Audit / lock context** — `UpdatedAt`/`UpdatedByUserId` stamped; blocked when `Date ≤ LockDate`; shows `CancelledAt`/`CancelledByUserId`/reason when cancelled; the current `Status` gates which actions are available (e.g. Finalize only on Draft). Finalize and Cancel additionally return `TRANSACTION_DAILY_CLOSE_LEDGER_FROZEN` while a non-Draft close exists for the row's `(account, date)`; the narrow field edit stays available because it cannot change §6.12 inputs.
- **States** — *loading* · *validating* (400 inline) · *lock-date blocked* · *preview shown* · *read-only* (Cancelled / locked) · *success* · *error*.
- **Navigation** — from Transactions list (manager) or a drafts list (Day Cockpit); returns there.

## Transactions list

- **Purpose** — the browsable, filterable ledger — the audit surface and the entry point to corrections.
- **Primary job** — find a specific transaction fast (by day, account, client, status) and open it.
- **Access** — branch-member: a Member sees rows on their **linked accounts** (§6.10 — a shared account shows both operators' rows; `Mine = true` narrows to own); Manager/Admin see the whole branch.
- **Permission fallback** — on the **list**, every empty/out-of-scope case is an **empty page short-circuit** (no 403, no row-existence leak): Member with no linked operator, linked with zero active account links, or an explicit `AccountId` outside scope. The 403s (`§6.10`) belong to the **detail read** (`GET /transaction/{id}`) and to mutations — not here.
- **Default view / filter** — `DateFrom = DateTo =` today (branch-local), all statuses, all in-scope accounts, `Mine` off, page 1 — the arriving question is almost always about *today*. Deep-links override it (e.g. the Work Queue's Draft-blocker link). In Meu turno, a linked Manager/Admin opening the list from navigation starts with `Mine = true` (the server resolves it to their linked operator; clearing it shows the whole branch); a Member's default is unchanged, since the server already scopes them to linked accounts. The Operator Day Cockpit's today's-transactions link deep-links with the cockpit's `(AccountId, today)`, so a Member and a linked Manager/Admin see the same rows for that account.
- **Data shown**
  - **Rows** — `Version`, `OriginTransactionId?`, `Date`, `Value`, `Direction`, `Status`, `AccountName`, `ClientName?`, `TransactionTypeName`, `DueDate`, `PaidAt?`, `Description?`, `CreatedAt`. `[DTO: ResponseListTransactionsJson.Items]` — names and guarded-action data arrive together; `OriginTransactionId` identifies plan membership and `Version` is the per-row `If-Match` value, so no detail lookup is required before a direct finalize/cancel.
  - **Paging** — `Page`, `PageSize`, `TotalCount`, `TotalPages`, `HasNext`/`HasPrevious`. `[DTO]`.
  - **Filters** — `[DTO: RequestListTransactionsJson]`, **role-shaped**: every role gets `DateFrom`/`DateTo`, `Status`, `ClientId` (`[composed]` `GET /client`), `Mine`, and deep-link-only `OriginTransactionId` for plan siblings. **Member:** account options come from `GET /operator/self-context` (the admin `GET /account` list is Manager/Admin-only), and the `OperatorId` filter is hidden — the server discards it for Members (`Mine` is the sanctioned own-rows filter). `OriginTransactionId` never widens linked-account scope; an out-of-scope plan is an empty page. **Manager/Admin:** full `AccountId` + `OperatorId` pickers, `[composed]` `GET /account` + `GET /operator`.
- **Actions** — open a row → Transaction — Edit; identify a cheque-plan row from `OriginTransactionId` and enumerate its siblings with the same filter; issue selected guarded sibling cancels directly from the returned row `Version`s; new entry → Transaction — Create; new cheque plan → Installment plan; filter/paginate → `GET /transaction`.
- **Audit / lock context** — rows with `Date ≤ LockDate` open read-only in the edit screen; Draft/Cancelled rows read visibly distinct (they don't count in totals).
- **States** — *loading* (row skeleton) · *empty* (nothing matches the filter) · *error* · *success*.
- **Navigation** — primary ledger destination in Gestão and Meu turno; Work Queue deep-link (Draft filter); Operator Day Cockpit (its today's-transactions link, filtered to the cockpit's account and today); returns to itself after edit/create when it was the origin.

## Installment (pre-dated cheque) plan

- **Purpose** — record a pre-dated cheque as N installment rows, with the full plan and its downstream impact visible before anything is saved.
- **Primary job** — build a plan that sums exactly and lands on sensible due dates, then commit it in one action.
- **Access** — same scope as the `POST /transaction/installment` write twin: Member with linked operator + account in scope, Manager/Admin elevated.
- **Permission fallback** — same as Transaction — Create: 400 `TRANSACTION_MEMBER_REQUIRES_OPERATOR_LINK` · 403 `TRANSACTION_MEMBER_ACCOUNT_OUT_OF_SCOPE`.
- **Default view / filter** — today's date; the type picker restricted to `SettlementRule = OperatorEnteredCheque` types (`[derived]` filter of the type lookup — the endpoint rejects any other type); manual rows by default, auto-generation opt-in.
- **Data shown**
  - **Plan form** — `Date`, `Value` (must equal the row sum), `Description?`, `TransactionTime?`, `TransactionTypeId`, `AccountId`, `ClientId?`, `RecordedByOperatorId?` (same role rules as Transaction — Create: Members omit it, unlinked Manager/Admin must supply it), manual `Installments[] {DueDate, Value}` (2–24 rows, strictly increasing, first in the future) **or** `AutoGenerateInstallments + DueDate + InstallmentCount`, `SaveAsDraft`. `[DTO: RequestCreateTransactionInstallmentJson]`. Lookups are role-shaped exactly as on Transaction — Create.
  - **Row preview** — `TotalValue`, `InstallmentCount`, per-row `Index`, `DueDate` (weekend/holiday-adjusted), `Value` (rounding residual on the last row), `Description` (`CH PRE (i/N)` convention). `[DTO: ResponseInstallmentPreviewJson / ResponseInstallmentPreviewRowJson]`.
  - **Impact preview** — the would-be open-cheque group (`OutstandingTotal`, `OldestOpenDueDate`, `OldestOpenBucket`, per-row buckets), the aggregated fiado delta (Tab-account plans with a client), the cash-variance shift on the plan's `(account, date)`. `[DTO: ResponseInstallmentPreviewImpactJson]`.
  - **Sum check** — running `Σ rows − Value` hint while editing manual rows. `[derived]` (the server enforces the exact-sum invariant on submit).
- **Actions** — **Preview** → `POST /transaction/installment/preview`; **Save** → `POST /transaction/installment` (returns the created rows `[DTO: ResponseCreateTransactionInstallmentJson]`); **Save as draft** → same with `SaveAsDraft = true` (§6.3 — every row lands `Draft`). Real creates use one fresh `Idempotency-Key` for the whole plan; an uncertain result retries the identical plan/key, and a committed replay bypasses DailyClose account coordination.
- **Audit / lock context** — blocked when `Date ≤ LockDate`; `RecordedByOperatorId` stamped; Draft plans stay out of the open-cheque report, fiado sums, and variance until finalized. A non-Draft close on the plan's `(account, date)` freezes the real plan create with `TRANSACTION_DAILY_CLOSE_LEDGER_FROZEN` (the preview is hypothetical). Sibling cancellation is cataloged on Transaction — Edit and enumerated by `OriginTransactionId`.
- **States** — *loading* lookups · *validating* (400 inline — exact-sum, row bounds, non-cheque type, non-increasing dates; verbatim) · *preview shown* · *lock-date blocked* · *success* (→ back to origin) · *error*.
- **Navigation** — from Transactions list, or from Transaction — Create when a cheque-rule type is picked; returns on save.

## Client statement (Extrato de fiado)

- **Purpose** — one client's fiado debt and its history: *quanto o José deve e desde quando*.
- **Primary job** — answer the counter question in one screen, then collect.
- **Access** — Manager/Admin: any client in the branch. Member: any branch client, once they hold at least one explicit `OperatorAccount(Tab)` grant (signed 1.8 — never implied by a Terminal assignment). Fiado is the client's debt with the lotérica, not with one drawer, so the statement is always the client's complete branch-wide history across every Tab — including repayments recorded on Tabs the caller is not assigned to — so its running balances reconcile row by row and its window reconciles to the balance. Shared counter work: keeps the entry mode, like Clients.
- **Permission fallback** — a Member with no Tab grant sees an explanation that fiado needs a Tab assignment, and no data.
- **Default view / filter** — the client passed in; the whole fiado history through branch-local today, newest first, paged.
- **Data shown**
  - **Client** — `Name`, `Phone`, `Cpf?` (masks per formatting §6). `[DTO: GET /client/{id} → ResponseClientJson]`.
  - **Balance** — `OutstandingTotal` (branch-wide `Out − In` as of today) and `OldestUnpaidDate?` (the oldest unpaid remainder). Netting is branch-wide FIFO per client: a payment on any Tab settles the oldest debt first. `[gap]` — gaps §0.3.3-H: `GET /client/{id}/fiado-statement` → `ResponseClientFiadoStatementJson`, server M7.7 Phase 5.5.
  - **Movements** — every active fiado sale (`Out`) and repayment (`In`) on every Tab: `Date`, `Direction`, `Value`, `AccountName`, `TransactionTypeName`, `Description?`, and `RunningBalance` — the client's balance after that row, computed server-side over the full history. Draft and Cancelled rows are excluded, as in the balance. `[gap]` — same read.
  - **Window** — `DateFrom`, `DateTo`, `OpeningBalance` (before the window), and `ClosingBalance` (at its end): each row's `RunningBalance` equals the chronologically previous row's `RunningBalance` plus its own `Out − In`, so consecutive rows reconcile on any single page; the window's oldest row starts from `OpeningBalance` and its newest ends at `ClosingBalance`, so `OpeningBalance + Σ(Out − In)` over **all** the window's rows — every page — `= ClosingBalance`, and a window ending today has `ClosingBalance = OutstandingTotal`. Paging per the list convention. `[gap]` — same read.
- **Actions** — **Receber pagamento** → Receber pagamento for this client; change the window → the same read with `DateFrom`/`DateTo`; page through movements; print or share → the device's own print / PDF (there is no export endpoint).
- **States** — *loading* · *empty* (no fiado history — all-clear) · *error* · *success*.
- **Navigation** — from Clients (a client row), Transaction — Create (a fiado client selected), and Fiado balance (a client drill); returns to its source.

## Receber pagamento

- **Purpose** — settle a client's open fiado debt, partially or in full.
- **Primary job** — see the client's current debt, enter the amount and how it was paid, register it.
- **Access** — Manager/Admin, and Members holding at least one explicit `OperatorAccount(Tab)` grant (signed 1.8). A client has no owning Tab — the debt is branch-wide — so any such caller may take a payment for any client, recorded on a Tab they may use (see Form).
- **Data shown**
  - **Client and current balance** — `[gap]` — gaps §0.3.3-H: the Client statement read.
  - **Form** — the Tab account receiving the payment (default: the workstation terminal's paired Tab in Meu turno; changeable among the caller's Tabs — a Member's granted Tabs, any Tab for Manager/Admin), amount, payment method (the choice of repayment transaction type — no separate field), date (branch-local today), description. `[gap]` — gaps §0.3.3-H: the *Receber pagamento* command, server M7.7 Phase 5.1.
- **Actions** — **Confirmar pagamento** → the *Receber pagamento* command `[gap]`: records an `In` for the client on the chosen Tab (`AccountId`) with a fresh `Idempotency-Key` (signed 1.7); an uncertain result retries the identical request with the same key. Netting is branch-wide, so whichever Tab records it, the client's oldest debt is reduced first; a partial payment leaves the remainder aged. Whether a cash repayment also enters the Terminal drawer is owned by server M9.5 Phase 2.2 `[gap]`.
- **Audit / lock context** — blocked when the date is at or before `LockDate`; a Tab write needs no DailyClose (M7.7 Phase 3 boundary).
- **States** — *loading* · *validating* · *success* (new balance shown) · *error* (row not assumed saved).
- **Navigation** — from Client statement, Clients, Transaction — Create, or Fiado balance; returns to Client statement when opened there, otherwise to its source.

## Daily ledger

- **Purpose** — the per-account statement: every movement in a window, wrapped in opening → closing balances.
- **Primary job** — reconcile one account's activity for a day (or short period) line by line.
- **Access** — manager-admin; whole-branch.
- **Default view / filter** — today, one account (`AccountId` is **required** by the endpoint); the UI preselects the last account viewed (`[derived]` client memory). Reconciliation happens account by account, so the forced choice fits the job.
- **Data shown**
  - **Header** — `AccountName`, window, `OpeningBalance`, `ClosingBalance`, `TotalIn`, `TotalOut`, `NetTotal`. `[DTO: ResponseDailyLedgerJson]`.
  - **Rows** — `Date`, `Value`, `Direction`, `Description?`, `TransactionTypeName`, `CategoryName`, `ClientName?`, `RecordedByOperatorName`, `DueDate`, `PaidAt?`. `[DTO: ResponseDailyLedgerItemJson]` (row `Id` carried for the deep-link).
  - **Paging** — `[DTO]`.
- **Actions** — change account/window (≤ 366 days; violations → 400 verbatim) → `GET /report/daily-ledger`; open a row → Transaction — Edit.
- **States** — *loading* · *empty* (no movement in the window) · *error* · *success*.
- **Navigation** — reports nav; from Manager Work Queue.

## Fiado balance

- **Purpose** — who owes what: outstanding fiado per client across the branch, as of a date.
- **Primary job** — scan the debtors and pick who to chase.
- **Access** — manager-admin; whole-branch.
- **Default view / filter** — `AsOfDate` = branch-local today (the server default when omitted), all clients.
- **Data shown**
  - **Total** — `TotalOutstanding`, `AsOfDate`. `[DTO: ResponseFiadoBalanceJson]`.
  - **Per-client rows** — `ClientName`, `OutstandingTotal`. `[DTO: ResponseFiadoClientBalanceItemJson]`.
- **Actions** — filter one client / change the as-of date → `GET /report/fiado/balance`; drill a client → Client statement, or Fiado aging with the client filter carried; quick action **Receber pagamento**.
- **States** — *loading* · *empty* (nothing outstanding — all-clear) · *error* · *success*.
- **Navigation** — reports nav; drills into Client statement and Fiado aging.

## Fiado aging

- **Purpose** — every unpaid fiado row, aged into buckets — how overdue the branch's credit is.
- **Primary job** — spot what slid into a worse bucket and act on it.
- **Access** — manager-admin; whole-branch.
- **Default view / filter** — `AsOfDate` today, all clients and accounts; server order `DueDate ASC, Date ASC, Id ASC` — oldest exposure first, which is already the action order.
- **Data shown**
  - **Rows** — `Date`, `DueDate`, `Value`, `DaysOutstanding`, `Bucket` (label per copy-guidelines §5; future-due rows appear as `Current` by design), `ClientName?`, `AccountName`, `Description?`. `[DTO: ResponseFiadoAgingItemJson]` (`TransactionId` carried for the deep-link).
  - **Bucket subtotals** — a sum per bucket. `[derived]` from the returned page (page-local when paginated — label it as such).
  - **Envelope** — paging, `AsOfDate`. `[DTO: ResponseFiadoAgingJson]`.
- **Actions** — filter client/account/as-of → `GET /report/fiado/aging`; drill a client → Client statement; open a row → Transaction — Edit (read context — `PaidAt` is disabled on fiado rows). Aging is computed server-side by **FIFO query-time netting**: a client's cumulative `In` payments cancel the oldest `Out` rows and only the unpaid remainder is aged (signed gap H decision, shipping with server M7.7 Phase 5.1 — until then this report still keys on per-row `PaidAt`, gap H). Fiado debt is never settled by editing past rows; settlement is **Receber pagamento**.
- **States** — *loading* · *empty* (no open fiado) · *error* · *success*.
- **Navigation** — from Fiado balance (client drill); reports nav.

## Open-cheque aging

- **Purpose** — pre-dated cheque plans that still have unpaid rows, grouped by plan, aged by their oldest open due date.
- **Primary job** — see which cheque plans are due or overdue and follow up.
- **Access** — manager-admin; whole-branch.
- **Default view / filter** — `AsOfDate` today; server order `OldestOpenDueDate ASC` — most urgent plans first.
- **Data shown**
  - **Plan groups** — `OutstandingTotal`, `OldestOpenDueDate`, `OldestOpenBucket`, `OpenRowCount`/`TotalRowCount` (an open-of-total count, `[derived]` phrasing), `ClientName?`, `AccountName`, `Description?`. `[DTO: ResponseOpenChequeAgingGroupJson]` (`OriginTransactionId` identifies the plan).
  - **Expanded rows** — per unpaid installment: `DueDate`, `Value`, `DaysOutstanding`, `Bucket`. `[DTO: ResponseOpenChequeAgingRowJson]` (`TransactionId` for the deep-link).
  - **Envelope** — paging, `AsOfDate`. `[DTO: ResponseOpenChequeAgingJson]`.
- **Actions** — filter account/client/as-of → `GET /report/cheques/open-aging`; expand a group; open an installment → Transaction — Edit (set `PaidAt` as cheques clear).
- **States** — *loading* · *empty* (no open cheques) · *error* · *success*.
- **Navigation** — reports nav; rows deep-link to Transaction — Edit.

## Cash-variance summary

- **Purpose** — variance per `(date, account)` over a period, with aggregates — the trend view that complements the Work Queue's single-day exceptions.
- **Primary job** — spot repeat offenders and patterns (an account short every Friday).
- **Access** — manager-admin; whole-branch.
- **Default view / filter** — current month-to-date, all accounts. Variance review follows the monthly lock rhythm, so the month is the natural window.
- **Data shown**
  - **Rows** — `DailyCloseId`, `Date`, `AccountName`, authoritative persisted `VarianceValue` (signed, formatting §3), `DailyCloseStatus` (a pending number reads differently from a signed-off one). Recalled/reopened/correction Draft snapshots are excluded even though their physical variance row is retained. `[DTO: ResponseCashVarianceSummaryItemJson]`; the id is resolved by exact `(Date, AccountId)`, never date alone.
  - **Aggregates** — `TotalVariance`, `MeanVariance`, `MaxVariance`, `MinVariance`. `[DTO: ResponseCashVarianceSummaryJson]`.
  - **Paging** — `[DTO]`.
- **Actions** — filter account/window (≤ 366 days) → `GET /report/cash-variance`; open a row directly by its `DailyCloseId` and route by status. No extra close-list lookup is needed.
- **States** — *loading* · *empty* (no cash-variance rows match the window — a Draft close has no variance row yet, so empty does not mean no closes exist) · *error* · *success*.
- **Navigation** — reports nav; from Work Queue (day aggregates → trend); rows route by close status like the Work Queue (only `Submitted` is approvable; finalized closes open read-only).

## Monthly reconciliation + lock

- **Purpose** — the month's gatekeeper: day-by-day closes, variance, and transaction counts, plus the blockers standing between the manager and advancing `LockDate`.
- **Primary job** — verify the month is clean, clear what isn't, lock it.
- **Access** — manager-admin; whole-branch.
- **Default view / filter** — the earliest unlocked month, initially `[derived]` from `Setting.LockDate` (`[composed]`: `GET /setting`) and, for a fresh `DateTime.MinValue` branch, `Branch.CreatedAt`. If the lock command discovers earlier active operational history, its `SETTING_LOCK_MONTH_INTERMEDIATE_UNRESOLVED` message names the first blocking `MM/yyyy`; show that month prominently beside the existing month navigation so the manager does not have to hunt for it. The view remains freely navigable via the `{year}/{month}` route.
- **Data shown**
  - **Lock readiness** — `LockReady` is true only when every close is Approved, zero Draft transactions remain, and every direct active Terminal `(account, date)` activity pair has a close. A completely empty month is ready; direct Terminal activity without any close is not. Weekends, holidays, and activation windows add no independent expectation, and paired-Tab fiado alone does not make its Terminal expected. `[DTO: ResponseMonthlyReconciliationJson]`.
  - **Blockers** — structured: `Type` (`UnapprovedClose` / `DraftTransactions` / `MissingExpectedClose`), `Day`, `DailyCloseId?`, `AccountId?`, `AccountName?`, `CloseStatus?`, `DraftTransactionCount?`. `[DTO: ResponseMonthlyReconciliationBlockerJson]` — the client composes the pt-BR sentence (copy-guidelines §5) and deep-links via the returned identifiers.
  - **Calendar days** — per day: closes (`AccountName`, `Status`, authoritative persisted `VarianceValue`, `DailyCloseId`), `ActiveTransactionCount`, `DraftTransactionCount`, `CancelledTransactionCount`, `NetVariance`. Draft retained snapshots are excluded from `VarianceValue`/`NetVariance`. `[DTO: ResponseMonthlyReconciliationDayJson / ...DayCloseJson]`.
  - **Current lock date** — `[composed]`: `GET /setting`.
- **Actions** — open a blocker: `UnapprovedClose` routes by its `CloseStatus` (only `Submitted` is approvable — `Submitted` → Daily-close approval · `Draft` → Close day · `Rejected` → Fix & resubmit); `DraftTransactions` → Transactions list (Draft filter, that day); `MissingExpectedClose` → Open day for the returned Terminal/day; change month → `GET /report/monthly-reconciliation/{year}/{month}` (out-of-range → 400 verbatim); **Bloquear mês** → `POST /setting/lock-month {Year, Month}` with the current Setting `ETag` in `If-Match`, behind a confirmation that states the consequence. The button follows this report's `LockReady`, which is **advisory**: the server's atomic recheck over the whole unlocked interval is the authority. When the lock is refused because an earlier month is unresolved, the returned `MM/yyyy` is shown prominently and navigable. The current/unfinished month is never lockable. Floor derivation, legacy-boundary repair, and coordination: loto-specs §6.6/§6.14.
- **Audit / lock context** — this screen *is* the lock control; once locked, the month's rows read as immutable everywhere else.
- **States** — *loading* · *clean month* (`LockReady`, zero blockers — all-clear, lock enabled) · *blockers present* · *error* · *success (locked)*.
- **Navigation** — from Work Queue (reconciliation-blockers group); reports nav. Unapproved-close blockers route by close status like the Work Queue (only `Submitted` is approvable; Draft → Close day, Rejected → Fix & resubmit, finalized closes open read-only); a Draft-transactions blocker → Transactions list (Draft filter, that day), then Transaction — Edit per row. Each returns here.

## My transaction summary

- **Purpose** — the operator's own production: totals and a per-category breakdown for a window.
- **Primary job** — answer "how much did I move this month?" without asking a manager.
- **Access** — any branch role (`[TokenAuthenticateBranch]`), single-operator resolution: Member → own linked operator; Manager/Admin must name `operatorId` or `mine` (neither → 400 `REPORT_OPERATOR_ID_REQUIRED` — this endpoint has **no** branch-wide roll-up).
- **Permission fallback** — Member with no linked operator, **or linked with zero active account links**, → **empty summary short-circuit** (not an error); Member naming another `operatorId` → 403 `REPORT_MEMBER_NOT_OWN_OPERATOR`.
- **Default view / filter** — current month; the totals feed the same monthly rhythm as payroll and the lock.
- **Data shown**
  - **Header** — `OperatorName`, window, `TotalTransactionCount`, `TotalInValue`, `TotalOutValue`, `NetValue`. `[DTO: ResponseOperatorTransactionSummaryJson]`.
  - **By category** — `CategoryName`, `Count`, `TotalIn`, `TotalOut`. `[DTO: ResponseOperatorCategoryTotalJson]`.
- **Actions** — change window (≤ 366 days) → `GET /report/operator-summary`. Managers reuse this same screen pointed at one operator (`operatorId`), entered from the Operators admin screen.
- **States** — *loading* · *empty* (no transactions in the window, or unlinked Member) · *error* · *success*.
- **Navigation** — Meu turno from Operator Day Cockpit for own context; Gestão entry from Operators for a manager-selected operator.

## My time-entry balance

- **Purpose** — hours worked against the daily target: totals, banco-de-horas balance, and the day-by-day trail.
- **Primary job** — answer "am I ahead or behind?" at a glance.
- **Access** — any branch role (`[TokenAuthenticateBranch]`): Member → own operator; Manager/Admin → `operatorId` (one), `mine` (own), or **neither → branch-wide roll-up** — the payroll view: one element per **active operator**, zero-entry operators included with empty `Days` and zeroed totals whether or not they carry a login link (M7.7 Phase 7), plus any operator with entries in the window even after deactivation. Labelling an operator's missing days *Sem registro* is a client-side derivation (requested window minus returned `Days`) — the server publishes no operating-day matrix.
- **Permission fallback** — unlinked Member → **empty `Operators` list** (short-circuit); Member naming another `operatorId` → 403 `REPORT_MEMBER_NOT_OWN_OPERATOR`.
- **Default view / filter** — current month (banco de horas settles monthly).
- **Data shown**
  - **Window** — `DateFrom`/`DateTo` on the wrapper (one range for all operators). `[DTO: ResponseTimeEntryBalanceSummaryJson]`.
  - **Per operator** — `OperatorName`, `TotalHours`, `TotalBalanceHours`, `PresentDays`, `AbsentDays`, `OwingDays`, `AbonadoDays`, `ContainsInProgress`. `[DTO: ResponseTimeEntryBalanceOperatorJson]` — decimal hours render as durations (formatting §5).
  - **Per day** — `Date`, `Status` (label per copy-guidelines §5), `TotalHours`, `BalanceHours`, `IsInProgress` (live-recomputed rows flagged), plus the day's target `[derived]` as `TotalHours − BalanceHours` (the contract carries no target field and no endpoint exposes the policy ledger; the identity holds for every status). `[DTO: ResponseTimeEntryBalanceSummaryDayJson]`. Balances are computed under the effective-dated `TimeEntryPolicy` applicable to each day (§3.20/§6.7), so a settings change never rewrites the days before it — a window spanning a change legitimately shows different targets per day. The target is a per-day value: it appears only on a single operator's day-by-day table, never on the branch roll-up, whose window can span a policy change.
  - **Anything-open flag** — `Operators.Any(ContainsInProgress)`. `[derived]` — the contract deliberately has no branch-level field.
- **Actions** — change window (≤ 366 days — the standard report guardrails apply here too, 400 verbatim) → `GET /report/timeentry-balance` (`mine` and `operatorId` are mutually exclusive — 400 verbatim); drill a day → Clock in / out (own) or Time-entry management (manager).
- **States** — *loading* · *empty* (no entries / unlinked) · *in-progress* variant (live rows marked) · *error* · *success*.
- **Navigation** — Meu turno for own context; in Gestão, the Manager/Admin roll-up doubles as the payroll summary reached from Time-entry management.

## Operator Day Cockpit

- **Purpose** — the operator's single "today" home: shows the day's state and the **next action**, so operators don't navigate modules. It is an operational action hub, **not** a financial KPI dashboard: no money totals or stat tiles — financial analysis belongs to My transaction summary.
- **Primary job** — know and do the next step of the day (open → record → close → resubmit).
- **Access** — a Member in Meu turno, or a Manager/Admin linked to an active Operator after explicitly switching to Meu turno; own operator + linked accounts establish the cockpit context, while the branch role and server permissions remain unchanged.
- **Permission fallback** — Member without a linked operator → a **setup-needed** state (no operator linked) with **Solicitar vínculo**, which sends the managers an operator-link request `[gap]` — gaps §0.3.3-V. A linked caller with no usable Terminal assignment sees the account setup-needed state with the same request action. A Manager/Admin without an active link is intercepted before cockpit entry and stays in Gestão with the setup explanation defined by the IA map. Read/list areas may empty-short-circuit per their own endpoints; **write** actions (open day, record, close) are disabled or surface their own permission/validation errors (e.g. create's 400 `TRANSACTION_MEMBER_REQUIRES_OPERATOR_LINK`). §6.10 governs transaction-list *reads* specifically — not the whole screen.
- **Default view / filter** — today (branch-local) on the **workstation terminal**, chosen from the Terminal accounts in `ResponseSelfContextJson.AvailableAccounts` (Tab and Bank never count): exactly one → bound automatically, no prompt; more than one → on first entry the cockpit asks *"Em qual terminal você vai operar hoje?"* before showing the next action, and its header shows the terminal with **Trocar**. The choice is kept per device, per branch, per branch-local day: a reload does not ask again; Switch branch and the next branch-local day clear it. A linked Manager/Admin in Meu turno follows the same rule. `PrimaryAccount` never guesses the workstation.
- **Data shown** *(next-action oriented)*
  - **Today's close state** — the workstation terminal's close for today, if any, and its status. `[composed]`: `GET /dailyclose?AccountId&DateFrom=DateTo=` today. When a Draft needs context, `GET /dailyclose/{id}` distinguishes plain work, a rejected-correction Draft (`RejectionReason`), and a predecessor-triggered opening recheck (`OpeningRecheckRequiredAt` + triggering-close/user ids); the lightweight list row does not carry those fields.
  - **Today's transactions + draft count** — the rows: `[composed]` `GET /transaction?AccountId={workstation}&DateFrom=DateTo=` today, paged — the `(account, date)` the close is judged on, never filtered by author. The Draft count is exact and separate: the same filter with `Status=Draft&PageSize=1`, reading `TotalCount` — exactly what blocks Submit, and what P3 evaluates. On a shared terminal it splits into *seus* (the same request with `Mine=true`, reading `TotalCount`) and *de outros operadores* (the difference).
  - **My open closes today** — every close today on my Terminal accounts that I recorded or that is still unclaimed: a set, not a single value. `[composed]`: `GET /dailyclose?DateFrom=DateTo=` today (a Member's read scope already limits it to linked accounts; a linked Manager/Admin keeps only their own Terminal assignments), keeping rows whose recorder is me or none. When it holds a terminal other than the workstation, an **outros terminais** strip beside the next action lists each with its status, so a terminal left unsubmitted is always visible.
  - **Clock status** — clocked in/out, live-running. `[composed]`: `GET /timeentry` (today, `Mine`).
  - **Next action** — `[derived]`; the first matching row wins:
    - **P1 Correction needed** — a close I recorded, on any unlocked date up to today, that is `Rejected` or an opening-recheck / rejected-correction Draft → *"Fechamento de {data} precisa de correção"* · the rejection reason, or *"O fechamento anterior foi corrigido; reconte este."* · **Corrigir fechamento** → Fix & resubmit. `[composed]`: `GET /dailyclose?Mine=true&DateFrom=LockDate+1&DateTo=today` (`Mine` matches the recording operator); a prior-day Draft is resolved with `GET /dailyclose/{id}` because list rows lack the correction fields.
    - **P2 Day not open** — no close today on the workstation terminal → *"Dia ainda não aberto"* · *"Abra o dia do {terminal} para registrar movimentações."* · **Abrir dia** → Open day.
    - **P3 Drafts block the close** — a Draft close today with Draft transactions on its `(account, today)` → *"{n} movimentações em rascunho"* · *"Finalize ou cancele os rascunhos antes de fechar o caixa."* · **Finalizar movimentações** → Transactions list (Draft filter, that day), which returns here.
    - **P4 Day open** — a Draft close today → *"Dia aberto"* · *"Registre as movimentações e feche o caixa no fim do turno."* · **Fechar caixa** → Close day; secondary **Nova transação**.
    - **P5 Another terminal still open** — *My open closes today* holds another terminal with an open or uncounted close → *"{terminal} ainda está aberto"* · *"Você tem outro caixa aberto hoje. Feche-o antes de encerrar o turno."* · **Ir para {terminal}** → that terminal's Close day.
    - **P6 Submitted** today → *"Aguardando aprovação do gerente"* · *"Enviado às {hora}."* · **Desfazer envio** only for the same-day recorder.
    - **P7 Approved** today → *"Dia encerrado e aprovado"* · *"Nada pendente hoje."* · no action; the approved close opens read-only.
- **Actions** — **Open day** → Open day (`POST /dailyclose`); **Record** → Transaction — Create; **Close day** → close flow (`PUT /dailyclose/{id}/items` + `POST /dailyclose/{id}/submit`); **Fix & resubmit** → edit a rejected or opening-recheck close and resubmit; **Trocar** terminal (more than one Terminal); **Solicitar vínculo** `[gap]` (setup-needed only); **Clock in/out** → `/timeentry` for Members only. Linked Manager/Admin callers keep the elevated time-entry contract and record their own hours in Gestão → Time-entry management instead of the Member tap.
- **Audit / lock context** — a rejected close shows its rejection reason (via `GET /dailyclose/{id}`); a submitted close shows submitted-at; `LockDate` rarely bites for today.
- **States** — *loading* · *terminal choice* (more than one Terminal, first entry of the day) · *setup-needed* (unlinked Member, or linked caller without a usable Terminal → guidance + **Solicitar vínculo**) · *error* · *success* (state + next action P1–P7). An unlinked Manager/Admin never enters *setup-needed* because the IA gate keeps them in Gestão. Variants: *rejected* and *opening changed* (P1 — show the reason or the recount explanation + source date after resolving the triggering close), *outros terminais* strip, *submitted* (P6, read-only), *dia encerrado* (P7).
- **Navigation** — Meu turno home after a Member branch session and after the explicit mode switch for a linked Manager/Admin; links to Transaction — Create (fast entry), Open day, Close day, Fix & resubmit, My transaction summary, and My time-entry balance. Clock in / out is present only for Members. The workstation-first selector and the multi-terminal strip are specified above; gap L keeps only fiado routing to the workstation's paired Tab (server M7.7 Phase 5.3). Manager-as-operator IA gap R is resolved by the navigation map.

---

## Open day

- **Purpose** — create a `Draft` close for a Terminal account — the flag that the drawer is being tracked.
- **Primary job** — one tap: confirm the account, open the day.
- **Access** — Member with linked operator + Terminal account in scope may open branch-local today; Manager/Admin may open a Terminal for today or a prior date before lock. Tab and Bank are rejected with `DAILYCLOSE_ACCOUNT_NOT_TERMINAL`; every role's future date is rejected with `DAILYCLOSE_FUTURE_DATE_NOT_ALLOWED`; a Member's prior date is rejected with `DAILYCLOSE_MEMBER_OPEN_REQUIRES_TODAY` (§6.13 Open matrix).
- **Mode & ownership** — dual-mode screen that keeps the caller's active mode: in Meu turno an operator opens their own workstation terminal from the Day Cockpit; in Gestão a Manager/Admin opens any Terminal from a Work Queue or Monthly reconciliation exception. A Manager/Admin in Gestão is never gated behind an operator link.
- **Permission fallback** — Member without a linked operator → 403 (verbatim, `EnsureCanOpen`); account out of scope → 403.
- **Default view / filter** — calendar-only `BranchLocalDate` (`YYYY-MM-DD`) from `GET /branch/current` (server-authoritative; never parsed as a JavaScript instant and never replaced by the device clock). In Meu turno the account is the cockpit's **workstation terminal** — never a guess from `PrimaryAccount`: the screen confirms *"Abrir dia para {terminal}"* and lets the operator switch terminal before opening; with no Terminal assignment it shows the setup-needed state. This also applies to a linked Manager/Admin in Meu turno. In Gestão the manager picks the Terminal; elevated backdating is an explicit Gestão control; future dates remain disabled and server-rejected.
- **Data shown** — account options, **role/mode-shaped**: Member and linked Manager/Admin in Meu turno → own context `[DTO: ResponseSelfContextJson.AvailableAccounts / ResponseOperatorAccountJson]`; Manager/Admin opening for any account from Gestão → `[composed]`: `GET /account` (self-context only returns the caller's own linked accounts — none at all for an unlinked manager).
- **Actions** — **Open day** → `POST /dailyclose {Date, AccountId}` → `[DTO: ResponseDailyCloseJson]`; the cockpit flips to record / close.
- **Audit / lock context** — Open stamps `OpenedByUserId` only; `RecordedBy*` and `SubmittedBy*` remain null until first count and Submit respectively. `LockDate` guard applies (rare for today); a duplicate open close for `(account, date)` → conflict verbatim (unique-constraint translation); the UI then resolves the existing close `[composed]`: `GET /dailyclose?AccountId&DateFrom=DateTo=` today, and jumps to it.
- **States** — *validating* · *conflict* (already open → offer the existing close) · *lock-blocked* · *success* · *error*.
- **Navigation** — from the Day Cockpit's next action, or from an elevated management investigation for a missing expected close; returns to the cockpit for Meu turno work or to the originating Work Queue / Monthly reconciliation view for Gestão work.

## Close day

- **Purpose** — the end-of-day count: enter closing values per product against their opening values, then submit for review.
- **Primary job** — count the drawer, type the values, submit — with the opening numbers and the day's movement visible so mistakes jump out.
- **Access** — the §6.13 edit-items matrix: before first count, any account-scoped Member may claim the unlocked close even when an elevated user opened it in the past; after claim, the recording Member may edit a plain Draft on the same branch-local day and a Rejected/opening-recheck correction until period lock; Manager/Admin anytime pre-lock. A direct prior-day Manager/Admin Recall/Reopen creates a Manager-owned plain Draft and does not grant Member access by inference.
- **Mode & ownership** — dual-mode screen that keeps the caller's active mode: in Meu turno an operator opens it from the Day Cockpit for their own shift; in Gestão a Manager/Admin opens it from the Work Queue or Monthly reconciliation to handle an exception. A Manager/Admin in Gestão is never gated behind an operator link.
- **Permission fallback** — **Member only:** out-of-scope account / no linked operator → 403. Manager/Admin need no operator link: they can inspect, edit items, and submit on behalf of any Terminal account in the branch. For every role: missing/cross-branch id → 404; **item edits** denied by state, recording operator, or local day → 409 `DAILYCLOSE_NOT_EDITABLE`; **submit** splits differently: a non-submittable state → 409 `DAILYCLOSE_NOT_SUBMITTABLE`, while a Member's missing link / not-recorder / wrong-day → 403 (verbatim, the §6.11 permission keys).
- **Default view / filter** — the close passed in; from the cockpit, the workstation terminal's close for today.
- **Data shown**
  - **Product rows** — **one call**, `GET /dailyclose/{id}/review`, enumerates every active product plus any retired product with a saved value on this close, ordered by `DisplayOrder`, including the full active set on a fresh close. Each row carries `ProductId`, `ProductName`, `DisplayOrder`, `OpeningValue?`, nullable `ClosingValue?`, and `IsCashVarianceProduct`. `[DTO: ResponseDailyCloseReviewItemJson]`. Prior-only retired products are omitted; a retired current value remains visible for reconciliation; newly active products appear with opening zero and null closing. Every non-variance row offers **Repetir abertura**, which copies the row's displayed `OpeningValue` into the closing input `[derived]`. It claims no provenance: the server returns zero both for a real zero and when no prior counted value exists (a first close, a newly active product).
  - **Opening values** — `OpeningValue?` per item from the most recent prior **counted** close (§6.5 server-derived; zero when no eligible prior item exists) in the same review response. The variance row is the sole null-opening row.
  - **Day's transactions** — context for the count, the same data Daily-close approval shows its reviewer. `[composed]`: `GET /transaction?AccountId&DateFrom=DateTo=` the close's `(account, date)`, paged — the close's own account and date, **never** the operator's wider account scope, because another terminal's movement would make the variance unexplainable. Draft rows render visibly distinct: they are the Submit blocker below. Show the rows and their net; **never** an "expected closing" / *valor esperado* derived from opening + movement — the operator counts the drawer and types what is there.
  - **Correction context** — `ItemsFirstRecordedAt?` plus `OpeningRecheckRequiredAt?`, `OpeningRecheckTriggeredByDailyCloseId?`, and `OpeningRecheckTriggeredByUserId?` on `ResponseDailyCloseReviewJson`. A non-null recheck timestamp means a predecessor's real count change returned this close to Draft; show that cause separately from any retained `RejectionReason`.
  - **Identity context** — rich Get/review responses expose `OpenedByUser*`, immutable `RecordedByUser*` / optional `RecordedByOperator*`, and current `SubmittedByUser*` / optional `SubmittedByOperator*`. List/dashboard expose recorder and submitter separately. An elevated recorder without an Operator is shown by user name with a null operator; that is valid, not missing data.
  - **Variance row** — always present in review and selected by `IsCashVarianceProduct = true` — **never by product-name matching**. Its `ClosingValue` is null while Draft, including after Recall/Reopen/rejected correction/opening recheck when the physical prior snapshot is retained but deliberately hidden. As the operator types, `POST /dailyclose/{id}/variance-preview` computes from the complete unsaved candidate list through the same calculator Submit uses. `[DTO: ResponseDailyCloseVariancePreviewJson {CashVariance}]`. Preview never saves.
  - **Item form** — `{ProductId, Value}` per editable, non-variance product plus optional `Notes`. `[DTO: RequestPutDailyCloseItemsJson / RequestUpsertDailyCloseItemJson]` — sending the variance product is rejected (`DAILYCLOSE_ITEM_PRODUCT_FORBIDDEN`). `Notes` is verbatim, max 1000; empty clears and omission preserves. Keep the loaded `ETag` with the form.
  - **Submit blockers** — the exact count of Draft transactions on the close's `(account, date)` — `[composed]`: `GET /transaction?AccountId&DateFrom=DateTo=&Status=Draft&PageSize=1`, reading `TotalCount`, never counted from a page of the list above (Submit returns `DAILYCLOSE_OUTSTANDING_DRAFT_TRANSACTIONS` until they are finalized/cancelled), whether a count has ever been saved (`ItemsFirstRecordedAt`; Submit returns `DAILYCLOSE_ITEMS_NOT_RECORDED` otherwise), and the server-only earliest intervening activity day without a counted close (`DAILYCLOSE_PRIOR_DAY_NOT_COUNTED`, whose message supplies the actionable date). A link to finalize the drafts carries this screen as its origin and returns here with the unsaved count restored.
  - **Fiado reference balance** — §6.5 expects the form to show it for reference, but the balance report is Manager/Admin-only, so a Member has no endpoint for it. `[gap]` — gaps §0.3.3-A; omitted for Members until resolved.
- **Actions**
  - **Preview variance** → `POST /dailyclose/{id}/variance-preview {Items}` as the operator types.
  - **Save** → `PUT /dailyclose/{id}/items {Notes?, Items}` with the loaded `If-Match`. **Autosaved** while the close is today's plain Draft (date = branch-local today, no `RejectionReason`, no `OpeningRecheckRequiredAt`): debounced, one request in flight at a time, fired only after a real value or Notes change — merely opening the screen never claims the recorder. A status line reads *"Salvando…"* / *"Salvo às {hora}"* / *"Não salvo — tentar novamente"*. Any other Draft — a correction, an opening recheck, or a prior day — keeps typed input on the device automatically and saves only on an explicit **Salvar**, with a visible *"alterações não salvas no servidor"* marker, because a real value change there can return the next later close to Draft. The first successful save claims the immutable recorder. When a save returns an affected successor, show *"O fechamento de {data} voltou para rascunho porque o fechamento anterior foi corrigido."*
  - **Submit** → `POST /dailyclose/{id}/submit` — always explicit, never automatic. A fresh backdated close claimed by a Member still needs Manager/Admin submission; only an opening-recheck or rejection marker lets the recorder resubmit a prior day.
  - **Desfazer envio** → `POST /dailyclose/{id}/recall` on a mistaken Submitted close: the recording Member on the same branch-local day, Manager/Admin before lock.
  - Save, submit, and recall rules, concurrency tokens, claim races, and coordination: loto-specs §6.13.
- **Audit / lock context** — every transition is stamped and blocked at or before `LockDate`. Submit freezes the count, Notes, and the account/day ledger; Desfazer envio and every return to Draft clear the current submission but keep the first-count recorder. A stale save reloads the form and a busy account retries, each with its verbatim message (see States).
- **States** — *loading* (review context) · *validating* · *live variance shown* · *autosaving / saved / not saved* (today's plain Draft) · *unsaved on server* (corrections) · *opening changed / recount required* · *successor returned to Draft* (explicit save consequence) · *stale-save conflict* (409 `DAILYCLOSE_STALE_WRITE` — reload required) · *coordination busy* (retryable 409 `DAILYCLOSE_LEDGER_COORDINATION_BUSY`) · *lock-blocked* · *submitted* (read-only, awaiting review) · *success* · *error*.
- **Navigation** — from the Day Cockpit, or from Work Queue / Monthly reconciliation when an elevated caller follows a close exception; after submit, back to the cockpit's awaiting-approval state in Meu turno or to the originating management view in Gestão.

## Fix & resubmit

- **Purpose** — the recovery path after a manager rejection or a predecessor-triggered opening change: see why, correct/recount, send again.
- **Primary job** — read the rejection and/or opening-recheck cause and resubmit without hunting for what changed.
- **Access** — the immutable first-count recording operator may edit an explicitly `Rejected` close, its correction Draft, or an opening-recheck Draft until the period locks; Manager/Admin may also correct it. A Manager submitting on that operator's behalf does not take ownership. Same-day Member restriction remains only for voluntary **Desfazer envio** of Submitted. A direct prior-day elevated Recall/Reopen is Manager-owned.
- **Mode & ownership** — dual-mode screen that keeps the caller's active mode: in Meu turno the recording operator opens it from the Day Cockpit; in Gestão a Manager/Admin opens it from the Work Queue or Monthly reconciliation to handle an exception. A Manager/Admin in Gestão is never gated behind an operator link.
- **Data shown**
  - **Rejection context** — `RejectionReason` `[DTO: GET /dailyclose/{id} → ResponseDailyCloseJson]`. When/by-whom comes only from the **generic audit pair** `UpdatedAt`/`UpdatedByUserId` — valid as rejection metadata while `Status = Rejected` (`[derived]`); there are no rejection-specific actor/timestamp fields, and resolving the user id to a name needs `GET /branch/users` (Manager/Admin-only). The operator's job needs the reason, not the rejector's name — so the screen shows reason + time for Members and adds the name only for elevated roles.
  - **Opening-recheck context** — `OpeningRecheckRequiredAt`, `OpeningRecheckTriggeredByDailyCloseId`, and `OpeningRecheckTriggeredByUserId` on Get/review. Resolve the triggering close for its date and show that its correction changed this close's opening. Keep this message separate from rejection; a cascade-demoted Rejected close legitimately carries both.
  - **The close form** — identical to Close day, **including the Day's transactions context**: review items, item form, and the close's `(account, date)` movement — a rejection is usually about a number the reviewer could not reconcile against that movement. A rejected close *has* items, so review serves it fully. `[DTO: ResponseDailyCloseReviewJson]`.
- **Actions** — edit items → `PUT /dailyclose/{id}/items` on an explicit **Salvar** (typed input is kept on the device meanwhile — corrections are never autosaved to the server, see Close day) (auto `Rejected → Draft` when applicable; the physical variance row is retained but hidden while Draft); live preview the correction/recount; resubmit → `POST /dailyclose/{id}/submit` (updates that row in place and clears opening-recheck lineage).
- **Audit / lock context** — the rejection reason remains visible through the correction Draft and clears on successful resubmission; resubmission restamps `SubmittedAt`; the lock guard is unchanged. Denials mirror Close day: item edits → 409 `DAILYCLOSE_NOT_EDITABLE` for invalid state or caller, submit → 409 `DAILYCLOSE_NOT_SUBMITTABLE` for state and 403 for Member identity causes; `DAILYCLOSE_OUTSTANDING_DRAFT_TRANSACTIONS` blocks a still-open ledger draft.
- **States** — *rejected* (reason leading) · *opening changed* (recount explanation leading) · *rejected + opening changed* (both facts) · *editing* (back in Draft) · *resubmitted* (awaiting) · *error*.
- **Navigation** — from the Day Cockpit's rejected state or a status-routed management exception; back to the cockpit after an own-context resubmit, or to the originating Work Queue / Monthly reconciliation view after elevated handling.

## Clock in / out

- **Purpose** — the operator's ponto: tap in, tap out, watch today's running total.
- **Primary job** — one tap, correctly routed — even across midnight.
- **Access** — **Member only** for the tap (dual-shape `PUT /timeentry` contract, §6.7: a Member sends `Action`, never `Segments`). Managers/Admins correct time — and a linked Manager/Admin records their own hours (Manager/Admin format, own operator) — in Time-entry management; the tap screen is not for them.
- **Permission fallback** — Member without a linked operator → setup-needed state; the tap itself fails **403** `TIMEENTRY_REQUIRES_OPERATOR_LINK` (a permission outcome, not validation — a malformed `OperatorId` is the separate 400).
- **Default view / filter** — today (branch-local), own operator.
- **Data shown**
  - **Today's entry** — `Status`, `TotalHours` (live-recomputed), `BalanceHours`, `IsInProgress`. `[DTO: GET /timeentry (Mine, today) → ResponseListTimeEntryItemJson]` — list items carry **no segments**; the segment pairs (`ClockIn`/`ClockOut?`, wall-clock — render as-is, formatting §4) come from the detail read. `[DTO: GET /timeentry/{timeEntryId} → ResponseTimeEntryJson / ResponseTimeEntrySegmentJson]`.
  - **Next tap** — an open segment exists → clock-out; none → clock-in. `[derived]`.
  - **Prior-day open shift** — when yesterday has an open segment, surface the §6.7 choice explicitly: close the overnight shift (`Close` on *yesterday's* date) vs start fresh (`Open` on today). `[derived]` from yesterday's `IsInProgress`.
- **Actions** — tap → `PUT /timeentry {OperatorId (own), Date, Status: Present, Action: Open | Close}` (`[DTO: RequestUpsertTimeEntryJson]`); repeated taps are idempotent no-ops (§6.7).
- **States** — *not clocked in* (clock-in CTA) · *running* (live timer `[derived]`, clock-out CTA) · *closed for today* · *overnight-choice* variant · *error* (verbatim).
- **Navigation** — from the Day Cockpit; links to My time-entry balance.

## Time-entry management

- **Purpose** — the manager's time console: day statuses (folga, férias, faltas), segment corrections, cleanup of forgotten punches.
- **Primary job** — fix a wrong or missing punch and set non-worked-day statuses.
- **Access** — manager-admin for every mutation (segment CRUD is `[TokenAuthorize]`; entry upsert/deactivate enforce the role in the use case). The list endpoint is branch-authenticated, but Member listing belongs to My time-entry balance — this is the manager surface.
- **Default view / filter** — current month, all operators, `InProgressFirst = true`, so forgotten punches are prioritized across the full result before paging. A focused queue sends `IsInProgress = true` and returns only open-segment entries.
- **Data shown**
  - **Entry rows** — `Date`, `OperatorName`, `Status` (label §5), `TotalHours`, `BalanceHours`, `IsInProgress`. `[DTO: ResponseListTimeEntryItemJson]`; filters `OperatorId`, `DateFrom`/`DateTo`, `Status`, `Mine`, `IsInProgress`, `InProgressFirst`, paging. `[DTO: RequestListTimeEntriesJson]`.
  - **Entry detail** — the full segment list with audit fields (`UpdatedAt`, `UpdatedByUserId`). `[DTO: ResponseTimeEntryJson / ResponseTimeEntrySegmentJson]`.
- **Actions** — set a day's status / edit its segments → `PUT /timeentry` (admin shape: full snapshot `Segments[{Id?, ClockIn, ClockOut?}]`, never `Action`). **The snapshot reconciles — it does not replace atomically:** every persisted active segment id must appear in the payload (a missing one → `TIMEENTRY_SEGMENT_NOT_FOUND`), an existing segment's `ClockIn` is immutable through this route (`TIMEENTRY_SEGMENT_CLOCK_IN_LOCKED` — use the granular segment `PUT`), new segments carry `Id = null`, and removals go through the granular `DELETE`. *(§6.7's "replaces the entire segment set atomically" wording is stale against this shipped behavior — flagged as a server spec-sync issue, not a catalog decision.)* Granular routes: add → `POST /timeentry/{timeEntryId}/segment`; edit → `PUT /timeentry/segment/{segmentId}`; remove → `DELETE /timeentry/segment/{segmentId}`; **Remover registro do dia** → `DELETE /timeentry/{timeEntryId}` — for an entry created by mistake (wrong operator, wrong date, duplicate), never to record an absence, which is a Status. The two are opposites: a Status keeps the day in the banco de horas (an abonado day scores the target with zero balance; an owing day scores −target), while removal takes the day out entirely and it falls back to *Sem registro*. Confirmation: *"O registro de {data} de {operador} será removido. O dia passa a contar como sem registro e sai do banco de horas. Essa ação não pode ser desfeita."*
- **Audit / lock context** — every admin edit stamps the audit pair on entry and segment; day-bounds and ≤ 24 h segment rules are server-enforced (verbatim on violation). A correction recomputes the entry's totals under the effective-dated `TimeEntryPolicy` for that entry's own date (§3.20/§6.7) — never today's constants — so fixing an old punch cannot silently apply a newer target or lunch tier. Every mutation route is blocked when the effective `Date ≤ LockDate` (`TIMEENTRY_DATE_LOCKED`): full-snapshot `PUT /timeentry`, whole-entry `DELETE`, and granular segment add/edit/remove. All share the month boundary through commit, so a write queued behind a successful lock is rejected instead of rewriting or removing payroll totals inside the locked month. A busy boundary returns retryable `SETTING_LOCK_MONTH_COORDINATION_BUSY`.
- **States** — *loading* · *empty* · *in-progress highlighted* · *error* · *success*.
- **Navigation** — Gestão admin navigation; pairs with the branch-wide roll-up of My time-entry balance for totals.

## Operators (admin)

- **Purpose** — manage the branch's employees: create, rename, link or unlink a login, deactivate.
- **Primary job** — get a new employee working (row + login link) in under a minute.
- **Access** — manager-admin.
- **Data shown** — `Name`, `UserId?` (linked active branch login or none). `[DTO: ResponseListOperatorsJson / ResponseOperatorJson]`; a non-null id resolves through the active `GET /branch/users` membership list. Removing a membership atomically clears every matching Operator link for that branch while preserving the employee row, history, and assignments, so no stale unresolvable identity remains.
- **Actions** — create → `POST /operator {Name, UserId?}`; rename / change link → `PUT /operator/{id}` (`UserId = null` **clears the login link but keeps the employee row** — history survives); deactivate → `DELETE /operator/{id}`. Linking enforces at most one active linked operator per user per branch (conflict verbatim). Resolve a pending operator-link request → link an existing operator or create one, then Account assignment `[gap]` — gaps §0.3.3-V (server M8).
- **States** — *loading* · *empty* · *error* (link conflicts verbatim) · *success*.
- **Navigation** — Gestão admin navigation; a row links to Account assignment and to My transaction summary in Gestão with that operator selected.

## Account assignment (admin)

- **Purpose** — wire operators to the accounts they may act on — the source of the Member account scope that every §6.10/§6.11 rule reads.
- **Primary job** — assign an account and mark the primary one.
- **Access** — manager-admin.
- **Data shown** — per selected operator (picker `[composed]` from `GET /operator`): assigned accounts with `AccountName`, `AccountType` (label §5), `IsPrimary`, `AccountInstitution?`, `AccountNumber?`. `[DTO: ResponseListOperatorAccountsJson / ResponseOperatorAccountJson]`.
- **Actions** — assign → `POST /operator/{operatorId}/accounts {AccountId}`; unassign → `DELETE /operator/{operatorId}/accounts/{accountId}`; set primary → `PUT /operator/{operatorId}/accounts/{accountId}/primary` (one primary per operator, server-enforced). The operator-facing read of this configuration is `GET /operator/self-context` (Day Cockpit / Open day).
- **States** — *loading* · *empty* (an operator with no accounts — exactly the state that breaks fast entry; the empty state says so) · *error* · *success*.
- **Navigation** — from Operators; Gestão admin navigation.

## Accounts (admin)

- **Purpose** — the branch's financial containers: terminals, bank accounts, and their optional paired Tab (fiado) accounts.
- **Primary job** — create the right account *type* and manage Terminal ↔ Tab pairing safely.
- **Access** — manager-admin.
- **Data shown** — `Name`, `Type` (label §5), `Institution?`, `Number?`, pairing state (`TabAccountId?` on terminals / `TerminalAccountId?` on tabs, `[derived]` into a paired/unpaired indication). `[DTO: ResponseListAccountsJson / ResponseAccountJson]`.
- **Actions** — create bank → `POST /account/bank`; create terminal → `POST /account/terminal` (optionally with a **new or existing** Tab, never both — 400 verbatim); create a Tab for a terminal → `POST /account/tab {TerminalAccountId}`; pair existing → `POST /account/pair-tab`; unpair → `DELETE /account/terminal/{terminalAccountId}/tab`; edit descriptive fields → `PUT /account/{id}` (**type is not editable** — the update DTO simply carries no type field, so there is no error to render; the UI never offers the control); deactivate → `DELETE /account/{id}` *(gap §0.3.3-S: deactivation currently ignores an existing Terminal↔Tab pairing — the atomic unpair-or-block rule ships with server M7.7 Phase 5)*.
- **States** — *loading* · *empty* · *error* (pairing conflicts verbatim: terminal already has a Tab, Tab already paired) · *success*.
- **Navigation** — Gestão admin navigation; pairs with Account assignment.

## Clients (admin + counter)

- **Purpose** — the fiado customer registry.
- **Primary job** — find or register a client fast — this happens mid-sale at the counter.
- **Access** — create/edit/list: **any branch role** (Members register clients during fiado sales); deactivate: manager-admin.
- **Default view / filter** — the full list, searchable by name (`[derived]` client-side — the list endpoint has no search filter).
- **Data shown** — `Name`, `Phone`, `Cpf?`, `Cep?`, `Address?`, `PhoneSecondary?`, `Email?`, `Notes?` — masks per formatting §6. `[DTO: ResponseListClientsJson / ResponseClientJson]`.
- **Actions** — open a client → Client statement; create → `POST /client`; edit → `PUT /client/{id}`; deactivate → `DELETE /client/{id}` (Manager/Admin). CPF is normalized to digits before submit (formatting §6); at most one active client per CPF per branch (conflict verbatim).
- **States** — *loading* · *empty* · *error* (CPF invalid/conflict verbatim) · *success*.
- **Navigation** — Gestão admin navigation for Manager/Admin; Meu turno navigation for Members and for a linked Manager/Admin in Meu turno; reachable inline from Transaction — Create (a fiado sale needs a client).

## Categories & Transaction Types (admin)

- **Purpose** — the classification model behind every transaction: categories fix the direction, types drive settlement and the fiado requirement.
- **Primary job** — add a new type (a new payment method) without breaking the classification invariant.
- **Access** — mutations manager-admin; list/get any branch role (these lists feed every type picker).
- **Data shown**
  - **Categories** — `Name`, `DefaultDirection` (label §5). `[DTO: ResponseListCategoriesJson / ResponseCategoryJson]`.
  - **Types** — `Name`, `CategoryName`, `SettlementRule` (label §5), `RequiresTabAccountAndClient`. `[DTO: ResponseListTransactionTypesJson / ResponseTransactionTypeJson]`.
- **Actions** — category: create → `POST /category {Name, DefaultDirection}` · rename → `PUT /category/{id}` (**direction is immutable** — the update DTO carries the name only; transactions denormalize direction at creation, §6.1) · deactivate → `DELETE /category/{id}`. Type: create → `POST /transaction-type {CategoryId, Name, SettlementRule, RequiresTabAccountAndClient}` · edit → `PUT /transaction-type/{id}` (**category is immutable** — not in the update DTO) · deactivate → `DELETE /transaction-type/{id}`.
- **States** — *loading* · *error* (verbatim) · *success*.
- **Navigation** — Gestão admin navigation; the pickers in Transaction — Create and Installment plan read these lists.

## Products (admin)

- **Purpose** — the daily-close snapshot lines (Telesena, Raspadinha, Dinheiro…), ordered to match physical counting.
- **Primary job** — add or reorder products so the close form mirrors the counting order at the drawer.
- **Access** — mutations manager-admin; list any branch role (the close form reads it).
- **Data shown** — `Name`, `DisplayOrder`. `[DTO: ResponseListProductsJson / ResponseProductJson]`.
- **Actions** — create → `POST /product {Name, DisplayOrder}`; edit → `PUT /product/{id}`; deactivate → `DELETE /product/{id}`.
- **Audit / lock context** — the **"Diferença Caixa" row is system-owned and server-guarded**: renaming it away — or renaming any other product *to* the reserved name — → 400 `PRODUCT_SYSTEM_PROTECTED`; deactivating it → 409 `PRODUCT_SYSTEM_PROTECTED`; duplicate names → 409 `PRODUCT_NAME_CONFLICT`. The UI disables those actions preemptively (`[derived]`), but the server is the real gate.
- **States** — *loading* · *empty* · *error* · *success*.
- **Navigation** — Gestão admin navigation; feeds the Close day form.

## Holidays (admin)

- **Purpose** — the branch's holiday calendar — it moves cheque/card due dates (§6.3/§6.8) and time-entry statuses.
- **Primary job** — import a year of Brazilian holidays in two taps; hand-add the local ones.
- **Access** — mutations manager-admin (enforced in the use cases); list and import *preview* any branch role.
- **Default view / filter** — the current year. `[DTO: RequestListHolidaysJson {Year?, DateFrom/DateTo?, paging}]`.
- **Data shown**
  - **List** — `Date`, `Description?`, paging. `[DTO: ResponseListHolidaysJson / ResponseHolidayJson]`. (`Source` is persisted per §3.17 but not returned here — show provenance only in import results.)
  - **Import preview** — per item: `Date`, `Description`, `Type` (label §5), `AlreadyExists`, `Source`. `[DTO: ResponseBrazilianHolidayPreviewJson / ...ItemJson]`.
  - **Import result** — per item: `Status` (label §5) `[DTO: ResponseBrazilianHolidayImportItemJson]`; `ImportedCount`/`SkippedCount` live on the envelope `[DTO: ResponseBrazilianHolidayImportJson]`.
- **Actions** — add manually (batch) → `POST /holiday {Holidays[] {Date, Description?}}`; edit a description → `PUT /holiday/{id}` (**the date is immutable** — deactivate + recreate to move one); deactivate → `DELETE /holiday/{id}`; preview an import → `GET /holiday/import-br/{year}/preview?includeOptionalFederal&source` (source labels per copy-guidelines §5); import → `POST /holiday/import-br/{year}?…` (Manager/Admin). A single-source outage → 502 `HOLIDAY_SOURCE_UNAVAILABLE` verbatim (the composite source never 502s).
- **States** — *loading* · *empty year* (CTA: import) · *preview shown* · *import done* (counts) · *error* · *success*.
- **Navigation** — Gestão admin navigation.

## Settings (admin)

- **Purpose** — the branch's few global knobs: the lock date and the time-entry constants.
- **Primary job** — check the lock date; adjust hour targets when policy changes.
- **Access** — read: any branch role (`GET /setting` feeds lock checks everywhere); update: manager-admin.
- **Data shown** — `LockDate`, `DailyTargetHours`, `LunchDeductionOver6H`, `LunchDeductionOver4H` — decimal hours render as durations (formatting §5: `7.33` → `7h 20min`). `[DTO: ResponseSettingJson]`. The three time constants are the branch's **current** values — the mirror of the latest effective-dated `TimeEntryPolicy` row (§3.20); history lives in the policy ledger and is never edited here.
- **Actions** — update time-entry constants → `PUT /setting` (partial: only provided fields change; `[DTO: RequestUpdateSettingJson]`) with the loaded response `ETag` in `If-Match`. A real change takes effect from the branch-local **day of the change** (a second same-day change replaces that day's values): today's and future balances use the new constants, while every earlier day keeps the policy that governed it (M7.7 Phase 7) — stated plainly in the screen's copy. Inline helper under the time-constant inputs: *"Alterações entram em vigor a partir de hoje ({data_atual}). Os registros de dias anteriores mantêm as metas vigentes na época e não são recalculados."* On save, **only when a constant actually changed** (a value-identical save appends nothing and shows no confirmation), confirm first — target changed, with or without lunch tiers: *"Confirmar alteração de meta?"* / *"A nova meta de {nova_meta} será aplicada a partir de hoje, {data_atual}. Os dias anteriores deste mês permanecerão calculados sob a meta anterior ({meta_antiga})."*; only lunch tiers changed: *"Confirmar alteração das regras de jornada?"* / *"As novas regras de jornada valem a partir de hoje, {data_atual}. Os dias anteriores mantêm as regras vigentes na época."* Buttons: **Cancelar** · **Confirmar e salvar**. `LockDate` is read-only here: sending it returns 400 `SETTING_LOCK_DATE_READ_ONLY`. Its sole write path is Monthly reconciliation's `POST /setting/lock-month {Year, Month}`, also guarded by the Setting-root `If-Match`; normal state advances, while an impossible current/future legacy value is repaired there only after full validation.
- **States** — *loading* · *stale* (409 `SETTING_STALE_WRITE` → reload and let the user reconcile) · *error* (`SETTING_LOCK_DATE_READ_ONLY` or field validation, verbatim) · *success* (store the returned new `ETag`).
- **Navigation** — Gestão admin navigation; conceptually paired with Monthly reconciliation's lock action.

## Branch members (admin)

- **Purpose** — who can log into this branch, and as what role.
- **Primary job** — add an **already-registered** user by e-mail and set their role. *(Server M8 replaces this with invitations: onboarding is operated — gap C — and every person joins by invitation; the invitation screens arrive via design 0.4.6.)*
- **Access** — manager-admin, with a **role matrix**: an Admin manages every role; a Manager may only target and assign `Manager`/`Member` — never touch an Admin membership or grant `Admin`. The UI gates accordingly (`[derived]`); the server enforces it. **Nobody acts on their own membership:** the caller's own row renders role change and removal disabled, with an explanation — a Manager/Admin can never demote or remove themselves (server rule with M8). Marking the own row needs the caller's user id `[gap]` — gaps §0.3.3-U.
- **Data shown** — `Name`, `Email`, `Role` (label §5), `Active`. `[DTO: ResponseListBranchUsersJson / ResponseBranchUserJson]`.
- **Actions** — add → `POST /branch/users {UserId | Email, Role}` (exactly one identifier — 400 verbatim otherwise); change role → `PUT /branch/users/{branchUserId}/role`; remove → `DELETE /branch/users/{branchUserId}`. Removal also clears that user's Operator login link(s) in the branch atomically; the **last-active-Admin invariant** is server-enforced (verbatim), and the UI also disables the offending action preemptively (`[derived]`).
- **States** — *loading* · *user not found* (404 — the e-mail has no registered active account; until M8's invitations the person must be onboarded first, gap C) · *error* (last-admin, duplicate membership, role-matrix denial — verbatim) · *success*.
- **Navigation** — Gestão admin navigation; pairs with Operators (a member's login is what an operator row links to).

---

## Backend contract gaps — resolved by server M7.5

Surfaced while mapping screens to the pre-M7.5 contract; **all resolved** by [server Milestone 7.5 — Frontend UX Contract Gaps](../../server/docs/milestones.md), shipped at spec `v31` — the synced contract is now `v37`. M7.7 Phase 2's review expansion supersedes the caveat noted in outcome 2, Phase 3 closes the ledger/opening-chain integrity gate, and Phase 4 plus its floor audit close atomic month locking below. The per-field tags above have been updated to the current DTOs. Outcomes:

1. **No variance on the daily-close list item.** Resolved as a **documented deferral** (M7.5 Phase 3): Phase 3 later added separate recorder/submitter audit fields, but intentionally did not add variance. The cross-join this gap described is no longer needed anywhere — the Work Queue reads inline variance from the dashboard's close rows and the approval screen from the review context. The gate reopens only if a future multi-date list/history screen needs inline variance per row.
2. **Opening values are not returned.** Initially resolved by M7.5 for closes that had items. M7.7 Phase 2 completed the contract: `GET /dailyclose/{dailyCloseId}/review` now returns every active product on fresh and populated closes with `DisplayOrder`, `OpeningValue?`, nullable `ClosingValue?`, and `IsCashVarianceProduct`.
3. **No dashboard aggregation endpoint.** Resolved (M7.5 Phase 1): `GET /report/dashboard?date=` returns `ResponseDashboardJson { Date, TotalVariance, MeanVariance, PendingApprovalCount, Closes, NotSubmitted }` — one call, no client-side joins. Close rows are ordered by account name (exception-first ordering is a UI concern); Draft closes surface only in `NotSubmitted`, carrying their close id for the deep-link.
4. **Draft finalize** (`POST /transaction/{id}/finalize`) — **already implemented**; a UI note, not a gap, so no milestone was needed (the UI just builds an explicit "Finalize" action).

---

## Backend contract gaps — resolved by server M7.7 Phase 2

Shipped at spec `v33`; the Close day and Daily-close approval entries above now consume these contracts directly:

- **D. Fresh-close opening values — resolved.** `GET /dailyclose/{id}/review` now enumerates every active product by `DisplayOrder` for fresh and populated closes, retaining a retired product only when the current close has its own saved value, with server-derived `OpeningValue?`, nullable `ClosingValue?`, and the reserved-product flag. The prior three-call composition and duplicated prior-close selection are gone.
- **F. Pre-submit variance — resolved.** `POST /dailyclose/{id}/variance-preview` accepts unsaved candidate item values and uses the same `ICashVarianceCalculator` path as Submit without persistence.
- **J. DailyClose Notes — resolved.** Draft `PUT /items` accepts Notes (max 1000, empty clears, omission preserves), guarded by the close's `xmin` through `If-Match`; submit freezes the value, an attempted Submitted-state change rejects the whole save with 409, and review/get return it.

---

## Backend contract gaps — resolved by server M7.7 Phase 3

Shipped together at spec `v35`; the 2026-08-11 amendment is part of this first Phase 3 delivery rather than a follow-up:

- **G. Submitted/approved variance integrity — resolved.** Submit freezes same-day ledger mutations, and a real counted-item/first-count change on an earlier close explicitly returns only the next eligible official opening-chain successor to Draft for resubmission/reapproval. Account-wide bounded/cancellable coordination, recorded-count eligibility, Terminal activity/open-close binding, uncounted prior-activity Submit blocking, explicit affected-successor responses, and Draft CashVariance suppression are shipped.
- **I. Rejected/opening-recheck correction window — resolved.** The first successful item save claims immutable recorder user/operator identity, so an elevated Open never leaves the window ownerless and a later Manager submit cannot steal it. An unclaimed unlocked close may be first-counted by any account-scoped Member; account coordination makes the first claim win. The recording operator may edit and resubmit an explicitly Rejected or opening-recheck-demoted close until `LockDate`; Manager/Admin may also correct it. Direct prior-day elevated Recall/Reopen is Manager-owned. Same-day Member restriction applies only to voluntary **Desfazer envio** of Submitted.
- **L (server open guards) — resolved.** DailyClose Open is Terminal-only; Member opens are branch-local today only, Manager/Admin may backdate before lock, and every role rejects future dates. Terminal activity requires an open close first. The workstation-first UX is now specified in Operator Day Cockpit and Open day; L remains in the open map only for fiado routing to the workstation's paired Tab (server M7.7 Phase 5.3).

---

## Backend contract gaps — resolved by server M7.7 Phase 4

Shipped at spec `v36` and floor-audited at `v37`; the Monthly reconciliation, Settings, and Manager Work Queue entries above consume the atomic contract directly:

- **K. Atomic month lock — resolved.** `POST /setting/lock-month {Year, Month}` is the sole lock path and `PUT /setting` rejects `LockDate`. The command validates the whole unlocked interval, prohibits current/future request months, lands on month-end, and derives expected `(Terminal, Date)` pairs only from a close in any state or direct active Terminal ledger activity. `Branch.CreatedAt` is the no-data floor: any earlier active close or non-soft-deleted transaction pulls validation back to its business month, so backfills remain allowed but can never be sealed without review. Ancient partial legacy boundaries clamp to this floor; an impossible current/future boundary from the retired PUT path can be replaced only after the command validates from the floor. Clean earlier history passes silently; an unresolved intermediate conflict names its `MM/yyyy`. The rule applies no calendar/activation-window expectation, ignores paired-Tab fiado for Terminal expectation, and keeps the direct-activity safety net for imported/bypassed rows. An exclusive branch boundary serializes floor discovery and check-and-set against the shared boundary every Phase 3 ledger/close-history write and every TimeEntry mutation acquires first; the fixed month-before-account order has no inversion. The single-month report uses the same evaluator and returns structured `MissingExpectedClose` blockers. Empty months without expected activity are lockable; CEF attestation remains out of MVP.

---

## Backend contract gaps — resolved by server M7.7 Phase 6

Shipped at spec `v39`; the screen entries above now consume the contracts directly:

- **B. Cash-variance deep-link — resolved.** Every summary row carries its exact `(Date, AccountId)` `DailyCloseId`, so sibling accounts cannot cross-link and the extra list lookup is gone.
- **E. Stale operator login identity — resolved.** Removing branch membership atomically clears matching Operator user links while retaining the employee records, audit history, and account assignments.
- **M. Installment sibling lookup — resolved.** `GET /transaction?OriginTransactionId=` enumerates a plan inside unchanged branch/Member account scope; Transaction — Edit now catalogs the confirm-and-cancel flow.
- **T. Forgotten-punch triage — resolved.** `GET /timeentry` filters by `IsInProgress` and can apply `InProgressFirst` before paging.
- **Design 0.4.2 server dependencies — delivered.** `GET /branch/current` owns branch-local date/time; financial creates use persisted 24-hour `Idempotency-Key` replay; guarded mutations use strong quoted-decimal `If-Match`/ETag `xmin` tokens and verbatim stale 409 keys. Design 0.4.2 remains a design-authoring item and now consumes these fixed server contracts.

---

## Backend contract gaps — resolved by server M7.7 Phase 7

Shipped at spec `v40`; the screen entries above now consume the contracts directly:

- **O. Effective-dated time-entry policy — resolved.** The signed 1.6a model shipped: a separate effective-dated `TimeEntryPolicy` entity (never a versioned `Setting`, never a per-entry snapshot) resolved per entry date, with a deterministic migration backfilling one MinValue-dated initial row per existing branch from its then-current constants and the branch seed creating it for new branches. Changing `DailyTargetHours` or either lunch tier via `PUT /setting` appends the row effective from the branch-local day of the change (a second same-day change mutates that day's row), so historical balances in every time-entry report are stable. The branch balance roll-up now includes **every active operator** — zero-entry rows with empty `Days` and zeroed totals, linked or not (`UserId = null`) — while operators deactivated after working inside the window are retained; ordering is unchanged. The reduced promise stands: *Sem registro* day labelling is a client-side window-minus-entries derivation; there is no server operating-day matrix.

---

## Design IA gap — resolved by design 0.4.1

- **R. Manager-as-operator mode — resolved.** Manager/Admin always enter a branch in **Gestão** at Manager Work Queue. When `GET /operator/self-context` returns an active linked Operator, the navigation exposes an explicit **Gestão ↔ Meu turno** switch and Meu turno owns Operator Day Cockpit. The switch changes navigation/home context only: the same branch token and `BranchUser.Role` remain authoritative, so server permissions and role-shaped DTOs do not change. An unlinked Manager/Admin remains in Gestão and receives a setup explanation on a shift-mode deep-link. Switching branches clears the mode with all other branch-scoped state. Because the switch lets one person record a close in Meu turno and approve it in Gestão, self-approval is an explicit accept, not an oversight: server permissions stay role-based (a same-user block would deadlock single-manager branches, and the recorder/submitter/approver audit stamps remain the control), and Daily-close approval shows a "Você registrou este fechamento" marker when the close's `RecordedByUserId` equals the current user — compared by user, not operator, so an unlinked Manager's own count is marked too. The client has no contracted way to know its own user id yet, so the marker is `[gap]` — see gap U. The navigation map above is authoritative; gap R no longer gates the navigation shell or Operator Day Cockpit blueprint.

---

## Design gap — resolved by design 0.4.5

- **P. Manager Work Queue date scope — resolved.** (1) **Default day:** branch-local today, literal — there is no operating calendar, so weekends and holidays get no special treatment, and the queue never switches day by itself. (2) **Cross-date backlog:** a persistent banner above the queue whenever an unlocked day before the selected date holds a Submitted, Rejected, or Draft close, with counts per status and **Ver dia mais antigo**; the counts and the oldest pending day come from the dashboard response (gap W). When today has no activity (no close rows and no NotSubmitted row carrying a DailyCloseId), the banner offers the jump. (3) **Draft count:** every unlocked day up to today, not only the earliest unlocked month — also from the dashboard response (gap W). The group order is fixed as well, with no status filter. Manager Work Queue consumes all three.

---

## Gaps & notes surfaced by 0.3.3 — open

These are the remaining open entries after M7.5, M7.7 Phases 2–4, 6, and 7, and design 0.4.1 and 0.4.5. B, E, G, I, K, M, O, P, R, and T no longer gate M4; L remains only for fiado routing to the workstation's paired Tab. None blocks M1 (visual direction), but **every screen named in the remaining map below is implementation-blocked for M4 blueprints and the build**. A parenthetical feature qualifier on a map row limits the gate to that feature; the rest of that screen's blueprint proceeds. Decisions land through [server M7.7](../../server/docs/milestones.md) (M9.5 verification feeds the fiado one; server M8 owns C/N/U/V; server M7.7 Phase 7.5 owns W; server M11 owns Q); the catalog entries are re-synced when their owning implementation lands.

**Phase 1 decisions — server M7.7, signed off by the dev team 2026-07-27; amended 2026-07-28, 2026-08-11, and 2026-08-19 (integrity-review follow-ups)** *(authoritative detail + implementation phases live in [server M7.7](../../server/docs/milestones.md). J shipped in Phase 2; G/I and L's server-open guards shipped in Phase 3; K shipped in Phase 4 and received its floor audit at v37; the cross-cutting frontend-support contracts shipped in Phase 6; the other entries remain pending their implementation phases):*

- **G → freeze at submit + explicit opening-chain invalidation — shipped Phase 3.** Submitting seals same-day ledger movement. A later official close whose opening genuinely changes because an earlier counted close is corrected is explicitly returned to Draft with an opening-recheck marker; no official value is recomputed silently. Terminal writes require an open close, Submit rejects never-counted/current or intervening uncounted-activity days, and account-wide coordination serializes close/ledger history with bounded retryable failure.
- **K → atomic `POST /setting/lock-month` — shipped Phase 4, findings-audited at v38.** Activity-based expected closers (a terminal with a close or **direct** cash-ledger activity that date — no operating-day calendar; **paired-Tab fiado alone does not make a Terminal expected**, #3); the current/unfinished month is **not** lockable; whole-interval validation; `CreatedAt` as the no-data floor lowered by any earlier active operational month; bounded legacy-boundary repair; race-safe `LockDate` enforcement on every TimeEntry mutation; empty-month-with-expected-activity not lockable; CEF attestation out of MVP; `LockDate` read-only via `PUT /setting`.
- **H → client-level balance + explicit *Receber pagamento*.** Per-row `PaidAt` drops from fiado semantics; aging reconciles by **FIFO query-time netting** (a client's `Out` rows oldest-first minus cumulative `In`, unpaid remainder aged; partial + overpayment covered), #1.
- **A → member-scoped paired-Tab balance read**, shown only to an operator holding the explicit Tab assignment (juniors without Tab access see no reference) — consistent with the L/S pairing decision.
- **I → operator edits a `Rejected` or opening-recheck-demoted close until period-lock, and Manager/Admin may also correct — shipped Phase 3**; the same-day rule survives only for voluntary Member Recall, while direct prior-day elevated Recall/Reopen is Manager-owned.
- **J → add the `Notes` write path — shipped in Phase 2** (editable on the `Draft` `PUT /items` path, frozen at submit, max 1000), surfaced on Daily-close approval. Column kept.
- **O → effective-dated `TimeEntryPolicy` entity — shipped Phase 7.** Not a versioned `Setting`; the migration backfilled one MinValue-dated policy row per branch from its current constants, the branch seed creates it for new branches, and every calculation resolves the policy applicable to each entry's date. The roll-up also gained zero-entry active operators.
- **L + S → require both assignments explicitly** (a Terminal grants till operation only; Tab / fiado authority is a separate deliberate grant — Tabs are senior-trusted, not defaults). Pairing is **terminal-centric** (Tab owned by the Terminal — no schema change, #4); access is **explicit but multi-terminal** (a roaming operator can hold Tab assignments on several terminals, granted per-terminal — never implied). **Deactivation atomically unpairs**, both directions.
- **Cross-cutting (feeds design 0.4.2) — shipped Phase 6:** financial creates require a persisted **Idempotency-Key**; mutations use strong `If-Match`/ETag **optimistic concurrency via PostgreSQL `xmin`** → verbatim `409` on stale writes; session context exposes the server-authoritative branch clock. The design item now consumes these contracts rather than inventing them.

> The individual open gap entries below retain their original **question** framing for context. For decided but unshipped entries (A · H · L · S), the signed decision above is authoritative and any "decide / choose" wording is historical context pending implementation. Resolved G/I/K/O/R are recorded above and removed from the M4 blocker map.

- **A. Operator-visible fiado reference balance — `[gap]`.** §6.5 says the close-day form displays the fiado balance for reference, but `GET /report/fiado/balance` is Manager/Admin-only, so a Member operator has no endpoint to fetch it. Either a member-scoped variant (scoped to the close's paired Tab account) ships later, or the reference display is dropped for Members. Until decided, Close day omits it for Members. **Owner: server M7.7 Phase 5** (decided with the 1.3 fiado-settlement decision).
- **C. Onboarding — decided: operated onboarding (server M8).** There is no public sign-up and no self-serve branch creation. The Lotero team creates each lotérica through an onboarding flow — a platform-only "Nova lotérica" wizard plus a command line for dev/staging, both calling one server use case — which invites the owner as Admin; every other person joins by invitation from a Manager/Admin. Google sign-in is a second way to sign in, never a second way in. The rationale and the endpoint lockdown are recorded in [server M8](../../server/docs/milestones.md). Screens follow via design 0.4.6: Invite accept (with **Criar senha** or **Continuar com Google**), the Plataforma area, and the owner setup checklist on Manager Work Queue. Login, Branch picker, and Branch members point here; until M8 ships, adding a member still requires an already-registered user.

### Added by the 2026-07 catalog review

- **H. Fiado settlement — `[decided, pending delivery]`.** Signed 2026-07-27 (decision 1.3 above): client-level balance + an explicit **Receber pagamento** command; per-row `PaidAt` drops out of fiado semantics; aging reconciles by FIFO query-time netting. Server M7.7 Phase 5 ships the command (carrying the receiving Tab `AccountId`), the netting, and the per-client statement read `GET /client/{id}/fiado-statement` (Manager/Admin, or a Member holding at least one Tab grant, see the client's complete branch-wide statement — its running balances reconcile row by row and its window reconciles across all pages — and may record a payment for any client on a Tab they may use; a client has no owning Tab); M9.5 Phase 2.2 decides whether a cash repayment also enters the Terminal drawer. Until then Fiado aging still reflects the retired per-row model (verified: the open-receivables query is direction-unfiltered and keys on `PaidAt IS NULL`), and Client statement + Receber pagamento are catalogued with `[gap]` data.
- **L. Paired-Tab routing — `[workstation UX catalogued; Phase 5.3 remainder pending]`.** Phase 3 enforces Terminal-only closes/open dates and requires an open close before Terminal activity. The workstation-first UX is specified in Operator Day Cockpit and Open day: one terminal binds automatically, several ask once per device and branch-local day, and a multi-terminal strip keeps other open closes visible. What remains is fiado routing to the workstation's paired Tab under the signed 1.8 rule — a separate `OperatorAccount(Tab)` grant, never implied by the Terminal assignment — shipping with server M7.7 Phase 5.3.
- **N. No password recovery, profile, or logout — extends gap C.** The server has no forgot-/change-password endpoint and no milestone covered it (M8 was invitations only — now extended with recovery, change-password, profile, logout + refresh-token revocation, and the first-branch bootstrap decision); `product.md`'s identity-tier table promises a "profile" surface, but no Profile screen, session menu, logout, or password flow is cataloged. The navigation shell's semantic Switch branch action is not a substitute for those missing account surfaces. Password recovery is a common launch-critical recovery need — launch-blocking with C. The auth/account screen set is cataloged via design 0.4.6 once the M8 contract lands.
- **Q. §7.2 promises reports the catalog doesn't have — deferred product surface, owner: server M11.** "Upcoming due dates (card settlements, pre-dated cheques)" has no cataloged screen (open-cheque aging covers cheques only; nothing lists upcoming card settlements by `DueDate`), and the monthly report of §7.2 item 4 (variance + Tarifa/Sobras Bolão + hours, CEF borderô comparison) is uncataloged. Both land on a **KPI page** — a second screen beside Manager Work Queue, which stays exception-first and is never softened into a KPI page — with recent closes at a glance (what the Work Queue's all-clear improvises today). It is server-gated: the monthly report joins a Product (Tarifa Bolão), a TransactionType (Sobra de Bolão), and TimeEntry per operator per day, which no endpoint spans. When the KPI page ships, M11 also renames `GET /report/dashboard` → `GET /report/manager-queue` and `ResponseDashboardJson` / `ResponseDashboardCloseJson` / `ResponseDashboardNotSubmittedJson` → `ResponseManagerQueue*Json`. **Tripwire:** if the web build starts before M11 is scheduled, pull the rename into the server milestone that precedes the first generated client. Owned by **server Milestone 11 — Post-MVP Reporting Surface** (M9.5's semantics audit feeds the bolão part); the catalog entries are written when M11 is scheduled.
- **S. Account deactivation ignores Terminal↔Tab pairing — `[gap]`, verified.** `DeactivateAccountUseCase` deactivates the account and its operator links but never touches the pairing — an active Terminal can keep pointing at an inactive Tab, and an inactive Terminal keeps reserving its Tab through the pairing constraint. Define atomic behavior (deactivation unpairs, or is blocked with a verbatim key while paired) for **both directions**. Owner: server M7.7 Phase 5, decided with 1.8; the Accounts screen renders the rule.

### Added by design 0.4.1

- **U. No contracted caller user id — `[gap]`, owner: server M8 (decided: `UserId` on `ResponseUserLoginJson`).** Gap R's self-recorded marker on Daily-close approval compares `RecordedByUserId` with the current user, and today no response gives the client that id: login returns `{Name, Email, ResponseToken}`, branch session `{Token, Branch}`, and `GET /operator/self-context` an `OperatorId` that is null for an unlinked Manager — the case the marker must cover. The id exists only inside the token, under a claim name nothing pins. M8 adds it to the login response (known at the identity tier, one field, no new endpoint; M8 already changes that response for Google sign-in). It also lets Branch members disable the caller's own row. Gates only those two features: the Daily-close approval and Branch members blueprints proceed without them.
- **V. Operator-link requests — `[gap]`, owner: server M8.** A Member with no operator link, or linked with no usable account assignment, sends the managers a request from the cockpit's setup-needed state (**Solicitar vínculo**); Manager Work Queue lists pending requests and Operators resolves them (link or create an operator, then Account assignment). Needs a request entity with create (Member) and list/resolve (Manager/Admin) endpoints. M8 also lets a Member invitation pre-select an existing operator, so accepting the invite creates the link — most requests then never arise.
- **W. Work Queue backlog aggregate — `[gap]`, owner: server M7.7 Phase 7.5.** The design side of gap P is decided, but its data cannot be composed reliably: `GET /dailyclose` pages 20 rows by default (100 max) with one overall `TotalCount`, so per-status counts need a call each, and the oldest pending day would depend on the list's undocumented order. `GET /report/dashboard` gains `Backlog { SubmittedCount, RejectedCount, DraftCount, OldestPendingDate? }` for active closes with `LockDate < Date <` the requested date, and `UnlockedDraftTransactionCount` for Draft transactions with `LockDate < Date ≤` branch-local today — one aggregation each, keeping the Work Queue at one call. Gates only the backlog banner and the Draft-transaction count; the rest of the Work Queue blueprint proceeds.

### Gap → screen map *(authoritative for the M4 per-screen gate; inline pointers in entries are convenience mirrors)*

| Gap | Blocked / affected screens                                                                                                                                      |
|-----|-----------------------------------------------------------------------------------------------------------------------------------------------------------------|
| A   | Close day                                                                                                                                                       |
| C   | Login · Branch picker · Branch members (+ the 0.4.6 auth addendum set)                                                                                          |
| H   | Fiado balance · Fiado aging · Transaction — Create (fiado types) · Transaction — Edit (`PaidAt`) · Close day (reference) · Client statement · Receber pagamento |
| L   | Transaction — Create (account/Tab routing) · Account assignment                                                                                                 |
| N   | Login · Branch picker · *Logout/session menu* (uncataloged — 0.4.6) (+ the 0.4.6 auth addendum set)                                                             |
| Q   | *(uncataloged M11 report screens)*                                                                                                                              |
| S   | Accounts                                                                                                                                                        |
| U   | Daily-close approval (self-recorded marker only) · Branch members (own-row guard only)                                                                          |
| V   | Operator Day Cockpit (link request only) · Manager Work Queue (link-request group only) · Operators (request resolution only)                                   |
| W   | Manager Work Queue (backlog banner and Draft count only)                                                                                                        |
