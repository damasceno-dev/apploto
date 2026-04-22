# LottoGest — Backend Milestones

> **Status:** Active
> **Started:** 2026-04-06
> **Approach:** Use-case-driven development with test pyramid

### Standing rules

**Vertical-slice test discipline:** Every phase that introduces use cases or controller endpoints must land with `CommonTestUtilities`, `Validators.Test`, `UseCases.Test`, and `WebApi.Test` updates in the same phase. No slice is considered done until its tests are green. The project-wide architecture tests (DI registration + auth-intent) remain a standing gate — new use cases and controllers must pass them automatically.

---

## Milestone 0 — Ground Zero

**Goal:** Remove old tutorial/prototype artifacts, upgrade to .NET 10, align naming with AGENTS.md, and establish the prod/dev database configuration baseline before any feature work begins.

**Precondition:** The User + RefreshToken auth flow (register, login, renew-token) is sound and stays. Everything else from the Fluxo era goes.

---

### Phase 1 — Cleanup

Remove all Fluxo-era code that doesn't map to the spec.

- [x] **1.1** Delete Fluxo entities: `Fluxo.cs`, `FluxoConta.cs`, `FluxoClassificação.cs`, `FluxoDetalhamento.cs`
- [x] **1.2** Delete Fluxo enums: `ContaTipoEnum`, `ClassificaçãoTipoEnum`, `DetalhamentoTipoEnum`
- [x] **1.3** Delete Fluxo repository: `FluxoContasRepository.cs`, `IFluxoContasRepository.cs`
- [x] **1.4** Delete Fluxo use cases: `FluxoContaRegisterUseCase.cs`, `FluxoContaGetByIdUseCase.cs`, `FluxoContaGetAllUseCase.cs`, `FluxoContaRegisterFluentValidation.cs`
- [x] **1.5** Delete Fluxo controllers: `FluxoContaController.cs`, `FluxoController.cs`, `FluxoClassificaçãoController.cs`, `FluxoDetalhamentoController.cs`
- [x] **1.6** Delete Fluxo DTOs: `RequestFluxoContaJson.cs`, `RequestFluxoJson.cs`, `RequestFluxoClassificaçãoJson.cs`, `RequestFluxoDetalhamentoJson.cs`, `ResponseFluxoContaJson.cs`, `ResponseFluxoJson.cs`
- [x] **1.7** Delete `WeatherForecast.cs`
- [x] **1.8** Rename `LotoDbContext` → `ServerDbContext`; update `DbSet`s to only `Users` + `RefreshTokens`
- [x] **1.9** Rename `LotoException` → `ServerException`; update all references across Exceptions, API, and Application layers
- [x] **1.10** Move surviving enums to `server.Domain/Entities/Enums/` per AGENTS.md; at minimum relocate `Role` and `TokenErrorType` out of entity/model files and update references
- [x] **1.11** Normalize auth filter naming to match AGENTS.md: keep `TokenAuthenticateFilter` and `TokenAuthorizeFilter`, remove `MyTokenAuthenticateFilter` / `MyTokenAuthorizeFilter`, and rename wrapper attributes accordingly
- [x] **1.12** Remove deleted types from DI registrations (`AppDependencyInjection`, `InfraDependencyInjection`)
- [x] **1.13** Delete all 8 existing EF migrations (will be recreated fresh after upgrade)
- [x] **1.14** Verify the solution compiles with zero errors and zero Fluxo/Loto/legacy auth-wrapper references (excluding `docs/`)

### Phase 2 — .NET 10 Upgrade

Upgrade framework and packages to match AGENTS.md targets.

- [x] **2.1** Update `TargetFramework` from `net8.0` to `net10.0` in all 6 `.csproj` files
- [x] **2.2** Bump `Microsoft.EntityFrameworkCore` packages to 10.x in `server.Infrastructure`
- [x] **2.3** Bump `Npgsql.EntityFrameworkCore.PostgreSQL` to 10.x in `server.Infrastructure`
- [x] **2.4** Bump `System.IdentityModel.Tokens.Jwt` to latest stable in `server.Infrastructure`
- [x] **2.5** Bump `FluentValidation` to latest stable in `server.Application`
- [x] **2.6** Bump `BCrypt.Net-Next` to latest stable in `server.Application`
- [x] **2.7** Replace `Swashbuckle.AspNetCore` with built-in `AddOpenApi()` + `MapOpenApi()` in `server.API`; update `Program.cs` and `ApiDependencyInjection`; ensure bearer security scheme is included in generated document
- [x] **2.8** Remove `Microsoft.VisualStudio.Web.CodeGeneration.Design` from `server.API` (unused scaffolding package)
- [x] **2.9** Add `public partial class Program { }` at the end of `Program.cs` (required for `WebApi.Test` integration testing)
- [x] **2.10** Add `InternalsVisibleTo("WebApi.Test")` to `server.Infrastructure` so `ServerDbContext` is accessible to integration tests
- [x] **2.11** Update `launchSettings.json` to replace `"launchUrl": "swagger"` with the correct route for the chosen interactive API explorer
- [x] **2.12** Create fresh initial migration: `dotnet ef migrations add InitialCreate`
- [x] **2.13** Verify the solution builds and the OpenAPI document is generated and accessible at `/openapi/v1.json`

### Phase 3 — Database Environments and Config Baseline

Establish the Development/Staging/Production database and appsettings workflow before test scaffolding.

- [x] **3.1** Keep `appsettings.json` limited to shared non-secret defaults only
- [x] **3.2** Update `appsettings.Development.json` to use fixed local-only settings for `loto_dev_local`
- [x] **3.3** Reserve `Development` for local use, `Staging` for the published non-production environment, and `Production` for the live environment
- [x] **3.4** Treat `appsettings.Staging.json` and `appsettings.Production.json` as generated outside git and add ignore rules for them
- [x] **3.5** Add local PostgreSQL Docker Compose under `infra/` for `loto_dev_local`
- [x] **3.6** Add a checked-in `infra/.env.example` for the hosted `Staging` / `Production` secret contract
- [x] **3.7** Add a hosted config render script under `infra/scripts/` that reads a server-side `.env` and generates `appsettings.Staging.json` or `appsettings.Production.json` on the host
- [x] **3.8** Document the local workflow (`docker compose up`, `dotnet ef database update`, API startup) and the hosted rule (`.env` -> generated hosted appsettings on the host before restart)
- [x] **3.9** Validate startup fails fast when `ConnectionStrings:DefaultConnection`, `Token:SigningKey`, or `Token:ExpirationTimeInMinutes` is missing
- [x] **3.10** Verify local boot works with `Development` and `loto_dev_local`
- [x] **3.11** Verify the User auth endpoints (register, login, renew-token) respond correctly against the local Development database

### Phase 4 — Test Infrastructure

Scaffold the test projects per AGENTS.md test strategy (empty but wired).

- [x] **4.1** Create `tests/CommonTestUtilities` class library; add references to Application, Communication, Infrastructure; add Bogus + NSubstitute packages
- [x] **4.2** Create `tests/Validators.Test` xUnit project; add references to Application, CommonTestUtilities; add Shouldly + xUnit packages
- [x] **4.3** Create `tests/UseCases.Test` xUnit project; add references to Application, CommonTestUtilities; add Shouldly + xUnit packages
- [x] **4.4** Create `tests/WebApi.Test` xUnit project; add references to API, CommonTestUtilities; add Shouldly + Testcontainers.PostgreSql + Microsoft.AspNetCore.Mvc.Testing + xUnit packages
- [x] **4.5** Add all 4 test projects to `server.sln`
- [x] **4.6** Verify `dotnet build` succeeds for the full solution including test projects
- [x] **4.7** Verify `dotnet test` runs (even if zero tests exist yet)

### Done criteria

- `dotnet build` succeeds with no warnings related to old naming
- `grep -r "Fluxo\|LotoDb\|LotoException\|MyTokenAuthenticateFilter\|MyTokenAuthorizeFilter\|namespace Server\." --include="*.cs" .` returns zero hits (excluding `docs/` and migrations)
- All `.csproj` files target `net10.0`
- All namespaces use `server.*` (lowercase s)
- Surviving enums live in `server.Domain/Entities/Enums/`
- Auth filters are named `TokenAuthenticateFilter` / `TokenAuthorizeFilter`
- `Development`, `Staging`, and `Production` map to `loto_dev_local`, `loto_staging`, and `loto_prod`
- `appsettings.json` contains only shared non-secret defaults
- `appsettings.Development.json` targets `loto_dev_local`
- `server/server.API/appsettings.Staging.json` and `server/server.API/appsettings.Production.json` are ignored by git
- `infra/docker-compose.yml` exists for local PostgreSQL
- `infra/.env.example` and a hosted appsettings render script exist
- `Program.cs` ends with `public partial class Program { }`
- 4 test projects exist in `tests/` and are wired into the solution
- Auth endpoints functional against a local PostgreSQL instance
- Fresh migration exists and applies cleanly
- `dotnet test` passes (even with zero tests)

---

## Milestone 1 — Branch Multi-Tenancy Bootstrap

**Goal:** Introduce `Branch` as the tenant boundary, move authorization from global `User.Role` to branch-scoped `BranchUser.Role`, and establish the minimum bootstrap flows required before any operator/account/ledger work begins.

**Scope boundary:** This milestone includes only `Branch`, `BranchUser`, branch-scoped auth/session, membership management for already-registered users, and the default branch bootstrap seeds from `loto-specs.md` section 5. It also includes the minimum Domain + Infrastructure shell for `Category`, `TransactionType`, `Product`, and `Setting` required to perform that bootstrap seeding. CRUD/admin features for those entities remain out of scope. Invitations and email delivery are explicitly deferred.

**Precondition:** Milestone 0 remains the foundation. In practice, Phase 4 test-project scaffolding from Ground Zero should be completed before the first Milestone 1 tests are written.

**Contract note:** `RequestUserRegisterJson` loses `Role`; `register`, `login`, and `renew-token` remain global-auth endpoints; `CreateBranchSession` becomes the branch-context entry point and issues a separate branch-scoped token for tenant endpoints. Branch-only authorization is driven by branch-token claims plus `BranchUser.Role`, never `User.Role`.

---

### Phase 1 — Domain and Authorization Model

Replace the remaining global-role assumptions with the branch-scoped tenancy model described in the spec, and add only the Domain + Infrastructure shell needed to seed branch defaults. This phase does not introduce CRUD/admin features for `Category`, `TransactionType`, `Product`, or `Setting`.

