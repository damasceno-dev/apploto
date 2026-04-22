# LottoGest — Backend Schema Specification

<!--
Sync group: loto-backend-docs
Canonical source: server/docs/loto-specs.md (this file is canonical; derived artifacts: server/docs/loto_presentation.html, server/docs/loto_entity_relationship_diagram.html)
Coverage: Full entity model, relationships, invariants, workflows, and Access-to-LottoGest mapping.
Spec revision: v8
-->

> **Status:** Revised spec (v8) — Operator user-link uniqueness documented
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

**Unique constraint:** `(BranchId, Name)`

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

**Unique constraint:** `(CategoryId, Name)`

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

**Unique constraint:** `(BranchId, Name)`

### 3.14 DailyClose

The daily register closing session. One row per account per day. Tracks the submission/approval workflow.

```csharp
public class DailyClose : EntityBase
{
    public DateTime Date { get; set; }
    public DailyCloseStatus Status { get; set; } = DailyCloseStatus.Draft;

    public Guid AccountId { get; set; }
    public Account Account { get; set; } = null!;

    public Guid? SubmittedByOperatorId { get; set; }
    public Operator? SubmittedByOperator { get; set; }

    public DateTime? SubmittedAt { get; set; }
    public DateTime? ApprovedAt { get; set; }

    public Guid? ApprovedByUserId { get; set; }
    public User? ApprovedByUser { get; set; }

    public string? RejectionReason { get; set; }
    public string? Notes { get; set; }

    public Guid BranchId { get; set; }
    public Branch Branch { get; set; } = null!;

    // Navigation
    public ICollection<DailyCloseItem> Items { get; set; } = [];
}
```

| Column | Type | Null | Notes |
|---|---|---|---|
| Id | uuid | PK | |
| Date | date | NOT NULL | The business day being closed |
| Status | smallint | NOT NULL | Enum: Draft=0, Submitted=1, Approved=2, Rejected=3 |
| AccountId | uuid | NOT NULL | FK → Account |
| SubmittedByOperatorId | uuid | NULL | FK → Operator |
| SubmittedAt | timestamptz | NULL | |
| ApprovedAt | timestamptz | NULL | |
| ApprovedByUserId | uuid | NULL | FK → User (manager/admin who approved) |
| RejectionReason | varchar(500) | NULL | |
| Notes | text | NULL | Operator notes about the day |
| BranchId | uuid | NOT NULL | FK → Branch |
| CreatedAt | timestamptz | NOT NULL | |
| Active | boolean | NOT NULL | |

**Unique constraint:** `(BranchId, AccountId, Date)` — one closing per account per day.

### 3.15 DailyCloseItem

Individual product values within a daily closing.

```csharp
public class DailyCloseItem : EntityBase
{
    public decimal Value { get; set; }

    public Guid DailyCloseId { get; set; }
    public DailyClose DailyClose { get; set; } = null!;

    public Guid ProductId { get; set; }
    public Product Product { get; set; } = null!;
}
```

| Column | Type | Null | Notes |
|---|---|---|---|
| Id | uuid | PK | |
| Value | numeric(14,2) | NOT NULL | The closing value for this product |
| DailyCloseId | uuid | NOT NULL | FK → DailyClose |
| ProductId | uuid | NOT NULL | FK → Product |
| CreatedAt | timestamptz | NOT NULL | |
| Active | boolean | NOT NULL | |

**Unique constraint:** `(DailyCloseId, ProductId)` — one value per product per closing session.

### 3.16 TimeEntry

Operator attendance tracking. Standalone — not mixed with the transaction flow.

```csharp
public class TimeEntry : EntityBase
{
    public DateTime Date { get; set; }
    public TimeOnly? ClockIn { get; set; }
    public TimeOnly? ClockOut { get; set; }
    public TimeEntryStatus Status { get; set; }
    public decimal TotalHours { get; set; }
    public decimal BalanceHours { get; set; }

    public Guid OperatorId { get; set; }
    public Operator Operator { get; set; } = null!;

    public Guid BranchId { get; set; }
    public Branch Branch { get; set; } = null!;
}
```

