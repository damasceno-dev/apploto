# LottoGest — Backend Schema Specification

<!--
Sync group: loto-backend-docs
Canonical source: server/docs/loto-specs.md (this file is canonical; derived artifacts: server/docs/loto_presentation.html, server/docs/loto_entity_relationship_diagram.html)
Coverage: Full entity model, relationships, invariants, workflows, and Access-to-LottoGest mapping.
Spec revision: v27
-->

> **Status:** Revised spec (v27) — Milestone 7 Slice 7.1 Create Impact Preview — §6.14 "Create impact preview" subsection added covering `POST /transaction/preview` (branch-authenticated, **same scope as the `POST /transaction` write twin** — Member-scoped via `TransactionCreatePreamble`, not Manager/Admin-only), the verbatim `RequestCreateTransactionJson` payload, `asOfDate` query parameter, the three create-specific impact rules (a Tab row appears in open receivables; a Tab row with a client pushes a single signed fiado delta; the cash-variance `VarianceDelta = −NetFlow(Direction, Value)` is **genuinely non-zero**, in contrast to the edit preview's structural zero), `Draft` → empty, and preview-never-commits. The shared impact envelope was renamed from `ResponseTransactionEditImpactJson` to the neutral `ResponseTransactionImpactJson` now used by both previews
> **Scope:** Entity model, relationships, business rules, domain knowledge  
> **Stack:** .NET + EF Core + PostgreSQL  
> **Revision notes:**  
> v1→v2: Fixed classification invariant (TransactionType is single source of truth, CategoryId/Direction denormalized), added transaction authorship (RecordedByOperatorId + CreatedByUserId), corrected Fiado product seed (calculated, not persisted), fixed lco_status mapping (unused in Access), fixed OriginTransactionId self-reference for installments.  
> v2: Added Account.TabAccountId invariants, OperatorAccount.IsPrimary uniqueness, branch consistency rules (6.9), CashVariance calculation semantics (6.10), seed data scope clarification.  
> v3: Rename 6h and 4h to 6H and 4H to attend for entities naming rules.  
> v4: Added Client.Cpf uniqueness per branch as a filtered unique constraint on active rows.  
> v5: Corrected the Account creation/pairing flow: Tab accounts are optional per terminal, pairing/unpairing is explicit for existing accounts, account creation is split into explicit Bank/Terminal/Tab operations, new Tabs are always created for an existing or newly-created Terminal, and a new paired Tab inherits the Terminal descriptive fields at creation time.  
> v6: Changed Client.Cpf storage from formatted varchar(14) to normalized digits varchar(11); application layer strips non-digit chars before persistence.
> v7: Added TransactionType settlement metadata (`SettlementRule`, `RequiresTabAccountAndClient`) and documented due-date and fiado enforcement semantics.
> v8: Added the active Operator user-link uniqueness invariant: at most one active linked Operator per `(User, Branch)`, enforced by filtered unique index.
> v9: Documented that updating an Operator with `UserId = null` clears the login link while preserving the Operator row.
> v10: Aligned §6.3 with the Phase 3.1 cheque-installment contract — manual rows by default, optional auto-generation with monthly stagger and weekend adjustment, exact-sum invariant, prefix-only description fallback, and SaveAsDraft propagation.
> v11: Added §6.10 documenting the Member transaction read scope: 403 vs 404 contract on Get, account-scope visibility on List (members see all rows on linked accounts regardless of recording operator), empty-scope short-circuit on List, and the enriched list response shape (joined names + paging metadata).
> v12: Added §6.11 documenting the transaction update contract: editable and non-editable fields, local-business-day permission matrix, Member account-scope ordering, Mine list filter behavior, update audit stamping, and lock-date behavior.
> v13–v15: (internal milestone iterations)
> v16: Milestone 4 DailyClose workflow hardened — §3.14 audit pair, §6.5 Submitted→Draft recall transition, §6.12 explicit Direction handling, §6.13 DailyClose contract including role × state × business-day matrix.
> v17: Milestone 5 Phase 1 TimeEntry & Holiday foundation — §3.16 updated with audit fields and filtered unique constraint; §3.17 updated with table and filtered unique constraint; §6.3 installment stagger now skips weekends and branch holidays; §6.7 references ITimeEntryCalculationService with midnight-crossing and exact-boundary notes; §6.8 documents holiday-aware business-day calculation via DueDateCalculator and IBranchHolidaySource.
> v18: Milestone 5 Phase 3.5 foundational live-running — §6.7 updated with partial-Present semantics (ClockIn required, ClockOut optional), live-running rule with synthetic effectiveClockOut on same branch-local day, forgotten-clock-out path returns (0, 0), ClockOut-without-ClockIn always invalid (TIMEENTRY_CLOCK_OUT_REQUIRES_CLOCK_IN); CalculateLiveRunning method signature and contract added.
> v19: Milestone 5 Phase 3.6 multi-segment TimeEntry — §3.16 TimeEntry updated to drop ClockIn/ClockOut and add Segments navigation; §3.16a TimeEntrySegment added with full DateTime clock fields, FK, filtered unique open-segment constraint, day-bounds invariant, and audit fields; §6.7 fully rewritten for the segment-list contract using DateTime semantics, top-up gap-aware lunch rule, dual-shape PUT (Member Action vs Admin Segments), idempotent no-ops, status-transition rule, day-bounds rule, and worked examples (overnight, live-running gap, forgotten open).
> v20: Clarified §6.7 Member tap routing for overnight vs forgotten-close cases: prior-day open segments are resolved by the next submitted Action and Date, not by server reinterpretation.
> v21: Added §5.1 Brazilian Holiday Calendar appendix documenting the M6 pure-function import source: 10 mandatory national holidays, 3 curated optional federal Easter-anchored entries, Law 9.093/1995 for Sexta-feira Santa, Law 14.759/2023 for Consciência Negra, and the Anonymous Gregorian / Meeus/Jones/Butcher Easter algorithm reference.
> v22: Milestone 6 Phase 6.5 multi-source Brazilian Holiday providers — §3.17 Holiday gains the `Source` column (`HolidaySource` enum: Manual=0, Canonical=1, BrasilApi=2, Nager=3) with a default of `Manual` and a Phase 6.5 migration backfilling existing rows. §5.1 grows a "Sources" subsection covering composite ordering (Nager → BrasilAPI → Canonical), the 13-concept identity catalog with name-match + ±3-day date proximity tiebreaker, the documented provider quirks (BrasilAPI's "Confraternização mundial" / "Dia da consciência negra" / "Independência do Brasil" renames; Nager's regional `global: false` rows being dropped; both providers missing Quarta-feira de Cinzas → always canonical backfill), and the 502 `HOLIDAY_SOURCE_UNAVAILABLE` contract for explicit single-source failures (Composite never 502s because canonical always backfills).
> v23: Milestone 7 Phase 1 Reporting Surface foundation — §6.14 added covering the read-only reporting contract, three permission buckets (Manager/Admin whole-branch views, operator-self with empty-scope short-circuit, write-twin scope for preview endpoints), `AgingBucket` enum definition with exact boundary semantics (day 30 → `Days0To30`, day 31 → `Days31To60`, day 90 → `Days61To90`, day 91+ → `Days91Plus`), date-range guardrails (closed window, span ≤ 366 days, `AsOfDate` defaults to branch-local today via `IBranchClock`), `Status = Active AND Active = true` filter on financial totals, and the preview-never-commits invariant pinned by reload assertions.
> v24: Milestone 7 Phase 4 Fiado Aging Report — §6.14 extended with "Fiado aging report" subsection documenting `GET /report/fiado/aging` per-row contract (`ResponseFiadoAgingItemJson` fields: `TransactionId`, `Date`, `DueDate`, `Value`, `DaysOutstanding`, `Bucket`, `ClientId`, `ClientName`, `AccountId`, `AccountName`, `Description`), `DaysOutstanding = max(0, (asOfDate.Date − dueDate.Date).Days)` formula, bucket assignment via `ReportAgingBucketizer.BucketFor(dueDate, asOfDate)`, future-due rows included in `Current` bucket, filter semantics (Tab-only, `PaidAt IS NULL`, optional `clientId`/`accountId`), deterministic ordering `DueDate ASC, Date ASC, Id ASC`, and `ResponseFiadoAgingJson` envelope shape.
> v25: Milestone 7 Slice 5 Open-Cheque Aging Report — §6.14 extended with "Open-cheque aging report" subsection documenting `GET /report/cheques/open-aging` per-origin-group rollup contract. Groups are built from rows with `OriginTransactionId IS NOT NULL AND Status = Active AND Active = true`; a group appears only when it has at least one row with `PaidAt IS NULL`. Group fields: `OriginTransactionId`, `OutstandingTotal` (sum of unpaid row values), `OldestOpenDueDate` (earliest unpaid DueDate), `OldestOpenBucket` (bucket for `OldestOpenDueDate`), `OpenRowCount`, `TotalRowCount`, `AccountId`, `AccountName`, `ClientId`, `ClientName`, `Description` (from origin row). Sibling rows within each group: unpaid installment rows loaded by `OriginTransactionId`; per-row fields: `TransactionId`, `DueDate`, `Value`, `DaysOutstanding`, `Bucket`. `AsOfDate` optional, defaults to `IBranchClock.LocalBusinessDate(UtcNow())`. Optional `accountId` and `clientId` filters. Ordering: `OldestOpenDueDate ASC, OriginTransactionId ASC`.
> v26: Milestone 7 Slice 7 Edit Impact Preview — §6.14 extended with "Edit impact preview" subsection documenting `POST /transaction/{id}/edit-preview` (Manager/Admin). The body reuses `RequestUpdateTransactionJson` verbatim (no parallel DTO, no field made optional); `asOfDate?` lives on the query string. The use case mirrors `PUT /transaction/{id}` step-for-step (role check → validate → no-tracking load with TransactionType + Account → cancelled/lock/fiado/client guards → relative validator) minus `IUnitOfWork` and the mutation/Commit, then delegates to the concrete `TransactionEditImpactProjector` helper (no interface — same convention as the other M7 compute helpers). Response `ResponseEditTransactionPreviewJson { TransactionId, Impact, Warnings }` always carries all three impact sections: `ReceivableImpact` (bucket-before/after on a DueDate shift, appears/disappears flags on a `PaidAt` flip), `FiadoBalanceImpact` (per-client outstanding deltas only when `AccountType = Tab` and the client changes; old client −signedValue, new client +signedValue, `signedValue = Out ? +Value : −Value`), and `CashVarianceImpact` (`AccountId`/`Date`/`DailyCloseStatus` populated whenever a close exists; `CurrentVariance`/`ProjectedVariance` live-recomputed via `ICashVarianceCalculator` for any non-Draft close (Submitted/Approved/Rejected), with the close status surfaced via `DailyCloseStatus` so the manager can tell a pending vs signed-off vs repudiated number; `VarianceDelta` is the computed net-flow delta, 0 today because no editable field changes §6.12's inputs but derived, never hardcoded; single-close boundary documented). Monthly lock-readiness is deferred to the Milestone 10 monthly-reconciliation report. Preview-never-commits is enforced by construction (no `IUnitOfWork`) and pinned by WebApi reload assertions plus a determinism test that compares every impact against the post-PUT state: receivable bucket via fiado aging, fiado deltas via before/after fiado balances, and cash variance via a real post-write calculator recompute.
> v27: Milestone 7 Slice 7.1 Create Impact Preview — §6.14 extended with "Create impact preview" subsection documenting `POST /transaction/preview`. **Permission is the same as the `POST /transaction` write twin** — `[TokenAuthenticateBranch]`, not Manager/Admin-only; the use case runs through `TransactionCreatePreamble`, so Members inherit the same linked-operator + account-scope checks as the real create flow (preview/write parity: anyone who can create the row can preview it). This contrasts with the edit-impact preview, which stays the deliberate Manager/Admin exception because it previews an edit to an existing persisted row loaded by id, not a scope-limited create. The body reuses `RequestCreateTransactionJson` verbatim; `asOfDate?` lives on the query string. The use case mirrors `CreateTransactionUseCase` step-for-step (validate → preamble resolve) minus `AddRange`/`Commit`, then delegates to `TransactionEditImpactProjector.ProjectCreate(...)` (the same single DI-registered Reports helper, now hosting both projections). Response `ResponseCreateTransactionPreviewJson { Impact, Warnings }` carries **no** `TransactionId` (nothing is created). The shared impact envelope `ResponseTransactionEditImpactJson` was renamed to the neutral `ResponseTransactionImpactJson` (free rename — v26 unreleased) and is now returned by both previews. A `Draft` would-be row short-circuits all three sections to empty/zero. For an `Active` row: `ReceivableImpact` marks a Tab row appearing (`RowAppearsInOpenReceivables = true`, `BucketAfter` from `ReportAgingBucketizer.BucketFor(DueDate, asOfDate)`, `BucketBefore` null; empty for non-Tab); `FiadoBalanceImpact` carries one `+signedValue` delta on the selected Tab client (`signedValue = Out ? +Value : −Value`, §6.4); `CashVarianceImpact.VarianceDelta = −NetFlow(Direction, Value)` (`NetFlow = In ? +Value : −Value`) is **genuinely non-zero** — the reason this slice exists, contrasting the edit preview's structural zero — with `CurrentVariance`/`ProjectedVariance` live-recomputed for any non-Draft close and `DailyCloseStatus` surfaced. For Member callers every impact query uses only the preamble-resolved `(account, client, date)` — no branch-wide balances, receivables, or all-account variance summaries. Preview-never-commits is enforced by construction (no `IUnitOfWork`/`ITransactionsRepository`) and pinned by a WebApi row-count assertion plus a determinism test that compares every impact against the post-create state: receivable bucket via fiado aging, fiado delta via fiado balance, and cash variance via a real post-create calculator recompute.
> v13: Extended §6.11 with Draft → Active finalization rules, reusing the same member account scope, mutation permission matrix, lock-date behavior, and update audit convention.
> v14: Extended §6.11 with the cancellation contract: required cancellation reason, terminal `Cancelled` state from `Draft` or `Active`, dedicated cancellation audit fields stamped from the same clock instant as the generic update audit fields, installment-sibling isolation, and exclusion of cancelled rows from active sums.
> v15: Added DailyClose/DailyCloseItem audit and uniqueness details, the DailyClose workflow contract including `Rejected -> Draft` and same-day `Submitted -> Draft` recall, most-recent-prior-close opening values, lock-date coverage for all DailyClose transitions, explicit CashVariance direction handling, and the system-only `"Diferença Caixa"` product invariant.
> v16: Closed the DailyClose workflow hardening pass: locked the Open enforcement order, clarified guard-owned versus account-scope/unique-constraint rules, and confirmed CashVariance is updated in place across rejected resubmits and submitted recall cycles.

---

## Table of Contents

1. [Domain Overview](#1-domain-overview)
2. [Multi-Tenancy Model](#2-multi-tenancy-model)
3. [Entity Reference](#3-entity-reference)
4. [Enums](#4-enums)
5. [Seed Data](#5-seed-data)
6. [Business Rules](#6-business-rules)
7. [Key Workflows](#7-key-workflows)
8. [Access-to-LottoGest Mapping](#8-access-to-lottogest-mapping)

---

## 1. Domain Overview

A *casa lotérica* is a privately-owned branch that operates under contract with Caixa Econômica Federal (CEF), Brazil's federal bank. It processes lottery sales, bill payments, cash deposits, transfers, and other financial services on behalf of CEF.

### The core problem

Every day, each operator (cashier) processes dozens of financial transactions at their terminal. At the end of the day, the operator must close their register: count what is physically in the drawer, compare it to what the system says should be there, and report the difference. The owner then reconciles this against CEF's own settlement report (*borderô*).

This is currently done on paper + Microsoft Access. LottoGest digitizes the entire flow.

### The two parallel systems

The system tracks the same financial reality from two angles:

**The transaction ledger** (`Transaction` table): every individual financial event — "R$2,000 cash deposit at 12:30", "R$560 credit card payment", "R$50 client tab". This is the analytical, auditable record. It answers: *what happened, when, how much, to whom, through which account.*

**The daily closing snapshot** (`DailyClose` + `DailyCloseItem` tables): the physical count of what is in the drawer at end of day — how much cash, how many Telesena tickets worth how much, Raspadinha scratch cards, etc. This is the reconciliation record. It answers: *what do we actually have right now, and does it match the ledger?*

The **CashVariance** (Diferença de Caixa) is the gap between expected (ledger) and actual (snapshot). This is the single most important number in the system — the owner reviews it daily for each operator.

### The Fiado (tab/credit) subsystem

Regular customers can buy on credit ("fiado"). In branches that use fiado, a terminal may have a paired **Tab account** that tracks credit separately from the cash drawer. Some branches may not use Tab accounts at all, and a terminal may exist without one until fiado is configured. Selling on credit records an Out transaction on the Tab account. When the customer pays, that is an In transaction on the same Tab account. The balance is the outstanding credit. This separation prevents credit from distorting the daily cash reconciliation.

### The three-date model

Every transaction has three dates that track its full lifecycle:

- **Date** (`Date`): when the financial event occurred
- **DueDate** (`DueDate`): when payment is expected (same day for cash/PIX, +1 for debit card, +2 business days for credit card, custom for checks)
- **PaidAt** (`PaidAt`): when money actually arrived/left — `null` means unpaid

This enables receivables tracking ("money expected this week"), aging reports, and reconciliation with CEF settlement reports.

---

## 2. Multi-Tenancy Model

LottoGest is designed as a **SaaS platform** where different owners operate independent lottery houses.

### Isolation boundary

`Branch` is the tenant. Every business entity carries a `BranchId` foreign key. Data isolation is enforced at the query level — every repository query filters by the authenticated user's branch context.

### User model

- **User** is global (authentication only): email, password, name
- **BranchUser** is the junction: ties a User to a Branch with a specific Role
- A single User can belong to multiple Branches (e.g., owner manages 2 lottery houses)
- **Operator** is the employee concept within a Branch, linked to a User but separable (operator can exist without login, historical data persists after user deactivation)
- **Account** is purely financial — terminal drawers, bank accounts, tab accounts

```
User (global auth)
 └── BranchUser (role per branch)
      └── Branch
           ├── Operator (employee)
           │    └── OperatorAccount → Account
           ├── Account (financial)
           ├── Transaction
           ├── Client
           ├── DailyClose
           ├── TimeEntry
           ├── Category
           ├── TransactionType
           ├── Product
           ├── Holiday
           └── Setting
```

### Why separate User, Operator, and Account

| Concern | Entity | Reason |
|---|---|---|
| Login/auth | User | Admin/manager may have no terminal account |
| Employee | Operator | Historical records survive user deactivation; operator can be linked to multiple accounts |
| Financial | Account | Accounts can be reassigned between operators; reporting by person stays separate from accounting by account |

---

## 3. Entity Reference

### Conventions

- All entities inherit from `EntityBase` (Id, CreatedAt, Active)
- All monetary values: `decimal` mapped to `numeric(14,2)` in PostgreSQL
- All IDs: `Guid`
- All timestamps: `DateTime` in UTC
- Tenant-scoped entities carry `BranchId`
- Soft delete via `Active = false` on EntityBase
- Transaction cancellation has dedicated fields (not just Active flag)

### 3.1 EntityBase

```csharp
public class EntityBase
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public bool Active { get; set; } = true;
}
```

### 3.2 Branch

The tenant — one per lottery house.

```csharp
public class Branch : EntityBase
{
    public string Name { get; set; } = string.Empty;
    public string? Cnpj { get; set; }
    public string? Address { get; set; }
    public string? Phone { get; set; }

    // Navigation
    public ICollection<BranchUser> BranchUsers { get; set; } = [];
    public ICollection<Account> Accounts { get; set; } = [];
    public ICollection<Operator> Operators { get; set; } = [];
}
```

| Column | Type | Null | Notes |
|---|---|---|---|
| Id | uuid | PK | |
| Name | varchar(255) | NOT NULL | Display name: "Lotérica Centro", "Lotérica Asa Sul" |
| Cnpj | varchar(18) | NULL | Brazilian company ID |
| Address | text | NULL | |
| Phone | varchar(20) | NULL | |
| CreatedAt | timestamptz | NOT NULL | |
| Active | boolean | NOT NULL | Default true |

### 3.3 User

Global authentication identity. No BranchId.

```csharp
public class User : EntityBase
{
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;

    // Navigation
    public ICollection<BranchUser> BranchUsers { get; set; } = [];
    public ICollection<RefreshToken> RefreshTokens { get; set; } = [];
}
```

| Column | Type | Null | Notes |
|---|---|---|---|
| Id | uuid | PK | |
| Name | varchar(255) | NOT NULL | |
| Email | varchar(255) | NOT NULL | UNIQUE |
| Password | varchar(255) | NOT NULL | Hashed |
| CreatedAt | timestamptz | NOT NULL | |
| Active | boolean | NOT NULL | |

### 3.4 RefreshToken

No changes from existing code.

```csharp
public class RefreshToken : EntityBase
{
    public string Value { get; set; } = string.Empty;
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
}
```

### 3.5 BranchUser

Junction: a User's role within a specific Branch.

```csharp
public class BranchUser : EntityBase
{
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    public Guid BranchId { get; set; }
    public Branch Branch { get; set; } = null!;

    public Role Role { get; set; } = Role.Member;
}
```

| Column | Type | Null | Notes |
|---|---|---|---|
| Id | uuid | PK | |
| UserId | uuid | NOT NULL | FK → User |
| BranchId | uuid | NOT NULL | FK → Branch |
| Role | smallint | NOT NULL | Enum: Admin=0, Manager=1, Member=2 |
| CreatedAt | timestamptz | NOT NULL | |
| Active | boolean | NOT NULL | |

**Unique constraint:** `(UserId, BranchId)` — a user has one role per branch.

### 3.6 Operator

The employee at a branch. Linked to a User for login, but exists independently for historical integrity.

```csharp
public class Operator : EntityBase
{
    public string Name { get; set; } = string.Empty;

    public Guid BranchId { get; set; }
    public Branch Branch { get; set; } = null!;

    public Guid? UserId { get; set; }
    public User? User { get; set; }

    // Navigation
    public ICollection<OperatorAccount> OperatorAccounts { get; set; } = [];
    public ICollection<TimeEntry> TimeEntries { get; set; } = [];
}
```

| Column | Type | Null | Notes                                                           |
|---|---|---|-----------------------------------------------------------------|
| Id | uuid | PK |                                                                 |
| Name | varchar(255) | NOT NULL | "Lenna", "Jennifer", "Tracy"                                    |
| BranchId | uuid | NOT NULL | FK → Branch                                                     |
| UserId | uuid | NULL | FK → User. Null = no login (former employee, or not yet set up) |
| CreatedAt | timestamptz | NOT NULL |                                                                 |
| Active | boolean | NOT NULL |                                                                 |

**Unique active user-link constraint:** `(BranchId, UserId) WHERE UserId IS NOT NULL AND Active = true` — a user can have at most one active linked Operator per branch. Multiple terminal/account access is represented through `OperatorAccount`, not through multiple active Operator rows for the same user. Operators without a login keep `UserId = null` and are not constrained by this index.

**Login-link clearing:** `PUT /operator/{id}` with `UserId = null` intentionally clears the login link while preserving the active `Operator` row for history, reporting, and account assignment continuity. This does not deactivate or delete the employee record.

### 3.7 Account

A financial account: terminal drawer, bank account, or tab (fiado) account.

```csharp
public class Account : EntityBase
{
    public AccountType Type { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Institution { get; set; }
    public string? Number { get; set; }

    public Guid BranchId { get; set; }
    public Branch Branch { get; set; } = null!;

    // Self-reference: terminal → its tab account
    public Guid? TabAccountId { get; set; }
    public Account? TabAccount { get; set; }

    // Navigation
    public ICollection<OperatorAccount> OperatorAccounts { get; set; } = [];
    public ICollection<Transaction> Transactions { get; set; } = [];
    public ICollection<DailyClose> DailyCloses { get; set; } = [];
}
```

| Column | Type | Null | Notes                                                                          |
|---|---|---|--------------------------------------------------------------------------------|
| Id | uuid | PK |                                                                                |
| Type | smallint | NOT NULL | Enum: Terminal=0, BankAccount=1, Tab=2                                         |
| Name | varchar(255) | NOT NULL | "Lenna", "CEF 1292", "Tab Lenna"                                               |
| Institution | varchar(255) | NULL | "Lotérica" for terminals, "Caixa Econômica" for bank accounts                  |
| Number | varchar(50) | NULL | Terminal number "1","2","3" or bank account number                             |
| BranchId | uuid | NOT NULL | FK → Branch                                                                    |
| TabAccountId | uuid | NULL | FK → Account (self). Only for Terminal type → points to its optional paired Tab account |
| CreatedAt | timestamptz | NOT NULL |                                                                                |
| Active | boolean | NOT NULL |                                                                                |

**Example data (for a branch where fiado is enabled on all three terminals):**

| Name | Type | TabAccountId | Institution | Number |
|---|---|---|---|---|
| Lenna | Terminal | → "Tab Lenna" | Lotérica | 1 |
| Jennifer | Terminal | → "Tab Jennifer" | Lotérica | 2 |
| Tracy | Terminal | → "Tab Tracy" | Lotérica | 3 |
| Tab Lenna | Tab | null | Lotérica | 4 |
| Tab Jennifer | Tab | null | Lotérica | 5 |
| Tab Tracy | Tab | null | Lotérica | 6 |
| CEF 1292 | BankAccount | null | Caixa Econômica | 5780706014 |
| CEF 4995 | BankAccount | null | Caixa Econômica | 5780706014 |

**TabAccountId invariants** (enforced at service layer and/or DB check constraints):
- Only `Terminal` accounts may have a non-null `TabAccountId`
- A Terminal may exist with `TabAccountId = null`
- The referenced account must be of type `Tab`
- A Tab account can belong to at most one Terminal (unique constraint on `TabAccountId` where not null)

**Account create/pair flow** (application layer):
- `CreateBankAccount` creates standalone Bank accounts
- `CreateTerminalAccount` creates standalone Terminals, Terminals linked to an existing Tab, or Terminals with a new Tab created in the same request
- When `CreateTerminalAccount` creates a new Tab in the same request, the new Tab starts with the same `Name`, `Institution`, and `Number` as the Terminal
- `CreateTabAccount` creates a new Tab for an existing Terminal that does not yet have one
- Pairing existing Terminal/Tab accounts is explicit
- Unpairing an existing Terminal/Tab association is explicit

### 3.8 OperatorAccount

Junction: which accounts an operator can use.

```csharp
public class OperatorAccount : EntityBase
{
    public Guid OperatorId { get; set; }
    public Operator Operator { get; set; } = null!;

    public Guid AccountId { get; set; }
    public Account Account { get; set; } = null!;

    public bool IsPrimary { get; set; } = false;
}
```

| Column | Type | Null | Notes |
|---|---|---|---|
| Id | uuid | PK | |
| OperatorId | uuid | NOT NULL | FK → Operator |
| AccountId | uuid | NOT NULL | FK → Account |
| IsPrimary | boolean | NOT NULL | Default false. True = this is the operator's main terminal |
| CreatedAt | timestamptz | NOT NULL | |
| Active | boolean | NOT NULL | |

**Unique constraint:** `(OperatorId, AccountId)`

**IsPrimary invariant:** At most one `IsPrimary = true` row per operator. Enforced via a unique filtered index: `UNIQUE (OperatorId) WHERE IsPrimary = true`.

### 3.9 Category

Top-level transaction classification. Determines the default direction (money in vs money out).

```csharp
public class Category : EntityBase
{
    public string Name { get; set; } = string.Empty;
    public Direction DefaultDirection { get; set; }

    public Guid BranchId { get; set; }
    public Branch Branch { get; set; } = null!;

    // Navigation
    public ICollection<TransactionType> TransactionTypes { get; set; } = [];
}
```

| Column | Type | Null | Notes |
|---|---|---|---|
| Id | uuid | PK | |
| Name | varchar(255) | NOT NULL | "Receita", "Saídas", "Despesas Comerciais", etc. |
| DefaultDirection | smallint | NOT NULL | Enum: In=0, Out=1 |
| BranchId | uuid | NOT NULL | FK → Branch |
| CreatedAt | timestamptz | NOT NULL | |
| Active | boolean | NOT NULL | |

**Unique constraint:** `(BranchId, Name) WHERE Active = true`

### 3.10 TransactionType

Subtype within a category. This is what the operator selects when creating a transaction.

```csharp
public class TransactionType : EntityBase
{
    public string Name { get; set; } = string.Empty;
    public SettlementRule SettlementRule { get; set; }
    public bool RequiresTabAccountAndClient { get; set; }

    public Guid CategoryId { get; set; }
    public Category Category { get; set; } = null!;

    // Navigation
    public ICollection<Transaction> Transactions { get; set; } = [];
}
```

| Column | Type | Null | Notes |
|---|---|---|---|
| Id | uuid | PK | |
| Name | varchar(255) | NOT NULL | "Depósito Dinheiro", "PIX", "Cartão de Crédito", etc. |
| SettlementRule | smallint | NOT NULL | Enum: SameDay=0, NextCalendarDay=1, NextBusinessDay=2, TwoBusinessDays=3, OperatorEnteredCheque=4 |
| RequiresTabAccountAndClient | boolean | NOT NULL | When true, transaction creation requires `Account.Type == Tab` and `ClientId != null` |
| CategoryId | uuid | NOT NULL | FK → Category |
| CreatedAt | timestamptz | NOT NULL | |
| Active | boolean | NOT NULL | Soft-disable without deleting |

**Note:** TransactionType inherits BranchId through its Category. No direct BranchId column needed.

**Unique constraint:** `(CategoryId, Name) WHERE Active = true`

**Important:** The same name can exist under different categories. "Cliente" exists under both "Entradas" (In) and "Saídas" (Out) — this is how the Fiado system distinguishes credit sales from client payments.

### 3.11 Client

A customer of the lottery house.

```csharp
public class Client : EntityBase
{
    public string Name { get; set; } = string.Empty;
    public string? Cpf { get; set; }
    public string? Cep { get; set; }
    public string? Address { get; set; }
    public string Phone { get; set; } = string.Empty;
    public string? PhoneSecondary { get; set; }
    public string? Notes { get; set; }
    public string? Email { get; set; }

    public Guid BranchId { get; set; }
    public Branch Branch { get; set; } = null!;

    // Navigation
    public ICollection<Transaction> Transactions { get; set; } = [];
}
```

| Column | Type | Null | Notes |
|---|---|---|---|
| Id | uuid | PK | |
| Name | varchar(255) | NOT NULL | |
| Cpf | varchar(11) | NULL | Brazilian personal ID (normalized digits, 11 chars) |
| Cep | varchar(9) | NULL | Postal code |
| Address | text | NULL | |
| Phone | varchar(20) | NOT NULL | Primary phone |
| PhoneSecondary | varchar(20) | NULL | |
| Notes | text | NULL | Freeform observations |
| Email | varchar(255) | NULL | |
| BranchId | uuid | NOT NULL | FK → Branch |
| CreatedAt | timestamptz | NOT NULL | |
| Active | boolean | NOT NULL | |

**Unique constraint:** `(BranchId, Cpf) WHERE Cpf IS NOT NULL AND Active = true` — at most one active client per CPF per branch. Prevents duplicate customer records while allowing the CPF field to remain optional.

### 3.12 Transaction

The central ledger. One row per financial event. This is the most important table in the system.

```csharp
public class Transaction : EntityBase
{
    // Core
    public DateTime Date { get; set; }
    public decimal Value { get; set; }
    public string? Description { get; set; }
    public TimeOnly? TransactionTime { get; set; }

    // Classification — TransactionType is the source of truth.
    // CategoryId and Direction are denormalized: set automatically from
    // TransactionType.CategoryId and Category.DefaultDirection at creation,
    // never independently editable.
    public Guid TransactionTypeId { get; set; }
    public TransactionType TransactionType { get; set; } = null!;

    public Guid CategoryId { get; set; }
    public Category Category { get; set; } = null!;

    public Direction Direction { get; set; }

    // Account
    public Guid AccountId { get; set; }
    public Account Account { get; set; } = null!;

    // Client (optional)
    public Guid? ClientId { get; set; }
    public Client? Client { get; set; }

    // Payment lifecycle
    public DateTime DueDate { get; set; }
    public DateTime? PaidAt { get; set; }

    // Installment linking — all installments in a group share the same OriginTransactionId,
    // including the first (which points to itself).
    public Guid? OriginTransactionId { get; set; }
    public Transaction? OriginTransaction { get; set; }

    // Authorship
    public Guid RecordedByOperatorId { get; set; }
    public Operator RecordedByOperator { get; set; } = null!;

    public Guid CreatedByUserId { get; set; }
    public User CreatedByUser { get; set; } = null!;

    // Status
    public TransactionStatus Status { get; set; } = TransactionStatus.Active;

    // Cancellation (soft delete with audit)
    public DateTime? CancelledAt { get; set; }
    public Guid? CancelledByUserId { get; set; }
    public User? CancelledByUser { get; set; }
    public string? CancellationReason { get; set; }

    // Tenant
    public Guid BranchId { get; set; }
    public Branch Branch { get; set; } = null!;
}
```

| Column | Type | Null | Notes |
|---|---|---|---|
| Id | uuid | PK | |
| Date | date | NOT NULL | When the financial event occurred |
| Value | numeric(14,2) | NOT NULL | Always positive |
| Description | varchar(500) | NULL | Freeform notes. For credit card: "Parcelado 3x" or "à vista" |
| TransactionTime | time | NULL | Time of day (used by cash deposits and other time-sensitive entries) |
| TransactionTypeId | uuid | NOT NULL | FK → TransactionType. **Source of truth for classification** |
| CategoryId | uuid | NOT NULL | FK → Category. **Denormalized** — always equals TransactionType.CategoryId, set at creation, never independently editable |
| Direction | smallint | NOT NULL | Enum: In=0, Out=1. **Denormalized** — always equals Category.DefaultDirection, set at creation, never independently editable |
| AccountId | uuid | NOT NULL | FK → Account |
| ClientId | uuid | NULL | FK → Client. Only for client-related transactions |
| DueDate | date | NOT NULL | When payment is expected |
| PaidAt | timestamptz | NULL | When actually paid. NULL = unpaid |
| OriginTransactionId | uuid | NULL | FK → Transaction (self). All installments in a group share the same value, including the first (self-reference) |
| RecordedByOperatorId | uuid | NOT NULL | FK → Operator. Which operator's context (terminal) this transaction belongs to |
| CreatedByUserId | uuid | NOT NULL | FK → User. Who actually created this record (may differ from operator for manager corrections) |
| Status | smallint | NOT NULL | Enum: Draft=0, Active=1, Cancelled=2 |
| CancelledAt | timestamptz | NULL | |
| CancelledByUserId | uuid | NULL | FK → User |
| CancellationReason | varchar(500) | NULL | |
| BranchId | uuid | NOT NULL | FK → Branch |
| CreatedAt | timestamptz | NOT NULL | |
| Active | boolean | NOT NULL | EntityBase default. Redundant with Status for transactions but kept for consistency |

**Nullable columns analysis** (8 nullable content columns):
- `Description` — not all transaction types use it
- `TransactionTime` — only cash deposits and similar need time tracking
- `ClientId` — only client-related transactions
- `PaidAt` — null = unpaid (meaningful null)
- `OriginTransactionId` — only installments (non-null for all members of a group, including the first)
- `CancelledAt` — only cancelled transactions
- `CancelledByUserId` — only cancelled transactions
- `CancellationReason` — only cancelled transactions

**Key indexes:**
- `(BranchId, Date, AccountId)` — the primary query pattern (daily ledger per operator)
- `(BranchId, AccountId, Direction, Date)` — Fiado balance calculation
- `(BranchId, DueDate)` WHERE `PaidAt IS NULL` — receivables/aging queries
- `(OriginTransactionId)` WHERE NOT NULL — installment group lookup
- `(BranchId, Status)` — draft/active filtering
- `(BranchId, RecordedByOperatorId, Date)` — "my transactions today" for operator view

### 3.13 Product

Categories of values tracked in the daily closing snapshot. Not physical inventory — these are the line items an operator fills in when closing their register.

```csharp
public class Product : EntityBase
{
    public string Name { get; set; } = string.Empty;
    public int DisplayOrder { get; set; } = 0;

    public Guid BranchId { get; set; }
    public Branch Branch { get; set; } = null!;

    // Navigation
    public ICollection<DailyCloseItem> DailyCloseItems { get; set; } = [];
}
```

| Column | Type | Null | Notes |
|---|---|---|---|
| Id | uuid | PK | |
| Name | varchar(255) | NOT NULL | "Dinheiro", "Telesena", "Raspadinha", "Jogos", "Loteria Especial", "Federal", "Tarifa Bolão", "Diferença Caixa" |
| DisplayOrder | int | NOT NULL | Controls form field ordering |
| BranchId | uuid | NOT NULL | FK → Branch |
| CreatedAt | timestamptz | NOT NULL | |
| Active | boolean | NOT NULL | |

**Unique constraint:** `(BranchId, Name) WHERE Active = true`

### 3.14 DailyClose

The daily register closing session. One row per account per day. Tracks the submission/approval workflow.

```csharp
public class DailyClose : EntityBase
{
    public DateTime Date { get; init; }
    public DailyCloseStatus Status { get; set; } = DailyCloseStatus.Draft;

    public Guid AccountId { get; init; }
    public Account Account { get; init; } = null!;

    public Guid? SubmittedByOperatorId { get; set; }
    public Operator? SubmittedByOperator { get; set; }

    public DateTime? SubmittedAt { get; set; }
    public DateTime? ApprovedAt { get; set; }

    public Guid? ApprovedByUserId { get; set; }
    public User? ApprovedByUser { get; set; }

    public string? RejectionReason { get; set; }
    public string? Notes { get; set; }

    public DateTime? UpdatedAt { get; set; }
    public Guid? UpdatedByUserId { get; set; }

    public Guid BranchId { get; init; }
    public Branch Branch { get; init; } = null!;

    // Navigation
    public ICollection<DailyCloseItem> Items { get; init; } = [];
}
```

| Column                | Type          | Null     | Notes                                              |
|-----------------------|---------------|----------|----------------------------------------------------|
| Id                    | uuid          | PK       |                                                    |
| Date                  | date          | NOT NULL | The business day being closed                      |
| Status                | smallint      | NOT NULL | Enum: Draft=0, Submitted=1, Approved=2, Rejected=3 |
| AccountId             | uuid          | NOT NULL | FK → Account                                       |
| SubmittedByOperatorId | uuid          | NULL     | FK → Operator                                      |
| SubmittedAt           | timestamptz   | NULL     |                                                    |
| ApprovedAt            | timestamptz   | NULL     |                                                    |
| ApprovedByUserId      | uuid          | NULL     | FK → User (manager/admin who approved)             |
| RejectionReason       | varchar(500)  | NULL     |                                                    |
| Notes                 | varchar(1000) | NULL     | Operator notes about the day                       |
| UpdatedAt             | timestamptz   | NULL     | Generic workflow mutation audit timestamp          |
| UpdatedByUserId       | uuid          | NULL     | User who last changed the workflow/item state      |
| BranchId              | uuid          | NOT NULL | FK → Branch                                        |
| CreatedAt             | timestamptz   | NOT NULL |                                                    |
| Active                | boolean       | NOT NULL |                                                    |

**Unique constraint:** filtered unique index `(BranchId, AccountId, Date) WHERE Active = true` — one active closing per account per day.

### 3.15 DailyCloseItem

Individual product values within a daily closing.

```csharp
public class DailyCloseItem : EntityBase
{
    public decimal Value { get; set; }

    public Guid DailyCloseId { get; init; }
    public DailyClose DailyClose { get; init; } = null!;

    public Guid ProductId { get; init; }
    public Product Product { get; init; } = null!;
}
```

| Column       | Type          | Null     | Notes                              |
|--------------|---------------|----------|------------------------------------|
| Id           | uuid          | PK       |                                    |
| Value        | numeric(14,2) | NOT NULL | The closing value for this product |
| DailyCloseId | uuid          | NOT NULL | FK → DailyClose                    |
| ProductId    | uuid          | NOT NULL | FK → Product                       |
| CreatedAt    | timestamptz   | NOT NULL |                                    |
| Active       | boolean       | NOT NULL |                                    |

**Unique constraint:** filtered unique index `(DailyCloseId, ProductId) WHERE Active = true` — one active value per product per closing session. Soft-deleted item rows do not block a later re-insert for the same product.

### 3.16 TimeEntry

Operator attendance tracking. Standalone — not mixed with the transaction flow. Clock pairs live on child `TimeEntrySegment` rows (full `DateTime`, branch-local wall clock); this parent row stores the day, status, and computed totals.

```csharp
public class TimeEntry : EntityBase
{
    public DateTime Date { get; init; }
    public TimeEntryStatus Status { get; set; }
    public decimal TotalHours { get; set; }
    public decimal BalanceHours { get; set; }

    public Guid OperatorId { get; init; }
    public Operator Operator { get; init; } = null!;

    public Guid BranchId { get; init; }
    public Branch Branch { get; init; } = null!;

    public DateTime? UpdatedAt { get; set; }
    public Guid? UpdatedByUserId { get; set; }
    public User? UpdatedByUser { get; set; }

    public ICollection<TimeEntrySegment> Segments { get; init; } = [];
}
```

| Column          | Type         | Null     | Notes                                                                    |
|-----------------|--------------|----------|--------------------------------------------------------------------------|
| Id              | uuid         | PK       |                                                                          |
| Date            | date         | NOT NULL | Branch-local calendar day                                                |
| Status          | smallint     | NOT NULL | Enum (see section 4)                                                     |
| TotalHours      | numeric(6,2) | NOT NULL | Net hours worked after lunch deduction                                   |
| BalanceHours    | numeric(6,2) | NOT NULL | TotalHours minus daily target. Positive = overtime, negative = owes time |
| OperatorId      | uuid         | NOT NULL | FK → Operator                                                            |
| BranchId        | uuid         | NOT NULL | FK → Branch                                                              |
| UpdatedAt       | timestamptz  | NULL     | Stamped on every upsert after creation                                   |
| UpdatedByUserId | uuid         | NULL     | FK → User; who last modified this entry                                  |
| CreatedAt       | timestamptz  | NOT NULL |                                                                          |
| Active          | boolean      | NOT NULL |                                                                          |

**Filtered unique constraint:** `(BranchId, OperatorId, Date) WHERE Active = true` — one active entry per operator per day.

### 3.16a TimeEntrySegment

Each clock-in/clock-out pair for a `TimeEntry`. A single day may have multiple segments (e.g. morning + afternoon after a lunch break). Clocks are stored as `DateTime` (branch-local wall clock, `timestamp without time zone`).

```csharp
public class TimeEntrySegment : EntityBase
{
    public DateTime ClockIn { get; set; }
    public DateTime? ClockOut { get; set; }

    public Guid TimeEntryId { get; init; }
    public TimeEntry TimeEntry { get; init; } = null!;

    public DateTime? UpdatedAt { get; set; }
    public Guid? UpdatedByUserId { get; set; }
    public User? UpdatedByUser { get; set; }
}
```

| Column | Type | Null | Notes |
|---|---|---|---|
| Id | uuid | PK | |
| ClockIn | timestamp (no tz) | NOT NULL | Branch-local wall clock; must fall within `[parent.Date, parent.Date + 1 day)` |
| ClockOut | timestamp (no tz) | NULL | Null = open shift; when set must be > ClockIn and ClockOut − ClockIn ≤ 24 h |
| TimeEntryId | uuid | NOT NULL | FK → TimeEntry (CASCADE on delete) |
| UpdatedAt | timestamptz | NULL | Stamped on admin edit |
| UpdatedByUserId | uuid | NULL | FK → User; who last modified this segment |
| CreatedAt | timestamptz | NOT NULL | |
| Active | boolean | NOT NULL | |

**Day-bounds invariant:** `segment.ClockIn ∈ [parent.Date, parent.Date + 1 day)`. When `ClockOut` is set: `ClockOut > ClockIn` and `ClockOut − ClockIn ≤ 24 h`, which implicitly bounds `ClockOut < parent.Date + 2 days`.

**Filtered unique constraint:** `(TimeEntryId) WHERE "ClockOut" IS NULL AND "Active" = true` — at most one open segment per TimeEntry at any moment. Named `IX_TimeEntrySegments_TimeEntryId_OpenShift`.

**Secondary index:** `(TimeEntryId, ClockIn)` for segment ordering queries.

**FK behaviour:** `OnDelete(DeleteBehavior.Cascade)` from `TimeEntry` — deleting (or hard-deleting) a TimeEntry also removes its segments.

### 3.17 Holiday

Branch-specific holidays that affect business day calculations and time entries.

```csharp
public class Holiday : EntityBase
{
    public DateTime Date { get; init; }
    public string? Description { get; set; }
    public HolidaySource Source { get; set; } = HolidaySource.Manual;

    public Guid BranchId { get; init; }
    public Branch Branch { get; init; } = null!;
}
```

| Column      | Type         | Null     | Notes                                                                                                    |
|-------------|--------------|----------|----------------------------------------------------------------------------------------------------------|
| Id          | uuid         | PK       |                                                                                                          |
| Date        | date         | NOT NULL |                                                                                                          |
| Description | varchar(500) | NULL     | Human-readable label (e.g. "Natal")                                                                      |
| Source      | smallint     | NOT NULL | `HolidaySource` enum (Manual=0, Canonical=1, BrasilApi=2, Nager=3); default `Manual`; informational only |
| BranchId    | uuid         | NOT NULL | FK → Branch                                                                                              |
| CreatedAt   | timestamptz  | NOT NULL |                                                                                                          |
| Active      | boolean      | NOT NULL |                                                                                                          |

**Filtered unique constraint:** `(BranchId, Date) WHERE Active = true` — one active holiday per date per branch.

**`Source` provenance.** `HolidaySource` records where each row came from. Manual entries from `POST /holiday` set `Manual`; the canonical-only import path sets `Canonical`; the multi-source composite path sets the per-concept claim (`BrasilApi`, `Nager`, or `Canonical` for backfilled concepts). The column is informational — no business rule reads it. The Phase 6.5 migration adds it with default `0` (Manual) and backfills every existing row to `Manual` (correct for any row predating Phase 6 imports and indistinguishable from a hand-entry).

### 3.18 Setting

Per-branch configuration. Single row per branch.

```csharp
public class Setting : EntityBase
{
    public DateTime LockDate { get; set; }
    public decimal DailyTargetHours { get; set; } = 7.33m;
    public decimal LunchDeductionOver6H { get; set; } = 1.0m;
    public decimal LunchDeductionOver4H { get; set; } = 0.25m;

    public Guid BranchId { get; set; }
    public Branch Branch { get; set; } = null!;
}
```

| Column               | Type         | Null     | Notes                                                                             |
|----------------------|--------------|----------|-----------------------------------------------------------------------------------|
| Id                   | uuid         | PK       |                                                                                   |
| LockDate             | date         | NOT NULL | Transactions on or before this date cannot be edited. Access: `conf_dtfechamento` |
| DailyTargetHours     | numeric(6,2) | NOT NULL | Default 7.33 (7h20m). Used in TimeEntry balance calculation                       |
| LunchDeductionOver6H | numeric(4,2) | NOT NULL | Default 1.00. Hours deducted for lunch when worked >6h                            |
| LunchDeductionOver4H | numeric(4,2) | NOT NULL | Default 0.25. Hours deducted for break when worked >4h but ≤6h                    |
| BranchId             | uuid         | NOT NULL | FK → Branch. UNIQUE — one setting row per branch                                  |
| CreatedAt            | timestamptz  | NOT NULL |                                                                                   |
| Active               | boolean      | NOT NULL |                                                                                   |

---

## 4. Enums

Only truly fixed, binary, or logic-dependent values are enums. Everything else is data.

```csharp
public enum Role
{
    Admin = 0,     // Full access, can manage branches and users
    Manager = 1,   // Can approve closings, cancel old transactions, view all operators
    Member = 2     // Operator: can only see/edit own terminal, submit closings
}

public enum Direction
{
    In = 0,        // Money enters (Positivo) — Receita, Entradas, Crédito Banco
    Out = 1        // Money leaves (Negativo) — Saídas, Despesas, Débito Banco
}

public enum AccountType
{
    Terminal = 0,     // Operator's cash drawer
    BankAccount = 1,  // CEF or other bank account
    Tab = 2           // Fiado (credit) account, optionally paired with a Terminal
}

public enum TransactionStatus
{
    Draft = 0,       // Saved but not finalized (mobile offline, or operator still editing)
    Active = 1,      // Finalized, counts in all calculations
    Cancelled = 2    // Soft-deleted with audit trail
}

public enum SettlementRule
{
    SameDay = 0,                 // DueDate = Date
    NextCalendarDay = 1,         // DueDate = Date + 1 calendar day
    NextBusinessDay = 2,         // DueDate = next Monday-Friday after Date
    TwoBusinessDays = 3,         // DueDate = second Monday-Friday after Date
    OperatorEnteredCheque = 4    // DueDate is supplied by the operator for pre-dated cheques
}

public enum DailyCloseStatus
{
    Draft = 0,       // Operator is entering values
    Submitted = 1,   // Operator finished, awaiting manager review
    Approved = 2,    // Manager confirmed
    Rejected = 3     // Manager found issues, operator needs to fix
}

public enum TimeEntryStatus
{
    Present = 1,
    DayOff = 2,                // Folga banco de horas — hours owed
    Sunday = 3,                // Abonado — no hours owed
    Holiday = 4,               // Abonado
    Vacation = 5,              // Abonado
    JustifiedAbsence = 6,      // Abonado
    UnjustifiedAbsence = 7     // Hours owed
}
```

**SettlementRule due-date semantics:**

| Value                 | DueDate behavior                                                                          |
|-----------------------|-------------------------------------------------------------------------------------------|
| SameDay               | DueDate equals `Date`                                                                     |
| NextCalendarDay       | DueDate equals `Date.AddDays(1)`                                                          |
| NextBusinessDay       | DueDate is the next business day after `Date`; weekends and branch holidays are skipped   |
| TwoBusinessDays       | DueDate is the second business day after `Date`; weekends and branch holidays are skipped |
| OperatorEnteredCheque | DueDate must be explicitly entered by the operator and must be on or after `Date`         |

### What is NOT an enum

| Concept | Why data table |
|---|---|
| Category | Owner may add new expense categories |
| TransactionType | New payment methods emerge (PIX was added to Access mid-lifecycle) |
| Product | New lottery products can appear from CEF |

---

## 5. Seed Data

When a new Branch is created, these rows are seeded automatically.

These are the minimum default seeds for the new operational flow, not a complete migration of every Access lookup row. The owner can create additional Categories, TransactionTypes, and Products through the admin interface as needed (e.g., the 30+ expense subtypes under Despesas Administrativas, Pessoal, and Financeiras that exist in the Access system).

### Categories

| Name                     | DefaultDirection | Access cat_id |
|--------------------------|------------------|---------------|
| Receita                  | In               | 1             |
| Crédito Banco            | In               | 2             |
| Entradas                 | In               | 3             |
| Despesas Administrativas | Out              | 4             |
| Despesas Comerciais      | Out              | 5             |
| Despesas Pessoal         | Out              | 6             |
| Despesas Financeiras     | Out              | 7             |
| Débito Banco             | Out              | 8             |
| Saídas                   | Out              | 9             |

### TransactionTypes

| Name              | Parent Category     | SettlementRule        | RequiresTabAccountAndClient | Access tipo_id |
|-------------------|---------------------|-----------------------|-----------------------------|----------------|
| Cliente           | Saídas              | SameDay               | true                        | 2              |
| Depósito Dinheiro | Saídas              | NextCalendarDay       | false                       | 3              |
| Cartão de Crédito | Saídas              | TwoBusinessDays       | false                       | 4              |
| MarketPlace       | Saídas              | SameDay               | false                       | 5              |
| Sobra de Bolão    | Despesas Comerciais | SameDay               | false                       | 6              |
| Sobra de Federal  | Despesas Comerciais | SameDay               | false                       | 9              |
| Depósito Cheque   | Saídas              | OperatorEnteredCheque | false                       | 15             |
| PIX               | Saídas              | SameDay               | false                       | 16             |
| Cartão de Débito  | Saídas              | NextBusinessDay       | false                       | 17             |
| Telesena          | Saídas              | SameDay               | false                       | 18             |
| Troca de Telesena | Saídas              | SameDay               | false                       | 19             |
| Raspadinha        | Saídas              | SameDay               | false                       | 20             |
| Encalhe Federal   | Saídas              | SameDay               | false                       | 22             |
| Cliente           | Entradas            | SameDay               | true                        | 23             |
| Pgto Prêmio       | Saídas              | SameDay               | false                       | 24             |
| Desconto          | Despesas Comerciais | SameDay               | false                       | 25             |
| Volante rejeitado | Despesas Comerciais | SameDay               | false                       | 26             |
| Tarifa cartão     | Despesas Comerciais | SameDay               | false                       | 27             |
| Outras Despesas   | Despesas Comerciais | SameDay               | false                       | 28             |

Note: "Cliente" appears twice — under Saídas (credit sale, money leaves) and Entradas (client payment, money enters). This is the Fiado in/out mechanism.

### Products (DailyClose items)

| Name             | DisplayOrder | Access prod_id |
|------------------|--------------|----------------|
| Telesena         | 1            | 2              |
| Raspadinha       | 2            | 3              |
| Jogos            | 3            | 4              |
| Loteria Especial | 4            | 5              |
| Dinheiro         | 5            | 6              |
| Tarifa Bolão     | 6            | 7              |
| Federal          | 7            | 11             |
| Diferença Caixa  | 8            | 13             |

Note: **Fiado is NOT a DailyClose product** — it is calculated at query time from Tab account transactions (sum of Out minus sum of In). The Access FrmCaixa form displays a calculated Fiado balance but does NOT persist it to TblEstoque. Access prod_id 1 (Fiado), 8 (Total Caixa), 9 (HorasTrabalhadas), 10 (Ausente), 12 (Operador) are NOT migrated — these were computed values or metadata shoehorned into the product/estoque pattern.

### 5.1 Brazilian Holiday Calendar

Milestone 6 adds a deterministic Brazilian calendar source used by `GET /holiday/import-br/{year}/preview` and `POST /holiday/import-br/{year}`. This source is branch-agnostic and has no external network dependency: fixed-date national holidays are emitted directly, and Easter-relative dates are computed with the Anonymous Gregorian algorithm, also known as the Meeus/Jones/Butcher algorithm. The supported tested range is 1900-2200.

When `includeOptionalFederal = false`, the API returns/imports only the 10 `National` entries. When `includeOptionalFederal = true`, it appends the 3 `OptionalFederal` entries below and returns all 13 rows in date order.

#### National holidays

| Date rule       | Description                | Type     | Basis                                                                                  |
|-----------------|----------------------------|----------|----------------------------------------------------------------------------------------|
| 1 Jan           | Confraternização Universal | National | Mandatory federal holiday                                                              |
| Easter - 2 days | Sexta-feira Santa          | National | Law 9.093/1995 religious-holiday basis; included in the mandatory operational baseline |
| 21 Apr          | Tiradentes                 | National | Mandatory federal holiday                                                              |
| 1 May           | Dia do Trabalho            | National | Mandatory federal holiday                                                              |
| 7 Sep           | Independência              | National | Mandatory federal holiday                                                              |
| 12 Oct          | Nossa Senhora Aparecida    | National | Mandatory federal holiday                                                              |
| 2 Nov           | Finados                    | National | Mandatory federal holiday                                                              |
| 15 Nov          | Proclamação da República   | National | Mandatory federal holiday                                                              |
| 20 Nov          | Consciência Negra          | National | Law 14.759/2023 declared the date a national holiday                                   |
| 25 Dec          | Natal                      | National | Mandatory federal holiday                                                              |

#### Optional federal operational subset

These rows are the curated M6 operational subset of MGI annual optional federal `pontos facultativos`. They are not mandatory national holidays by federal law. State or municipal law, banking practice, or branch policy may make any of them operationally closed for a specific branch.

| Date rule        | Description                      | Type            | Basis                                                                      |
|------------------|----------------------------------|-----------------|----------------------------------------------------------------------------|
| Easter - 47 days | Carnaval (terça)                 | OptionalFederal | MGI annual optional federal calendar; deterministic Easter-anchored subset |
| Easter - 46 days | Quarta-feira de Cinzas (até 14h) | OptionalFederal | MGI annual optional federal calendar; deterministic Easter-anchored subset |
| Easter + 60 days | Corpus Christi                   | OptionalFederal | MGI annual optional federal calendar; deterministic Easter-anchored subset |

The MGI annual optional calendar is broader than this subset and can include Carnival Monday, the Corpus Christi bridge Friday, Christmas Eve and New Year's Eve afternoons, Dia do Servidor Público, and other bridge days that vary by annual decree. Those are intentionally not bulk-imported by the M6 default because they are not fixed by federal law and are not all Easter-derived. Branch admins can add any additional local or annually declared optional day through the existing `POST /holiday` endpoint.

#### Sources

Both import endpoints accept a `source` query parameter (`Composite | Canonical | BrasilApi | Nager`) that selects which source the resolver uses. `source = Composite` is the default and is what existing M6 Phase 6 callers continue to get when they omit the parameter.

**Composite ordering.** When `source = Composite`, the resolver walks providers in priority order `Nager → BrasilAPI → Canonical`. For each of the 13 canonical concepts the highest-priority provider that returns a name-matching row claims it (with that provider's date and description); any concept still unclaimed after both providers run is backfilled from the canonical calendar. Composite **never** surfaces 502 because the canonical backfill always provides a value — failed provider results are logged as warnings and skipped.

**Concept catalog identity.** The 13 canonical concepts each have a stable `ConceptId` (`CONFRATERNIZACAO_UNIVERSAL`, `CARNAVAL_TERCA`, `QUARTA_FEIRA_CINZAS`, `SEXTA_FEIRA_SANTA`, `TIRADENTES`, `DIA_DO_TRABALHO`, `CORPUS_CHRISTI`, `INDEPENDENCIA`, `NOSSA_SENHORA_APARECIDA`, `FINADOS`, `PROCLAMACAO_REPUBLICA`, `CONSCIENCIA_NEGRA`, `NATAL`), a canonical description, a `BrazilianHolidayType`, an Easter-aware `ExpectedDateForYear` function (Easter is computed once via the Meeus/Jones/Butcher algorithm), and a list of accent-stripped lowercased name-match keywords. Provider rows are matched to concepts by **name pattern first, then ±3-day date proximity tiebreaker** among same-name candidates (Nager returns two "Carnaval" rows for Mon+Tue — the one nearer Easter−47 wins). Provider rows that match no concept are dropped silently. Provider rows that match by name but fall further than ±3 days from the expected date still match — the winning source's date overrides expected, so a future provider update or a leap-year corner case never silently drops a real holiday.

**Provider quirks.**

- **BrasilAPI** (`https://brasilapi.com.br/api/feriados/v1/{year}`) renames a few concepts versus the federal-law text: "Confraternização mundial" instead of "Confraternização Universal", "Dia da consciência negra" instead of "Consciência Negra", "Independência do Brasil" instead of "Independência". The catalog's name matchers normalize over these differences so the same concept still gets claimed. BrasilAPI also returns "Páscoa" (Easter Sunday) which matches no concept and is dropped.
- **Nager.Date** (`https://date.nager.at/api/v3/PublicHolidays/{year}/BR`) returns regional rows with `global: false` and a populated `counties` array (e.g. "Revolução Constitucionalista de 1932" for `BR-SP`). All `global: false` rows are dropped — the calendar tracks only national entries. Nager's English `name` field is concatenated with `localName` before normalization so English-keyword matchers (e.g. "good friday", "ash wednesday", "labour day") can also hit.
- **Quarta-feira de Cinzas** is missing from both providers in practice. With `source = Composite | BrasilApi | Nager`, the catalog's canonical Easter-46 date always backfills this concept, and the persisted `Holiday.Source` for that row is `Canonical` even when the rest of the import came from a provider.

**Per-row provenance.** Both `ResponseBrazilianHolidayPreviewItemJson` and `ResponseBrazilianHolidayImportItemJson` carry a `Source` field tagging the actual provenance per row (`Canonical | BrasilApi | Nager`). The top-level response envelopes (`ResponseBrazilianHolidayPreviewJson`, `ResponseBrazilianHolidayImportJson`) echo the requested `Source` so a UI can label the result. On `POST /holiday/import-br/{year}`, each persisted `Holiday.Source` column receives the per-concept tag from the resolver — so a row that BrasilAPI claimed during a composite run is persisted with `Holiday.Source = BrasilApi`, while a row that fell through to canonical backfill is persisted with `Holiday.Source = Canonical`.

**Failure contract.** Explicit single-source calls (`source = BrasilApi` or `source = Nager`) return **502 Bad Gateway** with the `HOLIDAY_SOURCE_UNAVAILABLE` error key when the provider HTTP call times out, returns a non-2xx status, or returns malformed JSON. The Infrastructure provider clients themselves never throw `server.Exceptions` types — they return a domain `BrazilianHolidayProviderResult<T>` with `Success = false`, and the Application-layer resolver is the single place that translates a failed result into `ExternalProviderUnavailableException`. `source = Composite` is unreachable for this error path because Canonical never fails.

**Operational pattern.** The route range stays `min(1900):max(2200)` — admins can preview/import any year inside that range. The recommended cron pattern (external to this repo, not shipped by Phase 6.5) is a single annual run in late December that imports `currentYear + 1`, so day one of the next year already has its calendar populated. Re-running the same year is all-Skipped because the `(BranchId, Date) WHERE Active = true` filtered unique index makes the import idempotent.

---

## 6. Business Rules

### 6.1 Transaction creation

- `Value` must be > 0 (always positive, `Direction` determines sign semantics)
- **Classification invariant:** `TransactionTypeId` is the only input from the operator. On creation, the system automatically sets:
  - `CategoryId` = `TransactionType.CategoryId`
  - `Direction` = `Category.DefaultDirection`
  - These denormalized fields are NEVER independently editable after creation
- The Fiado in/out distinction is handled by two separate TransactionTypes ("Cliente" under "Entradas" = In, "Cliente" under "Saídas" = Out), not by overriding Direction
- `RequiresTabAccountAndClient = true` on a TransactionType is enforced by the branch-consistency layer: the transaction must use an `Account` with `Type == Tab` and must include `ClientId`. Both seeded "Cliente" rows set this flag; all other seeded TransactionTypes set it to false.
- `DueDate` defaults are driven by `TransactionType.SettlementRule`:
  - `SameDay`: same as `Date`
  - `NextCalendarDay`: `Date + 1 day`
  - `NextBusinessDay`: next business day after `Date` (skips weekends and branch holidays)
  - `TwoBusinessDays`: second business day after `Date` (skips weekends and branch holidays)
  - `OperatorEnteredCheque`: operator enters custom date; must be ≥ `Date`
- `RecordedByOperatorId` is set from the authenticated operator's context
- `CreatedByUserId` is set from the authenticated user's session
- `Draft` status transactions are excluded from all financial calculations (sums, balances, reports)
- Only `Active` transactions count in ledger totals

### 6.2 Transaction cancellation

- Transactions on the current day: operator can cancel their own (checked via `RecordedByOperatorId == currentOperator.Id`)
- Transactions older than today: requires Role.Manager or Role.Admin
- Transactions on or before `Setting.LockDate`: cannot be cancelled by anyone
- Cancellation sets `Status = Cancelled`, `CancelledAt = now`, `CancelledByUserId`, and requires `CancellationReason`
- Cancelled transactions remain in the database, visible in audit views, excluded from financial calculations

### 6.3 Installments (cheque pre-dated)

When a pre-dated cheque is recorded with installments, the system creates N separate Transaction rows, one per installment. The endpoint requires a TransactionType with `SettlementRule = OperatorEnteredCheque`; otherwise the request is rejected.

**Manual rows are the default.** Each installment item carries its own `Value` and `DueDate`. The request-level `Value` must equal the exact sum of all row values. Row count is bounded `[2..24]`. Row due dates must all be on or after the transaction `Date`, the first must be in the future, and they must be strictly increasing.

**Auto-generation is optional** via `AutoGenerateInstallments = true` together with `InstallmentCount` and a base `DueDate`. When enabled:
- All generated rows have equal `Value`, rounded to 2 decimal places (`MidpointRounding.AwayFromZero`); any residual goes on the last row so the row sum matches the request total exactly.
- Generated `DueDate`s are staggered monthly from the base date (`baseDueDate.AddMonths(i-1)`); any row falling on a weekend or a branch holiday is moved to the next business day.
- If the auto-split would produce any non-positive row (e.g. `0.10` over 6 installments), the request is rejected.
- The base `DueDate` must be in the future; manual `Installments` must be empty when the flag is on.

**Row description format:** `CH PRE ({i}/{N}) - {Description}` when the operator supplies a non-empty description; otherwise the row description is exactly `CH PRE ({i}/{N})` with no trailing separator. The operator-supplied description is capped to leave room for the longest possible prefix (`CH PRE (24/24) - `, 17 chars) so the persisted row description fits the `varchar(500)` column.

**All installments share the same `OriginTransactionId`, including the first** (which points to itself). This simplifies group lookups: `WHERE OriginTransactionId = @groupId` returns all members. This matches the Access behavior where `lco_origem` on the first installment is set to its own `lco_id`.

`SaveAsDraft = true` persists every generated/manual row with `Status = Draft`; the default is `Active`.

Credit card transactions do NOT create multiple rows — the total is stored as one transaction with description "Parcelado 3x" or "à vista". The installment split is informational only for credit cards in the current system.

### 6.4 Fiado (tab) balance

Outstanding credit for an account = sum of `Out` transactions minus sum of `In` transactions on that Tab account, where `Status = Active` and optionally filtered by date range and/or client.

```sql
SELECT
    SUM(CASE WHEN Direction = 1 THEN Value ELSE 0 END)  -- Out (credit given)
  - SUM(CASE WHEN Direction = 0 THEN Value ELSE 0 END)  -- In (payments received)
  AS Balance
FROM Transaction
WHERE AccountId = @tabAccountId
  AND Status = 1  -- Active only
  AND Date <= @asOfDate
```

### 6.5 Daily closing

1. Operator opens DailyClose (creates with Status=Draft)
2. Operator enters DailyCloseItems (one per product — cash count, ticket values, etc.)
3. Operator submits (Status=Submitted, SubmittedAt=now)
4. Manager reviews:
   - Approve (Status=Approved, ApprovedAt=now)
   - Reject with reason (Status=Rejected, RejectionReason filled)
5. If rejected, the next item edit automatically transitions `Rejected -> Draft`; the operator edits and resubmits.
6. If submitted by mistake, the next item edit can automatically recall `Submitted -> Draft` when the editing caller is the recording operator Member on the same branch-local business day, or any Manager/Admin. The recall clears `SubmittedAt`, keeps the existing system-managed CashVariance row, and stamps the generic audit pair.

**Opening values** for the current day = closing values (DailyCloseItems) from the most recent prior DailyClose for the same account. The system queries the top row where `BranchId = @branchId AND AccountId = @accountId AND Date < @today`, ordered by `Date DESC, CreatedAt DESC, Id DESC`. This handles weekends, holidays, and missing close days without a holiday calendar dependency.

**CashVariance** (Diferença Caixa) is system-calculated and persisted as a DailyCloseItem with the "Diferença Caixa" product. See rule 6.12 for the calculation formula. The operator does not enter this value directly.

**Fiado balance** is NOT stored as a DailyCloseItem — it is always calculated at query time from Tab account transactions (see rule 6.4). The daily close form displays it for reference but does not persist it as a snapshot value.

### 6.6 Date locking

`Setting.LockDate` defines the cutoff. Transactions on or before this date cannot be created, edited, or cancelled. Every DailyClose workflow transition on or before this date is blocked: open, edit items, submit, approve, reject, `Rejected -> Draft` auto-transition, and `Submitted -> Draft` recall. The lock date is advanced by the manager after month-end reconciliation.

### 6.7 Time entry calculation

This logic is implemented in `ITimeEntryCalculationService` / `TimeEntryCalculationService` under `server.Application/Services/TimeEntries/`. The service consumes a list of segment inputs (full `DateTime` pairs) rather than a single clock pair, which makes overnight segments unambiguous without any wrap-around arithmetic.

**Method signature (Phase 3.6 target):**

```
ITimeEntryCalculationService.Calculate(
    status,
    segments: IReadOnlyList<TimeEntrySegmentInput>,  // (DateTime ClockIn, DateTime? ClockOut)
    entryDate: DateTime,
    branchLocalNow: DateTime,
    dailyTargetHours,
    lunchDeductionOver6H,
    lunchDeductionOver4H)
  → (TotalHours, BalanceHours)
```

**Dual-shape PUT contract:**

The PUT /timeentry endpoint accepts two mutually exclusive shapes:

- **Member shape:** supplies `Action: Open | Close` (no `Segments`). The server stamps the current branch-local `DateTime` as the segment clock. `Open` appends a new open segment (ClockOut = null). `Close` closes the single open segment for that TimeEntry. Sending both `Action` and `Segments` is rejected with `TIMEENTRY_MEMBER_SHOULD_NOT_SEND_SEGMENTS`. Sending neither when the caller is a Member is rejected with `TIMEENTRY_MEMBER_TAP_ACTION_REQUIRED`.
- **Admin shape (Manager or Admin):** supplies `Segments` (no `Action`) as an explicit list of `{ ClockIn: DateTime, ClockOut: DateTime? }` pairs. The server replaces the entire segment set atomically. Sending both `Action` and `Segments` is rejected with `TIMEENTRY_ADMIN_SHOULD_NOT_SEND_TAP_ACTION`. Sending neither `Action` nor `Segments` as an Admin is rejected with `TIMEENTRY_ADMIN_REQUIRES_SEGMENTS`.

**Idempotent Member no-ops:**

If a Member retries `Action: Open` when an open segment already exists, the use case returns the current entry unchanged (no-op, not a conflict). If a Member retries `Action: Close` when no open segment exists, the use case also returns the current entry unchanged. These no-ops exist so that mobile clients can safely retry on network errors.

**Status-transition rule:**

The PUT payload may change `Status`. If the current TimeEntry already has active segments and the incoming `Status` ≠ `Present`, the use case rejects with `TIMEENTRY_STATUS_CHANGE_REQUIRES_SEGMENT_CLEANUP`. Admins must deactivate all segments before switching a TimeEntry to a non-Present status.

**Day-bounds rule:**

For the Member (Action) shape, an `Open` tap requires the server-stamped `branchLocalNow ∈ [entryDate, entryDate + 1 day)` so the new segment's `ClockIn` satisfies §3.16a. A `Close` tap requires `branchLocalNow > openSegment.ClockIn` and `branchLocalNow - openSegment.ClockIn <= 24h`; this allows a valid overnight close on the prior-day row while still enforcing the segment span cap. For the Admin (Segments) shape, each supplied segment must satisfy the day-bounds invariant documented in §3.16a.

**Calculate rules (segment list):**

```
Input: segments, Status, entryDate, branchLocalNow
Constants: DailyTarget (from Setting), LunchDeduction rules (from Setting)

If Status ≠ Present:
    Segments must be empty (enforced by TIMEENTRY_NON_PRESENT_REJECTS_SEGMENTS).
    TotalHours and BalanceHours use the non-Present rules below.

If Status = Present:
    grossHours = sum of closed segment durations
                 + live contribution of any open segment (if date == branchLocalToday)
    # Closed segment duration: (segment.ClockOut − segment.ClockIn).TotalHours
    # Live contribution: (branchLocalNow − openSegment.ClockIn).TotalHours
    # DateTime subtraction is exact; no midnight wrap-around needed.

    total_gap = sum of gaps between consecutive segment pairs where the preceding segment
                is closed, including the gap from the last closed segment to an open trailing
                segment if present.
    # gap between seg[i] and seg[i+1] = seg[i+1].ClockIn − seg[i].ClockOut (when seg[i].ClockOut != null)
    # A trailing open segment (the last one, ClockOut = null) counts as
    # "following a closed segment" only if there is a closed segment before it.

    effective_lunch = max(0, lunch_tier(grossHours) − total_gap)
    # lunch_tier: grossHours > 6 → LunchDeductionOver6H (strictly > 6)
    #             grossHours > 4 → LunchDeductionOver4H (strictly > 4, ≤ 6)
    #             otherwise → 0
    # Boundary: exactly 4 h gross → 0; exactly 6 h gross → LunchDeductionOver4H.

    TotalHours  = grossHours − effective_lunch
    BalanceHours = TotalHours − DailyTarget

If Status ∈ {Sunday, Holiday, Vacation, JustifiedAbsence}:  (abonado)
    TotalHours = DailyTarget
    BalanceHours = 0

If Status ∈ {DayOff, UnjustifiedAbsence}:  (hours owed)
    TotalHours = 0
    BalanceHours = -DailyTarget
```

**Live-running (open segment on current day):**

When any active segment has `ClockOut = null` and `entryDate.Date == branchLocalNow.Date`, the entry is "in progress". The contribution of that open segment is `(branchLocalNow − segment.ClockIn).TotalHours`. Persisted `TotalHours`/`BalanceHours` are a last-write checkpoint; read endpoints recompute on every call so the API is the live source of truth.

When a segment has `ClockOut = null` and `entryDate.Date < branchLocalNow.Date` (forgotten clock-out), the open segment contributes 0 h (needs manual review). The entry is still marked `IsInProgress = true` in the response.

**Overnight vs forgotten-close disambiguation:**

A prior-day open segment is ambiguous in isolation: it can mean the worker is still on shift (overnight) or that they forgot to clock out. The Member tap protocol resolves this by the next submitted `Action` and `Date`; the server never reinterprets the submitted date or scans other dates to infer intent.

- **Overnight recorded correctly:** the worker taps `Action: Close` with `Date = <prior day>`. The server closes the open segment on that TimeEntry and stamps `ClockOut = branchLocalNow`. The segment span rule (`ClockOut - ClockIn <= 24h`) enforces the cap. See Example 4 and Example 8.
- **Forgotten close:** the worker taps `Action: Open` with `Date = <today>`. The server creates or updates today's TimeEntry with a fresh open segment. The prior-day open is untouched, contributes 0 h while it remains open, and stays visible as `IsInProgress = true` for manual review. See Example 6.

The mobile client routes `Close` to the prior-day row when an active prior-day open exists and the user is ending that overnight shift. Admin recovery for misclassified cases uses the Admin `Segments` snapshot in `PUT /timeentry` or the granular segment endpoints.

**Worked examples:**

```
Example 1 — 10-min gap (no lunch top-up needed):
  seg[0]: 08:00 → 12:00  (4 h gross)
  seg[1]: 12:10 → 17:00  (4h50 gross)
  grossHours = 8h50 ≈ 8.833 h  →  tier = LunchDeductionOver6H (1 h)
  total_gap = 10 min = 0.167 h
  effective_lunch = max(0, 1.00 − 0.167) = 0.833 h
  TotalHours = 8.833 − 0.833 = 8.00 h

Example 2 — 30-min gap:
  seg[0]: 08:00 → 12:00  (4 h)
  seg[1]: 12:30 → 17:00  (4h30)
  grossHours = 8.50 h  →  tier = LunchDeductionOver6H (1 h)
  total_gap = 0.50 h
  effective_lunch = max(0, 1.00 − 0.50) = 0.50 h
  TotalHours = 8.50 − 0.50 = 8.00 h

Example 3 — 1-h gap exactly meeting tier (no deduction):
  seg[0]: 08:00 → 12:00  (4 h)
  seg[1]: 13:00 → 17:00  (4 h)
  grossHours = 8.00 h  →  tier = LunchDeductionOver6H (1 h)
  total_gap = 1.00 h
  effective_lunch = max(0, 1.00 − 1.00) = 0
  TotalHours = 8.00 h

Example 4 — overnight single segment:
  seg[0]: 2026-05-12T22:00 → 2026-05-13T06:00  (8 h)
  grossHours = 8 h  →  tier = LunchDeductionOver6H (1 h)
  total_gap = 0
  effective_lunch = max(0, 1.00 − 0) = 1.00 h
  TotalHours = 7.00 h
  # DateTime subtraction requires no wrap-around; overnight is unambiguous.

Example 5 — multi-segment overnight:
  seg[0]: 2026-05-12T22:00 → 2026-05-13T02:00  (4 h)
  seg[1]: 2026-05-13T02:30 → 2026-05-13T06:00  (3h30)
  grossHours = 7.50 h  →  tier = LunchDeductionOver6H (1 h)
  total_gap = 30 min = 0.50 h
  effective_lunch = max(0, 1.00 − 0.50) = 0.50 h
  TotalHours = 7.50 − 0.50 = 7.00 h

Example 6 — forgotten open segment with closed sibling:
  seg[0]: 08:00 → 12:00  (closed, 4 h)
  seg[1]: 13:00 → null   (open, forgotten on a prior day)
  Live contribution of open segment on a prior day = 0
  total_gap = gap between seg[0] and seg[1] = 1 h (seg[1] has a preceding closed segment)
  grossHours = 4 h  →  tier = 0 (≤ 4 h)
  effective_lunch = 0
  TotalHours = 4 h  (needs manual review; entry marked IsInProgress = true)

Example 7 — live-running with gap before open segment:
  Current branchLocalNow = 14:00
  seg[0]: 08:00 → 12:00  (closed, 4 h)
  seg[1]: 13:00 → null   (open, same day)
  live contribution = 14:00 − 13:00 = 1 h
  grossHours = 4 + 1 = 5 h  →  tier = LunchDeductionOver4H (0.25 h)
  total_gap = 1 h (gap before the open segment)
  effective_lunch = max(0, 0.25 − 1.00) = 0
  TotalHours = 5 h

Example 8 — overnight via Member tap routing:
  Mon 22:00: Member sends Action = Open, Date = Monday
    → Monday TimeEntry gets seg[0].ClockIn = Mon 22:00, ClockOut = null

  Tue 08:00: Member sends Action = Close, Date = Monday
    → Server closes Monday seg[0].ClockOut = Tue 08:00
    → Duration = 10 h, allowed because span ≤ 24 h

  Tue 08:01: Member sends Action = Open, Date = Tuesday
    → Server creates Tuesday TimeEntry with a fresh open segment
    → Monday remains a completed overnight entry; Tuesday is a separate current-day entry
```

### 6.8 Credit card due date calculation

Business day aware: skips weekends and branch holidays.

```
baseDueDate = TransactionDate + 2 business days
// Business day = not Saturday, not Sunday, not in branch holiday set
// DueDateCalculator.Compute(TwoBusinessDays, date, null, branchHolidayDates)
```

Debit cards use the same logic with +1 business day instead of +2.

The branch holiday set is supplied at call time via `IBranchHolidaySource.GetHolidayDatesAsync`. Existing M3 transaction callers pass an empty set (no holiday skipping) until Phase 5 wires the real source.

### 6.9 Branch consistency

All entities referenced by a Transaction must belong to the same Branch. This is a service-layer validation that runs on every Transaction create/update:

- `Transaction.BranchId` must equal `Account.BranchId`
- `Transaction.BranchId` must equal `RecordedByOperator.BranchId`
- `Transaction.BranchId` must equal `Client.BranchId` (when ClientId is present)
- `Transaction.BranchId` must equal `TransactionType.Category.BranchId` (follow the chain: TransactionType → Category → BranchId)

The same principle applies to DailyClose: `DailyClose.BranchId` must equal `Account.BranchId` and `SubmittedByOperator.BranchId`.

This prevents cross-branch data leakage in a multi-tenant system. In EF Core, implement as a shared validation method called by all relevant use cases, not as a DB trigger (keeps the logic testable and explicit).

### 6.10 Member transaction read scope

Members read transactions through the same scope used by `MemberAccountScopeGuard`: a Member sees a transaction if and only if the transaction's `AccountId` is in the Member's set of active linked operator accounts. Three response shapes follow from this contract:

- **GET `/transaction/{id}`:**
  - Member without a linked operator → 403 with `TRANSACTION_MEMBER_REQUIRES_OPERATOR_LINK`.
  - Member linked to an operator but the transaction's account is not in their linked-account set → 403 with `TRANSACTION_MEMBER_ACCOUNT_OUT_OF_SCOPE`.
  - Missing or cross-branch transaction id → 404 with `TRANSACTION_NOT_FOUND` regardless of role. 404 is reserved for "not in your branch" so attackers cannot probe transaction existence outside their branch via the 403/404 split.

- **GET `/transaction` (list):**
  - A Member sees **every** row on a linked account, including rows recorded by another operator on the same account. List visibility is account-scoped, not recording-operator-scoped, so a shared terminal account exposes both operators' rows to both members linked to it.
  - A Member-supplied `OperatorId` filter is ignored server-side because Member list scope is linked-account based. To filter to the authenticated operator's own rows, clients use `Mine = true`.
  - `Mine = true` is a convenience filter: the server resolves the caller's linked operator and sets the repository `OperatorId` filter to that operator. It never expands or changes `AllowedAccountIds`, and combining `Mine = true` with an explicit `OperatorId` returns 400.
  - When a Member has no linked operator OR has a linked operator with zero active `OperatorAccount` rows AND does not supply an explicit `AccountId`, the use case short-circuits to `Items = []`, `TotalCount = 0` without calling the repository's list/count methods.
  - When a Member supplies an explicit `AccountId` outside their linked set, the same empty short-circuit applies.

The list response carries paging metadata (`TotalPages`, `HasNext`, `HasPrevious`) and joined names (`AccountName`, `ClientName`, `TransactionTypeName`) on each item so operational screens don't N+1 the API.

### 6.11 Transaction mutation contract

Transaction update is intentionally narrow. The client sends the editable subset only, and the server preserves the financial identity of the row. Draft finalization is a pure state transition: the client sends no request body, and the server promotes a `Draft` transaction to `Active`. Cancellation is a terminal state transition: the client sends a required `CancellationReason`, and the server moves a `Draft` or `Active` row to `Cancelled` with an explicit cancellation audit trail. All three operations share the same member account scope, mutation permission matrix, lock-date behavior, and generic update audit convention.

**Editable fields:**

- `Description`
- `DueDate`
- `PaidAt`
- `ClientId`
- `TransactionTime`

**Non-editable fields after creation:**

- `Date`
- `Value`
- `AccountId`
- `TransactionTypeId`
- `CategoryId`
- `Direction`
- `RecordedByOperatorId`
- `CreatedByUserId`
- `BranchId`
- `OriginTransactionId`
- `CreatedAt`
- `Status` through the update endpoint
- cancellation fields (`CancelledAt`, `CancelledByUserId`, `CancellationReason`)

`UpdatedAt` and `UpdatedByUserId` are server-stamped on every successful update. `UpdatedAt` uses the current UTC instant; `UpdatedByUserId` is the authenticated branch user's `UserId`.

The same audit convention applies to successful finalization: `POST /transaction/{transactionId}/finalize` sets `Status = Active`, stamps `UpdatedAt` with the current UTC instant, stamps `UpdatedByUserId` with the authenticated branch user's `UserId`, and returns `ResponseTransactionJson`.

The same audit convention also applies to successful cancellation: `POST /transaction/{transactionId}/cancel` accepts a required `CancellationReason` (max length 500), sets `Status = Cancelled`, and stamps the cancellation audit fields (`CancelledAt`, `CancelledByUserId`, `CancellationReason`) plus the generic update audit fields (`UpdatedAt`, `UpdatedByUserId`) from the same clock instant. The branch clock is captured once per request so the same-day permission check, the cancellation audit timestamp, and the generic update timestamp cannot drift apart under concurrent clock ticks. The endpoint returns `ResponseTransactionJson`.

**Entity-relative validation:**

- `DueDate` must be on or after the transaction `Date`.
- `PaidAt`, when present, must be on or after the transaction `Date`.
- A transaction type that requires fiado context must keep a valid `ClientId`.
- A replacement `ClientId`, when present, must resolve to an active client in the authenticated branch.

**Permission matrix for update, finalize, and cancel:**

- `Admin` and `Manager` can update branch-visible `Draft` or `Active` transactions, finalize branch-visible `Draft` transactions, and cancel branch-visible `Draft` or `Active` transactions, subject to validation where applicable and lock date.
- `Member` must have a linked active operator.
- `Member` must have an active `OperatorAccount` link to the transaction's account. A linked Member with zero active account links is denied by account scope before mutation-specific rules.
- `Member` must be the transaction's `RecordedByOperator`.
- `Member` may update, finalize, or cancel only on the same local business day as the transaction `Date`. MVP local business day uses `America/Sao_Paulo`; branch-level time zones can replace this later.

**Failure contract:**

- Missing or cross-branch transaction id → 404 `TRANSACTION_NOT_FOUND`.
- Update called for an already-cancelled transaction → 409 `TRANSACTION_CANNOT_UPDATE_CANCELLED`.
- Finalize called for any non-`Draft` transaction, including already `Active` or `Cancelled` → 409 `TRANSACTION_CANNOT_FINALIZE_NON_DRAFT`.
- Cancel called for an already-cancelled transaction → 409 `TRANSACTION_ALREADY_CANCELLED`.
- Cancel called with a missing `CancellationReason` → 400 `TRANSACTION_CANCELLATION_REASON_EMPTY`.
- Cancel called with a `CancellationReason` longer than 500 characters → 400 `TRANSACTION_CANCELLATION_REASON_MAX_LENGTH`.
- Transaction date at or before branch `LockDate` → 409 `TRANSACTION_DATE_LOCKED`.
- Member with no linked operator → 403 `TRANSACTION_MEMBER_REQUIRES_OPERATOR_LINK`.
- Linked Member without account scope → 403 `TRANSACTION_MEMBER_ACCOUNT_OUT_OF_SCOPE`.
- Linked Member who is not the recording operator → 403 `TRANSACTION_MEMBER_NOT_RECORDING_OPERATOR`.
- Linked recording Member outside the local business day → 403 `TRANSACTION_UPDATE_REQUIRES_SAME_DAY`.

**Installment sibling isolation:**

Cancelling an installment row never touches its siblings. Each installment is loaded by its own id and only that single row is mutated; rows that share an `OriginTransactionId` retain their `Status`, their generic update audit fields (`UpdatedAt`, `UpdatedByUserId`), and their cancellation audit fields. Reactivating the cancelled installment is intentionally not part of this contract — cancellation is terminal. Cancelled installment rows are also excluded from the active-sum query (`SumActiveValueByAccountAndDate`) used by Milestone 4 cash-variance calculations because the query filters on `Status == Active`.

**Why per-row.** A cheque-installment plan is N independent post-dated cheques (§6.3), not one atomic obligation. Cheques bounce or get renegotiated individually, and siblings may already have settled (`PaidAt` set) and been counted in a closed `DailyClose` snapshot. When the manager wants to cancel the whole plan (for example, the client returns the next day and pays the outstanding balance in cash), the frontend is responsible for offering a "cancel siblings too?" confirmation when the targeted row has active siblings and, if confirmed, issuing one `POST /transaction/{transactionId}/cancel` per sibling. The backend never cascades automatically. Each sibling cancel runs the full permission/lock/scope chain on its own and carries its own `CancellationReason`, so a sibling that legitimately cannot be cancelled (for example, already past a `LockDate` or in a different branch-local business day for a Member) is rejected without affecting the others.

The read/list rules in §6.10 and the mutation rules here intentionally share the same linked-account scope but expose different response shapes: `GET /transaction/{id}` uses 403/404, list uses empty result sets for empty or out-of-scope account filters, and update/finalize/cancel use 403 because they are attempted mutations.

### 6.12 CashVariance calculation

CashVariance (Diferença de Caixa) is **system-calculated, not operator-entered**. The operator enters closing product values (Dinheiro, Telesena, etc.); the system computes the variance and persists it as a DailyCloseItem.

```
CashVariance = TotalClosing - TotalOpening - TotalTransactions

Where:
  TotalClosing  = sum of today's DailyCloseItems (excluding CashVariance itself)
  TotalOpening  = sum of the most recent prior DailyCloseItems for the same account
                  where Date < today, excluding CashVariance
  TotalTransactions = SumActive(Direction.In) - SumActive(Direction.Out)
                      for today's Active transactions for the same account
```

The calculator reads `Direction.In` and `Direction.Out` with two explicit calls to `SumActiveValueByAccountAndDateAsNoTracking(branchId, accountId, date, direction)`, then subtracts `In - Out`. It never relies on an unfiltered transaction sum for CashVariance.

The CashVariance DailyCloseItem is written by the system when the operator submits the daily close, and updated in place if the close is rejected and resubmitted or recalled and submitted again. The operator cannot directly type a variance value.

### 6.13 DailyClose contract

**Workflow states.** The state machine is `Draft -> Submitted -> Approved | Rejected`, with two automatic edit-time transitions: `Rejected -> Draft` for resubmission after manager feedback, and `Submitted -> Draft` for same-day soft-final recall. `Approved` is terminal.

**Role x state x local-day matrix.**

| Operation  | Draft                                                               | Submitted                                                                                       | Approved        | Rejected                                       |
|------------|---------------------------------------------------------------------|-------------------------------------------------------------------------------------------------|-----------------|------------------------------------------------|
| Open       | Member with account in scope, Manager, Admin                        | n/a                                                                                             | n/a             | n/a                                            |
| Edit items | Member who recorded it on the same branch-local day, Manager, Admin | Recall allowed for recording-operator Member on the same branch-local day, or any Manager/Admin | Not editable    | Same rules as Draft; auto-transitions to Draft |
| Submit     | Member who recorded it on the same branch-local day, Manager, Admin | Not submittable                                                                                 | Not submittable | Same rules as Draft                            |
| Approve    | n/a                                                                 | Manager/Admin only                                                                              | Not approvable  | Not approvable                                 |
| Reject     | n/a                                                                 | Manager/Admin only                                                                              | Not rejectable  | Not rejectable                                 |

All local-day decisions use `IBranchClock.IsSameLocalDay` / `LocalBusinessDate`, never `DateTime.UtcNow.Date`.

**Account scope.** Get uses `404` for missing/cross-branch ids, `403 TRANSACTION_MEMBER_REQUIRES_OPERATOR_LINK` for Members without a linked operator, and `403 TRANSACTION_MEMBER_ACCOUNT_OUT_OF_SCOPE` when a linked Member targets an account outside `AllowedAccountIds`. List uses the same server-resolved `AllowedAccountIds`; empty scope or explicit out-of-scope account filters return an empty page without leaking row existence. Writes use `MemberAccountScopeGuard` and surface the same two `403` keys.

**Open ordering.** `POST /dailyclose` runs in this order: request validation -> account branch lookup -> `MemberAccountScopeGuard` -> `DailyCloseWorkflowGuard.EnsureCanOpen` -> `LockDateGuard` -> repository add/commit. `EnsureCanOpen` owns only the role/linked-operator part of the Open matrix: Manager/Admin are allowed even without a linked Operator; Member must have a linked Operator. Account membership belongs to `MemberAccountScopeGuard`, and duplicate active closes for `(BranchId, AccountId, Date)` are enforced by the filtered unique constraint plus PostgreSQL `23505` translation.

**Audit stamping.** Every workflow mutation stamps `UpdatedAt` and `UpdatedByUserId`. Submit, approve, reject, `Rejected -> Draft`, and `Submitted -> Draft` recall capture one `branchClock.UtcNow()` instant and use that same value for the workflow timestamp (`SubmittedAt` or `ApprovedAt`, where applicable) and `UpdatedAt`.

**Lock-date behavior.** `LockDateGuard` applies to every transition listed in §6.6. DailyClose callers pass `DAILYCLOSE_LOCK_DATE_VIOLATION`.

**Sibling-account isolation.** Submit computes and persists CashVariance for exactly one `(BranchId, AccountId, Date)` close. It reads transactions and prior close rows for that account only; sibling accounts never contribute to the variance.

**System-only product.** The `"Diferença Caixa"` product is resolved by display name and is owned by Submit. It is never accepted in client `PUT /items` payloads (`DAILYCLOSE_ITEM_PRODUCT_FORBIDDEN`), never deleted on rejection or recall, and is updated in place on resubmission or submit-after-recall.

---

### 6.14 Reporting Surface

**Read-only contract.** All Milestone 7 reporting endpoints are read-only. None call `Add`, none mutate persisted state, none open a unit of work. The three preview endpoints (`POST /transaction/installment/preview`, `POST /transaction/{id}/edit-preview`, `POST /transaction/preview`) are compute-only and never persist; the "preview cannot commit" invariant is enforced by construction (no `IUnitOfWork` dependency) and pinned by WebApi.Test reload / row-count assertions that verify persisted state is unchanged after a 200 response.

**Permission buckets.**

| Bucket                                   | Applies to                                                                                                                                  | Behavior                                                                                                                                                                       |
|------------------------------------------|---------------------------------------------------------------------------------------------------------------------------------------------|--------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| Manager/Admin only                       | Daily ledger, fiado balance, fiado aging, open-cheque aging, cash-variance summary, monthly reconciliation, transaction edit-impact preview | `[TokenAuthorize(Role.Manager, Role.Admin)]` per-action; Members receive 401/403                                                                                               |
| Any branch role with operator-self scope | Operator transaction summary, time-entry balance summary                                                                                    | `[TokenAuthenticateBranch]` per-action; Member with no linked operator → empty summary short-circuit; Member targeting another operator → 403 `REPORT_MEMBER_NOT_OWN_OPERATOR` |
| Same as write twin                       | Installment plan preview, create-impact preview                                                                                             | Member with linked operator + account scope, Manager/Admin elevated; mirrors the matching `POST /transaction/installment` / `POST /transaction` write permission rules         |

**Aging buckets.** Aging is computed server-side by `ReportAgingBucketizer.BucketFor(dueDate, asOfDate)` (static pure function, no DI registration):

| `daysOutstanding = (asOfDate.Date − dueDate.Date).Days` | Bucket       |
|---------------------------------------------------------|--------------|
| `dueDate > asOfDate` (future-due)                       | `Current`    |
| 0 – 30 (inclusive)                                      | `Days0To30`  |
| 31 – 60 (inclusive)                                     | `Days31To60` |
| 61 – 90 (inclusive)                                     | `Days61To90` |
| > 90                                                    | `Days91Plus` |

Boundary examples: day 30 → `Days0To30`; day 31 → `Days31To60`; day 90 → `Days61To90`; day 91 → `Days91Plus`.

**Date-range guardrails.** Every paginated report that accepts a `[DateFrom, DateTo]` window enforces:
- `DateFrom` and `DateTo` are valid (non-default) dates.
- `DateFrom <= DateTo` (inverted range → 400 `REPORT_DATE_RANGE_INVERTED`).
- `(DateTo − DateFrom).TotalDays <= 366` (too-wide span → 400 `REPORT_DATE_RANGE_TOO_WIDE`).

`AsOfDate` parameters are optional; when omitted the use case resolves branch-local today via `IBranchClock.LocalBusinessDate(IBranchClock.UtcNow())`.

**Financial total filters.** All financial aggregates filter on `Status = Active AND Active = true` (entity-base soft-delete). `Draft` and `Cancelled` rows are excluded from sums, balances, and totals. This applies to daily ledger balances, fiado balances, aging rows, cash-variance inputs, and monthly reconciliation totals.

**Aggregation.** Aggregate queries (`SumActiveByX`, `ListOpenReceivables`, `ListVarianceTimeSeries`) live in repositories. Use cases never materialize whole tables into memory and aggregate in C#.

**Fiado aging report.** `GET /report/fiado/aging` — Manager/Admin. Returns a paginated list of individual unpaid Tab-account receivable rows with per-row aging metadata.

*Endpoint:* `GET /report/fiado/aging?clientId?&accountId?&asOfDate?&page&pageSize`

*Filter semantics:*
- `clientId` (optional) — narrows to rows for a specific client.
- `accountId` (optional) — narrows to rows on a specific Tab account.
- `asOfDate` (optional) — defaults to branch-local today via `IBranchClock.LocalBusinessDate(UtcNow())` when omitted.
- Only rows with `Status = Active AND Active = true AND PaidAt IS NULL` and `Account.Type = Tab` are returned.
- `DueDate > asOfDate` rows are included (they belong to the `Current` bucket).

*Per-row contract (`ResponseFiadoAgingItemJson`):*

| Field             | Description                                                                    |
|-------------------|--------------------------------------------------------------------------------|
| `TransactionId`   | Transaction `Id`                                                               |
| `Date`            | Transaction event date                                                         |
| `DueDate`         | Payment due date                                                               |
| `Value`           | Transaction value                                                              |
| `DaysOutstanding` | `max(0, (asOfDate.Date − dueDate.Date).Days)` — zero for future-due rows       |
| `Bucket`          | `AgingBucket` assigned by `ReportAgingBucketizer.BucketFor(dueDate, asOfDate)` |
| `ClientId`        | Optional client id                                                             |
| `ClientName`      | Optional client name (joined)                                                  |
| `AccountId`       | Tab account id                                                                 |
| `AccountName`     | Tab account name (joined)                                                      |
| `Description`     | Optional transaction description                                               |

*Envelope (`ResponseFiadoAgingJson`):* `Items`, `TotalCount`, `TotalPages`, `HasNext`, `HasPrevious`, `AsOfDate`.

*Bucket assignment:* `DaysOutstanding` is computed as `max(0, (asOfDate.Date − dueDate.Date).Days)`. The bucket is then assigned by `ReportAgingBucketizer.BucketFor(dueDate, asOfDate)` per the table in §6.14: future-due → `Current`; 0–30 → `Days0To30`; 31–60 → `Days31To60`; 61–90 → `Days61To90`; > 90 → `Days91Plus`. Boundary: day 30 → `Days0To30`, day 31 → `Days31To60`, day 90 → `Days61To90`, day 91 → `Days91Plus`.

*Ordering:* `DueDate ASC, Date ASC, Id ASC` (deterministic, index-backed via `(BranchId, DueDate) WHERE PaidAt IS NULL`).

**Open-cheque aging report.** `GET /report/cheques/open-aging` — Manager/Admin. Returns a paginated list of cheque installment plans grouped by origin transaction, showing only plans that have at least one unpaid installment row.

*Endpoint:* `GET /report/cheques/open-aging?accountId?&clientId?&asOfDate?&page&pageSize`

*Precondition:* Only rows with `OriginTransactionId IS NOT NULL AND Status = Active AND Active = true` participate in the grouping. A group is included in results only when it has at least one row with `PaidAt IS NULL` (SQL HAVING equivalent).

*Filter semantics:*
- `accountId` (optional) — narrows to groups whose installment rows belong to a specific account.
- `clientId` (optional) — narrows to groups whose installment rows belong to a specific client.
- `asOfDate` (optional) — defaults to branch-local today via `IBranchClock.LocalBusinessDate(UtcNow())` when omitted. Used for `DaysOutstanding` and bucket computation only; it does not filter which rows are "open" (that is determined by `PaidAt IS NULL`).

*Per-group contract (`ResponseOpenChequeAgingGroupJson`):*

| Field               | Description                                                                    |
|---------------------|--------------------------------------------------------------------------------|
| `OriginTransactionId` | Shared `OriginTransactionId` for all sibling installment rows               |
| `OutstandingTotal`  | Sum of `Value` for unpaid (`PaidAt IS NULL`) sibling rows                      |
| `OldestOpenDueDate` | Earliest `DueDate` among unpaid sibling rows                                   |
| `OldestOpenBucket`  | `AgingBucket` for `OldestOpenDueDate` computed via `ReportAgingBucketizer`     |
| `OpenRowCount`      | Count of unpaid sibling rows                                                   |
| `TotalRowCount`     | Count of all active sibling rows (paid + unpaid)                               |
| `AccountId`         | Account id (from origin row)                                                   |
| `AccountName`       | Account name (joined from origin row)                                          |
| `ClientId`          | Optional client id (from origin row)                                           |
| `ClientName`        | Optional client name (joined from origin row)                                  |
| `Description`       | Optional description (from origin row)                                         |
| `Rows`              | Unpaid sibling installment rows (see per-row contract below)                   |

*Per-row contract (`ResponseOpenChequeAgingRowJson`):*

| Field             | Description                                                                    |
|-------------------|--------------------------------------------------------------------------------|
| `TransactionId`   | Installment row `Id`                                                           |
| `DueDate`         | Installment payment due date                                                   |
| `Value`           | Installment value                                                              |
| `DaysOutstanding` | `max(0, (asOfDate.Date − dueDate.Date).Days)` — zero for future-due rows       |
| `Bucket`          | `AgingBucket` assigned by `ReportAgingBucketizer.BucketFor(dueDate, asOfDate)` |

*Sibling inclusion:* Within each group the `Rows` list contains only unpaid sibling installment rows (those with `PaidAt IS NULL`), loaded by `OriginTransactionId`. The `TotalRowCount` covers all active siblings regardless of payment status.

*Envelope (`ResponseOpenChequeAgingJson`):* `Items`, `TotalCount`, `TotalPages`, `HasNext`, `HasPrevious`, `AsOfDate`.

*Ordering:* `OldestOpenDueDate ASC, OriginTransactionId ASC` (deterministic, oldest open installment plan first).

**Edit impact preview.** `POST /transaction/{id}/edit-preview` — Manager/Admin. The safety net for sensitive edits: it answers "if I save this change, what moves?" by computing receivable / fiado-balance / cash-variance deltas without persisting anything. Monthly lock-readiness is intentionally out of scope here — it belongs to the Milestone 10 monthly-reconciliation report.

*Endpoint:* `POST /transaction/{transactionId:guid}/edit-preview?asOfDate?`

*Payload:* reuses `RequestUpdateTransactionJson` from §6.11 **verbatim** — same editable subset (`Description`, `DueDate`, `PaidAt`, `ClientId`, `TransactionTime`), no parallel preview DTO, no field made optional, no "at least one change" rule. `asOfDate?` is supplied on the query string (`?asOfDate=YYYY-MM-DD`), not in the body; when omitted it defaults to branch-local today via `IBranchClock.LocalBusinessDate(UtcNow())`.

*Validation parity:* the use case mirrors `PUT /transaction/{id}` step-for-step — Manager/Admin role check → `UpdateTransactionFluentValidation` → no-tracking snapshot load (including `TransactionType` and `Account`) → 404 `TRANSACTION_NOT_FOUND` on miss/cross-branch → 409 `TRANSACTION_CANNOT_UPDATE_CANCELLED` → shared mutation permission guard (caller operator is always null; preview is Manager/Admin-only) → `LockDateGuard` (409 `TRANSACTION_DATE_LOCKED`) → 409 `TRANSACTION_FIADO_REQUIRES_CLIENT` when the type requires a Tab client and none is supplied → 404 `CLIENT_NOT_FOUND` for a cross-branch new client → the relative validator (400 `TRANSACTION_DUE_DATE_BEFORE_DATE` / `TRANSACTION_PAID_AT_BEFORE_DATE`) — but **minus** `IUnitOfWork` and the mutation/Commit step. The loaded snapshot is never mutated.

*Response (`ResponseEditTransactionPreviewJson`):* `{ TransactionId, Impact, Warnings }`. `Warnings` is currently always empty. `Impact` (`ResponseTransactionImpactJson` — the neutral envelope shared with the create-impact preview) always carries all three sections; each is "empty" when not relevant:

| Section              | DTO                                                                                                                                                                                   | Contents                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                   |
|----------------------|---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| `ReceivableImpact`   | `ResponseReceivableImpactJson { AgingBucket? BucketBefore, AgingBucket? BucketAfter, bool RowAppearsInOpenReceivables, bool RowDisappearsFromOpenReceivables }`                       | Both `PaidAt` null → `BucketBefore`/`BucketAfter` from the current/hypothetical `DueDate` via `ReportAgingBucketizer`, flags false. Unpaid → paid → `BucketBefore` set, `BucketAfter` null, `RowDisappearsFromOpenReceivables = true`. Paid → unpaid → `BucketBefore` null, `BucketAfter` set, `RowAppearsInOpenReceivables = true`. Both paid → all null/false.                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                           |
| `FiadoBalanceImpact` | `ResponseFiadoBalanceImpactJson { Deltas: ResponseClientBalanceDeltaJson { ClientId, ClientName, OutstandingDelta }[] }`                                                              | Deltas only when `AccountType = Tab` **and** the client changes. `signedValue = Direction.Out ? +Value : −Value` (§6.4); the old client gets `−signedValue`, the new client `+signedValue`. A null side contributes no delta. Otherwise `Deltas = []`.                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                     |
| `CashVarianceImpact` | `ResponseCashVarianceImpactJson { Guid? AccountId, DateTime? Date, DailyCloseStatus? DailyCloseStatus, decimal? CurrentVariance, decimal? ProjectedVariance, decimal VarianceDelta }` | `AccountId`/`Date`/`DailyCloseStatus` are populated whenever a daily close exists for `(branch, account, date)` (all null when no close exists). `CurrentVariance` is the day's real §6.12 variance, **live-recomputed via `ICashVarianceCalculator`** whenever the operator has committed a complete count set — populated for **any non-`Draft` close** (`Submitted`/`Approved`/`Rejected` all retain their last-submitted counts), and null for a `Draft` close (counts still being entered) or an absent close. The `DailyCloseStatus` field tells the manager which kind of number it is — pending (`Submitted`), signed-off (`Approved`), or repudiated (`Rejected`) — so they can judge the consequence of editing under it (the edit itself is gated by the lock date, not the close status). `VarianceDelta` is always the computed net-flow delta (`beforeNetFlow − afterNetFlow`, `NetFlow = In ? +Value : −Value`); it is 0 under today's edit contract — §6.12 is blind to every editable field and `Value`/`Direction`/`Date`/`AccountId` are immutable — but it is computed, never hardcoded. `ProjectedVariance = CurrentVariance + VarianceDelta`, set only when `CurrentVariance` is non-null. **Single-close boundary:** exactly one close is modeled because `AccountId`/`Date` are immutable; if they ever become editable the section must become a list of cash-variance impacts (old + new close). |

*Preview-never-commits invariant:* the use case takes no `IUnitOfWork` dependency, so it cannot persist by construction. This is pinned two ways — the Phase 12 architecture test scans the constructor for any `IUnitOfWork` parameter, and the WebApi tests reload the row after a 200 response and assert it is byte-for-byte unchanged. A determinism test additionally commits the same payload through `PUT /transaction/{id}` and compares every impact against the resulting state: `ReceivableImpact.BucketAfter` must match the row's actual `Bucket` in `GET /report/fiado/aging`; `FiadoBalanceImpact.Deltas` must match before/after client totals in `GET /report/fiado/balance`; `CashVarianceImpact.ProjectedVariance` must match a real post-write `ICashVarianceCalculator` recompute for the touched close.

**Create impact preview.** `POST /transaction/preview` — branch-authenticated, **same scope as the `POST /transaction` write twin** (not Manager/Admin-only). It answers "if I record this new transaction, what moves?" by computing the receivable / fiado-balance / cash-variance impact of a would-be row without persisting anything. This is the genuinely non-zero counterpart to the edit-impact preview: an *update* cannot change `Value`/`Direction`/`Date`/`Account`, so its cash-variance delta is structurally zero, whereas a *create* sets all of those fresh and therefore produces a real forecast — the reason `VarianceDelta`/`ProjectedVariance` live on the shared `ResponseCashVarianceImpactJson`.

*Endpoint:* `POST /transaction/preview?asOfDate?`

*Permission:* `[TokenAuthenticateBranch]`, **not** Manager/Admin-only. The use case runs through `TransactionCreatePreamble`, so Members inherit the same linked-operator + account-scope enforcement as the real create flow — preview/write parity: anyone who can create the row can preview that same create. A Member targeting an out-of-scope account gets 403 `TRANSACTION_MEMBER_ACCOUNT_OUT_OF_SCOPE`; a Member with no linked operator gets the create flow's 400 `TRANSACTION_MEMBER_REQUIRES_OPERATOR_LINK` (the `RecordedByOperator` resolver runs before the account-scope guard, so this is an `OnValidationException`, not a 403). This is the §6.14 "same scope as the write twin" rule that installment preview already follows; the edit-impact preview remains the deliberate Manager/Admin exception because it previews an edit to an existing persisted row loaded by id, not a scope-limited create. For Member callers every impact query uses only the preamble-resolved `(account, client, date)` — never a branch-wide balance, branch-wide receivable, or all-account variance summary.

*Payload:* reuses `RequestCreateTransactionJson` from §6.1 **verbatim** — same fields, no parallel preview DTO. `asOfDate?` is supplied on the query string (`?asOfDate=YYYY-MM-DD`), not in the body; when omitted it defaults to branch-local today via `IBranchClock.LocalBusinessDate(UtcNow())`.

*Validation parity:* the use case mirrors `POST /transaction` step-for-step — `CreateTransactionFluentValidation` → `TransactionCreatePreamble.Resolve` (type/category/account/client resolution, Member linked-operator + account-scope checks, cross-branch 404s for type/account/client, `TRANSACTION_REQUIRES_TAB_ACCOUNT_AND_CLIENT` 409, `TRANSACTION_DATE_LOCKED` 409, due-date computation) — but **minus** `IUnitOfWork`/`ITransactionsRepository` and the `Add`/`Commit` step. Nothing is created.

*Response (`ResponseCreateTransactionPreviewJson`):* `{ Impact, Warnings }` — **no `TransactionId`** (nothing is created). `Warnings` is currently always empty. `Impact` is the shared `ResponseTransactionImpactJson` (same envelope as the edit-impact preview). The would-be row is always unpaid (`RequestCreateTransactionJson` has no `PaidAt`) and its `Status` is `Draft` when `SaveAsDraft`, else `Active`. A **`Draft`** would-be row is invisible to every Active-filtered surface (§6.4 fiado sums, open receivables, §6.12 cash variance), so all three sections short-circuit to empty/zero. For an **`Active`** row:

| Section              | Create rule                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                               |
|----------------------|---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| `ReceivableImpact`   | A Tab-account row **appears** in open receivables: `RowAppearsInOpenReceivables = true`, `BucketBefore = null`, `BucketAfter = ReportAgingBucketizer.BucketFor(DueDate, asOfDate)` (`DueDate` is the preamble's computed due date — the exact value the write twin persists). Empty for a non-Tab (e.g. terminal) row.                                                                                                                                                                                                                                                                                                                                                                                                                                                    |
| `FiadoBalanceImpact` | A Tab row with a client carries a single delta `+signedValue` onto that client, where `signedValue = Direction.Out ? +Value : −Value` (§6.4). Empty for a non-Tab row or a Tab row with no client.                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                        |
| `CashVarianceImpact` | `VarianceDelta = −NetFlow(Direction, Value)` where `NetFlow = In ? +Value : −Value` — the new row adds to its `(account, Date)` `In − Out`, so the variance moves by the negative of its net flow. **Genuinely non-zero** (contrast the edit preview's structural zero). `AccountId`/`Date`/`DailyCloseStatus` are populated whenever a daily close exists for the create's `(branch, account, Date)`. `CurrentVariance` is live-recomputed via `ICashVarianceCalculator` for any non-`Draft` close (`Submitted`/`Approved`/`Rejected`), null for a `Draft` or absent close; `ProjectedVariance = CurrentVariance + VarianceDelta`, set only when `CurrentVariance` is non-null. Single-close boundary: exactly one close, since the create's `(account, Date)` is fixed. |

*Preview-never-commits invariant:* the use case injects neither `IUnitOfWork` nor `ITransactionsRepository`, so it cannot persist by construction (pinned at the architecture level by the Phase 12.3 scan, which covers `UseCases/Transactions/CreatePreview/`). The WebApi tests assert the branch transaction count is unchanged across the call, and a determinism test commits the same payload through `POST /transaction` and compares every impact against the resulting state: `ReceivableImpact.BucketAfter` must match the created row's actual `Bucket` in `GET /report/fiado/aging`; `FiadoBalanceImpact.Deltas` must match the selected client's total in `GET /report/fiado/balance`; `CashVarianceImpact.ProjectedVariance` must match a real post-create `ICashVarianceCalculator` recompute for the touched close.

---

## 7. Key Workflows

### 7.1 Operator daily flow (mobile app)

```
1. Login → system resolves Operator → finds primary Account (terminal)
2. "Open Day" → creates DailyClose with Status=Draft for today + account
3. Throughout the day: add transactions
   - Tap transaction type button
   - Fill required fields (value, time, client, due date — varies by type)
   - Save → Transaction with Status=Active
   - Or save as Draft → Transaction with Status=Draft (finalize later)
4. "Close Day" → enter closing snapshot values (DailyCloseItems)
   - System shows opening values (yesterday's closing for each product)
   - Operator enters today's closing values
   - System calculates/displays CashVariance
5. "Submit" → DailyClose Status=Submitted
6. Wait for manager approval
   - If Rejected: operator sees reason, edits, resubmits
   - If Approved: day is finalized
```

### 7.2 Manager daily flow (web dashboard)

```
1. Login → see yesterday's summary across all operators
   - CashVariance per operator (the key number)
   - Pending DailyClose submissions needing approval
   - Operators who haven't submitted yet (red alert)
2. Review each operator's submission
   - See DailyCloseItems (closing snapshot)
   - See transaction list for that day
   - Compare against expected values
   - Approve or Reject with reason
3. Receivables view
   - Outstanding Fiado by client (aging report)
   - Upcoming due dates (card settlements, pre-dated cheques)
4. Monthly report
   - CashVariance + Tarifa Bolão + Sobras Bolão + Hours by operator by day
   - Comparison with CEF borderô (manual for MVP)
5. Corrections
   - Cancel erroneous transactions (with reason)
   - Advance LockDate after month-end reconciliation
```

### 7.3 Fiado lifecycle

```
1. Client buys on credit:
   → Transaction: Type="Cliente" under "Saídas", Direction=Out,
     Account=Tab account, ClientId=client
   → Tab balance increases (customer owes more)

2. Client pays back:
   → Transaction: Type="Cliente" under "Entradas", Direction=In,
     Account=Tab account, ClientId=client
   → Tab balance decreases (customer owes less)

3. Manager checks outstanding tabs:
   → Query Tab account balance by client
   → Aging report: group unpaid transactions by age buckets
```

---

## 8. Access-to-LottoGest Mapping

### Tables

| Access Table     | LottoGest Entity             | Notes                                                                                                  |
|------------------|------------------------------|--------------------------------------------------------------------------------------------------------|
| TblUsuario       | User + BranchUser + Operator | Split into 3 concerns                                                                                  |
| TblContas        | Account                      | Added Tab type, self-referencing FK                                                                    |
| TblCategoria     | Category                     | Data table, not enum                                                                                   |
| TblTipoCategoria | TransactionType              | Child of Category                                                                                      |
| TblLancamentos   | Transaction                  | Added Status, CancelledAt, TransactionTime, OriginTransactionId, RecordedByOperatorId, CreatedByUserId |
| TblClientes      | Client                       | Absorbed TblTelefones                                                                                  |
| TblProduto       | Product                      | Only daily-close products, not workarounds                                                             |
| TblEstoque       | DailyClose + DailyCloseItem  | Split into session + items                                                                             |
| TblRegistroPonto | TimeEntry                    | Linked to Operator, not Account                                                                        |
| TblFeriados      | Holiday                      | Per-branch                                                                                             |
| TblConfiguracao  | Setting                      | Expanded with time-tracking config                                                                     |
| TblLogExclusao   | *(absorbed)*                 | Replaced by soft delete fields on Transaction                                                          |
| TblTelefones     | *(absorbed)*                 | Merged into Client                                                                                     |
| —                | Branch                       | NEW: multi-tenant                                                                                      |
| —                | BranchUser                   | NEW: role per branch                                                                                   |
| —                | Operator                     | NEW: employee concept                                                                                  |
| —                | OperatorAccount              | NEW: account assignment                                                                                |
| —                | RefreshToken                 | Existing: auth                                                                                         |

### Access columns → Transaction columns

| Access (TblLancamentos) | LottoGest (Transaction)       | Notes                                                                                                                             |
|-------------------------|-------------------------------|-----------------------------------------------------------------------------------------------------------------------------------|
| lco_id                  | Id                            | Auto-increment → Guid                                                                                                             |
| lco_data                | Date                          |                                                                                                                                   |
| lco_valor               | Value                         | Access decimal → Postgres numeric(14,2)                                                                                           |
| id_categoria            | CategoryId                    | Integer FK → Guid FK                                                                                                              |
| id_tipo                 | TransactionTypeId             | Integer FK → Guid FK                                                                                                              |
| lco_descricao           | Description + TransactionTime | Time values split out to dedicated field                                                                                          |
| id_cliente              | ClientId                      |                                                                                                                                   |
| lco_vencimento          | DueDate                       |                                                                                                                                   |
| lco_data_pagamento      | PaidAt                        |                                                                                                                                   |
| id_conta                | AccountId                     |                                                                                                                                   |
| lco_sinal               | Direction                     | "Positivo"/"Negativo" string → In/Out enum                                                                                        |
| lco_condicao            | *(removed)*                   | Overloaded in Access (payment condition + authorization flag). Not needed with proper TransactionType and Description             |
| lco_forma_pagamento     | *(removed)*                   | Redundant with TransactionType                                                                                                    |
| lco_origem              | OriginTransactionId           | Integer ID → Guid self-referencing FK. First installment references itself.                                                       |
| lco_status              | *(unused in Access)*          | Always empty in production data. LottoGest `Status` (Draft/Active/Cancelled) is new functionality, not a migration of this column |
| lco_dataRegistro        | CreatedAt                     | From EntityBase                                                                                                                   |
| *(none)*                | BranchId                      | NEW: multi-tenant                                                                                                                 |
| *(none)*                | RecordedByOperatorId          | NEW: which operator's context                                                                                                     |
| *(none)*                | CreatedByUserId               | NEW: who actually created the record                                                                                              |
| *(none)*                | CancelledAt                   | NEW: audit trail                                                                                                                  |
| *(none)*                | CancelledByUserId             | NEW: audit trail                                                                                                                  |
| *(none)*                | CancellationReason            | NEW: audit trail                                                                                                                  |
| *(none)*                | TransactionTime               | NEW: extracted from Description                                                                                                   |

### Entity count

|              | Access | LottoGest                                                           |
|--------------|--------|---------------------------------------------------------------------|
| Tables       | 13     | 18                                                                  |
| New entities | —      | Branch, BranchUser, Operator, OperatorAccount, DailyClose (session) |
| Absorbed     | —      | TblLogExclusao (→ Transaction soft delete), TblTelefones (→ Client) |