- [x] **1.1** Add `Branch` entity to `server.Domain` with `Name`, optional `Cnpj`, optional `Address`, optional `Phone`, and navigation to `BranchUsers`
- [x] **1.2** Add `BranchUser` entity to `server.Domain` with `UserId`, `BranchId`, `Role`, and `Active`
- [x] **1.3** Remove `Role` from `User`; keep `User` as global authentication identity only
- [x] **1.4** Update `RequestUserRegisterJson`, `UserRegisterUseCase`, `UserRegisterFluentValidation`, and `UserRegisterMapper` so registration no longer accepts or persists `Role`
- [x] **1.5** Update `User` and `RefreshToken` navigations so the auth model still maps cleanly after the `Role` removal
- [x] **1.6** Add `Direction` enum to `server.Domain/Entities/Enums/`
- [x] **1.7** Add `Category` entity to `server.Domain` with `Name`, `DefaultDirection`, `BranchId`, and navigation to `TransactionTypes`
- [x] **1.8** Add `TransactionType` entity to `server.Domain` with `Name`, `CategoryId`, and navigation to `Category`
- [x] **1.9** Add `Product` entity to `server.Domain` with `Name`, `DisplayOrder`, and `BranchId`
- [x] **1.10** Add `Setting` entity to `server.Domain` with `LockDate`, `DailyTargetHours`, `LunchDeductionOver6H`, `LunchDeductionOver4H`, and `BranchId`
- [x] **1.11** Add the required repository interfaces for `Branch`, `BranchUser`, `Category`, `TransactionType`, `Product`, and `Setting` to `server.Domain/Interfaces/`, including the batch persistence support needed for bootstrap seeding
- [x] **1.12** Add the required EF Core mappings, `DbSet`s, foreign keys, and uniqueness constraints for `Branch`, `BranchUser`, `Category`, `TransactionType`, `Product`, and `Setting` in `server.Infrastructure`
- [x] **1.13** Enforce hard uniqueness on `(UserId, BranchId)` for `BranchUser` to match the spec
- [x] **1.14** Add the Milestone 1 migration/model snapshot covering `Branch`, `BranchUser`, `Category`, `TransactionType`, `Product`, and `Setting`
- [x] **1.15** Add a two-token auth model: preserve the existing global token for base auth and add a separate branch-scoped token/session model that carries `UserId`, `BranchId`, `BranchUserId`, and `Role`
- [x] **1.16** Update token validation/authentication services so they can distinguish global user auth from branch-scoped auth
- [x] **1.17** Update authorization filters/attributes so branch-protected endpoints require the branch-scoped token and authorize against `BranchUser.Role`, not `User.Role`
- [x] **1.18** Preserve the existing global auth flow for `register`, `login`, and `renew-token` alongside the new branch token model
- [x] **1.19** Add regression coverage to prove a valid global token cannot satisfy branch-only authorization

### Phase 2 — Branch Bootstrap Flow

Implement the tenant-creation and branch-session flows that unlock later milestones. For this milestone, the bootstrap seed expectations are fixed: exactly 9 `Category` rows, 19 `TransactionType` rows, 8 `Product` rows, and 1 `Setting` row.

- [x] **2.1** Add `CreateBranch` request/response DTOs to `server.Communication`
- [x] **2.2** Add `CreateBranch` validator covering required `Name` and optional-field length limits
- [x] **2.3** Implement `CreateBranchUseCase` to create the `Branch`, create the creator's `BranchUser` as `Admin`, insert all default seeds, and commit only if the entire bootstrap succeeds atomically
- [x] **2.4** Seed the new branch with exactly 9 default `Category` rows defined in `loto-specs.md` section 5
- [x] **2.5** Seed the new branch with exactly 19 default `TransactionType` rows defined in `loto-specs.md` section 5
- [x] **2.6** Seed the new branch with exactly 8 default `Product` rows defined in `loto-specs.md` section 5
- [x] **2.7** Seed the new branch with exactly 1 `Setting` row using the spec-defined defaults `DailyTargetHours = 7.33`, `LunchDeductionOver6H = 1.00`, and `LunchDeductionOver4H = 0.25`
- [x] **2.8** Add `ListMyBranches` request/response contract and use case to return the authenticated user's active branch memberships with branch summary + caller role
- [x] **2.9** Add `CreateBranchSession` request/response contract and use case so a user can select one of their branches and receive the separate branch-scoped token while preserving the current global token model
- [x] **2.10** Reject branch-session creation when the authenticated user is not an active member of the requested branch
- [x] **2.11** Add `GetCurrentBranchSummary` response contract and use case to resolve the current branch from the branch-scoped token
- [x] **2.12** Add the corresponding `BranchController` endpoints for create/list/session/current-branch summary

### Phase 3 — Branch Membership Management

Implement branch-member administration without introducing invitations yet. The Manager/Admin permission matrix below is milestone-defined behavior for this phase; it is not being promoted into the spec sync group by this milestone update.

- [x] **3.1** Limit onboarding in this milestone to users that already exist in `User`
- [x] **3.2** Add `ListBranchUsers` response contract and use case to return active members from the current branch only
- [x] **3.3** Add `AddBranchUser` request/response DTOs using an existing registered user identifier or email plus the target branch role
- [x] **3.4** Add `AddBranchUser` validator covering required user identifier/email, required role, valid role enum, and email format when email is used
- [x] **3.5** Implement `AddBranchUserUseCase` so `Admin` can add `Admin`/`Manager`/`Member`, while `Manager` can add only `Manager`/`Member`
- [x] **3.6** When no membership exists for `(UserId, BranchId)`, `AddBranchUser` inserts a new `BranchUser`; when a deactivated membership already exists for that pair, it reactivates the existing row and updates its role instead of inserting a duplicate
- [x] **3.7** Reject `AddBranchUser` when the target user does not exist or already has an active membership in the branch
- [x] **3.8** Add `UpdateBranchUserRole` request/response DTOs and validator
- [x] **3.9** Implement `UpdateBranchUserRoleUseCase` so `Admin` can manage any membership role, while `Manager` can manage only `Manager`/`Member`
- [x] **3.10** Reject any role update that would leave the branch without at least one active `Admin`
- [x] **3.11** Add `RemoveBranchUser` request/response contract
- [x] **3.12** Implement `RemoveBranchUserUseCase` as soft deactivation (`Active = false`) on the existing membership row, not hard delete
- [x] **3.13** Allow `Manager` to remove only `Manager`/`Member`; allow `Admin` to remove any non-last-admin membership
- [x] **3.14** Reject any removal that would leave the branch without at least one active `Admin`
- [x] **3.15** Add the corresponding branch-membership endpoints to `BranchController` or a dedicated `BranchUserController`

### Phase 4 — Tests for the Tenancy Slice

Write tests for validators, use cases, and API behavior as part of the milestone, not as a follow-up.

- [x] **4.1** Ensure the Milestone 0 test-project scaffold exists before adding Milestone 1 tests
- [x] **4.2** Add `Validators.Test` coverage for `CreateBranch`, `CreateBranchSession`, `AddBranchUser`, and `UpdateBranchUserRole`
  Note: `4.2` covers all current Milestone 1 `FluentValidation` classes.
- [x] **4.3** Add `UseCases.Test` coverage for `CreateBranchUseCase`
- [x] **4.4** In `CreateBranchUseCase` tests, assert branch creation, creator membership as `Admin`, exact default seeds from spec section 5 (`9` categories, `19` transaction types, `8` products, `1` setting row with `DailyTargetHours = 7.33`, `LunchDeductionOver6H = 1.00`, `LunchDeductionOver4H = 0.25`), and atomic rollback on any bootstrap failure
- [x] **4.5** Add `UseCases.Test` coverage for `ListMyBranchesUseCase`, `CreateBranchSessionUseCase`, and `GetCurrentBranchSummaryUseCase`, including successful branch-token issuance
- [x] **4.6** Add `UseCases.Test` coverage for `ListBranchUsersUseCase`, `AddBranchUserUseCase`, `UpdateBranchUserRoleUseCase`, and `RemoveBranchUserUseCase`
- [x] **4.7** Add use-case tests for permission rules: `Manager` may manage only `Manager`/`Member` memberships; `Admin` may manage any membership subject to the last-admin invariant from 4.9
- [x] **4.8** Add use-case tests for the business-layer reactivation behavior when re-adding a previously deactivated membership, and for rejecting any `AddBranchUser` that would create a duplicate active membership for the same `(UserId, BranchId)` pair; the hard database uniqueness constraint itself is verified by 4.13 against real PostgreSQL
- [x] **4.9** Add use-case tests for the "must retain one active Admin" invariant
- [x] **4.10** Add use-case tests proving branch isolation: no membership read/write may target another branch through a valid token
- [x] **4.11** Add global-auth regression tests proving `register`, `login`, and `renew-token` still work after `User.Role` removal
  Note: `4.3` through `4.11` collectively cover all current Milestone 1 use cases; `4.7` through `4.11` are behavior-focused assertions that cut across those use cases rather than separate use-case classes.
- [x] **4.12** Add `WebApi.Test` happy-path coverage for all Milestone 1 endpoints
  Note: `4.12` covers all Milestone 1 HTTP endpoints and is intentionally not a 1:1 duplicate of `UseCases.Test`.
- [x] **4.13** Add `WebApi.Test` coverage for `401` unauthenticated, `403` unauthorized by branch role, `404` missing entity in branch scope, and `409` membership conflicts / last-admin violations, including the hard PostgreSQL uniqueness enforcement for `BranchUser (UserId, BranchId)`
- [x] **4.14** Add `WebApi.Test` coverage proving a global token is rejected and a valid branch-scoped token is accepted by branch-only endpoints
- [x] **4.15** Add project-wide architecture tests verifying every concrete `*UseCase` in `server.Application` is explicitly registered in DI and every controller endpoint in `server.API` declares explicit auth intent via `TokenAuthenticate`, `TokenAuthenticateBranch`, or `TokenAuthorize`; anonymous-by-design endpoints live in a reviewed allow-list inside the test so new use cases and controllers added by later milestones are covered automatically

### Done criteria

- `User` no longer carries a `Role`, and `RequestUserRegisterJson` plus the register flow no longer accept or persist `Role`
- `Branch`, `BranchUser`, `Category`, `TransactionType`, `Product`, and `Setting` exist in Domain, Infrastructure, and the Milestone 1 migration/model snapshot
- `BranchUser` enforces hard uniqueness on `(UserId, BranchId)`
- Removing a branch membership sets `Active = false` on the existing row, and re-adding a previously removed member reactivates that same row and updates its role
- Branch auth uses the two-token model: `register`, `login`, and `renew-token` keep the existing global auth flow, and branch selection/session issues a separate branch-scoped token
- Branch-scoped tokens carry enough data to resolve `UserId`, `BranchId`, `BranchUserId`, and `Role`
- `CreateBranch` creates the branch, the creator membership as `Admin`, and exactly `9` default `Category` rows, `19` default `TransactionType` rows, `8` default `Product` rows, and `1` `Setting` row with `DailyTargetHours = 7.33`, `LunchDeductionOver6H = 1.00`, and `LunchDeductionOver4H = 0.25`
- Membership management is limited to already-registered users; no invitation or email flow exists yet
- The branch always retains at least one active `Admin`
- Milestone-defined permission rules are enforced: `Manager` can manage only `Manager` and `Member` memberships; `Admin` can manage any membership subject to the last-admin invariant
- Validator, use-case, Web API, and architecture tests exist for the Milestone 1 flows, permissions, and auth wiring
- Global tokens are rejected by branch-only endpoints, and valid branch-scoped tokens are accepted by them