| Column | Type | Null | Notes |
|---|---|---|---|
| Id | uuid | PK | |
| Date | date | NOT NULL | |
| ClockIn | time | NULL | Null when absent |
| ClockOut | time | NULL | Null when absent |
| Status | smallint | NOT NULL | Enum (see section 4) |
| TotalHours | numeric(6,2) | NOT NULL | Net hours worked after lunch deduction |
| BalanceHours | numeric(6,2) | NOT NULL | TotalHours minus daily target. Positive = overtime, negative = owes time |
| OperatorId | uuid | NOT NULL | FK → Operator |
| BranchId | uuid | NOT NULL | FK → Branch |
| CreatedAt | timestamptz | NOT NULL | |
| Active | boolean | NOT NULL | |

**Unique constraint:** `(BranchId, OperatorId, Date)` — one entry per operator per day.

### 3.17 Holiday

Branch-specific holidays that affect business day calculations and time entries.

```csharp
public class Holiday : EntityBase
{
    public DateTime Date { get; set; }
    public string? Description { get; set; }

    public Guid BranchId { get; set; }
    public Branch Branch { get; set; } = null!;
}
```

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

| Column | Type | Null | Notes |
|---|---|---|---|
| Id | uuid | PK | |
| LockDate | date | NOT NULL | Transactions on or before this date cannot be edited. Access: `conf_dtfechamento` |
| DailyTargetHours | numeric(6,2) | NOT NULL | Default 7.33 (7h20m). Used in TimeEntry balance calculation |
| LunchDeductionOver6H | numeric(4,2) | NOT NULL | Default 1.00. Hours deducted for lunch when worked >6h |
| LunchDeductionOver4H | numeric(4,2) | NOT NULL | Default 0.25. Hours deducted for break when worked >4h but ≤6h |
| BranchId | uuid | NOT NULL | FK → Branch. UNIQUE — one setting row per branch |
| CreatedAt | timestamptz | NOT NULL | |
| Active | boolean | NOT NULL | |

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

| Value | DueDate behavior |
|---|---|
| SameDay | DueDate equals `Date` |
| NextCalendarDay | DueDate equals `Date.AddDays(1)` |
| NextBusinessDay | DueDate is the next Monday-Friday after `Date`; weekends are skipped, holidays are out of scope |
| TwoBusinessDays | DueDate is the second Monday-Friday after `Date`; weekends are skipped, holidays are out of scope |
| OperatorEnteredCheque | DueDate must be explicitly entered by the operator and must be on or after `Date` |

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

| Name | DefaultDirection | Access cat_id |
|---|---|---|
| Receita | In | 1 |
| Crédito Banco | In | 2 |
| Entradas | In | 3 |
| Despesas Administrativas | Out | 4 |
| Despesas Comerciais | Out | 5 |
| Despesas Pessoal | Out | 6 |
| Despesas Financeiras | Out | 7 |
| Débito Banco | Out | 8 |
| Saídas | Out | 9 |

### TransactionTypes

| Name | Parent Category | SettlementRule | RequiresTabAccountAndClient | Access tipo_id |
|---|---|---|---|---|
| Cliente | Saídas | SameDay | true | 2 |
| Depósito Dinheiro | Saídas | NextCalendarDay | false | 3 |
| Cartão de Crédito | Saídas | TwoBusinessDays | false | 4 |
| MarketPlace | Saídas | SameDay | false | 5 |
| Sobra de Bolão | Despesas Comerciais | SameDay | false | 6 |
| Sobra de Federal | Despesas Comerciais | SameDay | false | 9 |
| Depósito Cheque | Saídas | OperatorEnteredCheque | false | 15 |
| PIX | Saídas | SameDay | false | 16 |
| Cartão de Débito | Saídas | NextBusinessDay | false | 17 |
| Telesena | Saídas | SameDay | false | 18 |
| Troca de Telesena | Saídas | SameDay | false | 19 |
| Raspadinha | Saídas | SameDay | false | 20 |
| Encalhe Federal | Saídas | SameDay | false | 22 |
| Cliente | Entradas | SameDay | true | 23 |
| Pgto Prêmio | Saídas | SameDay | false | 24 |
| Desconto | Despesas Comerciais | SameDay | false | 25 |
| Volante rejeitado | Despesas Comerciais | SameDay | false | 26 |
| Tarifa cartão | Despesas Comerciais | SameDay | false | 27 |
| Outras Despesas | Despesas Comerciais | SameDay | false | 28 |

Note: "Cliente" appears twice — under Saídas (credit sale, money leaves) and Entradas (client payment, money enters). This is the Fiado in/out mechanism.