---

## Milestone 2 — Operator, Account & Client Foundation

**Goal:** Add the operational entities that the ledger depends on — `Operator`, `Account`, `OperatorAccount`, and `Client` — plus the authenticated operator runtime context needed by the next milestone.

**Scope boundary:** Full branch-scoped CRUD for `Operator`, `Account`, `OperatorAccount`, and `Client`, with explicit permissions and feature-complete tests delivered slice-by-slice. No `Transaction`, `DailyClose`, `TimeEntry`, `Holiday`, or admin CRUD for seeded lookup/config entities yet.

**Precondition:** Milestone 1 is fully closed, including Phase 4 tests and architecture guardrails.

---

### Phase 1 — Operator Slice

Add the `Operator` entity end-to-end: Domain, Infrastructure, migration, CRUD use cases, controller, and tests. This slice is self-contained and does not depend on `Account`, `OperatorAccount`, or `Client`.

- [x] **1.1** Add `Operator` entity to `server.Domain` with `Name`, `BranchId`, nullable `UserId`, and navigations to `Branch` and `User?`
  Note: the final `OperatorAccounts` navigation is introduced in Phase 2 together with `OperatorAccount`.
- [x] **1.2** Add `IOperatorsRepository` to `server.Domain/Interfaces/`
- [x] **1.3** Add EF configuration, `DbSet`, foreign keys, and `OperatorsRepository` implementation in `server.Infrastructure`
- [x] **1.4** Register the repository in Infrastructure DI
- [x] **1.5** Add the Phase 1 migration covering the `Operator` table
- [x] **1.6** Add `CreateOperator` request/response DTOs, validator, use case, and mapper
- [x] **1.7** Add `ListOperators` response DTO and use case (branch-scoped, active only)
- [x] **1.8** Add `GetOperator` response DTO and use case (branch-scoped, by id)
- [x] **1.9** Add `UpdateOperator` request/response DTOs, validator, use case, and mapping logic
- [x] **1.10** Add `DeactivateOperator` response DTO and use case as soft-delete (`Active = false`)
- [x] **1.11** Support nullable `UserId` link/unlink on create and update; when a `UserId` is provided, validate that the user exists and has an active `BranchUser` membership in the same branch
- [x] **1.12** Restrict all operator management endpoints to `Admin` and `Manager` via `TokenAuthorize`
- [x] **1.13** Add operator endpoints to a new `OperatorController`
- [x] **1.14** Register all new use cases in Application DI
- [x] **1.15** Add `CommonTestUtilities` builders for `Operator` entity and request DTOs
  Note: `OperatorBuilder`, `RequestCreateOperatorJsonBuilder`, `RequestUpdateOperatorJsonBuilder`, and `OperatorsRepositoryBuilder` added
- [x] **1.16** Add `Validators.Test` coverage for `CreateOperator` and `UpdateOperator`
- [x] **1.17** Add `UseCases.Test` coverage for the full operator slice: create, list, get, update, deactivate, and `UserId` link validation (including rejection when the user is not an active branch member)
- [x] **1.18** Add `WebApi.Test` happy-path and unhappy-path coverage for all operator endpoints, including permission checks and branch isolation

### Phase 2 — Account + OperatorAccount Slice

Add `AccountType` enum, `Account` and `OperatorAccount` entities end-to-end, their CRUD and assignment flows, the runtime self-context endpoint, and update `DeactivateOperator` to cascade. Tests land with the slice.

- [x] **2.1** Add `AccountType` enum (`Terminal`, `BankAccount`, `Tab`) to `server.Domain/Entities/Enums/`
- [x] **2.2** Add `Account` entity to `server.Domain` with `Type` (`AccountType`), `Name`, nullable `Institution`, nullable `Number`, `BranchId`, nullable `TabAccountId` (self-reference), and navigations to `Branch`, `TabAccount?`, and `OperatorAccounts`
- [x] **2.3** Add `OperatorAccount` entity to `server.Domain` with `OperatorId`, `AccountId`, `IsPrimary` (default `false`), and navigations to `Operator` and `Account`
- [x] **2.4** Add `IAccountsRepository` and `IOperatorAccountsRepository` to `server.Domain/Interfaces/`
- [x] **2.5** Add EF configurations, `DbSet`s, foreign keys, and repository implementations for `Account` and `OperatorAccount` in `server.Infrastructure`
- [x] **2.6** Enforce hard uniqueness on `(OperatorId, AccountId)` for `OperatorAccount`
- [x] **2.7** Enforce at most one active primary account per operator via unique filtered index: `UNIQUE (OperatorId) WHERE IsPrimary = true AND Active = true`
- [x] **2.8** Enforce that only `Terminal`-type accounts may have non-null `TabAccountId`, and that a `Tab` account can belong to at most one `Terminal` via unique filtered index on `TabAccountId WHERE TabAccountId IS NOT NULL`
- [x] **2.9** Register all new repositories in Infrastructure DI
- [x] **2.10** Add the Phase 2 migration covering `Account`, `OperatorAccount`, and their constraints
- [x] **2.11** Add explicit create operations for accounts: `CreateBankAccount`, `CreateTerminalAccount`, and `CreateTabAccount`, with request DTOs, validators, and mappers appropriate to each account type
- [x] **2.12** Add `ListAccounts` response DTO and use case (branch-scoped, active only), including derived reverse-pairing data for Tab accounts
- [x] **2.13** Add `GetAccount` response DTO and use case (branch-scoped, by id), including derived reverse-pairing data for Tab accounts
- [x] **2.14** Add `UpdateAccount` request/response DTOs, validator, and use case for descriptive fields only; keep `Account.Type` immutable after creation and manage Terminal↔Tab pairing through explicit pair/unpair operations
- [x] **2.15** Add `DeactivateAccount` response DTO and use case; deactivation cascades to all active `OperatorAccount` links for that account; cascade is one-way — reactivating an account later does NOT auto-restore previously deactivated links
  Note: use case, cascade logic, and DI registration added; controller endpoint deferred to next batch.
- [x] **2.16** Enforce account invariants in validators and use cases: only `Terminal` may set `TabAccountId`; a Terminal may exist without a Tab; referenced Tab/Terminal accounts must be active and same-branch; a `Tab` can belong to at most one active `Terminal`; pairing and unpairing of existing accounts are explicit operations
- [x] **2.17** Add `AssignAccount` use case and endpoint to create an `OperatorAccount` link; when a deactivated `(OperatorId, AccountId)` row already exists, reactivate it instead of inserting a duplicate
  Note: use case, validator, mapper, and DI registration added; controller endpoint deferred to next batch.
- [x] **2.18** Add `UnassignAccount` use case and endpoint to soft-deactivate an `OperatorAccount` link; if the unassigned account was the operator's primary, clear the `IsPrimary` flag
  Note: use case and DI registration added; controller endpoint deferred to next batch.
- [x] **2.19** Add `SetPrimaryAccount` use case and endpoint; enforce at most one active primary per operator by clearing the previous primary before setting the new one
  Note: use case and DI registration added; controller endpoint deferred to next batch.
- [x] **2.20** Add `ListOperatorAccounts` response DTO and use case
  Note: response DTOs (`ResponseOperatorAccountJson`, `ResponseListOperatorAccountsJson`), use case, mapper, and DI registration added; controller endpoint deferred to next batch.
- [x] **2.21** Update `DeactivateOperator` from Phase 1 to cascade soft-deactivation to all active `OperatorAccount` links for that operator; cascade is one-way — reactivating an operator later does NOT auto-restore previously deactivated links; reassignment is always explicit
  Note: `DeactivateOperatorUseCase` updated with cascade; existing tests updated and cascade + one-way tests added.
- [x] **2.22** Restrict all account and operator-account management endpoints to `Admin` and `Manager`
  Note: `AccountController` and the operator-account routes on `OperatorController` now use `TokenAuthorize(Role.Manager, Role.Admin)`; the self-context endpoint remains branch-authenticated for any branch role.
- [x] **2.23** Add a read-only self-context use case and endpoint that resolves the authenticated branch member to their linked `Operator` plus primary and available `Account` context; this endpoint is accessible to any branch role including `Member`
  Note: `GetOperatorSelfContextUseCase`, `ResponseSelfContextJson`, mapper, and DI registration added; controller endpoint deferred to next batch.
- [x] **2.24** Add account and operator-account endpoints to a new `AccountController` or extend existing controllers
  Note: added `AccountController` for account create/list/get/update/deactivate + pair/unpair routes, and extended `OperatorController` with operator-account assignment/list/primary routes plus `self-context`.
- [x] **2.25** Register all new use cases in Application DI
  Note: verified complete in `server.Application/AppDependencyInjection.cs`; the architecture test confirms the account/operator-account/self-context use cases are registered.
- [x] **2.26** Add `CommonTestUtilities` builders for `Account`, `OperatorAccount`, and related request DTOs
  Note: `AccountBuilder`, `RequestCreateBankAccountJsonBuilder`, `RequestCreateTerminalAccountJsonBuilder`, `RequestCreateTabAccountJsonBuilder`, `RequestUpdateAccountJsonBuilder`, and `AccountsRepositoryBuilder` added; `OperatorAccountBuilder`, `OperatorAccountsRepositoryBuilder`, and `RequestAssignAccountJsonBuilder` added in this batch.
- [x] **2.27** Add `Validators.Test` coverage for account create, update, and assign flows
  Note: create validators are covered per account-type use case (`CreateBankAccount`, `CreateTerminalAccount`, `CreateTabAccount`); `UpdateAccount` and `OperatorAccounts/AssignAccount` validators are also covered.
- [x] **2.28** Add `UseCases.Test` coverage for the full account slice: create, list, get, update, deactivate with cascade, `Type` immutability, Tab-pairing invariants, assign/unassign/reactivation, set-primary, self-context resolution, and updated `DeactivateOperator` cascade to `OperatorAccount`
  Note: `DeactivateAccount` cascade (5 tests), updated `DeactivateOperator` cascade (5 tests, 2 updated + 3 new), and `GetOperatorSelfContext` (4 tests) all added in this batch.
  Note: create, list, get, update, Type immutability, and all Tab-pairing invariants covered earlier; assign/reactivate, unassign (including primary-clear), set-primary (clear-previous + idempotent), and list-operator-accounts use-case tests added in this batch. Deactivate-with-cascade, self-context, and DeactivateOperator cascade deferred to next batch.
- [x] **2.29** Add `WebApi.Test` happy-path and unhappy-path coverage for all account and operator-account endpoints, including Tab constraint enforcement, deactivation cascade (both Account and Operator sides), and branch isolation
  Note: added focused HTTP coverage for the account and operator-account surfaces, including terminal-bound tab creation, explicit pair/unpair flows, permission checks, branch isolation, conflict/not-found paths, account-side deactivation cascade, self-context, and operator-side cascade verification.

### Phase 3 — Client Slice

Full CRUD for `Client` with its own entity, infrastructure, migration, and tests. The Client permission model below is milestone-defined behavior for this phase; it is not being promoted into the spec sync group by this milestone update.

- [x] **3.1** Add `Client` entity to `server.Domain` with `Name`, required `Phone`, nullable `Cpf`, nullable `Cep`, nullable `Address`, nullable `PhoneSecondary`, nullable `Notes`, nullable `Email`, `BranchId`, and navigation to `Branch`
- [x] **3.2** Add `IClientsRepository` to `server.Domain/Interfaces/`
- [x] **3.3** Add EF configuration, `DbSet`, foreign keys, and `ClientsRepository` implementation in `server.Infrastructure`
- [x] **3.4** Enforce `Client` CPF uniqueness per branch via unique filtered index: `UNIQUE (BranchId, Cpf) WHERE Cpf IS NOT NULL AND Active = true`
- [x] **3.5** Register the repository in Infrastructure DI
- [x] **3.6** Add the Phase 3 migration covering the `Client` table and its constraints
- [x] **3.7** Add `CreateClient` request/response DTOs, validator, use case, and mapper; require `Name` and `Phone`; validate CPF format and email format when present
- [x] **3.8** Add `ListClients` response DTO and use case (branch-scoped, active only)
- [x] **3.9** Add `GetClient` response DTO and use case (branch-scoped, by id)
- [x] **3.10** Add `UpdateClient` request/response DTOs, validator, use case, and mapper
- [x] **3.11** Add `DeactivateClient` response DTO and use case
- [x] **3.12** Allow `Member`, `Manager`, and `Admin` to create, read, and update clients; restrict client deactivation to `Admin` and `Manager`
  Note: `ClientController` uses `[TokenAuthenticateBranch]` on Create/List/Get/Update and `[TokenAuthorize(Role.Manager, Role.Admin)]` on Deactivate.
- [x] **3.13** Enforce CPF uniqueness per branch at the use-case level: reject create or update when another active `Client` in the same branch already has the same CPF
- [x] **3.14** Add client endpoints to a new `ClientController`
  Note: `POST /client`, `GET /client`, `GET /client/{clientId:guid}`, `PUT /client/{clientId:guid}`, `DELETE /client/{clientId:guid}` added.
- [x] **3.15** Register all new use cases in Application DI
  Note: `CreateClientUseCase`, `ListClientsUseCase`, `GetClientUseCase`, `UpdateClientUseCase`, and `DeactivateClientUseCase` all registered.
- [x] **3.16** Add `CommonTestUtilities` builders for `Client` entity and request DTOs
  Note: `ClientBuilder`, `RequestCreateClientJsonBuilder`, `RequestUpdateClientJsonBuilder`, and `ClientsRepositoryBuilder` added.
- [x] **3.17** Add `Validators.Test` coverage for `CreateClient` and `UpdateClient`
- [x] **3.18** Add `UseCases.Test` coverage for the full client slice: create, list, get, update, deactivate, CPF uniqueness, CPF/email format validation, and permission rules
  Note: create and update covered (CPF uniqueness, format validation, and validation failures included); list (4 tests: happy path, empty, branch isolation, active-only), get (3 tests: found, not-found, branch isolation), deactivate (5 tests: success, response data, empty-id, not-found, branch isolation) added in this batch. Permission-rule enforcement deferred to 3.12/3.14 (controller wiring).
- [x] **3.19** Add `WebApi.Test` happy-path and unhappy-path coverage for all client endpoints, including CPF uniqueness enforcement at the database level, permission differences between `Member` and `Admin`/`Manager`, and branch isolation
  Note: `ClientControllerHappyPathTest` (14 tests: create as Admin/Member/Manager, CPF normalization, null CPF, list, list-empty, list-as-member, get, get-as-member, update, update-as-member, update CPF normalization, deactivate as Admin, deactivate as Manager) and `ClientControllerUnhappyPathTest` (21 tests: 401 on all 5 endpoints, 403 Member-deactivate with active-state guard, 400 name/phone validation, 404 not-found/cross-branch on get/update/deactivate, 409 CPF conflict same-branch create, 409 formatted CPF conflict, 201 CPF same-CPF cross-branch, 409 CPF conflict update) added. `SeedClientAsync` added to `TestSeeder`.
  `ClientCpfUniquenessConstraintTest` (3 tests) added to prove the filtered `UNIQUE (BranchId, Cpf) WHERE Cpf IS NOT NULL AND Active = true` index is applied by the migration: duplicate CPF in same branch → `DbUpdateException` + SqlState 23505 + constraint name `IX_Clients_BranchId_Cpf`; same CPF in different branch → allowed; multiple null CPFs in same branch → allowed.
  `ApiExceptionHandler` updated with a `DbUpdateException` + Postgres 23505 translation layer: known unique-constraint names are mapped to domain error messages (currently `IX_Clients_BranchId_Cpf` → `CLIENT_CPF_CONFLICT`), so concurrent write races that bypass the application-layer pre-check return 409 instead of 500. All 151 WebApi.Test tests pass.

### Done criteria

- `Operator`, `Account`, `OperatorAccount`, and `Client` exist in Domain, Infrastructure, and their respective Milestone 2 migrations
- `OperatorAccount` enforces unique `(OperatorId, AccountId)` and at most one active primary account per operator
- `Account.Type` is immutable after creation; only `Terminal` may set `TabAccountId`; a Terminal may exist without a Tab; referenced Tab/Terminal accounts must be active and same-branch; a `Tab` can belong to at most one active `Terminal`; pairing and unpairing existing accounts are explicit operations
- `Client.Phone` is required; `Client.Cpf` is unique per branch when present per `loto-specs.md` v4 (filtered index on active rows)
- `Operator` can exist without a linked `User`, but linking validates that the `User` has an active `BranchUser` in the same branch
- Deactivating an `Operator` or `Account` cascades soft-deactivation to all active `OperatorAccount` links; cascade is one-way — reactivating the parent does NOT auto-restore child links; reassignment is always explicit
- `Admin` and `Manager` manage Operators, Accounts, and OperatorAccount assignments; Client permissions are milestone-defined: `Member` can create/read/update, only `Admin`/`Manager` can deactivate
- Authenticated branch members can resolve their own operator/account runtime context via the self-context endpoint
- The project-wide architecture tests (DI registration + auth-intent) pass with all Milestone 2 additions

---

## Milestone 3 — Transaction Ledger Core

- [x] **Follow-up hardening from previous milestones:** Strengthen older Account/Operator read-get-deactivate use-case tests to assert exact scoped repository arguments instead of relying on `Arg.Any(...)`-based mock setups, and bundle that work with broader feature-local FluentValidation extension cleanup so the test-helper and validation refactors land together.

**Goal:** Implement the `Transaction` entity and its full lifecycle — create (single + installment), get, list, update (whitelisted fields), finalize draft, and cancel — with branch-consistency validation, lock-date enforcement, member-terminal scope, structural due-date rules, and denormalized `CategoryId`/`Direction` invariants. Adds `SettlementRule` and `RequiresTabAccountAndClient` metadata to `TransactionType` so lifecycle rules key off structure, not names.

**Scope boundary:** Full transaction lifecycle: create (single + installment), read, list, update (whitelisted subset), finalize draft, cancel. No daily close, time entry, or reporting. Admin CRUD for `TransactionType` (including mutation of `SettlementRule` and `RequiresTabAccountAndClient`) is deferred to Milestone 6.

**Precondition:** Milestone 2 is fully closed. `Operator`, `Account`, `OperatorAccount`, `Client`, `Category`, `TransactionType`, `Product`, and `Setting` are branch-scoped and tested.

**Key behaviors:**