### Products (DailyClose items)

| Name | DisplayOrder | Access prod_id |
|---|---|---|
| Telesena | 1 | 2 |
| Raspadinha | 2 | 3 |
| Jogos | 3 | 4 |
| Loteria Especial | 4 | 5 |
| Dinheiro | 5 | 6 |
| Tarifa Bolão | 6 | 7 |
| Federal | 7 | 11 |
| Diferença Caixa | 8 | 13 |

Note: **Fiado is NOT a DailyClose product** — it is calculated at query time from Tab account transactions (sum of Out minus sum of In). The Access FrmCaixa form displays a calculated Fiado balance but does NOT persist it to TblEstoque. Access prod_id 1 (Fiado), 8 (Total Caixa), 9 (HorasTrabalhadas), 10 (Ausente), 12 (Operador) are NOT migrated — these were computed values or metadata shoehorned into the product/estoque pattern.

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
  - `NextBusinessDay`: next Monday-Friday after `Date`
  - `TwoBusinessDays`: second Monday-Friday after `Date` (future: skip holidays)
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

When a pre-dated cheque is recorded with installments:
- The system creates N separate Transaction rows, one per installment
- Each installment's `Value` = total / N
- Each installment's `DueDate` = base due date + (i-1) months
- Each installment's `Description` = "CH PRE (1/3)", "CH PRE (2/3)", etc.
- **All installments share the same `OriginTransactionId`, including the first** (which points to itself). This simplifies group lookups: `WHERE OriginTransactionId = @groupId` returns all members.

This matches the Access behavior where `lco_origem` on the first installment is set to its own `lco_id`.

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
5. If rejected, operator edits and resubmits

**Opening values** for the current day = closing values (DailyCloseItems) from the previous day for the same account. The system queries: `DailyClose WHERE AccountId=X AND Date = @today - 1`.

**CashVariance** (Diferença Caixa) is system-calculated and persisted as a DailyCloseItem with the "Diferença Caixa" product. See rule 6.10 for the calculation formula. The operator does not enter this value directly.

**Fiado balance** is NOT stored as a DailyCloseItem — it is always calculated at query time from Tab account transactions (see rule 6.4). The daily close form displays it for reference but does not persist it as a snapshot value.

### 6.6 Date locking

`Setting.LockDate` defines the cutoff. Transactions on or before this date cannot be created, edited, or cancelled. DailyClose entries on or before this date cannot be modified. The lock date is advanced by the manager after month-end reconciliation.

### 6.7 Time entry calculation

This logic belongs in a domain service (`TimeEntryCalculationService`), not in the entity:

```
Input: ClockIn, ClockOut, Status
Constants: DailyTarget (from Setting), LunchDeduction rules (from Setting)

If Status = Present:
    grossMinutes = ClockOut - ClockIn (handle midnight crossing)
    grossHours = grossMinutes / 60
    lunchDeduction = grossHours > 6 ? Setting.LunchDeductionOver6H
                   : grossHours > 4 ? Setting.LunchDeductionOver4H
                   : 0
    TotalHours = grossHours - lunchDeduction
    BalanceHours = TotalHours - DailyTarget

If Status ∈ {Sunday, Holiday, Vacation, JustifiedAbsence}:  (abonado)
    TotalHours = DailyTarget
    BalanceHours = 0

If Status ∈ {DayOff, UnjustifiedAbsence}:  (hours owed)
    TotalHours = 0
    BalanceHours = -DailyTarget
```

### 6.8 Credit card due date calculation

Business day aware: skip weekends, future enhancement to skip holidays.

```
baseDueDate = TransactionDate + 2 days
if baseDueDate is Saturday: add 2 days
if baseDueDate is Sunday: add 1 day
// Future: loop while baseDueDate is in Holiday table, add 1 day
```

Debit cards use the same logic with +1 day instead of +2.

### 6.9 Branch consistency

All entities referenced by a Transaction must belong to the same Branch. This is a service-layer validation that runs on every Transaction create/update:

- `Transaction.BranchId` must equal `Account.BranchId`
- `Transaction.BranchId` must equal `RecordedByOperator.BranchId`
- `Transaction.BranchId` must equal `Client.BranchId` (when ClientId is present)
- `Transaction.BranchId` must equal `TransactionType.Category.BranchId` (follow the chain: TransactionType → Category → BranchId)