- `Transaction` entity carries the full field set from `loto-specs.md` section 3.12: `Date`, `Value`, `Description`, `TransactionTime`, `TransactionTypeId`, `CategoryId` (denormalized), `Direction` (denormalized), `AccountId`, `ClientId`, `DueDate`, `PaidAt`, `OriginTransactionId` (self-reference for installments), `RecordedByOperatorId`, `CreatedByUserId`, `Status` (`TransactionStatus`), `CancelledAt`, `CancelledByUserId`, `CancellationReason`, and `BranchId`
- `TransactionTypeId` is the sole classification input; `CategoryId` and `Direction` are denormalized at creation from `TransactionType.CategoryId` and `Category.DefaultDirection`, never independently editable
- Immutable-after-create fields use `init`: `TransactionTypeId`, `CategoryId`, `Direction`, `AccountId`, `RecordedByOperatorId`, `CreatedByUserId`, `OriginTransactionId`, `Date`, `Value`, `BranchId`, `Branch`. Everything touched by the update whitelist or by status transitions stays settable
- `Value` is always positive; `Direction` determines sign semantics
- `Transaction.RecordedByOperatorId` is always required in persistence (non-null column) and identifies which operator's terminal context owns the row. `CreatedByUserId` identifies who actually created the record (may differ from operator for manager corrections). On create, `RequestCreateTransactionJson.RecordedByOperatorId` is role-dependent: `Member` must not supply a non-null value — any non-null `RecordedByOperatorId` in the request is rejected with 400 (even when the value equals the caller's own operator), and the server always resolves the field server-side from the caller's linked operator (also 400 when the Member has no operator link); `Manager`/`Admin` may supply any active operator in the branch, default to the caller's linked operator when one exists, and fail with 400 when neither is available because the payload lacks the required operator context. A `Manager`/`Admin` without an `Operator` link is a valid structural state (e.g. newly onboarded admins) and must still be able to act on older-day rows via elevated role
- Caller operator resolution assumes the branch invariant that each user has at most one active linked `Operator` per branch. Multiple terminal/account access is represented through `OperatorAccount`, not through multiple active `Operator` rows for the same user. Repository APIs must make this invariant explicit and must not silently choose a caller operator with `FirstOrDefault`
- Draft transactions (`Status = Draft`) are excluded from all financial calculations; only Active transactions count
- `TransactionType` gains `SettlementRule` (enum: `SameDay`, `NextCalendarDay`, `NextBusinessDay`, `TwoBusinessDays`, `OperatorEnteredCheque`) and `RequiresTabAccountAndClient` (bool). `SettlementRule` drives the default `DueDate`; `RequiresTabAccountAndClient` enforces `Account.Type == Tab` AND `ClientId != null` at the branch-consistency layer
- `CreateBranch` seeds the new columns for every one of the 19 default `TransactionType` rows (e.g. `PIX` → `SameDay`, `Depósito Dinheiro` → `NextCalendarDay`, `Cartão de Débito` → `NextBusinessDay`, `Cartão de Crédito` → `TwoBusinessDays`, `Depósito Cheque` → `OperatorEnteredCheque`, both `Cliente` rows → `SameDay`), and `RequiresTabAccountAndClient = true` only for the two `Cliente` rows. Because the project is still pre-production, the Phase 1 migration only needs a simple one-time data update for existing development rows created before these columns existed, using the known seeded `(TransactionType.Name, Category.Name)` pairs. The durable behavior is proven by `CreateBranchUseCase` unit tests and `WebApi.Test` integration coverage that assert newly created branches persist the exact mapping
- `DueDate` defaults by `SettlementRule`: `SameDay` → `Date`; `NextCalendarDay` → `Date + 1 day`; `NextBusinessDay` → skip weekends, +1; `TwoBusinessDays` → skip weekends, +2; `OperatorEnteredCheque` → operator-entered and must satisfy `DueDate >= Date`. Holiday skipping is out of scope for this milestone
- Installment creation is gated on `SettlementRule == OperatorEnteredCheque`. N rows (2 ≤ N ≤ 24), with `Value = Math.Round(total / N, 2, MidpointRounding.AwayFromZero)` for rows 1..N-1 and the last row absorbing the rounding residual so `Sum(row.Value) == total` exactly. `DueDate` is staggered monthly from the caller-supplied base `DueDate`. All rows share the first row's `Id` as `OriginTransactionId` (including the first, which self-references)
- Fiado: both seeded `TransactionType` rows named "Cliente" (one under "Entradas", one under "Saídas") carry `RequiresTabAccountAndClient = true`. Category membership alone encodes In/Out, so there are no name-based rules
- **Update editable fields are restricted to** `Description`, `DueDate`, `PaidAt`, `ClientId`, `TransactionTime`. Anything else (Value, Date, TransactionTypeId, AccountId, RecordedByOperatorId) requires cancel + recreate. Update uses `PUT` with full replacement of this editable subset: the client sends all five fields on every call; `null` means clear where the field permits it (e.g. `PaidAt`) and is rejected where it doesn't (e.g. `ClientId` on a `RequiresTabAccountAndClient` row)
- Update and Cancel share the same permission matrix: same-day (`Date == today`) by caller's linked operator → any active branch role; older than today OR recorded by a different operator → `Manager`/`Admin`; on or before `Setting.LockDate` → blocked for everyone. The same-day own-operator path only applies when the caller has a linked `Operator`; callers without one (valid for `Manager`/`Admin`) fall through to the elevated-role path
- `Member` role can only act (create/read/update/finalize/cancel) on transactions whose `AccountId` is linked to the caller's operator via active `OperatorAccount`. For list queries, the server resolves the Member's active linked-account set and narrows the repository filter to that set (via an `AllowedAccountIds` filter on the domain-level list filter, never on the request DTO); an explicit `AccountId` filter outside the linked set returns an empty result rather than leaking existence. Read vs write asymmetry on out-of-scope rows is deliberate: for Get, a row whose `AccountId` is outside the Member's linked set returns `NotFound` (matching the cross-branch behavior) so existence is never leaked through reads; for write paths (Create/Update/Finalize/Cancel), `MemberAccountScopeGuard` throws `TokenWithoutPermissionException` (403) — the caller is actively attempting a denied operation and the refusal itself is the informative signal, so a 403 is the correct response rather than masking it as `NotFound`
- Finalize draft: `Draft → Active` only. Permission = caller's linked operator matching `RecordedByOperatorId`, or `Manager`/`Admin`. `LockDate` and member-scope checks apply
- Cancellation sets `Status = Cancelled`, `CancelledAt = now`, `CancelledByUserId = caller`, and requires non-empty `CancellationReason`. Cancelled rows stay in the database for audit but are excluded from financial calculations. Cancelling an installment row affects only that row; group-cancel is deferred
- Branch consistency enforced at service layer: `Transaction.BranchId` must match `Account.BranchId`, `RecordedByOperator.BranchId`, `Client.BranchId` (when present), and `TransactionType.Category.BranchId`
- Lock-date enforcement: any create/update/finalize/cancel targeting `Date <= Setting.LockDate` is blocked
- List ordering is fully deterministic: `Date DESC, TransactionTime DESC NULLS LAST, CreatedAt DESC, Id DESC`. Pagination is offset-based using `Page` and `PageSize`; no cursor pagination in this milestone
- Key indexes: `(BranchId, Date, AccountId)`, `(BranchId, AccountId, Direction, Date)`, `(BranchId, DueDate) WHERE PaidAt IS NULL`, `(OriginTransactionId) WHERE NOT NULL`, `(BranchId, Status)`, `(BranchId, RecordedByOperatorId, Date)`
- Spec sync (`loto-specs.md` sections 3.10/3.12/4/6.1, `loto_presentation.html`, `loto_entity_relationship_diagram.html`) lands with this milestone: `TransactionType` gains `SettlementRule` and `RequiresTabAccountAndClient`, sections 4/6.1 document the settlement enum values and fiado semantics, and `check-loto-doc-sync.sh` must pass before the milestone closes

---

### Phase 1 — Foundation: Domain, Infrastructure, Shared Services, Spec Sync

Ship domain entities, persistence, shared application services, seed-factory updates, and the spec sync that Milestone 3 requires before any user-facing slice. No HTTP endpoint lands in this phase; Phase 2 exercises the wiring end-to-end.

- [x] **1.1** Add `TransactionStatus` enum to `server.Domain/Entities/Enums/` with values `Draft = 0`, `Active = 1`, `Cancelled = 2`
- [x] **1.2** Add `SettlementRule` enum to `server.Domain/Entities/Enums/` with values `SameDay = 0`, `NextCalendarDay = 1`, `NextBusinessDay = 2`, `TwoBusinessDays = 3`, `OperatorEnteredCheque = 4`
- [x] **1.3** Extend `TransactionType` entity with settable `SettlementRule` and settable `RequiresTabAccountAndClient` (bool) — settable (not `init`) so Milestone 6 admin CRUD can adjust them later
- [x] **1.4** Add `Transaction` entity to `server.Domain` covering the full `loto-specs.md` section 3.12 field set, using `init` setters on `TransactionTypeId`, `CategoryId`, `Direction`, `AccountId`, `RecordedByOperatorId`, `CreatedByUserId`, `OriginTransactionId`, `Date`, `Value`, `BranchId`, and `Branch`; keep `Status`, `Description`, `DueDate`, `PaidAt`, `TransactionTime`, `ClientId`, the cancellation fields, and `Active` settable
- [x] **1.5** Add `TransactionListFilter` domain model to `server.Domain/Models/` with `AccountId?`, `DateFrom?`, `DateTo?`, `Status?`, `OperatorId?`, `ClientId?`, `AllowedAccountIds?` (server-resolved; never client-bound), `Page`, and `PageSize`
- [x] **1.6** Add `ITransactionsRepository` with `Add`, `AddRange`, `GetByIdAndBranchId` (tracked; loads Draft/Active/Cancelled), `GetByIdAndBranchIdAsNoTracking`, `ListByBranchIdAsNoTracking(Guid branchId, TransactionListFilter filter)`, `CountByBranchIdAsNoTracking(Guid branchId, TransactionListFilter filter)` (applies the same filter predicates as the list method, including `AllowedAccountIds`, so `TotalCount` in the paged response is consistent with the returned slice), `ListByOriginTransactionIdAndBranchIdAsNoTracking`, and `SumActiveValueByAccountAndDateAsNoTracking(Guid branchId, Guid accountId, DateTime date, Direction? direction = null)`
- [x] **1.7** Extend `ITransactionTypesRepository` with `GetActiveByIdAndBranchIdWithCategoryAsNoTracking(Guid id, Guid branchId)` — branch resolved via `Category.BranchId` because `TransactionType` has no direct `BranchId`
- [x] **1.8** Extend `ISettingsRepository` with `GetByBranchIdAsNoTracking(Guid branchId)` — single-row fetch for `LockDate`
- [x] **1.9** Add `TransactionConfiguration` with `numeric(14,2)` on `Value`, `loto-specs.md` section 3.12 column types, FKs using `DeleteBehavior.Restrict`, and the six key indexes from the Key behaviors block
- [x] **1.10** Extend `TransactionTypeConfiguration` with `SettlementRule` as `smallint NOT NULL`, `RequiresTabAccountAndClient` as `bool NOT NULL`, and a new `UNIQUE (CategoryId, Name)` index enforcing that no two `TransactionType` rows can share a `Name` within the same `Category`
- [x] **1.11** Add `DbSet<Transaction> Transactions` to `ServerDbContext`
- [x] **1.12** Implement `TransactionsRepository` with `AsNoTracking()` on read variants, the `Date DESC, TransactionTime DESC NULLS LAST, CreatedAt DESC, Id DESC` ordering, and `AllowedAccountIds` narrowing when present on the filter; factor the `TransactionListFilter → IQueryable<Transaction>` predicate pipeline into a single private helper that both `ListByBranchIdAsNoTracking` and `CountByBranchIdAsNoTracking` reuse, so the list slice and the `TotalCount` are guaranteed to apply exactly the same predicates
- [x] **1.13** Implement the new `TransactionTypesRepository` and `SettingsRepository` methods from 1.7 and 1.8
- [x] **1.14** Register `ITransactionsRepository → TransactionsRepository` in `InfraDependencyInjection`
- [x] **1.15** Extend `CreateBranchSeedFactory` so every seeded `TransactionType` row carries an explicit `SettlementRule` and `RequiresTabAccountAndClient` value: `Depósito Cheque → OperatorEnteredCheque`, `PIX → SameDay`, `Cartão de Débito → NextBusinessDay`, `Cartão de Crédito → TwoBusinessDays`, `Depósito Dinheiro → NextCalendarDay`, all other non-`Cliente` rows → `SameDay`; both `Cliente` rows → `SameDay` with `RequiresTabAccountAndClient = true`; all non-`Cliente` rows → `RequiresTabAccountAndClient = false`
- [x] **1.16** Add shared `TransactionBranchConsistencyService` in `server.Application/Services/Transactions/` that resolves `Account`, `RecordedByOperator`, optional `Client`, and `TransactionType` (with `Category`) through scoped repository reads (`GetActive*ByIdAndBranchId*`), enforces the `RequiresTabAccountAndClient` invariant, and returns the resolved `TransactionType` for denormalization. Cross-branch FKs fall out as `NotFoundException` (404) because the scoped read returns `null` first — this is deliberate, not a bug: returning `ConflictException` (409) would require a second unfiltered read per FK and would leak the existence of cross-branch resources. Only the fiado invariant violation throws `ConflictException` (409)
- [x] **1.17** Add shared `DueDateCalculator` in `server.Application/Services/Transactions/` keyed on `SettlementRule`, weekend-skipping for business-day variants; guard that throws when `OperatorEnteredCheque` is reached without an explicit date (defence in depth — the use case short-circuits first)
- [x] **1.18** Add shared `LockDateGuard` in `server.Application/Services/Transactions/` that reads `Setting.LockDate` via `ISettingsRepository.GetByBranchIdAsNoTracking` and throws `ConflictException` when `targetDate <= LockDate`
- [x] **1.19** Add shared `MemberAccountScopeGuard` in `server.Application/Services/Transactions/` that no-ops for `Manager`/`Admin` and, for `Member`, loads `IOperatorAccountsRepository.ListActiveByOperatorIdAsNoTracking` and throws `TokenWithoutPermissionException` when the target `AccountId` is not in the active linked set
- [x] **1.20** Add `TransactionValidationExtensions` in `server.Application/UseCases/Transactions/` exposing C# 13 `extension<T>(IRuleBuilder<T, TValue>)` blocks for rules shared across Create, Installment, and Update: `ValueIsPositive`, `ValuePrecisionWithin14x2`, `DueDateOnOrAfterDate`, `PaidAtOnOrAfterDate`
- [x] **1.21** Register the four shared services in `AppDependencyInjection`
- [x] **1.22** Create EF migration `Milestone3Phase1TransactionLedger` covering the `Transactions` table with its six indexes and the two new `TransactionType` columns
- [x] **1.23** Extend the `Milestone3Phase1TransactionLedger` migration with a simple one-time data update for existing development `TransactionType` rows created before these columns existed, matched by the known seeded `(TransactionType.Name, Category.Name)` pairs from `CreateBranchSeedFactory`; this is a development-stage convenience update, not a production-grade custom-row backfill guarantee
- [x] **1.24** Extend `CreateBranchUseCase` unit tests to assert that each of the 19 seeded `TransactionType` rows persists the expected `SettlementRule` and `RequiresTabAccountAndClient` values
- [x] **1.25** Add `WebApi.Test` integration coverage that reloads the 19 seeded `TransactionType` rows from the real database after `CreateBranch` and re-asserts the same per-row `SettlementRule` and `RequiresTabAccountAndClient` mapping
- [x] **1.26** Extend `TransactionTypeBuilder` in `CommonTestUtilities` with `WithSettlementRule` and `WithRequiresTabAccountAndClient`
- [x] **1.27** Add `TransactionBuilder` in `CommonTestUtilities` with a default-valid entity and fluent `WithValue`, `WithStatus`, `WithBranchId`, `WithAccount`, `WithRecordedByOperator`, `WithTransactionType`, `WithClient`, `WithDueDate`, `WithPaidAt`, `WithOriginTransactionId`
- [x] **1.28** Add `TransactionsRepositoryBuilder` in `CommonTestUtilities` with exact-arg return setups for `GetByIdAndBranchIdAsNoTracking`, `ListByBranchIdAsNoTracking`, `CountByBranchIdAsNoTracking`, `SumActiveValueByAccountAndDateAsNoTracking`, and `ListByOriginTransactionIdAndBranchIdAsNoTracking`
- [x] **1.29** Extend `TransactionTypesRepositoryBuilder` with an exact-arg helper for `GetActiveByIdAndBranchIdWithCategoryAsNoTracking`
- [x] **1.30** Extend `SettingsRepositoryBuilder` with an exact-arg helper for `GetByBranchIdAsNoTracking`
- [x] **1.31** Update `loto-specs.md` section 3.10 (`TransactionType` adds `SettlementRule` and `RequiresTabAccountAndClient`), section 3.12 (full `Transaction` field set), section 4 (document `SettlementRule` enum values), and section 6.1 (document fiado semantics via `RequiresTabAccountAndClient`); update `loto_presentation.html` and `loto_entity_relationship_diagram.html` for the added columns; bump the shared `Spec revision` on all three files and keep `Sync group: loto-backend-docs`
- [x] **1.32** Run `bash server/docs/check-loto-doc-sync.sh`; must pass before Phase 2 starts

### Phase 2 — Create Transaction (single)

Add the single-transaction create path end-to-end, including role-dependent `RecordedByOperatorId` resolution and the full branch-consistency + member-scope + lock-date guard chain.

- [x] **2.1** Add `RequestCreateTransactionJson` with `Date`, `Value`, `Description?`, `TransactionTime?`, `TransactionTypeId`, `AccountId`, `ClientId?`, `DueDate?` (null → defaulted by `SettlementRule`), `RecordedByOperatorId?` (role-dependent per the Key behaviors block), and `SaveAsDraft` (default `false`)
- [x] **2.2** Add `ResponseCreateTransactionJson` echoing persisted state including denormalized `CategoryId`, `Direction`, computed `DueDate`, and `Status`
- [x] **2.3** Add `CreateTransactionFluentValidation` applying shape-only rules via `TransactionValidationExtensions`
- [x] **2.4** Add `CreateTransactionMapper`
- [x] **2.5** Implement `CreateTransactionUseCase` with the flow: authenticate branch user → validate DTO shape → resolve caller's linked operator via `IOperatorsRepository.GetActiveLinkedByUserIdAndBranchIdAsNoTracking` (may be `null` for `Manager`/`Admin`) → apply role-dependent `RecordedByOperatorId` resolution (Member: reject any non-null `RecordedByOperatorId` in the request with `OnValidationException` (400) regardless of whether the value matches the caller's operator, and require a linked operator — missing link → 400; the server always resolves the field server-side from the caller's linked operator; Manager/Admin: accept a supplied override, default to caller operator when present, throw `OnValidationException` (400) when neither is available) → `TransactionBranchConsistencyService` → `MemberAccountScopeGuard` → denormalize `CategoryId` and `Direction` from the resolved `TransactionType` → compute or validate `DueDate` via `DueDateCalculator` (reject `null` for `OperatorEnteredCheque`) → `LockDateGuard` → build the entity with `Status = SaveAsDraft ? Draft : Active`, `CreatedByUserId = userId`, `BranchId = branchId` → `Add` + `Commit` → map to response
- [x] **2.6** Add `TransactionController` with explicit `[Route("")]` on `POST /transaction` and `[TokenAuthenticateBranch]` applied at the method level (consistent with the existing controllers — `AccountController`, `ClientController`, `OperatorController` — where each action declares its own auth filter rather than relying on a class-level attribute); declare `[ProducesResponseType]` metadata for 201/400/401/403/404/409
- [x] **2.7** Register `CreateTransactionUseCase` in `AppDependencyInjection`
- [x] **2.8** Add `RequestCreateTransactionJsonBuilder` in `CommonTestUtilities`
- [x] **2.9** Add `Validators.Test` coverage for `CreateTransactionFluentValidation`: success and targeted failures (`TransactionTypeId` empty, `Value <= 0`, precision overflow, `Date` default, `DueDate < Date`, `RecordedByOperatorId` supplied as empty `Guid`)
- [x] **2.10** Add `UseCases.Test` coverage for `CreateTransactionUseCase`: happy path asserting denormalized `CategoryId`/`Direction` come from the resolved type (not from the input); one test per `SettlementRule` value for default `DueDate`; `OperatorEnteredCheque` without `DueDate` fails validation; draft vs active status; `NotFoundException` (404) on each branch-mismatch / missing FK (account, transaction type, client) — scoped reads return `null` first, so cross-branch collapses into 404 by design and does not leak existence; fiado invariant failures (non-Tab account; missing client) → `ConflictException` (409); lock-date → `ConflictException` (409); `OnValidationException` (400) when a Member supplies any non-null `RecordedByOperatorId` — covered by two tests: one where the supplied value matches the caller's linked operator, one where it differs — proving the rejection is unconditional on value; `OnValidationException` (400) when a Member has no linked operator; `TokenWithoutPermissionException` (403) when a Member targets an unlinked account; Manager/Admin happy path when operator is supplied; Manager/Admin happy path defaulting to the caller's linked operator when no override is supplied; `OnValidationException` (400) when Manager/Admin has no operator link and supplies none; exact-arg assertion on `Add` with `BranchId == branchUser.BranchId`; `DidNotReceive().Commit()` on every failure path
- [x] **2.11** Add `WebApi.Test` coverage for `POST /transaction`: 201 happy path verifying denormalized fields via `factory.ReloadAsync<Transaction>`; 400 validation; 401 unauthenticated; 403 member-scope violation; 404 missing FK or cross-branch FK (same contract — scoped reads collapse branch mismatches into 404 to prevent existence leakage); 409 lock-date; 409 fiado invariant; branch isolation (token for branch A cannot write against branch B's account — also 404)

### Phase 2.5 — Caller Operator Identity Invariant

Make caller-operator resolution deterministic before installment creation, read/list, update, finalize, and cancel reuse the transaction permission model.

- [x] **2.5.1** Add the backend invariant that a user may have at most one active `Operator` linked to them per branch. Keep `Operator.UserId` nullable so unlinked operators remain valid
- [x] **2.5.2** Add the spec sync for the new `Operator` invariant: update `loto-specs.md` section 3.6 to document "at most one active `Operator` per `(User, Branch)`, enforced by filtered unique index" in the same style as section 3.5 documents `BranchUser`; update `loto_presentation.html` and `loto_entity_relationship_diagram.html` to reflect the new `Operators` index; bump the shared `Spec revision` on all three files; keep `Sync group: loto-backend-docs`
- [x] **2.5.3** Run `bash server/docs/check-loto-doc-sync.sh`; must pass before the implementation is considered complete
- [x] **2.5.4** Add resource message key `OPERATOR_USER_ALREADY_LINKED` in `ResourcesErrorMessages.resx` for the new duplicate active operator-user link conflict
- [x] **2.5.5** Add an EF Core filtered unique index on `Operators(BranchId, UserId)` where `UserId IS NOT NULL AND Active = true`, plus the migration/model snapshot update
- [x] **2.5.6** Before the filtered unique index is created, handle existing development duplicate rows for active `(BranchId, UserId)` operator links. Either fail loud in the migration with a helpful message and a one-liner developers can run to inspect/repair duplicates, or ship a one-time dev-data reconciliation that deactivates all but the newest duplicate before index creation. Follow the same development-stage cleanup intent as item 1.23
- [x] **2.5.7** Replace `IOperatorsRepository.GetActiveByUserIdAndBranchId` / `OperatorsRepository.GetActiveByUserIdAndBranchId` so caller-operator resolution no longer depends on `FirstOrDefault`. Use an API name and implementation that reflects the invariant explicitly, such as `GetActiveLinkedByUserIdAndBranchIdAsNoTracking`, and make the query deterministic while the database uniqueness enforces zero-or-one results
- [x] **2.5.8** Add repository support for duplicate-link checks during operator create/update, including an exclude-current-operator path for update
- [x] **2.5.9** Update `CreateOperatorUseCase` and `UpdateOperatorUseCase` to throw `ConflictException(ResourcesErrorMessages.OPERATOR_USER_ALREADY_LINKED)` when assigning a user already linked to another active operator in the same branch
- [x] **2.5.10** Update `GetOperatorSelfContextUseCase`, `CreateTransactionUseCase`, and later transaction phases to use the renamed deterministic caller-operator repository API
- [x] **2.5.11** Add `UseCases.Test` coverage for create/update duplicate user-link conflicts and for transaction caller-operator resolution using the new repository API
- [x] **2.5.12** Add `WebApi.Test` coverage proving duplicate active operator-user links return 409 and that transaction create still resolves `RecordedByOperatorId` from the single active linked operator
- [x] **2.5.13** Keep transaction create behavior unchanged: `Member` callers still cannot supply `RecordedByOperatorId`; the server resolves it from the single active operator link

### Phase 3 — Create Installment Transactions (cheque pre-dated)

Add the parallel installment endpoint gated on `SettlementRule == OperatorEnteredCheque`. Reuses the Phase 2 service chain.

- [ ] **3.1** Add `RequestCreateTransactionInstallmentJson` with all fields from Create plus `InstallmentCount` (int; 2 ≤ N ≤ 24)
- [ ] **3.2** Add `ResponseCreateTransactionInstallmentJson` returning the array of persisted rows
- [ ] **3.3** Add `CreateTransactionInstallmentFluentValidation` reusing `TransactionValidationExtensions` plus the `InstallmentCount` range rule
- [ ] **3.4** Add `CreateTransactionInstallmentMapper`
- [ ] **3.5** Implement `CreateTransactionInstallmentUseCase`: reuses the Phase 2 chain, rejects with `ConflictException` when the resolved `TransactionType.SettlementRule != OperatorEnteredCheque`, constructs N rows sharing `OriginTransactionId == rows[0].Id`, splits value via `Math.Round(total / N, 2, MidpointRounding.AwayFromZero)` for rows 1..N-1 with the residual absorbed by the last row so `Sum(row.Value) == total` exactly, staggers `DueDate` monthly from the caller-supplied base, sets per-row `Description = "CH PRE ({i}/{N})"`, and calls `AddRange` + `Commit` exactly once
- [ ] **3.6** Add `POST /transaction/installment` endpoint on `TransactionController`
- [ ] **3.7** Register `CreateTransactionInstallmentUseCase` in `AppDependencyInjection`
- [ ] **3.8** Add `RequestCreateTransactionInstallmentJsonBuilder` in `CommonTestUtilities`
- [ ] **3.9** Add `Validators.Test` coverage: `InstallmentCount` in `[2, 24]`, plus the shared shape rules
- [ ] **3.10** Add `UseCases.Test` coverage: N=2, N=3, N=12 — assert `Sum(row.Value) == total` exactly; staggered monthly `DueDate`; all rows share `OriginTransactionId == rows[0].Id`; `AddRange(Arg.Is<IEnumerable<Transaction>>(rows => rows.Count() == N))` called once; `Commit()` called once; `Conflict` when the resolved type is not `OperatorEnteredCheque`
- [ ] **3.11** Add `WebApi.Test` coverage: `POST` with N=3 reloads by `OriginTransactionId`, asserts count, shared origin, and summed value equals the original total exactly

### Phase 4 — Get + List Transactions

Add the read surface: single view, filtered list with offset pagination, deterministic ordering, and Member-scoped narrowing via `AllowedAccountIds`.

- [ ] **4.1** Add `ResponseTransactionJson` — rich single view reused across Get, Update, Finalize, and Cancel
- [ ] **4.2** Add `ResponseListTransactionsJson` with a lightweight item projection and paging metadata (`Page`, `PageSize`, `TotalCount`)
- [ ] **4.3** Add `RequestListTransactionsJson` query-bound with `AccountId?`, `DateFrom?`, `DateTo?`, `Status?`, `OperatorId?`, `ClientId?`, `Page`, `PageSize` — `AllowedAccountIds` is NOT on this DTO
- [ ] **4.4** Add `GetTransactionUseCase` returning `ResponseTransactionJson`; cross-branch and Member-out-of-scope both return `NotFound` (no existence leak)
- [ ] **4.5** Add `ListTransactionsFluentValidation` covering `Page >= 1`, bounded `PageSize`, and `DateFrom <= DateTo`
- [ ] **4.6** Add `ListTransactionsUseCase` that builds `TransactionListFilter` from the request and, for `Member`, resolves the active linked-account set via `IOperatorAccountsRepository.ListActiveByOperatorIdAsNoTracking` and sets `AllowedAccountIds`; if the caller also supplied an explicit `AccountId` outside the linked set, return an empty response (`Items = []`, `TotalCount = 0`) rather than leak existence. Otherwise call both `ListByBranchIdAsNoTracking(branchId, filter)` and `CountByBranchIdAsNoTracking(branchId, filter)` with the same resolved filter instance, and populate `ResponseListTransactionsJson.TotalCount` from the count call (never from `Items.Count`, which only reflects the current page)
- [ ] **4.7** Add `GET /transaction/{transactionId:guid}` and `GET /transaction` (with `[FromQuery] RequestListTransactionsJson`) to `TransactionController`
- [ ] **4.8** Register the two use cases in `AppDependencyInjection`
- [ ] **4.9** Add `Validators.Test` coverage for `ListTransactionsFluentValidation`
- [ ] **4.10** Add `UseCases.Test` coverage for `GetTransactionUseCase`: happy path; `NotFound` on cross-branch id; `NotFound` when Member targets a row whose account is outside their linked set
- [ ] **4.11** Add `UseCases.Test` coverage for `ListTransactionsUseCase` with exact-arg assertions on both `ListByBranchIdAsNoTracking` and `CountByBranchIdAsNoTracking` proving the resolved filter reaches each repo call with identical values (including the Member-narrowed `AllowedAccountIds`); assert that `ResponseListTransactionsJson.TotalCount` is populated from the count call, not from `Items.Count`; cover the "Member with explicit unlinked `AccountId`" short-circuit returning `Items = []`, `TotalCount = 0` without hitting either repo method (`DidNotReceive()` on both)
- [ ] **4.12** Add `WebApi.Test` coverage: seed two branches, token for branch A lists only branch A's rows; each filter narrows correctly; Member cannot see another operator's rows via explicit filter; list ordering matches `Date DESC, TransactionTime DESC NULLS LAST, CreatedAt DESC, Id DESC`; offset pagination returns expected slices across `Page` values and `TotalCount` in the response body equals the full filtered row count (not the page slice size) across every page request — e.g. seed 7 rows matching the filter, request `PageSize = 3` on pages 1/2/3, assert item counts 3/3/1 and `TotalCount = 7` on every response

### Phase 5 — Update Transaction (PUT full-replacement of editable subset)

Whitelist exactly five fields. The client sends all five on every call; `null` clears where the field permits it and is rejected where it doesn't.

- [ ] **5.1** Add `RequestUpdateTransactionJson` with exactly `Description`, `DueDate`, `PaidAt`, `ClientId`, `TransactionTime` — all five are part of the contract
- [ ] **5.2** Add `UpdateTransactionFluentValidation` covering shape-only rules via `TransactionValidationExtensions`; entity-relative rules (`DueDate >= transaction.Date`, `PaidAt >= transaction.Date`) are enforced inside the use case against the loaded entity
- [ ] **5.3** Implement `UpdateTransactionUseCase` with the flow: load tracked via `GetByIdAndBranchId` (any status) → `NotFound` on miss/cross-branch → `ConflictException` when `Status == Cancelled` → resolve caller's linked operator (may be `null` for Manager/Admin) → `MemberAccountScopeGuard` → permission matrix (same-day with linked operator matching `RecordedByOperatorId` → any active role; otherwise `Manager`/`Admin`; Member callers without linked operator denied) → `LockDateGuard` → when `transaction.TransactionType.RequiresTabAccountAndClient` reject any request that sets `ClientId = null` with `ConflictException` → when supplied `ClientId` is non-null, verify active in-branch via `IClientsRepository.GetActiveByIdAndBranchIdAsNoTracking` → direct assignment of the five fields on the tracked entity → `Commit` → map to `ResponseTransactionJson`
- [ ] **5.4** Add `PUT /transaction/{transactionId:guid}` endpoint returning `ResponseTransactionJson`
- [ ] **5.5** Register `UpdateTransactionUseCase` in `AppDependencyInjection`
- [ ] **5.6** Add `RequestUpdateTransactionJsonBuilder` in `CommonTestUtilities`
- [ ] **5.7** Add `Validators.Test` coverage for shape-only failures
- [ ] **5.8** Add `UseCases.Test` coverage: permission matrix (Member same-day own-operator allowed; Member older-day denied; Member other-operator denied; Member unlinked account 403; Manager any; Admin any; Manager/Admin without linked operator still elevated); lock-date blocks all; `Conflict` on cancelled row; `NotFound` cross-branch; `Conflict` when clearing `ClientId` on a `RequiresTabAccountAndClient` row; exact-arg assertion on `GetByIdAndBranchId` (tracked variant)
- [ ] **5.9** Add `WebApi.Test` coverage: 200 happy path with reload-based field verification; 403 for scope/role failures; 409 for lock-date and cancelled; 404 for cross-branch; persistence regression confirming only the whitelisted fields change (e.g. `Value`, `Date`, and `RecordedByOperatorId` are unchanged after update)

### Phase 6 — Finalize Draft Transaction

Draft → Active state transition for mobile offline save-then-sync.

- [ ] **6.1** Response is `ResponseTransactionJson`; no request body
- [ ] **6.2** Implement `FinalizeTransactionUseCase` (no validator — pure state transition): load tracked via `GetByIdAndBranchId` → `NotFound` cross-branch → `ConflictException` unless `Status == Draft` → `MemberAccountScopeGuard` → permission = caller's linked operator matches `RecordedByOperatorId` OR `Manager`/`Admin` → `LockDateGuard` → set `Status = Active` → `Commit`
- [ ] **6.3** Add `POST /transaction/{transactionId:guid}/finalize` endpoint returning `ResponseTransactionJson`
- [ ] **6.4** Register `FinalizeTransactionUseCase` in `AppDependencyInjection`
- [ ] **6.5** Add `UseCases.Test` coverage: success from `Draft`; `Conflict` from `Active` and `Cancelled`; `LockDate` blocks; branch isolation; Member scope; permission matrix
- [ ] **6.6** Add `WebApi.Test` coverage: 200 success; 409 already-active; 403 on scope/role violations

### Phase 7 — Cancel Transaction

Terminal state transition with audit trail; same permission matrix as Update.

- [ ] **7.1** Add `RequestCancelTransactionJson` with required non-empty `CancellationReason` (max length 500)
- [ ] **7.2** Add `CancelTransactionFluentValidation`
- [ ] **7.3** Implement `CancelTransactionUseCase`: load tracked via `GetByIdAndBranchId` → `NotFound` cross-branch → `ConflictException` unless `Status ∈ {Draft, Active}` (already-cancelled → 409) → `MemberAccountScopeGuard` → same permission matrix as Update → `LockDateGuard` → set `Status = Cancelled`, `CancelledAt = DateTime.UtcNow`, `CancelledByUserId = userId`, `CancellationReason = request.CancellationReason` → `Commit`. Cancelling an installment row never touches its siblings
- [ ] **7.4** Add `POST /transaction/{transactionId:guid}/cancel` endpoint returning `ResponseTransactionJson`
- [ ] **7.5** Register `CancelTransactionUseCase` in `AppDependencyInjection`
- [ ] **7.6** Add `RequestCancelTransactionJsonBuilder` in `CommonTestUtilities`
- [ ] **7.7** Add `Validators.Test` coverage: required `CancellationReason`, length cap at 500
- [ ] **7.8** Add `UseCases.Test` coverage: each permission branch; lock-date blocks; already-cancelled `Conflict`; audit fields assigned correctly; a follow-up `SumActiveValueByAccountAndDateAsNoTracking` call in the integration layer confirms the cancelled row is excluded from active sums
- [ ] **7.9** Add `WebApi.Test` coverage: 200 happy path; 403 permission/scope; 409 lock-date; 409 already-cancelled; installment-sibling isolation — `POST` N=3 installments, cancel row 2, reload rows 1 and 3, and confirm both remain `Active`

### Done criteria

- `Transaction` entity, `TransactionStatus` and `SettlementRule` enums, and the `TransactionType` additions (`SettlementRule`, `RequiresTabAccountAndClient`) exist in Domain, Infrastructure, and the Milestone 3 Phase 1 migration
- The `Transactions` table carries the six key indexes from the Key behaviors block
- All 19 seeded `TransactionType` rows persist explicit `SettlementRule` and `RequiresTabAccountAndClient` values for newly-created branches, asserted by `CreateBranchUseCase` unit tests and `WebApi.Test` integration coverage; the Phase 1 migration includes a simple one-time data update for existing development rows created before the new columns existed
- Single and installment create, get, list, update, finalize, and cancel endpoints are live and tested end-to-end
- `RequestCreateTransactionJson.RecordedByOperatorId` honours the role-dependent rules from the Key behaviors block, including the "Manager/Admin without an Operator link" path
- Update is `PUT` with full replacement of the five-field editable subset; persistence regression confirms non-whitelisted fields never change through the update endpoint
- List ordering is `Date DESC, TransactionTime DESC NULLS LAST, CreatedAt DESC, Id DESC`; pagination is offset-based via `Page`/`PageSize` (no cursor pagination)
- `Member` scoping is enforced on create/read/list/update/finalize/cancel via `MemberAccountScopeGuard` and the server-resolved `AllowedAccountIds` filter
- Lock-date and branch-consistency invariants are enforced across every write path
- Spec sync (`loto-specs.md`, `loto_presentation.html`, `loto_entity_relationship_diagram.html`) reflects the new `TransactionType` columns; `check-loto-doc-sync.sh` passes
- `ITransactionsRepository.SumActiveValueByAccountAndDateAsNoTracking(branchId, accountId, date, direction?)` is ready for Milestone 4's `CashVariance` computation
- The project-wide architecture tests (DI registration + auth-intent) pass with the new use cases and `TransactionController`

---

## Milestone 4 — Daily Close Workflow

**Goal:** Implement the daily register closing flow: open/submit/approve/reject, daily close items per product, opening-value carryover from previous day, and persisted cash-variance calculation.

**Scope boundary:** `DailyClose` and `DailyCloseItem` entities plus the full workflow. No time entry, holiday, or reporting yet.

**Precondition:** Milestone 3 is fully closed. Active transactions are available for cash-variance calculation.

**Key behaviors:**

- `DailyClose` entity carries: `Date`, `Status` (`DailyCloseStatus`: Draft, Submitted, Approved, Rejected), `AccountId`, `SubmittedByOperatorId`, `SubmittedAt`, `ApprovedAt`, `ApprovedByUserId`, `RejectionReason`, `Notes`, and `BranchId`
- `DailyCloseItem` entity carries: `Value`, `DailyCloseId`, `ProductId`
- One `DailyClose` per `(BranchId, AccountId, Date)` — enforced by unique constraint
- One `DailyCloseItem` per `(DailyCloseId, ProductId)` — enforced by unique constraint
- Workflow: Draft → Submitted → Approved or Rejected → resubmit cycle
- Opening values = previous day's closing items for the same account
- `CashVariance = TotalClosing - TotalOpening - TotalTransactions` — system-calculated when operator submits, updated on resubmission; operator cannot directly enter a variance value; persisted as a `DailyCloseItem` with the "Diferença Caixa" product
- Fiado balance is NOT stored as a `DailyCloseItem`; it is calculated at query time from Tab account transactions
- Lock-date enforcement applies: closes on or before `Setting.LockDate` cannot be modified
- Branch consistency: `DailyClose.BranchId` must match `Account.BranchId` and `SubmittedByOperator.BranchId`

---

## Milestone 5 — Time Entry & Holiday

**Goal:** Implement operator attendance tracking with hours/balance calculation from `Setting`, holiday management, and manager review flows.

**Scope boundary:** `TimeEntry` and `Holiday` entities. No reporting dashboards yet.

**Precondition:** Milestone 2 `Operator` and `Setting` are available.

**Key behaviors:**

- `TimeEntry` entity carries: `Date`, `ClockIn` (nullable `TimeOnly`), `ClockOut` (nullable `TimeOnly`), `Status` (`TimeEntryStatus`), `TotalHours`, `BalanceHours`, `OperatorId`, and `BranchId`
- `Holiday` entity carries: `Date`, nullable `Description`, and `BranchId`
- One `TimeEntry` per `(BranchId, OperatorId, Date)` — enforced by unique constraint
- Calculation logic driven by `TimeEntryStatus`:
  - Present: `TotalHours = (ClockOut - ClockIn) / 60 - lunchDeduction`; `BalanceHours = TotalHours - DailyTargetHours`
  - Abonado statuses (Sunday, Holiday, Vacation, JustifiedAbsence): `TotalHours = DailyTargetHours`; `BalanceHours = 0`
  - Owing statuses (DayOff, UnjustifiedAbsence): `TotalHours = 0`; `BalanceHours = -DailyTargetHours`
- Lunch deduction from `Setting`: `LunchDeductionOver6H` when worked >6h, `LunchDeductionOver4H` when worked >4h but ≤6h, zero otherwise
- `Holiday` is branch-scoped and feeds into the time-entry status resolution and future due-date calculation

---

## Milestone 6 — Configuration & Lookup Admin CRUD

**Goal:** Add admin maintenance for the seeded lookup and config entities: `Category`, `TransactionType`, `Product`, and `Setting`. These were bootstrapped by `CreateBranch` in Milestone 1 but have had no admin UI or update flows until now.

**Scope boundary:** Branch-scoped CRUD restricted to `Admin` and `Manager`. No reporting yet.

**Precondition:** Core operational milestones (2–5) are stable.

**Key behaviors:**

- `Category`: create, update, deactivate; name unique per branch; `DefaultDirection` is set at creation
- `TransactionType`: create, update, deactivate; linked to a `Category`; admin CRUD also manages `SettlementRule` and `RequiresTabAccountAndClient` metadata introduced in Milestone 3. The `UNIQUE (CategoryId, Name)` index introduced in Milestone 3 Phase 1 continues to apply: admin create/update must surface a 409 when it would violate the pair uniqueness
- `Product`: create, update, deactivate; name unique per branch; `DisplayOrder` management
- `Setting`: update only (one row per branch, created by `CreateBranch`); covers `LockDate`, `DailyTargetHours`, `LunchDeductionOver6H`, `LunchDeductionOver4H`

---

## Milestone 7 — Reporting & Reconciliation

**Goal:** Add manager dashboards, fiado aging, transaction queries, cash-variance reporting, monthly summaries, and reconciliation views.

**Scope boundary:** Read-only query endpoints and computed views. No new write flows.

**Precondition:** Milestones 3–6 are stable and producing data.

**Key behaviors:**

- Daily ledger by account and date range
- Fiado balance per client and aging (outstanding receivables filtered by `DueDate WHERE PaidAt IS NULL`)
- Cash-variance summaries across accounts and date ranges
- Operator transaction summaries by date
- Monthly reconciliation views for manager review before lock-date advancement
- Time-entry balance summaries per operator and period

---

## Milestone 8 — Invitation & Email Onboarding

**Goal:** Replace the current "add already-registered user" membership flow with an invitation system that allows branch admins/managers to invite users by email, including users who have not yet registered.

**Scope boundary:** Invitation entity, email delivery integration, accept/decline flow, and the registration-via-invitation path.

**Precondition:** Milestone 1 membership management is stable. This milestone was explicitly deferred from Milestone 1.

**Key behaviors:**

- Invitation entity with token, target email, target role, expiration, and status
- Email delivery integration (provider TBD)
- Accept flow: existing user joins branch; new user registers and joins in one step
- Decline/expiration handling
- Permission matrix: `Admin` and `Manager` can invite; invited role follows the existing permission rules from Milestone 1

---

## Milestone 9 — Deployment, CI & Observability

**Goal:** Establish the production deployment pipeline, continuous integration, health checks, structured logging, and monitoring for the hosted `Staging` and `Production` environments.

**Scope boundary:** Infrastructure automation and operational readiness. No new business features.

**Precondition:** Core business milestones are stable enough for a production deploy.

**Key behaviors:**

- CI pipeline: build, test (all three suites), and architecture-test gate on every push
- CD pipeline: deploy to `Staging` on merge to main, promote to `Production` on tag/release
- Health check endpoints for database connectivity and service readiness
- Structured logging and error tracking integration
- Automated EF migration application during deploy
- Environment-specific secret management aligned with the `infra/` contract from Milestone 0

---

## Milestone 10 — Access Data Import

**Goal:** Provide a one-time migration path from the legacy Microsoft Access database to the new system, preserving historical operators, accounts, clients, transactions, daily closes, and time entries.

**Scope boundary:** Import tooling and data mapping. Not a recurring sync — a one-time cutover per branch.

**Precondition:** All entity milestones (2–6) are complete so the target schema exists.

**Key behaviors:**

- Map legacy Access IDs to new Guid-based entities
- Preserve historical transaction records, daily closes, and time entries
- Handle legacy data quality issues (missing fields, orphaned references)
- Validate imported data against current business rules where possible, flag violations for manual review
- Import is branch-scoped: each legacy database maps to one `Branch`