The same principle applies to DailyClose: `DailyClose.BranchId` must equal `Account.BranchId` and `SubmittedByOperator.BranchId`.

This prevents cross-branch data leakage in a multi-tenant system. In EF Core, implement as a shared validation method called by all relevant use cases, not as a DB trigger (keeps the logic testable and explicit).

### 6.10 CashVariance calculation

CashVariance (Diferença de Caixa) is **system-calculated, not operator-entered**. The operator enters closing product values (Dinheiro, Telesena, etc.); the system computes the variance and persists it as a DailyCloseItem.

```
CashVariance = TotalClosing - TotalOpening - TotalTransactions

Where:
  TotalClosing  = sum of today's DailyCloseItems (excluding CashVariance itself)
  TotalOpening  = sum of yesterday's DailyCloseItems for the same account
                  (excluding CashVariance)
  TotalTransactions = sum of today's Active transactions for the same account
```

The CashVariance DailyCloseItem is written by the system when the operator submits the daily close, and updated if the close is rejected and resubmitted. The operator cannot directly type a variance value.

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

| Access Table | LottoGest Entity | Notes |
|---|---|---|
| TblUsuario | User + BranchUser + Operator | Split into 3 concerns |
| TblContas | Account | Added Tab type, self-referencing FK |
| TblCategoria | Category | Data table, not enum |
| TblTipoCategoria | TransactionType | Child of Category |
| TblLancamentos | Transaction | Added Status, CancelledAt, TransactionTime, OriginTransactionId, RecordedByOperatorId, CreatedByUserId |
| TblClientes | Client | Absorbed TblTelefones |
| TblProduto | Product | Only daily-close products, not workarounds |
| TblEstoque | DailyClose + DailyCloseItem | Split into session + items |
| TblRegistroPonto | TimeEntry | Linked to Operator, not Account |
| TblFeriados | Holiday | Per-branch |
| TblConfiguracao | Setting | Expanded with time-tracking config |
| TblLogExclusao | *(absorbed)* | Replaced by soft delete fields on Transaction |
| TblTelefones | *(absorbed)* | Merged into Client |
| — | Branch | NEW: multi-tenant |
| — | BranchUser | NEW: role per branch |
| — | Operator | NEW: employee concept |
| — | OperatorAccount | NEW: account assignment |
| — | RefreshToken | Existing: auth |

### Access columns → Transaction columns

| Access (TblLancamentos) | LottoGest (Transaction) | Notes |
|---|---|---|
| lco_id | Id | Auto-increment → Guid |
| lco_data | Date | |
| lco_valor | Value | Access decimal → Postgres numeric(14,2) |
| id_categoria | CategoryId | Integer FK → Guid FK |
| id_tipo | TransactionTypeId | Integer FK → Guid FK |
| lco_descricao | Description + TransactionTime | Time values split out to dedicated field |
| id_cliente | ClientId | |
| lco_vencimento | DueDate | |
| lco_data_pagamento | PaidAt | |
| id_conta | AccountId | |
| lco_sinal | Direction | "Positivo"/"Negativo" string → In/Out enum |
| lco_condicao | *(removed)* | Overloaded in Access (payment condition + authorization flag). Not needed with proper TransactionType and Description |
| lco_forma_pagamento | *(removed)* | Redundant with TransactionType |
| lco_origem | OriginTransactionId | Integer ID → Guid self-referencing FK. First installment references itself. |
| lco_status | *(unused in Access)* | Always empty in production data. LottoGest `Status` (Draft/Active/Cancelled) is new functionality, not a migration of this column |
| lco_dataRegistro | CreatedAt | From EntityBase |
| *(none)* | BranchId | NEW: multi-tenant |
| *(none)* | RecordedByOperatorId | NEW: which operator's context |
| *(none)* | CreatedByUserId | NEW: who actually created the record |
| *(none)* | CancelledAt | NEW: audit trail |
| *(none)* | CancelledByUserId | NEW: audit trail |
| *(none)* | CancellationReason | NEW: audit trail |
| *(none)* | TransactionTime | NEW: extracted from Description |

### Entity count

| | Access | LottoGest |
|---|---|---|
| Tables | 13 | 18 |
| New entities | — | Branch, BranchUser, Operator, OperatorAccount, DailyClose (session) |
| Absorbed | — | TblLogExclusao (→ Transaction soft delete), TblTelefones (→ Client) |
