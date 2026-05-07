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
- Update, Finalize, and Cancel route their authorization through `TransactionMutationPermissionGuard`: same branch-local business day by caller's linked operator → any active branch role; older than the branch-local business day OR recorded by a different operator → `Manager`/`Admin`; on or before `Setting.LockDate` → blocked for everyone. Same-day comparisons use `BranchClock.IsSameLocalDay` or the equivalent branch-clock local business-date helper, never `DateTime.UtcNow.Date`. The same-day own-operator path only applies when the caller has a linked `Operator`; callers without one (valid for `Manager`/`Admin`) fall through to the elevated-role path
- `Member` role can only act (create/read/update/finalize/cancel) on transactions whose `AccountId` is linked to the caller's operator via active `OperatorAccount`. For list queries, the server resolves the Member's active linked-account set and narrows the repository filter to that set (via an `AllowedAccountIds` filter on the domain-level list filter, never on the request DTO); an explicit `AccountId` filter outside the linked set returns an empty result rather than leak account existence. Members see all transactions on their linked accounts regardless of which operator recorded the row; this is an account-scope rule, not an own-row-only rule. For Get and write paths, authenticated Member authorization/scope failures return clear `403 Forbidden` errors; missing or cross-branch rows still return `404 NotFound`
- Finalize draft: `Draft → Active` only. `TransactionMutationPermissionGuard`, `LockDateGuard`, member-scope checks, and update audit stamping apply
- Cancellation sets `Status = Cancelled`, `CancelledAt = clock.UtcNow`, `CancelledByUserId = caller`, `CancellationReason`, and the generic update audit fields. Cancelled rows stay in the database for audit but are excluded from financial calculations. Cancelling an installment row affects only that row; group-cancel is deferred
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

### Phase 2.6 (2.5 Refactor)

Close the actionable hardening items from Phase 2.5 without changing the caller-operator contract. Keep the strict caller-operator lookup semantics from 2.5: do not silently choose between duplicate active links with `FirstOrDefault`.

- [x] **2.6.1** Extend API-level PostgreSQL unique-violation normalization so `IX_Operators_BranchId_UserId` maps to `ConflictException(ResourcesErrorMessages.OPERATOR_USER_ALREADY_LINKED)` and serializes as the stable 409 error contract. Keep provider-specific `23505` / constraint-name inspection in `server.API/ExceptionHandling/PostgresExceptionHandler.cs`, not in Application use cases. Add a unit test for `PostgresExceptionHandler` under `WebApi.Test/ExceptionHandling/` (or `UseCases.Test/Services/` if placed in Application) covering the constraint-name-to-error-message mapping in isolation, so future unique indexes can be added without an end-to-end race test
- [x] **2.6.2** Add `WebApi.Test` concurrency coverage for the operator-user link race: fire two simultaneous `POST /operator` requests for the same active branch member and assert exactly one `201 Created` and one `409 Conflict`. Assert the 409 response body contains `OPERATOR_USER_ALREADY_LINKED` (not just the status code), so a future refactor of the translator cannot silently swap in a generic message
- [x] **2.6.3** Add `WebApi.Test` coverage for re-link-after-deactivate: seed an active operator linked to `UserId = X`, deactivate it, then create a new active operator linked to the same, and assert `201 Created`. This proves the filtered index predicate `Active = true`, not just the pre-check
- [x] **2.6.4** Add explicit `UseCases.Test` coverage that updating an operator with its existing `UserId` succeeds because the duplicate-link check excludes the current operator id
- [x] **2.6.5** Add a clearer invariant pre-assert to `tests/WebApi.Test/Infrastructure/TestSeeder.cs::SeedOperatorAsync` so repeated active seeded operators for the same `(BranchId, UserId)` fail as a fixture setup error before hitting the database constraint
- [x] **2.6.6** Update the milestone/spec note for the operator user-link workflow to document that `PUT /operator/{id}` with `UserId = null` intentionally clears the login link while preserving the operator row for history and reports
- [x] **2.6.7** Improve the Phase 2.5 migration duplicate-data failure message with a concrete development repair command that deactivates all but the newest active duplicate per `(BranchId, UserId)` before retrying the migration
- [x] **2.6.8** Extract `OperatorUserLinkGuard` in `server.Application/Services/Operators/` to encapsulate user-exists/active validation, active branch-membership validation, and no-active-link-in-branch validation with optional `exceptOperatorId`. Both `CreateOperatorUseCase` and `UpdateOperatorUseCase` call it; the duplicated `EnsureUserLinkIsAvailable` and `ValidateUserBranchMembership` helpers are deleted. `SeedOperatorAsync` from the Web API test infrastructure also routes through the guard/pre-assert path from 2.6.5 so bootstrap data cannot violate the invariant
- [x] **2.6.9** Add `UseCases.Test/Services/Operators/OperatorUserLinkGuardTest.cs` covering each guard outcome: user not found, user inactive, not a branch member, already linked, already linked with `exceptOperatorId` excluded, and clean pass. Existing `CreateOperatorUseCaseTest` and `UpdateOperatorUseCaseTest` drop duplicated inline assertions and instead assert `guard.Received(1).EnsureLinkable(...)`

### Phase 3 — Create Installment Transactions (cheque pre-dated)

Add the parallel installment endpoint gated on `SettlementRule == OperatorEnteredCheque`. Reuses the Phase 2 service chain.

- [x] **3.1** Add `RequestCreateTransactionInstallmentJson` with all fields from Create plus manual `Installments` (`Value`, `DueDate`) as the default entry mode and optional auto-generation fields (`AutoGenerateInstallments`, `InstallmentCount`, base `DueDate`)
- [x] **3.2** Add `ResponseCreateTransactionInstallmentJson` returning the array of persisted rows
- [x] **3.3** Add `CreateTransactionInstallmentFluentValidation` reusing `TransactionValidationExtensions`; validate manual rows (2 ≤ N ≤ 24, positive values, exact total sum, first due date future, strictly increasing due dates) and optional auto-generation (`InstallmentCount` in range plus required future base `DueDate`)
- [x] **3.4** Add `CreateTransactionInstallmentMapper`
- [x] **3.5** Implement `CreateTransactionInstallmentUseCase`: reuses the Phase 2 chain, rejects with `ConflictException` when the resolved `TransactionType.SettlementRule != OperatorEnteredCheque`, constructs rows sharing `OriginTransactionId == rows[0].Id`, supports manual row values/dates by default, supports optional auto-generation that splits value via `Math.Round(total / N, 2, MidpointRounding.AwayFromZero)` with residual absorbed by the last row so `Sum(row.Value) == total` exactly, staggers generated `DueDate` monthly from the caller-supplied base with weekend adjustment, sets per-row description to `CH PRE ({i}/{N})` plus the trimmed operator description when supplied, rejects generated non-positive rows, and calls `AddRange` + `Commit` exactly once
- [x] **3.6** Add `POST /transaction/installment` endpoint on `TransactionController`
- [x] **3.7** Register `CreateTransactionInstallmentUseCase` in `AppDependencyInjection`
- [x] **3.8** Add `RequestCreateTransactionInstallmentJsonBuilder` in `CommonTestUtilities`
- [x] **3.9** Add `Validators.Test` coverage: manual row count and auto `InstallmentCount` in `[2, 24]`, shared shape rules, exact total-sum validation, future first due date, strictly increasing due dates, and item value rules
- [x] **3.10** Add `UseCases.Test` coverage: manual rows preserve exact values/dates/descriptions; auto N=2, N=3, N=12 asserts `Sum(row.Value) == total` exactly; generated monthly `DueDate`; weekend adjustment; all rows share `OriginTransactionId == rows[0].Id`; `AddRange(Arg.Is<IEnumerable<Transaction>>(rows => rows.Count() == N))` called once; `Commit()` called once; `Conflict` when the resolved type is not `OperatorEnteredCheque`; generated non-positive row split rejects before persistence
- [x] **3.11** Add `WebApi.Test` coverage: `POST` with manual rows reloads by `OriginTransactionId`, asserts count, shared origin, exact values/dates/descriptions, and summed value equals the original total exactly; unhappy paths cover validation and non-cheque conflicts

### Phase 3.1 — Installment Contract Refactor

Refine Phase 3 before commit so cheque installments match operator-entered cheque reality instead of forcing an equal monthly split.

- [x] **3.1.1** Move shared `RecordedByOperatorId` role resolution into `TransactionRecordedByOperatorResolver` under `server.Application/Services/Transactions` and reuse it from both single-create and installment-create use cases
- [x] **3.1.2** Make manual cheque rows the default request model: each installment carries its own `Value` and `DueDate`, and the request-level `Value` must equal the exact sum of all rows
- [x] **3.1.3** Keep auto-generation optional through `AutoGenerateInstallments`, `InstallmentCount`, and a base `DueDate`; generated values stay equal except for the residual row and generated dates are monthly with weekend adjustment
- [x] **3.1.4** Move installment planning business rules out of `CreateTransactionInstallmentMapper`; the use case now builds row values, dates, origin id, and descriptions before mapping to `Transaction`
- [x] **3.1.5** Preserve operator text in generated descriptions using `CH PRE ({i}/{N}) - {Description}` when `Description` is supplied, otherwise `CH PRE ({i}/{N})`
- [x] **3.1.6** Guard generated splits so no persisted installment row has `Value <= 0`, including tiny-total cases such as `0.10 / 6` and `0.11 / 7`
- [x] **3.1.7** Rename Transaction Web API integration tests to the controller-suite convention (`TransactionControllerHappyPathTest`, `TransactionControllerUnhappyPathTest`) and add installment unhappy-path coverage

### Phase 3.2 — Create/Installment Hardening Refactor

Close the remaining Phase 3 / 3.1 alignment gaps before treating the cheque-installment contract as stable. This phase is intentionally refactor-focused: no new operator-facing behavior should land here unless it is required to make the current create/installment contract consistent, testable, and spec-aligned.

- [x] **3.2.1** Combine the recorded-by resolution cleanup with the shared write-path cleanup: extract a dedicated `TransactionRecordedByOperatorResolver` under `server.Application/Services/Transactions/` and centralize the shared single-create/installment-create write preamble (`GetAuthenticatedBranchUser` → caller linked operator lookup → recorded-by resolution → branch consistency → member account scope → lock-date) so the two create flows cannot drift in ordering or semantics
- [x] **3.2.2** Propagate the Phase 3.1 installment contract into the LottoGest doc sync group: update `loto-specs.md` section `6.3`, update the affected `loto_presentation.html` examples/narrative, update `loto_entity_relationship_diagram.html` if any transaction-field or relationship notes need to change, bump the shared `Spec revision` on all three files together, and require `bash server/docs/check-loto-doc-sync.sh` to pass after the semantic sync
- [x] **3.2.3** Apply description-length protection consistently across transaction create flows: reuse `TransactionValidationExtensions.DescriptionMaxLength()` in `CreateTransactionFluentValidation`, add the corresponding installment-create validation, and reserve the maximum `CH PRE ({i}/{N}) - ` prefix budget so persisted cheque-installment descriptions still respect the `varchar(500)` database limit instead of failing at the database layer
- [x] **3.2.4** Align guard ordering between single-create and installment-create so `MemberAccountScopeGuard` runs before the installment non-cheque `ConflictException` branch, preserving the intended `403`-before-`409` behavior when a `Member` targets an out-of-scope account
- [x] **3.2.5** Remove the duplicate auto-generate due-date rule from `CreateTransactionInstallmentFluentValidation` so the same invalid auto-generated `DueDate` does not emit duplicate validation messages
- [x] **3.2.6** Add missing installment draft coverage in both `UseCases.Test` and `WebApi.Test`, proving `SaveAsDraft = true` persists all generated/manual installment rows with `Status = Draft`
- [x] **3.2.7** Add explicit test coverage for manual-installment description fallback when request `Description` is `null` or whitespace, proving the persisted row description is exactly `CH PRE ({i}/{N})` with no dangling separator
- [x] **3.2.8** Rename the confusing `RequestCreateTransactionInstallmentJsonBuilder` helpers so the flag-only path and the fully-configured auto-generation path are unambiguous
- [x] **3.2.9** Extract the pure installment planning logic from `CreateTransactionInstallmentUseCase` into `server.Application/Services/Transactions/InstallmentPlanBuilder.cs`, covering manual rows, auto-generated rows, shared origin-id creation, description composition, and non-positive generated-row protection
- [x] **3.2.10** Move the installment row shape out of `CreateTransactionInstallmentMapper` and colocate it with the extracted planner so the mapper stays focused on DTO/entity transformation only
- [x] **3.2.11** Bring the installment slice back into the documented C# 13 mapping style from `AGENTS.md`: eliminate the old-style `this`-parameter mapping on the planner-owned row type and keep instance-based mappings in `extension(Type instance)` blocks once the planner extraction is complete

### Phase 4 — Get + List Transactions

Add the read surface: single view, filtered list with offset pagination, deterministic ordering, and Member-scoped narrowing via `AllowedAccountIds`.

- [x] **4.1** Add `ResponseTransactionJson` — rich single view reused across Get, Update, Finalize, and Cancel
- [x] **4.2** Add `ResponseListTransactionsJson` with a lightweight item projection and paging metadata (`Page`, `PageSize`, `TotalCount`)
- [x] **4.3** Add `RequestListTransactionsJson` query-bound with `AccountId?`, `DateFrom?`, `DateTo?`, `Status?`, `OperatorId?`, `ClientId?`, `Page`, `PageSize` — `AllowedAccountIds` is NOT on this DTO
- [x] **4.4** Add `GetTransactionUseCase` returning `ResponseTransactionJson`; cross-branch and Member-out-of-scope both return `NotFound` (no existence leak)
- [x] **4.5** Add `ListTransactionsFluentValidation` covering `Page >= 1`, bounded `PageSize`, and `DateFrom <= DateTo`
- [x] **4.6** Add `ListTransactionsUseCase` that builds `TransactionListFilter` from the request and, for `Member`, resolves the active linked-account set via `IOperatorAccountsRepository.ListActiveByOperatorIdAsNoTracking` and sets `AllowedAccountIds`; if the caller also supplied an explicit `AccountId` outside the linked set, return an empty response (`Items = []`, `TotalCount = 0`) rather than leak existence. Otherwise call both `ListByBranchIdAsNoTracking(branchId, filter)` and `CountByBranchIdAsNoTracking(branchId, filter)` with the same resolved filter instance, and populate `ResponseListTransactionsJson.TotalCount` from the count call (never from `Items.Count`, which only reflects the current page)
- [x] **4.7** Add `GET /transaction/{transactionId:guid}` and `GET /transaction` (with `[FromQuery] RequestListTransactionsJson`) to `TransactionController`
- [x] **4.8** Register the two use cases in `AppDependencyInjection`
- [x] **4.9** Add `Validators.Test` coverage for `ListTransactionsFluentValidation`
- [x] **4.10** Add `UseCases.Test` coverage for `GetTransactionUseCase`: happy path; `NotFound` on cross-branch id; `NotFound` when Member targets a row whose account is outside their linked set
- [x] **4.11** Add `UseCases.Test` coverage for `ListTransactionsUseCase` with exact-arg assertions on both `ListByBranchIdAsNoTracking` and `CountByBranchIdAsNoTracking` proving the resolved filter reaches each repo call with identical values (including the Member-narrowed `AllowedAccountIds`); assert that `ResponseListTransactionsJson.TotalCount` is populated from the count call, not from `Items.Count`; cover the "Member with explicit unlinked `AccountId`" short-circuit returning `Items = []`, `TotalCount = 0` without hitting either repo method (`DidNotReceive()` on both)
- [x] **4.12** Add `WebApi.Test` coverage: seed two branches, token for branch A lists only branch A's rows; each filter narrows correctly; Member cannot see rows outside linked accounts via explicit filters; list ordering matches `Date DESC, TransactionTime DESC NULLS LAST, CreatedAt DESC, Id DESC`; offset pagination returns expected slices across `Page` values and `TotalCount` in the response body equals the full filtered row count (not the page slice size) across every page request — e.g. seed 7 rows matching the filter, request `PageSize = 3` on pages 1/2/3, assert item counts 3/3/1 and `TotalCount = 7` on every response

### Phase 5 — Update Transaction (PUT full-replacement of editable subset)

Whitelist exactly five fields. The client sends all five on every call; `null` clears where the field permits it and is rejected where it doesn't.

- [x] **5.1** Add `RequestUpdateTransactionJson` with exactly `Description`, `DueDate`, `PaidAt`, `ClientId`, `TransactionTime` — all five are part of the contract
- [x] **5.2** Add `UpdateTransactionFluentValidation` covering shape-only rules via `TransactionValidationExtensions`; entity-relative rules (`DueDate >= transaction.Date`, `PaidAt >= transaction.Date`) are enforced inside the use case against the loaded entity
- [x] **5.3** Implement `UpdateTransactionUseCase` with the flow: load tracked via `GetByIdAndBranchId` (any status) → `NotFound` on miss/cross-branch → `ConflictException` when `Status == Cancelled` → resolve caller's linked operator (may be `null` for Manager/Admin) → `MemberAccountScopeGuard` → permission matrix (same-day with linked operator matching `RecordedByOperatorId` → any active role; otherwise `Manager`/`Admin`; Member callers without linked operator denied) → `LockDateGuard` → when `transaction.TransactionType.RequiresTabAccountAndClient` reject any request that sets `ClientId = null` with `ConflictException` → when supplied `ClientId` is non-null, verify active in-branch via `IClientsRepository.GetActiveByIdAndBranchIdAsNoTracking` → direct assignment of the five fields on the tracked entity → `Commit` → map to `ResponseTransactionJson`
- [x] **5.4** Add `PUT /transaction/{transactionId:guid}` endpoint returning `ResponseTransactionJson`
- [x] **5.5** Register `UpdateTransactionUseCase` in `AppDependencyInjection`
- [x] **5.6** Add `RequestUpdateTransactionJsonBuilder` in `CommonTestUtilities`
- [x] **5.7** Add `Validators.Test` coverage for shape-only failures
- [x] **5.8** Add `UseCases.Test` coverage: permission matrix (Member same-day own-operator allowed; Member older-day denied; Member other-operator denied; Member unlinked account 403; Manager any; Admin any; Manager/Admin without linked operator still elevated); lock-date blocks all; `Conflict` on cancelled row; `NotFound` cross-branch; `Conflict` when clearing `ClientId` on a `RequiresTabAccountAndClient` row; exact-arg assertion on `GetByIdAndBranchId` (tracked variant)
- [x] **5.9** Add `WebApi.Test` coverage: 200 happy path with reload-based field verification; 403 for scope/role failures; 409 for lock-date and cancelled; 404 for cross-branch; persistence regression confirming only the whitelisted fields change (e.g. `Value`, `Date`, and `RecordedByOperatorId` are unchanged after update)

### Phase 5.5 — Phase 4/5 Refactor

Close the read/list/update hardening items before Finalize and Cancel copy the same transaction permission model. This phase intentionally skips mapper relocation and optimistic-concurrency / ETag support; those are not part of the current MVP refactor.

- [x] **5.5.1** Change `GetTransactionUseCase` member-scope failures from masked 404s to clear 403s: Member without a linked operator returns `TRANSACTION_MEMBER_REQUIRES_OPERATOR_LINK`, Member linked to an operator but not linked to the transaction account returns the member account-scope error, and missing/cross-branch transaction ids remain `TRANSACTION_NOT_FOUND` 404. Update `TransactionController` response metadata plus `UseCases.Test` and `WebApi.Test` expectations accordingly
- [x] **5.5.2** Extract a shared member transaction scope resolver under `server.Application/Services/Transactions/` that resolves the caller's linked operator and active linked account ids once. Reuse it from Get, List, and `MemberAccountScopeGuard` so each caller translates the same resolved state into its own response shape (`403`, empty list, or write denial) without duplicating repository calls
- [x] **5.5.3** Complete the list empty-scope short-circuit: when a Member has no linked operator or has a linked operator with zero active linked accounts and does not supply an explicit `AccountId`, return `Items = []`, `TotalCount = 0` without calling `ListByBranchIdAsNoTracking` or `CountByBranchIdAsNoTracking`. Add `UseCases.Test` coverage for both no-linked-operator and linked-operator-with-no-accounts cases
- [x] **5.5.4** Document and test the member list policy that members see all rows on their linked accounts regardless of `RecordedByOperatorId`. Update the spec sync group to say this explicitly, and add Web API coverage for a shared terminal account where Member A can list rows recorded by Member B because both rows belong to a linked account
- [x] **5.5.5** Extract the Update/Finalize/Cancel permission decision into a reusable transaction mutation permission guard returning explicit outcomes such as allow, missing linked operator, wrong recording operator, and same-day required. Replace the nested boolean ladder in Update before Phase 6 and Phase 7 implement the same rule
- [x] **5.5.6** Replace `DateTime.UtcNow.Date` in the transaction mutation permission rule with an injectable branch clock, exposed through `BranchClock.IsSameLocalDay` or an equivalent local business-date helper. MVP behavior may hard-code the Sao_Paulo/Brasilia business date until branch timezone configuration exists. Add boundary tests around UTC evening/night hours proving same-day decisions follow the local business day
- [x] **5.5.7** Split update permission error messages for Member no-linked-operator vs linked-operator-but-not-recording-operator. Keep both as 403, but give clients distinct resource keys so the frontend can show either "link your user to an operator" or "ask the recording operator or a manager"
- [x] **5.5.8** Move loaded-entity-relative update validation into a small helper in the Update slice, for example `UpdateTransactionEntityRelativeValidator.EnsureValid(transaction, request)`, covering `DueDate >= transaction.Date` and `PaidAt >= transaction.Date`. Keep FluentValidation limited to request-shape rules
- [x] **5.5.9** Enrich the list response for operational screens: add `ClientName`, `AccountName`, and `TransactionTypeName` to list items; add `TotalPages`, `HasNext`, and `HasPrevious` to `ResponseListTransactionsJson`; and update the repository list path to use a single branch-scoped projection query rather than forcing frontend N+1 lookups
- [x] **5.5.10** Add update audit fields to `Transaction`: `UpdatedAt` and `UpdatedByUserId`. Stamp them in `UpdateTransactionUseCase`, include them in the rich transaction response when useful, add the EF migration/model snapshot update, and cover persistence in unit and Web API tests
- [x] **5.5.11** Add `TransactionBuilder.From(Transaction existing)` in `CommonTestUtilities` and replace the update-test `ReplaceTransaction` clone helper with it. Keep immutable domain fields immutable; improve the test builder instead of weakening the entity model
- [x] **5.5.12** Clean up Phase 4/5 tests: use `TransactionsRepositoryBuilder` in `ListTransactionsUseCaseTest` instead of direct `Substitute.For<ITransactionsRepository>()`, add Draft-update success coverage, and replace opaque permission-matrix inline data with named theory data or separate facts for clearer failures
- [x] **5.5.13** Add a dedicated spec sync for transaction update and member read/list semantics: add a new `loto-specs.md` rule section for the five editable fields, non-editable fields, local-business-day permission matrix, member linked-account visibility policy, 403 vs 404 read contract, update audit fields, and lock-date behavior; update `loto_presentation.html` / `loto_entity_relationship_diagram.html` when affected, bump shared sync metadata, and run `bash server/docs/check-loto-doc-sync.sh`
- [x] **5.5.14** Add an explicit Member-without-linked-accounts update permission test: Member has a linked operator but zero active `OperatorAccount` rows → 403 through `MemberAccountScopeGuard` / member transaction scope guard before `TransactionMutationPermissionGuard` runs. Cover the path in both `UseCases.Test` and `WebApi.Test`
- [x] **5.5.15** Decide and document the Member `OperatorId` list-filter behavior. Either strip `OperatorId` from `TransactionListFilter` for Member callers because their list scope is linked-account based, or document in the 5.5.4/spec policy that a Member may pass any `OperatorId` and the result is still filtered by `AllowedAccountIds` first
- [x] **5.5.16** Add `Mine` (`bool`, default `false`) to `RequestListTransactionsJson`. When `Mine == true` and the caller has a linked operator, set `filter.OperatorId = callerOperator.Id` server-side; supplying both `Mine = true` and explicit `OperatorId` returns 400. This is a convenience filter only and must not change `AllowedAccountIds` or repository scoping semantics
- [x] **5.5.17** Add dedicated unit tests for the three new shared transaction services: `UseCases.Test/Services/Transactions/MemberTransactionScopeResolverTest.cs`, `UseCases.Test/Services/Transactions/TransactionMutationPermissionGuardTest.cs`, and `UseCases.Test/Services/Transactions/BranchClockTest.cs`. Each test class covers its behavior matrix in isolation so Get/List/Update/Finalize/Cancel use-case tests only verify delegation (`Received(1).EnsureAllowed(...)` style), not the full decision tree again
- [x] **5.5.18** Assert the split update permission resource keys in existing Member update tests and in Phase 6/7 finalize/cancel tests: `TRANSACTION_MEMBER_REQUIRES_OPERATOR_LINK` for no-linked-operator and the new linked-but-not-recording-operator key (for example `TRANSACTION_MEMBER_NOT_RECORDING_OPERATOR`) for linked-but-not-recording-operator. Without exact key assertions, 5.5.7 can silently collapse back to one message
- [x] **5.5.19** Relocate entity-relative update validation tests when 5.5.8 extracts the helper: move `DueDate >= transaction.Date` and `PaidAt >= transaction.Date` failure cases into `UpdateTransactionUseCaseTest` or a dedicated `UpdateTransactionEntityRelativeValidatorTest` under the Update slice, and delete those cases from `UpdateTransactionFluentValidationTest` so validator tests cover shape rules only
- [x] **5.5.20** Assert the enriched list response shape. In `ListTransactionsUseCaseTest`, assert list items carry `ClientName`, `AccountName`, and `TransactionTypeName` from the repository projection. In the list-focused Web API tests, seed seven matching rows with `PageSize = 3` and assert `TotalPages`, `HasNext`, and `HasPrevious` on pages 1/2/3 (`true/false`, `true/true`, `false/true`)
- [x] **5.5.21** Add Web API parity for the list empty-scope short-circuit: seed a Member with a linked operator but zero active `OperatorAccount` rows, call `GET /transaction`, and assert `200 OK` with `Items = []` and `TotalCount = 0`. This proves the 5.5.3 behavior survives the HTTP pipeline, not just the use case
- [x] **5.5.22** Extend the architecture test suite to assert every public service type under `server.Application/Services/` and `server.API/ExceptionHandling/` can be resolved from the configured root container. Reuse the same reflection pattern as the existing all-use-cases-registered test

### Phase 6 — Finalize Draft Transaction

Draft → Active state transition for mobile offline save-then-sync.

- [x] **6.1** Response is `ResponseTransactionJson`; no request body
- [x] **6.2** Implement `FinalizeTransactionUseCase` (no validator — pure state transition): load tracked via `GetByIdAndBranchId` → `NotFound` on miss/cross-branch → `ConflictException` unless `Status == Draft` → resolve member/account context through the shared member transaction scope resolver / guard → authorize through `TransactionMutationPermissionGuard` (same guard as Update and Cancel; same-day comparisons use `BranchClock.IsSameLocalDay` / local business date) → `LockDateGuard` → set `Status = Active` → stamp `UpdatedByUserId` and `UpdatedAt` using the shared audit convention → `Commit`
- [x] **6.3** Add `POST /transaction/{transactionId:guid}/finalize` endpoint returning `ResponseTransactionJson`
- [x] **6.4** Register `FinalizeTransactionUseCase` in `AppDependencyInjection`
- [x] **6.5** Add `UseCases.Test` coverage: success from `Draft`; `Conflict` from `Active` and `Cancelled`; `LockDate` blocks; branch isolation; Member scope; permission matrix through `TransactionMutationPermissionGuard`; branch-local same-day boundary behavior through the clock; update-audit stamping; named theory data or separate facts for permission-matrix failures; `TransactionBuilder.From(existing)` for transaction variants; shared member transaction scope resolver setup
- [x] **6.6** Add `WebApi.Test` coverage: 200 success with reload-based `Status`, `UpdatedByUserId`, and `UpdatedAt` verification; 409 already-active; 403 on member scope/permission violations; local-business-day permission behavior verified by injecting a test `BranchClock` and seeding a transaction whose UTC day differs from its branch-local day; finalize permission should follow the branch-local day in both directions
- [x] **6.7** Document finalize semantics in the same spec section as the update mutation rules (planned §6.11) rather than creating a separate finalize-only rules section, because Update, Finalize, and Cancel share the permission matrix

### Phase 7 — Cancel Transaction

Terminal state transition with audit trail, using the same `TransactionMutationPermissionGuard` and local business-day semantics as Update and Finalize.

- [x] **7.1** Add `RequestCancelTransactionJson` with required non-empty `CancellationReason` (max length 500)
- [x] **7.2** Add `CancelTransactionFluentValidation`
- [x] **7.3** Implement `CancelTransactionUseCase`: validate request → load tracked via `GetByIdAndBranchId` → `NotFound` on miss/cross-branch → `ConflictException` unless `Status ∈ {Draft, Active}` (already-cancelled → 409) → resolve member/account context through the shared member transaction scope resolver / guard → authorize through `TransactionMutationPermissionGuard` (same guard as Update and Finalize; same-day comparisons use `BranchClock.IsSameLocalDay` / local business date) → `LockDateGuard` → set `Status = Cancelled`, `CancelledAt = clock.UtcNow`, `CancelledByUserId = userId`, `CancellationReason = request.CancellationReason`, `UpdatedByUserId = userId`, and `UpdatedAt = the same clock timestamp` → `Commit`. Cancelling an installment row never touches its siblings; keep this behavior paired with the 7.9 sibling-isolation Web API test
- [x] **7.4** Add `POST /transaction/{transactionId:guid}/cancel` endpoint returning `ResponseTransactionJson`
- [x] **7.5** Register `CancelTransactionUseCase` in `AppDependencyInjection`
- [x] **7.6** Add `RequestCancelTransactionJsonBuilder` in `CommonTestUtilities`
- [x] **7.7** Add `Validators.Test` coverage: required `CancellationReason`, length cap at 500
- [x] **7.8** Add `UseCases.Test` coverage: named permission-matrix cases through `TransactionMutationPermissionGuard`; branch-local same-day boundary behavior through the clock; lock-date blocks; already-cancelled `Conflict`; cancellation audit fields plus generic update audit fields assigned by the same convention; `TransactionBuilder.From(existing)` for status/date/operator variants; shared member transaction scope resolver setup; a follow-up `SumActiveValueByAccountAndDateAsNoTracking` call in the integration layer confirms the cancelled row is excluded from active sums
- [x] **7.9** Add `WebApi.Test` coverage: 200 happy path with reload-based `Status`, cancellation audit, and generic update audit verification; 403 permission/scope; 409 lock-date; 409 already-cancelled; installment-sibling isolation — `POST` N=3 installments, cancel row 2, reload rows 1 and 3, and confirm both remain `Active`. This sibling-isolation test is the executable contract for the final sentence in 7.3
- [x] **7.10** Document cancel semantics in the same spec section as the update/finalize mutation rules (planned §6.11), including shared permission guard behavior, local-business-day semantics, lock-date behavior, cancellation audit fields, generic update audit fields, and installment sibling isolation

### Done criteria

- `Transaction` entity, `TransactionStatus` and `SettlementRule` enums, and the `TransactionType` additions (`SettlementRule`, `RequiresTabAccountAndClient`) exist in Domain, Infrastructure, and the Milestone 3 Phase 1 migration
- The `Transactions` table carries the six key indexes from the Key behaviors block
- All 19 seeded `TransactionType` rows persist explicit `SettlementRule` and `RequiresTabAccountAndClient` values for newly-created branches, asserted by `CreateBranchUseCase` unit tests and `WebApi.Test` integration coverage; the Phase 1 migration includes a simple one-time data update for existing development rows created before the new columns existed
- Single and installment create, get, list, update, finalize, and cancel endpoints are live and tested end-to-end
- The operator user-link invariant is complete: filtered unique index on active `(BranchId, UserId)`, deterministic caller-operator lookup, use-case duplicate-link guard/pre-check, API-level PostgreSQL `23505` translation for the operator user-link race, and Web API race/re-link coverage
- Shared transaction write orchestration is centralized where it actually overlaps: single-create/installment-create use the shared create preamble, while Update/Finalize/Cancel share the member transaction scope resolver, `TransactionMutationPermissionGuard`, branch clock/local-business-day semantics, lock-date guard, and audit stamping convention
- `RequestCreateTransactionJson.RecordedByOperatorId` honours the role-dependent rules from the Key behaviors block, including the "Manager/Admin without an Operator link" path
- Update is `PUT` with full replacement of the five-field editable subset; persistence regression confirms non-whitelisted fields never change through the update endpoint
- `TransactionMutationPermissionGuard` is the only permission matrix implementation for Update, Finalize, and Cancel; all same-day decisions use `BranchClock.IsSameLocalDay` / branch-local business date rather than `DateTime.UtcNow.Date`
- `Transaction` carries generic mutation audit fields (`UpdatedByUserId`, `UpdatedAt`) and cancellation audit fields (`CancelledAt`, `CancelledByUserId`, `CancellationReason`); Update, Finalize, and Cancel stamp the generic audit fields consistently
- List ordering is `Date DESC, TransactionTime DESC NULLS LAST, CreatedAt DESC, Id DESC`; pagination is offset-based via `Page`/`PageSize` (no cursor pagination); list items include `ClientName`, `AccountName`, and `TransactionTypeName`; list responses include `TotalPages`, `HasNext`, and `HasPrevious`
- `Member` scoping is enforced on create/read/list/update/finalize/cancel via the member transaction scope resolver / guard and the server-resolved `AllowedAccountIds` filter; read/write scope failures use clear 403 errors, while list scope misses return empty result sets
- Lock-date and branch-consistency invariants are enforced across every write path
- Spec sync (`loto-specs.md`, `loto_presentation.html`, `loto_entity_relationship_diagram.html`) reflects the new `TransactionType` columns, installment contract, transaction update/finalize/cancel mutation rules in §6.11, member linked-account visibility, 403 vs 404 read contract, update/cancellation audit fields, and list response shape; shared sync metadata is bumped consistently and `check-loto-doc-sync.sh` passes
- `ITransactionsRepository.SumActiveValueByAccountAndDateAsNoTracking(branchId, accountId, date, direction?)` is ready for Milestone 4's `CashVariance` computation
- API-level `DbUpdateException` / PostgreSQL unique-violation translation is established for known unique indexes introduced by this milestone family and remains extendable for future database-enforced invariants
- The project-wide architecture tests (DI registration + auth-intent) pass with the new use cases and `TransactionController`

---

## Milestone 4 — Daily Close Workflow

**Goal:** Implement the daily register closing flow — open, edit close items, submit, approve, reject, **same-day Submitted-recall**, and resubmit cycle — with opening-value carryover from the **most recent prior close for the same `(BranchId, AccountId)`** (handles weekends/holidays gracefully without a holiday-calendar dependency), system-managed `CashVariance` persisted as a `DailyCloseItem`, and full reuse of the Member account scope, branch clock, audit-stamping, and 403-vs-404 conventions established in Milestone 3 Phase 5.5.

**Scope boundary:** `DailyClose`, `DailyCloseItem`, and the full workflow state machine. No time entry, holiday calendar, or reporting dashboards. Admin CRUD for `Product` is deferred to Milestone 6, but the seeded `Diferença Caixa` product must be reachable so the system-managed `CashVariance` line item can attach to it.

**Precondition:** Milestone 3 is fully closed. `Transaction` is live with `SumActiveValueByAccountAndDateAsNoTracking`, `IBranchClock`, `MemberAccountScopeGuard`, the shared member transaction scope resolver, the local-business-day permission idiom, the generic update audit convention (`UpdatedAt` + `UpdatedByUserId`), and the `loto-specs.md` sync group at the revision shipped by Milestone 3 Phase 5.5.

**Key behaviors:**

- `DailyClose` entity carries the field set from `loto-specs.md` §3.14 plus the Milestone 3 generic mutation audit pair: `UpdatedAt` (nullable) and `UpdatedByUserId` (nullable). Workflow stamps both audit fields on every transition (Submit, Approve, Reject, Rejected→Draft auto-transition on edit, Submitted→Draft recall on edit) from the *same* `branchClock.UtcNow()` instant the workflow-specific timestamp (`SubmittedAt` / `ApprovedAt`) uses
- `DailyCloseStatus` enum: `Draft = 0`, `Submitted = 1`, `Approved = 2`, `Rejected = 3`. Transitions: `Draft → Submitted → Approved | Rejected`; `Rejected → Draft` happens automatically on the next item edit so the operator can fix and re-submit without an explicit "reopen" endpoint; `Submitted → Draft` happens automatically on the next item edit when the **soft-final Submit** conditions are met (recording-operator Member on same branch-local business day, OR any Manager/Admin); `Approved` is terminal — the only path back to a non-terminal state is a future Reject of a future Submit, never a recall
- One `DailyClose` per `(BranchId, AccountId, Date)` — unique constraint on the table, with API-level PostgreSQL `23505` translation reusing the `PostgresExceptionHandler` pattern from Milestone 3 so a race on Open Day produces a clean `409 Conflict`
- One **active** `DailyCloseItem` per `(DailyCloseId, ProductId)` — filtered unique constraint (`WHERE Active = true`), same `23505` translation pattern so a race on item upsert (or on system-managed CashVariance creation during Submit) produces a clean `409 Conflict`. Soft-deleted rows do not participate in the constraint, so a future re-insert after a soft-delete is allowed
- `DailyClose.Date` is a *branch-local business day*, not a UTC date. "Today's close" / "yesterday's opening values" / the same-day permission ladder use `IBranchClock.IsSameLocalDay` / `LocalBusinessDate`, never `DateTime.UtcNow.Date`. The local-business-day rule is the same idiom shipped by Milestone 3 §6.11; this milestone reuses it, never re-implements it
- Member account scope is enforced on every read and write path:
  - **Read (Get):** missing/cross-branch id → `404 NotFound`; Member with no linked operator → `403 TRANSACTION_MEMBER_REQUIRES_OPERATOR_LINK`; Member linked but `DailyClose.AccountId ∉ AllowedAccountIds` → `403 TRANSACTION_MEMBER_ACCOUNT_OUT_OF_SCOPE`. The two existing Milestone 3 keys are reused — no new keys for the same semantic failure
  - **Read (List):** the same `AllowedAccountIds` server-resolved filter applies. Empty-scope (no linked operator OR linked-but-zero-active-accounts AND no explicit `AccountId`) short-circuits to `Items=[]`, `TotalCount=0` without calling the repository — same as Milestone 3 List
  - **Write paths:** account-scope failures surface as `403` with the same two distinct Milestone 3 keys. `MemberAccountScopeGuard` is updated in Phase 1 to surface the two keys explicitly (no-linked-operator vs linked-but-out-of-scope) instead of collapsing both to a single message — so write paths get the same precision the Get path already has after Milestone 3 5.5.1
- The shared member-scope resolver is **renamed** from `IMemberTransactionScopeResolver` to `IMemberAccountScopeResolver` (and the matching record `MemberTransactionScope` → `MemberAccountScope`) and moved from `server.Application/Services/Transactions/` to `server.Application/Services/Members/`. The logic is unchanged; this is a mechanical rename so the same service is honestly named for both Transaction and DailyClose callers. The Phase 1 architecture test prevents the rename from leaking unregistered. `MemberAccountScopeGuard` keeps its name and now lives next to the renamed resolver under `Services/Members/`
- A new `DailyCloseWorkflowGuard` in `server.Application/Services/DailyCloses/` is the **only** permission-matrix implementation for the DailyClose state machine. It encapsulates state-machine transitions with explicit outcomes:
  - **Open:** allowed when no `(BranchId, AccountId, Date)` row exists yet; submitter must be a Member with the account in scope OR a Manager/Admin
  - **Edit items:** allowed on `Draft` or `Rejected` rows for any caller passing the role × scope × local-business-day matrix; allowed on `Submitted` rows under the **soft-final Submit** rule (Member where `callerOperator.Id == close.SubmittedByOperatorId` AND `IBranchClock.IsSameLocalDay(close.Date)`, OR any Manager/Admin); rejected on `Approved` with `409 DAILYCLOSE_NOT_EDITABLE`; rejected on `Submitted` outside the soft-final conditions with `409 DAILYCLOSE_NOT_EDITABLE`. The guard returns an explicit outcome so the use case knows whether to apply no auto-transition (Draft), `Rejected → Draft`, or `Submitted → Draft` (recall). The `Diferença Caixa` product is system-only — operator payloads referencing that product id are rejected with a dedicated `400` validation key (`DAILYCLOSE_ITEM_PRODUCT_FORBIDDEN`), never silently dropped
  - **Submit:** allowed only from `Draft` or `Rejected`. Same role × linked-operator × local-business-day matrix as `TransactionMutationPermissionGuard`, keyed on `DailyClose.Date` rather than `transaction.Date`. Manager/Admin can submit on older business days
  - **Approve / Reject:** Manager/Admin only, regardless of date. Member callers get `403`. `RejectionReason` is required on Reject (max 500), validated by a dedicated FluentValidation rule
  - Use-case tests verify delegation (`Received(1).EnsureAllowed(...)` style); the full role × state × date matrix (including the recall outcome variants for `EnsureCanEditItems`) lives in `DailyCloseWorkflowGuardTest` per the Milestone 3 5.5.17 isolation-test convention
- **Open Day** creates `DailyClose` with `Status = Draft`, `SubmittedByOperatorId` set to the caller's linked operator (or `null` for Manager/Admin without a link, valid since Milestone 3), `SubmittedAt = null`, no items, and runs branch consistency (`Account.BranchId == DailyClose.BranchId == SubmittedByOperator.BranchId`) and `MemberAccountScopeGuard`
- **Edit items** is **batch PUT** of all close items in one call: clients send the full list of `(ProductId, Value)` lines in one payload; the server upserts each line per `(DailyCloseId, ProductId)`. Items present in the payload but not currently persisted are inserted; items already persisted but absent from the payload are deactivated (soft delete via `Active = false`); items in both are updated to the new `Value`. The system-managed `Diferença Caixa` row is never affected by an Edit-items call — it is owned exclusively by Submit, even when the close is in `Submitted` and the soft-final-recall conditions are met. Items are branch-consistent (`Product.BranchId == DailyClose.BranchId`). Lock-date applies on `close.Date`. If the close is in `Rejected`, the use case automatically transitions it to `Draft` before applying the upsert. If the close is in `Submitted` and the soft-final-Submit conditions are met (recording-operator Member on same branch-local business day, OR any Manager/Admin), the use case automatically transitions `Submitted → Draft`, clears `SubmittedAt`, and applies the upsert; the prior `Diferença Caixa` row from the previous Submit stays in place untouched (it is overwritten on the next Submit). Both auto-transitions stamp `UpdatedAt`/`UpdatedByUserId` from the same captured `branchClock.UtcNow()` instant. Edits on `Approved` are always rejected with `409 DAILYCLOSE_NOT_EDITABLE`; edits on `Submitted` outside the soft-final conditions are rejected with the same key
- **Submit** runs on `Draft` or `Rejected` rows: workflow guard "submit allowed" → member account scope → `LockDateGuard` on `DailyClose.Date` (passing `DAILYCLOSE_LOCK_DATE_VIOLATION` as the resource key) → compute the persisted `CashVariance` server-side via `CashVarianceCalculator` → upsert the `Diferença Caixa` line **through the tracked `close.Items` collection** (the use case loaded the close via `GetByIdAndBranchId` tracked variant which materializes Items): if a tracked active item with `ProductId == cashVarianceProductId` exists, mutate its `Value` in place; otherwise add a new `DailyCloseItem` to the collection. Never deleted on rejection or recall so the audit trail is preserved → re-resolve `SubmittedByOperatorId` from the caller's linked operator at submit time (not at open time, to handle the rare case where the operator link changed between Open and Submit) → stamp `Status = Submitted`, `SubmittedAt`, `UpdatedAt`, `UpdatedByUserId` from the *same* captured `branchClock.UtcNow()` instant → `Commit`. The workflow-specific timestamp and the generic audit pair never drift apart by even a microsecond
- **CashVariance formula** persisted on Submit (specifying `Direction` handling that §6.12 currently leaves implicit):
  - `TotalClosing = Σ DailyCloseItem.Value` for `(DailyCloseId = today's close, Active = true, ProductId ≠ CashVarianceProductId)`
  - `TotalOpening = Σ DailyCloseItem.Value` for `(DailyCloseId = the most recent prior close for the same (BranchId, AccountId) with Date < today's Date, Active = true, ProductId ≠ CashVarianceProductId)`. If no prior close exists, opening = 0. The prior close is read via `IDailyClosesRepository.GetMostRecentBeforeDateByBranchIdAndAccountIdAsNoTracking(branchId, accountId, currentCloseDate)` — top-1 ordered by `Date DESC, CreatedAt DESC, Id DESC`. This handles weekends, holidays, and "operator forgot to open yesterday" gracefully without needing a holiday-calendar dependency, while still degrading to opening = 0 when there genuinely is no prior history (first close for the account)
  - `TotalTransactions = SumActive(In) − SumActive(Out)` for `(BranchId, AccountId, Date)`, computed via `ITransactionsRepository.SumActiveValueByAccountAndDateAsNoTracking` with explicit `Direction.In` and `Direction.Out` calls subtracted in the calculator. `Status = Cancelled` and `Status = Draft` are excluded by the repository's `Active` predicate, matching Milestone 3 financial-calculation semantics
  - `CashVariance = TotalClosing − TotalOpening − TotalTransactions`
  - The `CashVarianceProductId` is resolved once per submit via `ICashVarianceProductResolver` (looking up the seeded product by name `"Diferença Caixa"`, which remains the user-facing display string)
- **Approve / Reject** stamp `ApprovedAt` + `ApprovedByUserId` (Approve) or `RejectionReason` (Reject), plus the generic `UpdatedAt`/`UpdatedByUserId` from the same captured instant. `LockDateGuard` applies to both. `Reject` clears `ApprovedAt`/`ApprovedByUserId` if a previously-approved row is rejected (defence in depth — the workflow guard does not allow that transition, but the use case keeps the data shape consistent)
- **Resubmit cycle:** an Edit-items call against a `Rejected` row auto-transitions to `Draft` (stamping the audit pair). The operator then calls Submit, which runs the same Submit path and recomputes `CashVariance`. Approve/Reject can run again on the resubmitted row
- **Recall cycle (soft-final Submit):** an Edit-items call against a `Submitted` row by the recording operator on the same branch-local business day (or by any Manager/Admin) auto-transitions to `Draft`, clears `SubmittedAt`, and applies the upsert (stamping the audit pair from one captured instant). The operator then calls Submit again, which recomputes `CashVariance` and overwrites the prior `Diferença Caixa` row in place. This makes a fat-finger Submit recoverable without a Reject from a manager; once a manager has acted (Approve or Reject), the recall window is closed and only the Reject path can return the close to an editable state
- **Lock-date enforcement** blocks every workflow transition (Open, Edit items, Submit, Approve, Reject, the Rejected→Draft auto-transition, AND the Submitted→Draft recall auto-transition) when `DailyClose.Date <= Setting.LockDate`. The shared `LockDateGuard` (currently in `Services/Transactions/`) is moved to `Services/Settings/` and parameterized with a resource-key argument so each caller passes its own `409` message: M3 transactions pass `TRANSACTION_DATE_LOCKED`, M4 DailyClose paths pass `DAILYCLOSE_LOCK_DATE_VIOLATION`. The migration is mechanical and ships in Phase 1 alongside the resolver rename
- **Branch consistency:** `DailyClose.BranchId == Account.BranchId == SubmittedByOperator.BranchId` (when operator non-null); `DailyCloseItem.DailyClose.BranchId == DailyCloseItem.Product.BranchId`
- **Fiado balance** is NOT stored as a `DailyCloseItem` (matches §6.5); the closing screen displays it for reference but it is computed at query time from Tab account transactions per §6.4. This milestone does not add the read endpoint
- **List response shape** mirrors Milestone 3 5.5.9: items carry `AccountName`, `SubmittedByOperatorName?` (nullable), `ApprovedByUserName?` (nullable), and the envelope carries `TotalCount`, `TotalPages`, `HasNext`, `HasPrevious`. The repository `ListByBranchIdAsNoTracking` runs a single branch-scoped projection (Includes) — no frontend N+1
- **List ordering** is deterministic: `Date DESC, CreatedAt DESC, Id DESC`. Pagination is offset-based with `Page`/`PageSize` (no cursor pagination), matching Milestone 3 conventions
- **Architecture test** picks up every new service under `server.Application/Services/DailyCloses/`, `server.Application/Services/Members/`, and `server.Application/Services/Settings/` (the new home of `LockDateGuard`) automatically through the reflection pattern shipped by Milestone 3 5.5.22 — no new test code, but DI registrations must follow
- **Spec sync** updates the existing §3.14 (DailyClose adds the audit pair), §3.15 (DailyCloseItem), §6.5 (workflow state machine including the `Rejected → Draft` auto-transition AND the new soft-final-Submit `Submitted → Draft` recall transition + opening-day source-of-truth), §6.6 (extend the lock-date rule to "every DailyClose workflow transition, including the recall auto-transition"), §6.12 (CashVariance formula with explicit Direction handling), and adds a new §6.13 "DailyClose contract" covering: workflow state machine including the recall transition, role × state × local-business-day matrix (including the recall conditions), account-scope `403`/`404`/empty-list contract, audit field stamping rules, lock-date behavior on every transition, sibling-account isolation on Submit, and the system-only `Diferença Caixa` product (never present in client `PUT /items` payloads, never affected by the recall auto-transition). `loto_presentation.html` and `loto_entity_relationship_diagram.html` get the same revision bump. `check-loto-doc-sync.sh` must pass before each user-facing slice closes

---

### Phase 1 — Foundation: Domain, Infrastructure, Shared-Scope Refactor, Spec Sync

Ship domain entities, persistence, the shared member-scope rename + namespace move, the workflow guard skeleton, the `CashVarianceCalculator` skeleton, the unique-constraint translator extension, the resource keys, and the spec sync that Milestone 4 requires before any user-facing slice. No HTTP endpoint lands in this phase; later phases exercise the wiring end-to-end.

- [x] **1.1** Add `DailyCloseStatus` enum to `server.Domain/Entities/Enums/` with values `Draft = 0`, `Submitted = 1`, `Approved = 2`, `Rejected = 3`
- [x] **1.2** Add `DailyClose` entity to `server.Domain` covering the `loto-specs.md` §3.14 field set: `Date`, `Status` (`DailyCloseStatus`), `AccountId`, nullable `SubmittedByOperatorId`, nullable `SubmittedAt`, nullable `ApprovedAt`, nullable `ApprovedByUserId`, nullable `RejectionReason` (max 500), nullable `Notes`, `BranchId`, plus the generic mutation audit pair (`UpdatedAt`, `UpdatedByUserId`). Use `init` setters for `AccountId`, `BranchId`, `Date` (immutable after Open Day); keep `Status`, `SubmittedByOperatorId`, all timestamps, audit fields, and `RejectionReason` settable. Navigations: `Branch`, `Account`, `SubmittedByOperator?`, `ApprovedByUser?`, `Items` (default empty)
- [x] **1.3** Add `DailyCloseItem` entity to `server.Domain` with `Value` (numeric `(14,2)`), `DailyCloseId`, `ProductId`. Use `init` setters for `DailyCloseId` and `ProductId`; keep `Value` and `Active` settable. Navigations: `DailyClose`, `Product`
- [x] **1.4** Add `IDailyClosesRepository` to `server.Domain/Interfaces/` with: `Add`; `GetByIdAndBranchId` (tracked) — includes `Account`, `SubmittedByOperator`, `ApprovedByUser`, `Items`, and `Items.Product` so Submit can mutate tracked Items in place and the rich response can map joined names; `GetByIdAndBranchIdAsNoTracking` (read view, same Includes); `GetMostRecentBeforeDateByBranchIdAndAccountIdAsNoTracking(Guid branchId, Guid accountId, DateTime beforeDate)` — top-1 ordered by `Date DESC, CreatedAt DESC, Id DESC`, includes `Items` only (used by `CashVarianceCalculator` to read prior closing items; supports weekends/holidays/missing closes gracefully); `ListByBranchIdAsNoTracking(Guid branchId, DailyCloseListFilter filter)` — single projection query with Includes for `Account`, `SubmittedByOperator`, `ApprovedByUser` (no Items — the list response is lightweight); `CountByBranchIdAsNoTracking(Guid branchId, DailyCloseListFilter filter)` — applies the same predicate pipeline as the list method, including `AllowedAccountIds`
- [x] **1.5** Add `IDailyCloseItemsRepository` to `server.Domain/Interfaces/` with `Add`, `AddRange`, and `ListActiveByDailyCloseIdAsNoTracking(Guid dailyCloseId)` (used by `CashVarianceCalculator` for `TotalClosing` when the calculator does not have access to a tracked close). Submit performs its `CashVariance` upsert directly through the tracked `close.Items` collection from `GetByIdAndBranchId` — no `Update`/no-tracking lookup is needed for the upsert path. PUT items uses tracked entity mutations on the loaded close as well
- [x] **1.6** Add `DailyCloseListFilter` domain model to `server.Domain/Models/` with `AccountId?`, `Status?`, `DateFrom?`, `DateTo?`, `OperatorId?` (informational filter; never used to short-circuit Member scope), `Mine?` (`bool?`, server-resolved like Milestone 3 5.5.16), `AllowedAccountIds?` (server-resolved; never client-bound), `Page`, `PageSize`
- [x] **1.7** Add `DailyCloseConfiguration` to `server.Infrastructure/Persistence/Configurations/` mapping the `DailyClose` table with explicit column types, `DeleteBehavior.Restrict` on FKs to `Account`, `SubmittedByOperator`, `ApprovedByUser`, and `Branch`; create the unique constraint `UNIQUE (BranchId, AccountId, Date) WHERE Active = true` (filtered) and a non-filtered fallback index `(BranchId, Date, AccountId)` for list queries; add `(BranchId, Status)` and `(BranchId, AccountId, Status)` indexes to support the filtered list paths; map `RejectionReason` as `varchar(500)`, `Notes` as `varchar(1000)`
- [x] **1.8** Add `DailyCloseItemConfiguration` mapping `DailyCloseItem` with `Value` as `numeric(14,2) NOT NULL`, the unique constraint `UNIQUE (DailyCloseId, ProductId) WHERE Active = true`, and `DeleteBehavior.Restrict` on FKs
- [x] **1.9** Add `DbSet<DailyClose> DailyCloses` and `DbSet<DailyCloseItem> DailyCloseItems` to `ServerDbContext`
- [x] **1.10** Implement `DailyClosesRepository` and `DailyCloseItemsRepository` in `server.Infrastructure/Persistence/Repositories/` with `AsNoTracking()` on read variants, the documented list ordering (`Date DESC, CreatedAt DESC, Id DESC`), `Active`-filter on reads, single projection query with the three Includes for `ListByBranchIdAsNoTracking`, and a private predicate helper shared with `CountByBranchIdAsNoTracking` so the list slice and `TotalCount` apply identical predicates
- [x] **1.11** Register `IDailyClosesRepository → DailyClosesRepository` and `IDailyCloseItemsRepository → DailyCloseItemsRepository` in `InfraDependencyInjection`
- [x] **1.12** Add EF migration `Milestone4Phase1DailyCloseFoundation` covering the `DailyCloses` and `DailyCloseItems` tables, the audit pair (already mapped on `DailyClose`), and the unique/key indexes from 1.7 and 1.8
- [x] **1.13** Mechanically rename `IMemberTransactionScopeResolver` → `IMemberAccountScopeResolver` and the matching record `MemberTransactionScope` → `MemberAccountScope`. Move the resolver implementation file from `server.Application/Services/Transactions/` to `server.Application/Services/Members/`. Move `MemberAccountScopeGuard` from `Services/Transactions/` to `Services/Members/` (its name does not change). Update the namespaces accordingly
- [x] **1.14** Update every Milestone 3 caller of the renamed resolver/guard to the new names and namespaces: `GetTransactionUseCase`, `ListTransactionsUseCase`, `UpdateTransactionUseCase`, `FinalizeTransactionUseCase`, `CancelTransactionUseCase`, `CreateTransactionUseCase`, `CreateTransactionInstallmentUseCase`, the shared transaction write preamble service, and any test that substituted the old type. The dedicated Milestone 3 unit-test class for the resolver (`MemberTransactionScopeResolverTest.cs`) is renamed to `MemberAccountScopeResolverTest.cs` and re-homed under `tests/UseCases.Test/Services/Members/`
- [x] **1.14a** Update `MemberAccountScopeGuard` to surface the two distinct Member-scope keys explicitly instead of collapsing both to a single error: when the caller is a Member with no linked operator, throw `TokenWithoutPermissionException(TRANSACTION_MEMBER_REQUIRES_OPERATOR_LINK)`; when the caller is a Member linked to an operator but the target `AccountId ∉ AllowedAccountIds`, throw `TokenWithoutPermissionException(TRANSACTION_MEMBER_ACCOUNT_OUT_OF_SCOPE)`. The guard reuses the resolved `MemberAccountScope` record so the no-operator branch is detected from `scope.LinkedOperator == null`. Update affected M3 transaction-write tests (`UpdateTransactionUseCaseTest`, `FinalizeTransactionUseCaseTest`, `CancelTransactionUseCaseTest`, `CreateTransactionUseCaseTest`, `CreateTransactionInstallmentUseCaseTest`) so the no-linked-operator-Member case asserts `TRANSACTION_MEMBER_REQUIRES_OPERATOR_LINK` instead of the previously-collapsed key. The matching `WebApi.Test` cases get the same assertion update
- [x] **1.14b** Move the shared `LockDateGuard` from `server.Application/Services/Transactions/` to `server.Application/Services/Settings/` and parameterize it with a resource-key argument (`Task EnsureNotLocked(Guid branchId, DateTime targetDate, string conflictResourceKey, CancellationToken ct = default)` or equivalent shape) so each caller passes its own `409` message. Update every M3 caller (the transaction use cases that invoke `LockDateGuard`) to pass the existing `TRANSACTION_DATE_LOCKED` resource key explicitly. M4 callers (Phase 2/4/5/6) pass `DAILYCLOSE_LOCK_DATE_VIOLATION`. Confirm the M3 transaction tests still pass after the parameter is threaded through
- [x] **1.15** Add resource message keys in `ResourcesErrorMessages.resx`: `DAILYCLOSE_NOT_FOUND`, `DAILYCLOSE_DATE_REQUIRED`, `DAILYCLOSE_ACCOUNT_REQUIRED`, `DAILYCLOSE_LOCK_DATE_VIOLATION`, `DAILYCLOSE_DATE_CONFLICT` (used by the `(BranchId, AccountId, Date)` race translator), `DAILYCLOSE_ITEM_DUPLICATE` (used by the `(DailyCloseId, ProductId)` race translator), `DAILYCLOSE_NOT_EDITABLE`, `DAILYCLOSE_NOT_SUBMITTABLE`, `DAILYCLOSE_NOT_APPROVABLE`, `DAILYCLOSE_NOT_REJECTABLE`, `DAILYCLOSE_REJECTION_REASON_REQUIRED`, `DAILYCLOSE_REJECTION_REASON_LENGTH`, `DAILYCLOSE_ITEM_PRODUCT_FORBIDDEN`, `DAILYCLOSE_ITEM_VALUE_NEGATIVE`, `DAILYCLOSE_BRANCH_MISMATCH`. Reuse `TRANSACTION_MEMBER_REQUIRES_OPERATOR_LINK` and `TRANSACTION_MEMBER_ACCOUNT_OUT_OF_SCOPE` for the cross-cutting Member-scope failures — do **not** duplicate them under a `DAILYCLOSE_*` prefix
- [x] **1.16** Extend `server.API/ExceptionHandling/PostgresExceptionHandler.cs` so the new unique-constraint names (`IX_DailyCloses_BranchId_AccountId_Date` and `IX_DailyCloseItems_DailyCloseId_ProductId`) translate to `ConflictException(ResourcesErrorMessages.DAILYCLOSE_DATE_CONFLICT)` and `ConflictException(ResourcesErrorMessages.DAILYCLOSE_ITEM_DUPLICATE)` respectively. Add `PostgresExceptionHandler` unit-test coverage for both new mappings in isolation, mirroring the Milestone 3 2.6.1 pattern
- [x] **1.17** Add `IDailyCloseWorkflowGuard` and `DailyCloseWorkflowGuard` skeleton in `server.Application/Services/DailyCloses/`. Public surface: `EnsureCanOpen(BranchUser caller, Operator? callerOperator, Guid accountId, DateTime branchLocalDate)`, `EnsureCanEditItems(DailyClose close, BranchUser caller, Operator? callerOperator)` (returns an outcome enum: `EditOnDraft`, `EditOnRejectedAutoTransitionToDraft`, `EditOnSubmittedRecallToDraft`), `EnsureCanSubmit(DailyClose close, BranchUser caller, Operator? callerOperator)`, `EnsureCanApprove(DailyClose close, BranchUser caller)`, `EnsureCanReject(DailyClose close, BranchUser caller)`. Behavior comes online phase-by-phase; the skeleton implements `EnsureCanOpen` for non-throwing behavior but throws `NotImplementedException` from the EditItems/Submit/Approve/Reject methods so accidental wire-up before their phases fails loudly
- [x] **1.18** Add `ICashVarianceCalculator` and `CashVarianceCalculator` skeleton in `server.Application/Services/DailyCloses/`. Public surface: `Task<decimal> CalculateAsync(Guid branchId, Guid accountId, DateTime branchLocalDate, Guid currentDailyCloseId, Guid cashVarianceProductId, CancellationToken ct)`. Concrete implementation lands in Phase 5; the skeleton throws `NotImplementedException` so any accidental wire-up before Phase 5 fails loudly. Use ASCII identifiers for all C# types — the seeded display name `"Diferença Caixa"` remains the user-facing string but never appears in C# type names, file paths, or namespaces
- [x] **1.19** Register the new shared services in `AppDependencyInjection`: `IDailyCloseWorkflowGuard → DailyCloseWorkflowGuard`, `ICashVarianceCalculator → CashVarianceCalculator`, the renamed `IMemberAccountScopeResolver → MemberAccountScopeResolver`, and re-confirm `MemberAccountScopeGuard` (renamed namespace) is registered. The architecture test in Milestone 3 5.5.22 picks them up automatically via reflection
- [x] **1.20** Add `DailyClosesRepositoryBuilder`, `DailyCloseItemsRepositoryBuilder`, `DailyCloseBuilder`, and `DailyCloseItemBuilder` in `tests/CommonTestUtilities/`. Builders default to a valid Draft close on the branch-local "today" with no items; expose `WithStatus`, `WithDate`, `WithAccount`, `WithBranchId`, `WithSubmittedByOperator`, `WithApprovedByUser`, `WithItems`, and `WithUpdated(DateTime, Guid)`. Repository builders expose exact-arg helpers for `GetByIdAndBranchId`, `GetByIdAndBranchIdAsNoTracking`, `GetMostRecentBeforeDateByBranchIdAndAccountIdAsNoTracking`, `ListByBranchIdAsNoTracking`, `CountByBranchIdAsNoTracking`, and `ListActiveByDailyCloseIdAsNoTracking` so use-case tests can assert exact branch/account/date arguments per Milestone 3 testing conventions
- [x] **1.21** Update `loto-specs.md` §3.14 (`DailyClose` adds `UpdatedAt` and `UpdatedByUserId`), §3.15 (`DailyCloseItem` field shape unchanged but cross-link the unique constraint), §6.5 (workflow state machine including the `Rejected → Draft` auto-transition AND the new soft-final-Submit `Submitted → Draft` recall transition + opening-day source-of-truth), §6.6 (extend the lock-date rule to "every DailyClose workflow transition, including the recall auto-transition"), and §6.12 (explicit Direction handling for `CashVariance`). Add a new §6.13 "DailyClose contract" covering: workflow state machine including the recall transition, role × state × local-business-day matrix (including the recall conditions: recording-operator Member on same branch-local business day OR any Manager/Admin), account-scope `403`/`404`/empty-list contract, audit field stamping rules, lock-date behavior on every transition, sibling-account isolation on Submit, and the system-only `Diferença Caixa` product (never present in client `PUT /items` payloads, never affected by the recall auto-transition). Update `loto_presentation.html` and `loto_entity_relationship_diagram.html` for the audit-pair addition and the new state arrow. Bump the shared `Spec revision` on all three files; keep `Sync group: loto-backend-docs`
- [x] **1.22** Run `bash server/docs/check-loto-doc-sync.sh`; must pass before Phase 2 starts

### Phase 2 — Open Day (Create DailyClose)

Add the `POST /dailyclose` path end-to-end, including Member-scope, branch-consistency, lock-date, and the `(BranchId, AccountId, Date)` race translator wired in Phase 1.

- [x] **2.1** Add `RequestOpenDailyCloseJson` to `server.Communication` with `Date` (`DateTime`, matching Milestone 3 `Transaction.Date`) and `AccountId` (`Guid`)
- [x] **2.2** Add `ResponseDailyCloseJson` echoing the persisted close: id, `Date`, `Status`, `AccountId`, `AccountName`, `BranchId`, nullable `SubmittedByOperatorId` + `SubmittedByOperatorName`, nullable `SubmittedAt`, nullable `ApprovedAt` + `ApprovedByUserId` + `ApprovedByUserName`, nullable `RejectionReason`, nullable `Notes`, audit fields (`CreatedAt`, `UpdatedAt`, `UpdatedByUserId`), and an `Items` collection of `ResponseDailyCloseItemJson` (defined here): `Id`, `ProductId`, `ProductName`, `Value`, `CreatedAt`. The list excludes soft-deleted items by default (the projection filters `Active = true`). This response is reused by Get, Submit, Approve, Reject, and Edit-items
- [x] **2.3** Add `OpenDailyCloseFluentValidation` covering shape-only rules: `Date` non-default, `AccountId` non-empty
- [x] **2.4** Add `OpenDailyCloseMapper` for the request → entity transformation (set `Status = Draft`, no items, no `SubmittedAt`)
- [x] **2.5** Implement `OpenDailyCloseUseCase` with the flow: authenticate branch user → validate DTO shape → resolve caller's linked operator via the deterministic `IOperatorsRepository.GetActiveLinkedByUserIdAndBranchIdAsNoTracking` (may be `null` for Manager/Admin) → branch consistency on `Account` (`IAccountsRepository.GetActiveByIdAndBranchIdAsNoTracking` → `404` on miss) → `MemberAccountScopeGuard` against `request.AccountId` (surfaces the two distinct keys per Phase 1.14a) → `IDailyCloseWorkflowGuard.EnsureCanOpen` → `LockDateGuard.EnsureNotLocked(branchId, request.Date, DAILYCLOSE_LOCK_DATE_VIOLATION)` → build the entity (`Status = Draft`, `SubmittedByOperatorId = callerOperator?.Id`, `BranchId = branchUser.BranchId`) → `Add` + `Commit`. The `(BranchId, AccountId, Date)` race produces `23505`; the `PostgresExceptionHandler` from 1.16 translates it to `ConflictException(DAILYCLOSE_DATE_CONFLICT)` (409). Map to `ResponseDailyCloseJson`
- [x] **2.6** Add `DailyCloseController` with `[TokenAuthenticateBranch]` applied at the method level (consistent with `TransactionController`). Add `[Route("dailyclose")]` at the controller level and `[Route("")]` on the `POST` action. Declare `[ProducesResponseType]` for 201/400/401/403/404/409
- [x] **2.7** Register `OpenDailyCloseUseCase` in `AppDependencyInjection`
- [x] **2.8** Add `RequestOpenDailyCloseJsonBuilder` in `tests/CommonTestUtilities/`
- [x] **2.9** Add `Validators.Test` coverage for `OpenDailyCloseFluentValidation`: success and targeted failures (default `Date`, empty `AccountId`)
- [x] **2.10** Add `UseCases.Test` coverage for `OpenDailyCloseUseCase`: happy path with exact-arg `Add` assertion (`BranchId`, `AccountId`, `Date`, `Status = Draft`, `SubmittedByOperatorId == callerOperator.Id`); Manager/Admin without linked operator path persists `SubmittedByOperatorId = null`; `404` on cross-branch `AccountId`; `403 TRANSACTION_MEMBER_REQUIRES_OPERATOR_LINK` when Member has no linked operator; `403 TRANSACTION_MEMBER_ACCOUNT_OUT_OF_SCOPE` when Member targets an unlinked account; `409 DAILYCLOSE_LOCK_DATE_VIOLATION` when `Date <= Setting.LockDate`; `DidNotReceive().Commit()` on every failure path; delegation assertion `workflowGuard.Received(1).EnsureCanOpen(...)`
- [x] **2.11** Add `WebApi.Test` coverage for `POST /dailyclose`: 201 happy path verifying persisted shape via `factory.ReloadAsync<DailyClose>`; 400 validation; 401 unauthenticated; 403 Member out-of-scope; 404 cross-branch `AccountId`; 409 lock-date; 409 `DAILYCLOSE_DATE_CONFLICT` race — fire two concurrent opens for the same `(BranchId, AccountId, Date)` and assert exactly one `201` and one `409` carrying the documented error key (parallels Milestone 3 2.6.2)

### Phase 3 — Get + List DailyClose (read surface)

Single view, filtered list with offset pagination, deterministic ordering, and Member-scoped narrowing via `AllowedAccountIds`. Uses the projection-with-Includes pattern from Milestone 3 5.5.9 to avoid N+1.

- [x] **3.1** Add `ResponseListDailyCloseItemJson` (lightweight list-row shape): id, `Date`, `Status`, `AccountId`, `AccountName`, nullable `SubmittedByOperatorId`, nullable `SubmittedByOperatorName`, nullable `ApprovedByUserId`, nullable `ApprovedByUserName`, nullable `SubmittedAt`, nullable `ApprovedAt`, `CreatedAt`
- [x] **3.2** Add `ResponseListDailyClosesJson` with `Items` (`IReadOnlyList<ResponseListDailyCloseItemJson>`), `Page`, `PageSize`, `TotalCount`, `TotalPages`, `HasNext`, `HasPrevious`
- [x] **3.3** Add `RequestListDailyClosesJson` query-bound with `AccountId?`, `Status?`, `DateFrom?`, `DateTo?`, `OperatorId?`, `Mine?` (default `false`), `Page` (default 1), `PageSize` (default 20). `AllowedAccountIds` is **not** on this DTO
- [x] **3.4** Add `ListDailyClosesFluentValidation` covering `Page >= 1`, bounded `PageSize` (1..100), `DateFrom <= DateTo`, and the Milestone 3 5.5.16 rule: `Mine = true` AND explicit `OperatorId` → 400
- [x] **3.5** Implement `GetDailyCloseUseCase` returning `ResponseDailyCloseJson` (the rich shape from 2.2): load via `IDailyClosesRepository.GetByIdAndBranchIdAsNoTracking` (with Items projection) → `404` on miss/cross-branch → resolve member scope through the renamed `IMemberAccountScopeResolver` → if Member with no linked operator return `403 TRANSACTION_MEMBER_REQUIRES_OPERATOR_LINK` → if Member linked but `AccountId ∉ AllowedAccountIds` return `403 TRANSACTION_MEMBER_ACCOUNT_OUT_OF_SCOPE` → map to response (joined names from the included nav properties)
- [x] **3.6** Implement `ListDailyClosesUseCase`: build `DailyCloseListFilter` from the request → for `Member`, resolve scope via `IMemberAccountScopeResolver` and set `filter.AllowedAccountIds`; if Member also supplied an explicit `AccountId` outside the linked set, return `Items=[], TotalCount=0` immediately without hitting the repo (`DidNotReceive()` on both list and count); if Member has no linked operator OR linked-but-zero-active-accounts AND no explicit `AccountId`, short-circuit to empty result without hitting the repo (matches Milestone 3 5.5.3) → resolve `Mine = true` server-side to `filter.OperatorId = callerOperator.Id` (matches Milestone 3 5.5.16) → call `ListByBranchIdAsNoTracking` and `CountByBranchIdAsNoTracking` with the same resolved filter instance → populate `TotalPages`, `HasNext`, `HasPrevious` in the response (`TotalCount` always comes from the count call, never `Items.Count`)
- [x] **3.7** Add `GET /dailyclose/{dailyCloseId:guid}` and `GET /dailyclose` (with `[FromQuery] RequestListDailyClosesJson`) to `DailyCloseController`. Declare `[ProducesResponseType]` for 200/400/401/403/404 on Get and 200/400/401 on List
- [x] **3.8** Register `GetDailyCloseUseCase` and `ListDailyClosesUseCase` in `AppDependencyInjection`
- [x] **3.9** Add `RequestListDailyClosesJsonBuilder` in `tests/CommonTestUtilities/`
- [x] **3.10** Add `Validators.Test` coverage for `ListDailyClosesFluentValidation`
- [x] **3.11** Add `UseCases.Test` coverage for `GetDailyCloseUseCase`: happy path; `404` on cross-branch id; `403 TRANSACTION_MEMBER_REQUIRES_OPERATOR_LINK` for Member without linked operator; `403 TRANSACTION_MEMBER_ACCOUNT_OUT_OF_SCOPE` for Member linked but out-of-scope; Manager/Admin always allowed; exact-arg assertion on `GetByIdAndBranchIdAsNoTracking(id, branchUser.BranchId)`
- [x] **3.12** Add `UseCases.Test` coverage for `ListDailyClosesUseCase` with exact-arg assertions on both list and count repo methods proving they receive identical filter instances; `TotalCount` comes from the count call; the Member-narrowed `AllowedAccountIds` reaches both calls; the empty-scope short-circuit (no linked operator) uses `DidNotReceive()` on both repo methods; the empty-scope short-circuit (linked but zero accounts) likewise; the explicit-unlinked-`AccountId` short-circuit likewise; `Mine = true` resolves to `filter.OperatorId = callerOperator.Id` server-side
- [x] **3.13** Add `WebApi.Test` coverage: seed two branches; token for branch A lists only branch A's rows; each filter narrows correctly; ordering matches `Date DESC, CreatedAt DESC, Id DESC`; offset pagination — seed 7 matching rows with `PageSize = 3` and assert item counts 3/3/1 and `TotalCount = 7` plus `TotalPages`/`HasNext`/`HasPrevious` (`true/false`, `true/true`, `false/true`) on pages 1/2/3; Member empty-scope returns `200` with empty result and no leak (matches Milestone 3 5.5.21 parity); Get returns `403` for Member out-of-scope and `404` for cross-branch

### Phase 4 — Edit DailyClose Items (batch PUT with soft-final-Submit recall)

Mobile-friendly batch upsert. The client sends the full list of close items in one `PUT` payload; the server upserts each `(DailyCloseId, ProductId)` line, deactivates lines absent from the payload, never touches the system-managed `Diferença Caixa` row, auto-transitions `Rejected → Draft` and `Submitted → Draft` (under the soft-final-Submit conditions) before applying edits, and stamps the audit pair from one captured clock instant.

- [x] **4.1** Extend `IProductsRepository` with `ListActiveByIdsAndBranchIdAsNoTracking(IEnumerable<Guid> productIds, Guid branchId)` (used by the batch PUT for one-shot product resolution; mismatch on branch or active state filters out the product so the use case can detect missing/cross-branch references) and `GetActiveByBranchIdAndNameAsNoTracking(Guid branchId, string name)` if it does not already exist. Implement in `ProductsRepository`; update `ProductsRepositoryBuilder` with exact-arg helpers for both methods
- [x] **4.2** Add `ICashVarianceProductResolver` (`Task<Guid> GetIdAsync(Guid branchId, CancellationToken ct)`) and `CashVarianceProductResolver` to `server.Application/Services/DailyCloses/`. Reads the seeded product by display name `"Diferença Caixa"` (the user-facing string from `CreateBranchSeedFactory`); throws an explicit `InvalidOperationException` naming the branch and the product name if the seed is missing (bootstrap defect, not a runtime condition). All C# type names and file paths use ASCII (`CashVariance...`); the Portuguese display string only appears as a string literal in the resolver implementation and the seed factory. Register `ICashVarianceProductResolver → CashVarianceProductResolver` in `AppDependencyInjection`. Add `tests/UseCases.Test/Services/DailyCloses/CashVarianceProductResolverTest.cs` covering happy path, missing-seed throws, and exact-arg assertion on the repo
- [x] **4.3** Add `RequestUpsertDailyCloseItemJson` (`ProductId`, `Value`) and `RequestPutDailyCloseItemsJson` (`Items` — `IReadOnlyList<RequestUpsertDailyCloseItemJson>`)
- [x] **4.4** Add `PutDailyCloseItemsFluentValidation`: `Items` not null, each item has non-empty `ProductId`, `Value >= 0` (close lines may legitimately be zero — e.g. an unused product that day), and `Value` precision within `numeric(14,2)` — i.e. at most 14 total digits with at most 2 decimal places, matching the persisted column. Reject duplicate `ProductId` within the payload. The "system-only `Diferença Caixa` product is forbidden" check requires a branch-scoped product lookup, so it lives in the use case, not the validator
- [x] **4.5** Wire `DailyCloseWorkflowGuard.EnsureCanEditItems` (replaces its `NotImplementedException`) and return an outcome enum from the public method:
  - `Status == Draft` AND caller passes role × scope × local-business-day matrix (Member: own-operator + same-day OR Manager/Admin) → outcome `EditOnDraft`
  - `Status == Rejected` AND same matrix as Draft → outcome `EditOnRejectedAutoTransitionToDraft`
  - `Status == Submitted` AND ((Member, `callerOperator.Id == close.SubmittedByOperatorId`, `IBranchClock.IsSameLocalDay(close.Date, branchClock.UtcNow())`) OR (Manager/Admin)) → outcome `EditOnSubmittedRecallToDraft`
  - `Status == Submitted` outside the recall conditions → throws `ConflictException(DAILYCLOSE_NOT_EDITABLE)`
  - `Status == Approved` → always throws `ConflictException(DAILYCLOSE_NOT_EDITABLE)`
- [x] **4.6** Implement `PutDailyCloseItemsUseCase` with the flow: authenticate → validate DTO shape → load tracked `DailyClose` (with items) via `GetByIdAndBranchId` → `404` on miss/cross-branch → resolve caller's linked operator → `MemberAccountScopeGuard` against `close.AccountId` (surfaces the two distinct keys per Phase 1.14a) → call `IDailyCloseWorkflowGuard.EnsureCanEditItems` and capture the returned outcome → `LockDateGuard.EnsureNotLocked(branchId, close.Date, DAILYCLOSE_LOCK_DATE_VIOLATION)` → resolve all `ProductId`s in the payload via `IProductsRepository.ListActiveByIdsAndBranchIdAsNoTracking(productIds, branchId)`; mismatch (cross-branch or inactive) → `404` (no existence leak); resolve `cashVarianceProductId` via `ICashVarianceProductResolver`; if **any** payload `ProductId` matches it, throw `OnValidationException(DAILYCLOSE_ITEM_PRODUCT_FORBIDDEN)` (400) → capture `var now = branchClock.UtcNow();` once → apply auto-transition based on outcome: `EditOnDraft` → no status change; `EditOnRejectedAutoTransitionToDraft` → `Status = Draft`; `EditOnSubmittedRecallToDraft` → `Status = Draft`, `SubmittedAt = null` (the prior `Diferença Caixa` row is **not** deleted) → upsert items: for each payload line, if a matching active item already exists in `close.Items`, mutate its `Value` in-place (tracked entity → UPDATE); otherwise call `await IDailyCloseItemsRepository.Add(new DailyCloseItem { … })` so EF Core registers the entity as `EntityState.Added` and issues INSERT (adding directly to the navigation collection with a client-generated Guid key causes EF Core to issue UPDATE → 0 rows → `DbUpdateConcurrencyException`); for each currently-active item whose `ProductId` is **not** in the payload AND is **not** `cashVarianceProductId`, set `Active = false` (soft delete) → stamp `UpdatedAt = now`, `UpdatedByUserId = userId` → `Commit` → return the rich `ResponseDailyCloseJson`
- [x] **4.7** Add `PUT /dailyclose/{dailyCloseId:guid}/items` endpoint returning `ResponseDailyCloseJson`. Declare `[ProducesResponseType]` for 200/400/401/403/404/409. Register `PutDailyCloseItemsUseCase` in `AppDependencyInjection`
- [x] **4.8** Add `RequestPutDailyCloseItemsJsonBuilder` (and a small `RequestUpsertDailyCloseItemJsonBuilder`) in `tests/CommonTestUtilities/`
- [x] **4.9** Add `Validators.Test` coverage for `PutDailyCloseItemsFluentValidation`: success; null `Items`; empty `ProductId`; negative `Value`; `Value` with more than 14 total digits (precision overflow); `Value` with more than 2 decimal places (scale overflow); duplicate `ProductId` within the payload
- [x] **4.10** Add `UseCases.Test` coverage for `PutDailyCloseItemsUseCase`: insert-only happy path on `Draft`; update-only happy path (preserves persisted ids); mixed insert + update + soft-delete (assert exact `Active = false` on the omitted line and that `Diferença Caixa` is never deactivated even when omitted); auto-transition `Rejected → Draft` with audit pair stamped from the captured clock instant; soft-final recall — auto-transition `Submitted → Draft` for recording-operator Member on same branch-local business day, with `SubmittedAt` cleared and audit pair stamped (assert single captured-instant equality); soft-final recall as Manager/Admin (always allowed); recall denied for Member-other-operator → `409 DAILYCLOSE_NOT_EDITABLE`; recall denied for Member-recording-operator-but-older-day → `409 DAILYCLOSE_NOT_EDITABLE`; `409 DAILYCLOSE_NOT_EDITABLE` on `Approved`; `400 DAILYCLOSE_ITEM_PRODUCT_FORBIDDEN` when payload references the `Diferença Caixa` product id; `404` on cross-branch `DailyClose`; `404` on cross-branch `Product`; `403` Member out-of-scope; `409 DAILYCLOSE_LOCK_DATE_VIOLATION`; delegation assertion `workflowGuard.Received(1).EnsureCanEditItems(...)`; `DidNotReceive().Commit()` on failure paths
- [x] **4.11** Add `WebApi.Test` coverage: 200 happy path on `Draft` with reload-based item set verification (insert/update/soft-delete); 200 happy path on `Rejected` confirming auto-transition to `Draft` and the audit pair; 200 happy path on `Submitted` (recall by recording operator, same day) confirming auto-transition to `Draft`, `SubmittedAt = null`, item set updated, AND the prior `Diferença Caixa` row is preserved unchanged (seed it manually before the call and reload to assert `Active = true` and `Value` unchanged); 200 recall as Manager/Admin on a Member's submitted close; 409 recall denied for Member-other-operator; 409 recall denied on `Submitted` from older day (Member); 200 recall as Manager/Admin on `Submitted` from older day; 200 confirming `Diferença Caixa` row is never affected on non-recall edits; 400 forbidden product key; 409 not-editable on `Approved`; 403 Member out-of-scope; 404 cross-branch close

### Phase 5 — Submit DailyClose (server computes CashVariance)

End-to-end submit path. Workflow guard, member scope, lock-date, the `CashVarianceCalculator` becomes concrete, the `Diferença Caixa` line is upserted, and the close transitions to `Submitted` with all timestamps stamped from one captured clock instant.

- [x] **5.1** No request body; response is `ResponseDailyCloseJson`
- [x] **5.2** Implement `CashVarianceCalculator` (concrete behavior, replacing the Phase 1.18 skeleton's `NotImplementedException`). Internal flow: load `TotalClosing` from `IDailyCloseItemsRepository.ListActiveByDailyCloseIdAsNoTracking(currentDailyCloseId)`, summing `Value` where `ProductId != cashVarianceProductId`; load the most recent prior close via `IDailyClosesRepository.GetMostRecentBeforeDateByBranchIdAndAccountIdAsNoTracking(branchId, accountId, branchLocalDate)` (top-1 ordered `Date DESC`; `null` → `TotalOpening = 0`) and sum its active items where `ProductId != cashVarianceProductId`; load `TotalTransactionsIn = ITransactionsRepository.SumActiveValueByAccountAndDateAsNoTracking(branchId, accountId, branchLocalDate, Direction.In)`, `TotalTransactionsOut` likewise with `Direction.Out`; return `TotalClosing - TotalOpening - (TotalTransactionsIn - TotalTransactionsOut)`. Decimal precision uses standard `decimal` math (inputs are already `numeric(14,2)`)
- [x] **5.3** Add `tests/UseCases.Test/Services/DailyCloses/CashVarianceCalculatorTest.cs`: opening = 0 when no prior close exists for the account; opening from the most recent prior close's items only (excludes the CashVariance product line); prior close from 3 days ago is used when the account skipped the intervening days (proves the most-recent-prior semantic, not literal date-1); transactions sum subtracts `In − Out` correctly; sibling-account isolation — exact-arg `Received(1)` on the transactions-repo helper proves only `accountId` is queried; cancelled/draft transactions excluded by repository contract (mock returns reflect `Active`-only sums); typical mixed positive variance; typical mixed negative variance; CashVariance product exclusion in both `TotalClosing` and `TotalOpening`
- [x] **5.4** Wire `DailyCloseWorkflowGuard.EnsureCanSubmit` (replaces its `NotImplementedException`). Behavior: rejects non-`Draft`/non-`Rejected` with `409 DAILYCLOSE_NOT_SUBMITTABLE`; otherwise applies the role × linked-operator × local-business-day matrix (Member: own-operator + same-day; Manager/Admin: always allowed regardless of date) — same matrix as `TransactionMutationPermissionGuard` from Milestone 3 5.5.5, keyed on `close.Date`
- [x] **5.5** Implement `SubmitDailyCloseUseCase` with the flow: authenticate → load tracked `DailyClose` (with `Items` and `Items.Product` includes from the `GetByIdAndBranchId` tracked variant) → `404` on miss/cross-branch → resolve caller's linked operator → `MemberAccountScopeGuard` → `IDailyCloseWorkflowGuard.EnsureCanSubmit` → `LockDateGuard.EnsureNotLocked(branchId, close.Date, DAILYCLOSE_LOCK_DATE_VIOLATION)` → resolve `cashVarianceProductId` via `ICashVarianceProductResolver` → call `ICashVarianceCalculator.CalculateAsync` → upsert the CashVariance line **through the tracked `close.Items` collection**: locate the existing tracked active item where `ProductId == cashVarianceProductId` and mutate its `Value` in place; otherwise add a new `DailyCloseItem` to the collection (EF tracks the insert from the navigation). The unique-index race on `(DailyCloseId, ProductId)` (filtered on `Active = true`) is translated to `409 DAILYCLOSE_ITEM_DUPLICATE` via the Phase 1.16 translator → re-resolve `SubmittedByOperatorId` from the caller's linked operator at submit time (overwrite the entity field) → capture `var now = branchClock.UtcNow();` → stamp `Status = Submitted`, `SubmittedAt = now`, `UpdatedAt = now`, `UpdatedByUserId = userId` → `Commit` → return rich response
- [x] **5.6** Add `POST /dailyclose/{dailyCloseId:guid}/submit` endpoint returning `ResponseDailyCloseJson`. Declare `[ProducesResponseType]` for 200/401/403/404/409. Register `SubmitDailyCloseUseCase` in `AppDependencyInjection`
- [x] **5.7** Add `UseCases.Test` coverage for `SubmitDailyCloseUseCase`: success from `Draft` adding a new CashVariance item to the tracked collection with the calculator's result; success from `Rejected` mutating the existing tracked CashVariance item's `Value` in place (preserves the persisted `Id`); the captured clock instant is the same value for `SubmittedAt` and `UpdatedAt` (assert reference equality of the captured `DateTime`); `404` cross-branch; `403` Member out-of-scope (both no-linked-operator and linked-but-out-of-scope key variants); `409 DAILYCLOSE_NOT_SUBMITTABLE` on `Submitted`/`Approved`; `409 DAILYCLOSE_LOCK_DATE_VIOLATION`; `403` permission failure through `DailyCloseWorkflowGuard` (Member submitting an older-day close); delegation assertions `workflowGuard.Received(1).EnsureCanSubmit(...)` and `cashVarianceCalculator.Received(1).CalculateAsync(...)`; `DidNotReceive().Commit()` on every failure path; submit re-resolves `SubmittedByOperatorId` from the caller's linked operator (not from the entity's existing value) — assert with a test where the entity's `SubmittedByOperatorId` differs from the caller's current linked operator
- [x] **5.8** Add `WebApi.Test` coverage: 200 happy path with reload-based verification of `Status`, `SubmittedAt`, `UpdatedAt` (single instant), `SubmittedByOperatorId`, and the persisted CashVariance row's `Value`; sibling-account isolation across two seeded accounts; 200 resubmit on `Rejected` mutates the existing CashVariance row in place (assert `Id` is unchanged); 200 resubmit-after-recall (Open → PUT items → Submit → PUT items recall → Submit) likewise updates the existing CashVariance row in place (`Id` unchanged); the most-recent-prior-close opening lookup verified by seeding a 3-days-prior close and asserting opening uses its values (not `0`); 409 lock-date; 409 already-submitted; 403 Member submitting an older-day close (workflow guard rejects); 200 Manager/Admin submitting an older-day close (workflow guard allows)

### Phase 6 — Approve / Reject (manager review)

Manager-only state transitions with `RejectionReason` validation, lock-date enforcement, single-instant audit stamping, and the two end-to-end lifecycle integration tests (reject-cycle and recall-cycle) that pin in-place `Diferença Caixa` update across resubmissions.

- [x] **6.1** Add `RequestRejectDailyCloseJson` with required non-empty `RejectionReason` (max 500). No request body for Approve. Add `RejectDailyCloseFluentValidation`: required `RejectionReason`, max length 500
- [x] **6.2** Wire `DailyCloseWorkflowGuard.EnsureCanApprove` and `DailyCloseWorkflowGuard.EnsureCanReject` (replacing their `NotImplementedException`). Both: Manager/Admin only (Member → `403`); both require `Status == Submitted` — Approve throws `409 DAILYCLOSE_NOT_APPROVABLE` otherwise, Reject throws `409 DAILYCLOSE_NOT_REJECTABLE` otherwise
- [x] **6.3** Implement `ApproveDailyCloseUseCase`: authenticate → load tracked → `404` cross-branch → `IDailyCloseWorkflowGuard.EnsureCanApprove` → `LockDateGuard.EnsureNotLocked(branchId, close.Date, DAILYCLOSE_LOCK_DATE_VIOLATION)` → `var now = branchClock.UtcNow();` → stamp `Status = Approved`, `ApprovedAt = now`, `ApprovedByUserId = userId`, `UpdatedAt = now`, `UpdatedByUserId = userId` → `Commit` → rich response
- [x] **6.4** Implement `RejectDailyCloseUseCase`: validate request → authenticate → load tracked → `404` cross-branch → `IDailyCloseWorkflowGuard.EnsureCanReject` → `LockDateGuard.EnsureNotLocked(branchId, close.Date, DAILYCLOSE_LOCK_DATE_VIOLATION)` → `var now = branchClock.UtcNow();` → stamp `Status = Rejected`, `RejectionReason = request.RejectionReason`, defensively clear `ApprovedAt`/`ApprovedByUserId` (workflow guard does not allow Approved → Rejected, but the data shape stays consistent), `UpdatedAt = now`, `UpdatedByUserId = userId` → `Commit` → rich response
- [x] **6.5** Add `POST /dailyclose/{dailyCloseId:guid}/approve` and `POST /dailyclose/{dailyCloseId:guid}/reject` endpoints. Declare `[ProducesResponseType]` for 200/400 (Reject only)/401/403/404/409. Register `ApproveDailyCloseUseCase` and `RejectDailyCloseUseCase` in `AppDependencyInjection`
- [x] **6.6** Add `RequestRejectDailyCloseJsonBuilder` in `tests/CommonTestUtilities/`
- [x] **6.7** Add `Validators.Test` coverage for `RejectDailyCloseFluentValidation`: success; required `RejectionReason`; length cap at 500
- [x] **6.8** Add `UseCases.Test` coverage for both use cases: success with single-instant audit (assert `ApprovedAt`/`UpdatedAt` and `RejectionReason`/`UpdatedAt` use the same captured `DateTime`); `409` on wrong-state (cannot approve `Draft`/`Approved`/`Rejected`; cannot reject `Draft`/`Rejected`/`Approved`); `403` on Member; `409` on lock-date; `404` on cross-branch; delegation assertions; `DidNotReceive().Commit()` on failure paths; defensive `ApprovedAt`/`ApprovedByUserId` clearing observable on the in-memory entity in Reject
- [x] **6.9** Add `WebApi.Test` coverage: 200 Approve happy path with reload-based verification; 200 Reject happy path likewise; 403 Member on both; 409 wrong-state on both; 409 lock-date on both
- [x] **6.10** Add `WebApi.Test` reject-cycle lifecycle integration: Open → PUT items (Draft) → Submit (Submitted) → Reject with reason "test" (Rejected, `RejectionReason` set) → PUT items (auto-transition to Draft, audit pair stamped, items applied) → Submit (Submitted again, `Diferença Caixa` row updated **in place** — assert its `Id` is the same as after the first Submit) → Approve (Approved, `ApprovedAt`/`ApprovedByUserId` set). Reload at every step
- [x] **6.11** Add `WebApi.Test` recall-cycle lifecycle integration: Open → PUT items (Draft) → Submit (Submitted, `Diferença Caixa` v1) → PUT items as the same Member recording-operator on the same business day (auto-transition Submitted → Draft, `SubmittedAt = null`, items updated, audit pair stamped, prior `Diferença Caixa` v1 unchanged) → Submit (Submitted again, `Diferença Caixa` row updated **in place** to v2 — assert `Id` is the same as v1) → Approve. Reload at every step
- [x] **6.12** Extract shared DailyClose rich-response mapping into `DailyCloseSharedMapper` under the feature root. Keep `OpenDailyCloseMapper` focused on `RequestOpenDailyCloseJson → DailyClose`, and have Get, PutItems, Submit, Approve, Reject, and Open use the shared mapper instead of depending on the Open operation folder for response projection

### Phase 6.5 — Hardening + spec sync close-out

Mirroring Milestone 3 Phase 5.5: a small refactor pass after the user-facing surface is live, instead of letting it accumulate. Skips premature optimization (no caching, no ETag, no projection-DTO splitting) — only the items needed to keep the milestone consistent with the conventions it inherits.

- [x] **6.5.1** Audit `[ProducesResponseType]` declarations on every new `DailyCloseController` action. Each action declares **exactly** the status codes it can return — no missing codes (regression risk against the OpenAPI contract) and no spurious codes (e.g. no `[ProducesResponseType(409)]` on `GET`). The `WebApi.Test` suite for each endpoint hits every declared code at least once across happy/unhappy tests
- [x] **6.5.2** Confirm the architecture test from Milestone 3 5.5.22 picks up every new service under `server.Application/Services/DailyCloses/`, `server.Application/Services/Members/`, and `server.Application/Services/Settings/` (the new home of `LockDateGuard`). If the reflection pattern misses anything, fix the DI registration, not the test
- [x] **6.5.3** Add `tests/UseCases.Test/Services/DailyCloses/DailyCloseWorkflowGuardTest.cs` covering the full state × role × local-business-day matrix in isolation: each `EnsureCan*` method gets one happy case per allowed combination and one failure case per documented denial reason (including same-day boundary behavior across UTC midnight). The `EnsureCanEditItems` matrix is the largest — it must include: Draft × Member-own-operator-same-day; Draft × Member-other-operator (denied); Draft × Manager/Admin; Rejected × Member-own-operator-same-day → outcome `EditOnRejectedAutoTransitionToDraft`; Submitted × Member-own-operator-same-day → outcome `EditOnSubmittedRecallToDraft`; Submitted × Member-other-operator (denied); Submitted × Member-own-operator-OLDER-day (denied); Submitted × Manager/Admin → outcome `EditOnSubmittedRecallToDraft`; Approved × Member (denied); Approved × Manager/Admin (denied). Use named `MemberData` `[Theory]` per the Milestone 3 5.5.12 convention — opaque inline ints fail loudly when test names don't read like the spec
- [x] **6.5.4** Confirm `tests/UseCases.Test/Services/DailyCloses/CashVarianceCalculatorTest.cs` from 5.3 covers the documented edge cases: opening = 0; no transactions; only `In`; only `Out`; mixed `In`/`Out`; sibling-account isolation; large-precision values; the `Diferença Caixa` exclusion in `TotalClosing` and `TotalOpening` (an item with that `ProductId` must not contribute to either sum)
- [x] **6.5.5** Update `loto-specs.md` §6.12 with the explicit Direction handling shipped by Phase 5 (the formula was implicit before this milestone). Confirm the new §6.13 "DailyClose contract" from 1.21 covers the recall transition, the role × state × local-business-day matrix, and the `Diferença Caixa` system-only invariant. Confirm the `loto_presentation.html` workflow narrative includes both the Rejected→Draft and Submitted→Draft recall arrows. Confirm `loto_entity_relationship_diagram.html` has the audit pair on `DailyClose`. Bump the shared `Spec revision` on all three files
- [x] **6.5.6** Run `bash server/docs/check-loto-doc-sync.sh`; must pass before the milestone closes
- [x] **6.5.7** Wire `DailyCloseWorkflowGuard.EnsureCanOpen` explicitly instead of leaving it as a no-op. Preserve the existing Open use-case enforcement order (validation → account branch lookup → `MemberAccountScopeGuard` → workflow guard → lock-date → unique constraint), document which Open rules belong outside the guard (account scope, duplicate close race), and add workflow-guard tests proving Manager/Admin are allowed, Member without a linked operator is rejected, and linked Member happy path still delegates cleanly from `OpenDailyCloseUseCase`

### Done criteria

- All workflow endpoints (`POST /dailyclose`, `GET /dailyclose/{id}`, `GET /dailyclose`, `PUT /dailyclose/{id}/items`, `POST /dailyclose/{id}/submit`, `POST /dailyclose/{id}/approve`, `POST /dailyclose/{id}/reject`) are live and tested end-to-end
- `DailyCloseStatus` enum and `DailyClose` / `DailyCloseItem` entities exist in Domain, Infrastructure, and the Milestone 4 Phase 1 migration
- One `DailyClose` per `(BranchId, AccountId, Date)` and one **active** `DailyCloseItem` per `(DailyCloseId, ProductId)` (filtered unique constraint `WHERE Active = true`, so soft-deleted rows do not block re-insert) — unique-index races translated to clean `409` by `PostgresExceptionHandler`
- `DailyClose` carries the generic mutation audit pair (`UpdatedAt`, `UpdatedByUserId`); every workflow transition (Edit-items, Submit, Approve, Reject, Rejected→Draft auto-transition, AND Submitted→Draft recall auto-transition) stamps the workflow-specific timestamp and the audit pair from the **same** captured `branchClock.UtcNow()` instant
- The soft-final-Submit recall flow is live: a recording-operator Member on the same branch-local business day (or any Manager/Admin) can edit a `Submitted` close, which auto-transitions to `Draft` with `SubmittedAt` cleared while the prior `Diferença Caixa` row stays in place; the next Submit overwrites it. `Approved` remains terminal
- Member account scope is enforced via the renamed shared `IMemberAccountScopeResolver` + `MemberAccountScopeGuard` (now under `server.Application/Services/Members/`); the guard surfaces the two distinct keys `403 TRANSACTION_MEMBER_REQUIRES_OPERATOR_LINK` (no linked operator) and `403 TRANSACTION_MEMBER_ACCOUNT_OUT_OF_SCOPE` (linked but target out of scope) on every read AND write path; the same empty-list contract from Milestone 3 applies — no duplicate keys
- `LockDateGuard` is moved to `server.Application/Services/Settings/` and parameterized with a resource-key argument; M3 transaction callers pass `TRANSACTION_DATE_LOCKED`, M4 DailyClose callers pass `DAILYCLOSE_LOCK_DATE_VIOLATION`
- The Milestone 3 callers (Get/List/Update/Finalize/Cancel/CreateTransaction/CreateTransactionInstallment) all reference the renamed types from Phase 1; the architecture test passes
- Workflow state transitions go through `DailyCloseWorkflowGuard`; use-case tests verify delegation; the full role × state × local-business-day matrix lives in `DailyCloseWorkflowGuardTest`, including the recall outcome variants for `EnsureCanEditItems`
- All same-day comparisons go through `IBranchClock.IsSameLocalDay` / `LocalBusinessDate`; no `DateTime.UtcNow.Date` calls in any DailyClose use case or service
- `CashVariance` is system-computed via `CashVarianceCalculator` using `SumActiveValueByAccountAndDateAsNoTracking` with explicit `Direction` handling; persisted as a `DailyCloseItem` referencing the seeded `"Diferença Caixa"` product (resolved by display name through `ICashVarianceProductResolver`; all C# type names are ASCII); the opening source is the **most recent prior close for the same `(BranchId, AccountId)`** (handles weekend/holiday/missing-prior-day gracefully); updated on resubmission and on submit-after-recall through the tracked `close.Items` collection (in place); never deleted on rejection or recall; never present in client `PUT /items` payloads (operator references rejected with `400 DAILYCLOSE_ITEM_PRODUCT_FORBIDDEN`)
- `PUT /dailyclose/{id}/items` is the single mobile-friendly batch endpoint: insert + update + soft-delete in one call; the system-managed `Diferença Caixa` row is never affected by it, including on the soft-final recall auto-transition
- List response carries `AccountName`, `SubmittedByOperatorName?`, `ApprovedByUserName?` joined names + `TotalCount`/`TotalPages`/`HasNext`/`HasPrevious` paging metadata; single projection query, no N+1
- Lock-date and branch consistency enforced on every workflow transition (Open, Edit-items, Submit, Approve, Reject, Rejected→Draft auto-transition, Submitted→Draft recall auto-transition)
- Spec sync covers §3.14, §3.15, §6.5 (including the recall arrow), §6.6, §6.12 (with explicit Direction handling), and the new §6.13 (including the recall semantics); revision bumped consistently across all three sync-group files; `check-loto-doc-sync.sh` passes
- API-level `DbUpdateException` / PostgreSQL unique-violation translation is extended for `IX_DailyCloses_BranchId_AccountId_Date` and `IX_DailyCloseItems_DailyCloseId_ProductId`
- The project-wide architecture tests (DI registration + auth-intent) pass with the new use cases, services, and `DailyCloseController`
- All three test suites green (`Validators.Test`, `UseCases.Test`, `WebApi.Test`)

---

## Milestone 5 — Time Entry & Holiday

**Goal:** Implement operator attendance tracking with `TotalHours` and `BalanceHours` derived from `Setting`, branch-scoped `Holiday` management, and holiday-aware business-day calculation for transaction due dates and auto-generated cheque installment dates.

**Scope boundary:** `TimeEntry`, `Holiday`, `TimeEntryStatus`, attendance calculation, Holiday CRUD, TimeEntry upsert/read/deactivate flows, and holiday-aware due-date calculation. No reporting dashboards, no payroll export, no time-entry approval workflow, no live clock-event log, and no admin CRUD for `Setting` yet.

**Precondition:** Milestone 4 is fully closed. `Operator`, `Setting`, branch-scoped auth, `IBranchClock`, `MemberAccountScopeResolver`, the M4 `LockDateGuard` location, the audit-stamping convention, architecture tests, and `PostgresExceptionHandler` unique-constraint translation pattern are already available.

**Key behaviors:**

- `TimeEntry` is a daily attendance record, not a punch-event log.
- `TimeEntry` carries the field set from `loto-specs.md` §3.16 plus the generic mutation audit pair `UpdatedAt` and `UpdatedByUserId`.
- `Holiday` carries the field set from `loto-specs.md` §3.17.
- One active `TimeEntry` per `(BranchId, OperatorId, Date)` is enforced by a filtered unique index.
- One active `Holiday` per `(BranchId, Date)` is enforced by a filtered unique index.
- TimeEntry writes use a single `PUT /timeentry` upsert keyed by `(OperatorId, Date)`.
- TimeEntry writes are not blocked by `Setting.LockDate`; LockDate remains a financial and DailyClose cutoff for now.
- `Member` callers may upsert only their linked operator's own entry for the same branch-local business day, and only with `Status = Present`.
- `Manager` and `Admin` may upsert or deactivate any operator's TimeEntry in the branch on any date.
- `Member` read scope is their own linked operator only. A Member without a linked operator gets an empty list for list calls and `403` for targeted get/write calls.
- `Holiday` reads are available to any authenticated branch role. Holiday writes are restricted to `Manager` and `Admin`.
- `TimeEntryCalculationService` computes `TotalHours` and `BalanceHours` server-side from `Status`, clocks, and the branch `Setting`.
- Clients never send `TotalHours` or `BalanceHours`.
- Holiday-aware due-date calculation extends the existing M3 transaction behavior so `NextBusinessDay`, `TwoBusinessDays`, and auto-generated installment dates skip weekends and active branch holidays.
- Manual cheque due dates remain operator-entered and are not auto-moved by the holiday calendar.

### Phase 1 — Foundation

Add the domain, persistence, shared services, resource keys, migration, and spec sync needed before user-facing endpoints.

- [x] **1.1** Add `TimeEntryStatus` enum to `server.Domain/Entities/Enums/` with values from `loto-specs.md`: `Present`, `DayOff`, `Sunday`, `Holiday`, `Vacation`, `JustifiedAbsence`, and `UnjustifiedAbsence`
- [x] **1.2** Add `TimeEntry` entity to `server.Domain` with `Date`, nullable `ClockIn`, nullable `ClockOut`, `Status`, `TotalHours`, `BalanceHours`, `OperatorId`, `BranchId`, `UpdatedAt`, `UpdatedByUserId`, and navigations to `Operator`, `Branch`, and `UpdatedByUser?`
- [x] **1.3** Use `init` setters on `TimeEntry.OperatorId`, `TimeEntry.BranchId`, and `TimeEntry.Date`; keep clocks, status, calculated hours, `Active`, and audit fields settable
- [x] **1.4** Add `Holiday` entity to `server.Domain` with `Date`, nullable `Description`, `BranchId`, and navigation to `Branch`
- [x] **1.5** Use `init` setters on `Holiday.BranchId` and `Holiday.Date`; keep `Description` and `Active` settable
- [x] **1.6** Add `TimeEntryListFilter` domain model with `OperatorId?`, `DateFrom?`, `DateTo?`, `Status?`, `Mine?`, server-resolved `AllowedOperatorIds?`, `Page`, and `PageSize`
- [x] **1.7** Add `HolidayListFilter` domain model with optional `Year`, `DateFrom?`, `DateTo?`, `Page`, and `PageSize`
- [x] **1.8** Add `ITimeEntriesRepository` with `Add`, tracked get by id and branch, no-tracking get by id and branch, tracked get by `(BranchId, OperatorId, Date)`, list/count by branch filter, and active existence helpers needed for uniqueness pre-checks
- [x] **1.9** Add `IHolidaysRepository` with `Add`, tracked get by id and branch, no-tracking get by id and branch, active get by `(BranchId, Date)`, list/count by branch filter, and `ListActiveDatesByBranchIdAsNoTracking`
- [x] **1.10** Add EF Core configurations, `DbSet`s, column types, indexes, and repository implementations for `TimeEntry` and `Holiday`
- [x] **1.11** Enforce filtered unique index `UNIQUE (BranchId, OperatorId, Date) WHERE Active = true` for `TimeEntry`
- [x] **1.12** Enforce filtered unique index `UNIQUE (BranchId, Date) WHERE Active = true` for `Holiday`
- [x] **1.13** Add list-supporting indexes for `TimeEntry` on `(BranchId, Date)` and `(BranchId, OperatorId, Date)`
- [x] **1.14** Add the Phase 1 migration/model snapshot for `TimeEntry`, `Holiday`, and their constraints
- [x] **1.15** Add resource keys for TimeEntry and Holiday validation, not-found, permission, and conflict errors
- [x] **1.16** Extend `PostgresExceptionHandler` to map `IX_TimeEntries_BranchId_OperatorId_Date` to `TIMEENTRY_DATE_CONFLICT`
- [x] **1.17** Extend `PostgresExceptionHandler` to map `IX_Holidays_BranchId_Date` to `HOLIDAY_DATE_CONFLICT`
- [x] **1.18** Implement `ITimeEntryCalculationService` / `TimeEntryCalculationService` under `server.Application/Services/TimeEntries/`
- [x] **1.19** Implement the calculation rules from `loto-specs.md` §6.7, including midnight crossing, lunch tiers, abonado statuses, owing statuses, and boundary behavior at exactly 4h and exactly 6h
- [x] **1.20** Add `IBranchHolidaySource` and `BranchHolidaySource` under application services to read active holiday dates from `IHolidaysRepository`
- [x] **1.21** Refactor `DueDateCalculator` so business-day methods stay synchronous and accept an `IReadOnlySet<DateOnly>` holiday date set while staying deterministic and testable
- [x] **1.22** Register new repositories and application services in DI
- [x] **1.23** Add `CommonTestUtilities` entity builders and repository builders for `TimeEntry` and `Holiday`: `TimeEntryBuilder`, `HolidayBuilder`, `TimeEntriesRepositoryBuilder`, `HolidaysRepositoryBuilder`
- [x] **1.24** Update `loto-specs.md` §3.16, §3.17, §6.3, §6.7, and §6.8 plus `loto_presentation.html` and `loto_entity_relationship_diagram.html` for the new entities, audit fields, constraints, time-entry rules, holiday contract, installment auto-generation wording, and holiday-aware due-date behavior
- [x] **1.25** Bump shared spec sync metadata across all three docs
- [x] **1.26** Run `bash docs/check-loto-doc-sync.sh`

### Phase 2 — Holiday Calendar CRUD Slice

Implement branch-scoped Holiday calendar management first so TimeEntry and due-date behavior have calendar data to consume. Holiday reads are list/filter based; no single `GET /holiday/{id}` endpoint is planned because the list row carries the full Holiday shape. Date checks use `GET /holiday?dateFrom={date}&dateTo={date}`.

- [x] **2.1** Add `RequestCreateHolidaysJson` containing a non-empty list of `RequestCreateHolidayJson` rows, `RequestCreateHolidayJson`, `RequestUpdateHolidayJson`, `RequestListHolidaysJson`, `ResponseHolidayJson`, `ResponseCreateHolidaysJson`, and `ResponseListHolidaysJson`; then add matching `CommonTestUtilities` request builders for create batch, update, and list
- [x] **2.2** Add Holiday validators covering non-empty create batch, required row `Date`, optional `Description` length, duplicate dates inside the same create batch, update description length, paging, and date/year filter shape
- [x] **2.3** Implement `CreateHolidaysUseCase` restricted to `Manager` and `Admin`, treating the batch as all-or-nothing and returning all persisted `ResponseHolidayJson` items
- [x] **2.4** Reject create when any requested date already has an active Holiday in the same branch, and translate `(BranchId, Date)` filtered unique race-condition violations to `409 HOLIDAY_DATE_CONFLICT`
- [x] **2.5** Implement `ListHolidaysUseCase` for any authenticated branch role with optional `Year`, `DateFrom`, `DateTo`, `Page`, and `PageSize`; return `TotalCount`, `TotalPages`, `HasNext`, and `HasPrevious`
- [x] **2.6** Implement `UpdateHolidayUseCase` restricted to `Manager` and `Admin`; update `Description` only, with `Date` immutable after creation
- [x] **2.7** Implement `DeactivateHolidayUseCase` restricted to `Manager` and `Admin`, using soft delete
- [x] **2.8** Add `HolidayController` with explicit per-action `[Route]`, branch auth filters, and audited `[ProducesResponseType]` metadata for `POST /holiday`, `GET /holiday`, `PUT /holiday/{holidayId:guid}`, and `DELETE /holiday/{holidayId:guid}`; do not add a single-item `GET /holiday/{holidayId:guid}`
- [x] **2.9** Register Holiday use cases in Application DI
- [x] **2.10** Add `Validators.Test` coverage for Holiday batch create, update, and list validation
- [x] **2.11** Add `UseCases.Test` coverage for Holiday batch create, list, update, deactivate, duplicate-date conflict, request-internal duplicate dates, role permissions, branch isolation, exact-argument repository assertions, and paging metadata
- [x] **2.12** Add `WebApi.Test` happy-path and unhappy-path coverage for all Holiday endpoints, including `400`, `401`, `403`, `404`, `409`, filtered unique-index race translation, soft-delete recreation, branch isolation, date/year list filters, and pagination metadata over multiple pages

### Phase 3 — TimeEntry Write Path

Implement the mobile-friendly TimeEntry upsert and manager/admin deactivation flow.

- [x] **3.1** Add `RequestUpsertTimeEntryJson` with `OperatorId`, `Date`, `Status`, nullable `ClockIn`, and nullable `ClockOut`; then add `RequestUpsertTimeEntryJsonBuilder` to `CommonTestUtilities`
- [x] **3.2** Add `ResponseTimeEntryJson` with ids, date, clocks, status, calculated hours, operator info, branch id, created/audit fields, and active flag
- [x] **3.3** Add `UpsertTimeEntryFluentValidation` for shape-only validation: non-empty `OperatorId`, non-default `Date`, valid enum status, and clock ordering when both clocks are present
- [x] **3.4** Keep status-relative validation in the use case or calculation service: `Present` requires both clocks; non-`Present` statuses reject clocks
  Note: Refactored in Phase 3.5 — `Present` requires only `ClockIn`; partial entries on the current branch-local business day carry server-computed live-running hours.
- [x] **3.5** Enforce `Sunday` status only when `Date.DayOfWeek == Sunday`
- [x] **3.6** Enforce `Holiday` status only when an active Holiday exists for the branch/date
- [x] **3.7** Add `ITimeEntryWritePermissionGuard` under `Services/TimeEntries/` with explicit outcome enum values: `SelfSameDayPresent`, `Elevated`, `MissingLinkedOperator`, `NotOwnOperator`, `OlderDayMember`, and `MemberNonPresent`
- [x] **3.8** Implement `UpsertTimeEntryUseCase` flow: authenticate branch user, validate request, resolve caller linked operator, validate target operator belongs to branch, authorize, load existing entry by `(BranchId, OperatorId, Date)` tracked, load branch Setting, calculate hours, insert or mutate in place, stamp `UpdatedAt`/`UpdatedByUserId` from one captured clock instant, commit, and map response
- [x] **3.9** Treat `PUT /timeentry` insert and update as `200 OK` with `ResponseTimeEntryJson`
- [x] **3.10** Do not call `LockDateGuard` from TimeEntry write use cases in this milestone
- [x] **3.11** Implement `DeactivateTimeEntryUseCase` restricted to `Manager` and `Admin`, using soft delete and audit stamping
- [x] **3.12** Add `TimeEntryController` routes for `PUT /timeentry` and `DELETE /timeentry/{timeEntryId:guid}` with explicit auth and response metadata
- [x] **3.13** Register TimeEntry write use cases and services in Application DI
- [x] **3.14** Add `Validators.Test` coverage for TimeEntry upsert shape validation
- [x] **3.15** Add isolated `TimeEntryCalculationServiceTest` coverage for all calculation branches, lunch thresholds, exact 4h/6h boundaries, and midnight crossing
- [x] **3.16** Add isolated `TimeEntryWritePermissionGuardTest` coverage for the full role × operator × local-business-day × status matrix
- [x] **3.17** Add `UseCases.Test` coverage for upsert insert, upsert update, calculated values, audit stamping, `Present` validation, Sunday/Holiday validation, branch isolation, duplicate race path, Member self-same-day success, Member older-day failure, Member another-operator failure, Member non-present failure, Manager/Admin elevated success, and deactivation
- [x] **3.18** Add `WebApi.Test` coverage for TimeEntry write/deactivate endpoints, including reload-based persistence assertions, `400`, `401`, `403`, `404`, `409`, branch isolation, and unique-index race translation

### Phase 3.5 — Open-Shift Present Refactor

**Goal:** Replace the "Present requires both clocks" constraint with a press-to-clock-in / press-to-clock-out flow. Partial Present rows (`ClockIn` set, `ClockOut` null) on the current branch-local business day carry server-computed live-running `TotalHours` / `BalanceHours` derived from `(branchLocalNow.TimeOfDay - ClockIn)` with the standard lunch tiers. Closed shifts and forgotten clock-outs from prior days remain stable snapshots.

**Scope boundary:** Refactor of the `PUT /timeentry` upsert path, `TimeEntryCalculationService` (new live-running method), `IBranchClock` (new local-datetime accessor), the use case's status-relative validation, the response DTO, and the spec sync group. No schema migration, no new endpoints, no changes to the permission guard outcomes, no changes to Slice 3.C (Deactivate) territory. The Phase 4 read path will inherit the same live-running pattern (recompute on read).

**Precondition:** Phase 3 is fully landed. Phase 3.5 amends Phase 3 item 3.4 (Present clock requirement)..

**Key behaviors:**

- For `Status = Present`, `ClockIn` is required and `ClockOut` is optional.
- When `ClockOut` is null on a Present entry whose `Date` equals the current branch-local business day, the calc service returns `(branchLocalNow.TimeOfDay - ClockIn)` with the standard lunch-tier rules. The PUT response carries those values; the persisted row stores the same as a "last-write checkpoint".
- When `ClockOut` is null on a Present entry whose `Date` is in the past (forgotten clock-out), the calc service returns `(0m, 0m)`. The row is flagged via `IsInProgress = true` for manager review.
- When both clocks are set, calculation is unchanged and the row is a stable snapshot forever.
- Submitting `ClockOut` without `ClockIn` is rejected as `400 TIMEENTRY_CLOCK_OUT_REQUIRES_CLOCK_IN` regardless of status.
- For non-Present statuses, both clocks must be null (unchanged).
- The PUT body is authoritative: the second tap re-states both clocks. Reopening a completed shift by sending `ClockOut = null` is allowed subject to the existing permission guard (no new role check).
- The validator's clock-equality and shape rules are unchanged. The `Sunday` and `Holiday` calendar rules are unchanged.
- The response DTO gains a server-computed `IsInProgress` flag.
- The Phase 4 read path will recompute live-running values on every read; the persisted `TotalHours` / `BalanceHours` for a partial Present row may be stale by the time it's read.

**Slice plan:** items 3.5.1 – 3.5.6 land in Slice 3.5.A (foundational, no client-visible behavior change). Items 3.5.7 – 3.5.14 land in Slice 3.5.B (contract change closes the phase).

- [x] **3.5.1** Update `server/docs/loto-specs.md` §6.7 to define partial-Present semantics and the live-running rule. `ClockIn` required for Present; `ClockOut` optional. `ClockOut` null + `Date == branchLocalToday` → live-running with synthetic `effectiveClockOut = branchLocalNow.TimeOfDay`. `ClockOut` null + `Date < branchLocalToday` → `(TotalHours, BalanceHours) = (0m, 0m)`. `ClockOut` set + `ClockIn` null → invalid (use case rejects). Update `server/docs/loto_presentation.html` and `server/docs/loto_entity_relationship_diagram.html` to mirror §6.7. Bump shared sync metadata across all three files. Run `bash server/docs/check-loto-doc-sync.sh` and confirm green.
- [x] **3.5.2** Add `DateTime LocalBusinessDateTime(DateTime utcInstant)` to `IBranchClock` and implement in `BranchClock`. Returns the full São Paulo-local `DateTime` (not truncated). Refactor `LocalBusinessDate` to call `LocalBusinessDateTime` and return `.Date`. Behavior of `LocalBusinessDate` and `IsSameLocalDay` must be byte-for-byte unchanged.
- [x] **3.5.3** Add `BranchClockTest` coverage for `LocalBusinessDateTime`: returns the full local `DateTime` for representative UTC instants. Existing `LocalBusinessDate` and `IsSameLocalDay` cases stay untouched.
- [x] **3.5.4** Add `(decimal TotalHours, decimal BalanceHours) CalculateLiveRunning(TimeOnly clockIn, DateTime entryDate, DateTime branchLocalNow, decimal dailyTargetHours, decimal lunchDeductionOver6H, decimal lunchDeductionOver4H)` to `ITimeEntryCalculationService` and implement in `TimeEntryCalculationService`. Behavior:
  - If `branchLocalNow.Date != entryDate.Date` (forgotten clock-out from a prior day): return `(0m, 0m)`.
  - If `effectiveClockOut < clockIn` on the same calendar day (clock drift, NTP skew, or Manager-entered future ClockIn): return `(0m, -dailyTargetHours)` — zero work elapsed, full target owed. Uses strict `<` so the just-clocked-in case (`effectiveClockOut == clockIn`) still flows through `CalculatePresent` and returns `(0, -dailyTargetHours)` via the normal zero-span path.
  - Otherwise: synthesize `effectiveClockOut = TimeOnly.FromDateTime(branchLocalNow)` and apply the existing CalculatePresent math (including midnight-crossing safety and lunch tiers).
- [x] **3.5.5** Add `TimeEntryCalculationServiceTest` coverage for `CalculateLiveRunning`: just-clocked-in (gross 0, balance = -DailyTarget), gross < 4h (no lunch), gross > 4h and ≤ 6h (Over4H), gross > 6h (Over6H), forgotten-clock-out previous day → `(0, 0)`, same-day `effectiveClockOut < clockIn` (clock drift) → `(0, -DailyTarget)`. Existing `Calculate` cases stay untouched.
- [x] **3.5.6** Add resource key `TIMEENTRY_CLOCK_OUT_REQUIRES_CLOCK_IN` to `server.Exceptions/ResourcesErrorMessages.resx` and `ResourcesErrorMessages.Designer.cs`. Text: "Não é possível registrar horário de saída sem horário de entrada".
- [x] **3.5.7** Add `bool IsInProgress { get; init; }` to `ResponseTimeEntryJson`.
- [x] **3.5.8** Update `UpsertTimeEntryMapper.ToResponse` to compute `IsInProgress = timeEntry.Status == TimeEntryStatus.Present && timeEntry.ClockIn is not null && timeEntry.ClockOut is null`.
- [x] **3.5.9** Rename resource key `TIMEENTRY_PRESENT_REQUIRES_BOTH_CLOCKS` → `TIMEENTRY_PRESENT_REQUIRES_CLOCK_IN` in resx and designer. Update Portuguese text to "O status Presente exige horário de entrada".
- [x] **3.5.10** Update `UpsertTimeEntryUseCase.EnsureStatusRelativeRulesAsync`:
  - First, for any status, throw `OnValidationException` with `TIMEENTRY_CLOCK_OUT_REQUIRES_CLOCK_IN` if `ClockIn == null && ClockOut != null`.
  - For `Status == Present`, throw `OnValidationException` with `TIMEENTRY_PRESENT_REQUIRES_CLOCK_IN` if `ClockIn == null`. `ClockOut` may be null.
  - For non-Present statuses, throw `OnValidationException` with `TIMEENTRY_NON_PRESENT_REJECTS_CLOCKS` if either clock is set (unchanged).
  - Sunday and Holiday calendar checks run after, unchanged.

  Update `UpsertTimeEntryUseCase.Execute` to branch the calc call:
  - If `Status == Present && ClockOut == null`: call `calculationService.CalculateLiveRunning(ClockIn.Value, request.Date, branchClock.LocalBusinessDateTime(branchClock.UtcNow()), DailyTargetHours, LunchDeductionOver6H, LunchDeductionOver4H)`.
  - Else: call `calculationService.Calculate(...)` as today.
- [x] **3.5.11** Update `UpsertTimeEntryUseCaseTest`:
  - Re-key the existing both-clocks-null Present failure test from `TIMEENTRY_PRESENT_REQUIRES_BOTH_CLOCKS` to `TIMEENTRY_PRESENT_REQUIRES_CLOCK_IN`. Rename to `Execute_ShouldThrowOnValidation_WhenPresentMissingClockIn`.
  - Add `Execute_ShouldSucceed_WhenPresentWithClockInOnly`: partial Present, Manager role; assert `IsInProgress == true`, `Add` received once with `ClockOut == null`, exact-arg assertion that `CalculateLiveRunning` was invoked once with the request's `ClockIn`/`Date`/`branchClock.LocalBusinessDateTime(now)`/setting values; `Calculate` not invoked.
  - Add `Execute_ShouldThrowOnValidation_WhenClockOutSuppliedWithoutClockIn`: ClockIn = null, ClockOut = 17:00, any status; assert `TIMEENTRY_CLOCK_OUT_REQUIRES_CLOCK_IN`, `Commit` not received.
  - Add `Execute_ShouldUpdateInPlaceAndComputeFinalHours_WhenSecondTapClocksOut`: seed an existing partial Present row, run the upsert with both clocks; assert same `Id`, final `TotalHours`/`BalanceHours` from `Calculate`, `IsInProgress == false`, `Commit` received once, `Add` not received, `CalculateLiveRunning` not invoked.
- [x] **3.5.12** Update `TimeEntryControllerUpsertHappyPathTest`:
  - Add `Upsert_ShouldReturn200WithIsInProgressTrue_WhenManagerSendsClockInOnly`: partial Present, asserts `IsInProgress == true` and reload-based persistence with `ClockOut == null`.
  - Add `Upsert_ShouldComputeFinalHoursAndIsInProgressFalse_WhenSecondTapCompletesShift`: first PUT with ClockIn only, second PUT with both clocks, assert same id between responses, second response `IsInProgress == false` and `TotalHours` computed via the standard `Calculate`, reload-based confirmation.
- [x] **3.5.13** Update `TimeEntryControllerUpsertUnhappyPathTest`:
  - Re-key + rename `Upsert_ShouldReturn400_WhenPresentMissingClocks` → `Upsert_ShouldReturn400_WhenPresentMissingClockIn` with `TIMEENTRY_PRESENT_REQUIRES_CLOCK_IN`.
  - Add `Upsert_ShouldReturn400_WhenClockOutSuppliedWithoutClockIn` asserting `TIMEENTRY_CLOCK_OUT_REQUIRES_CLOCK_IN`.
- [x] **3.5.14** Mark Phase 3.5 items 3.5.1 – 3.5.13 complete in this file. Run `dotnet test tests/Validators.Test`, `dotnet test tests/UseCases.Test`, `dotnet test tests/WebApi.Test`. All green.

#### Done criteria

- `PUT /timeentry` with `Status = Present`, `ClockIn` set, `ClockOut` null on the current branch-local business day returns `200 OK` with `IsInProgress = true` and `TotalHours` / `BalanceHours` computed via `CalculateLiveRunning`.
- A second `PUT` to the same `(BranchId, OperatorId, Date)` with both clocks updates the row in place; `IsInProgress = false` and hours computed from the standard `Calculate`.
- `PUT` with `ClockOut` set and `ClockIn` null returns `400 TIMEENTRY_CLOCK_OUT_REQUIRES_CLOCK_IN`.
- `IBranchClock` exposes `LocalBusinessDateTime`; `LocalBusinessDate` and `IsSameLocalDay` semantics are unchanged.
- `TimeEntryCalculationService.CalculateLiveRunning` returns `(0, 0)` for forgotten clock-outs from prior days, and `(0, -dailyTargetHours)` when `effectiveClockOut < clockIn` on the same calendar day (defensive guard against clock drift or Manager-entered future ClockIn).
- All three test suites pass; `bash server/docs/check-loto-doc-sync.sh` passes.

### Phase 4 — TimeEntry Read Path

Implement get/list with role-aware operator scope, pagination, and the Phase 3.5 read-time live-running recompute. The persisted `TotalHours` / `BalanceHours` for an in-progress current-day partial Present row are a "last-write checkpoint" that may be stale; the read path must recompute them on every call so the API is the source of truth for live values.

**Read-time live-running recompute (used by both Get and List):** when a row satisfies `Status == Present && ClockIn != null && ClockOut == null && Date == branchLocalToday`, the mapper replaces `TotalHours` / `BalanceHours` with `ITimeEntryCalculationService.CalculateLiveRunning(ClockIn, Date, branchClock.LocalBusinessDateTime(branchClock.UtcNow()), DailyTargetHours, LunchDeductionOver6H, LunchDeductionOver4H)`. Closed shifts (both clocks set), non-Present statuses, and prior-day partials (forgotten clock-outs) pass through unchanged. `IsInProgress` always mirrors the row's underlying state and is computed by the mapper, not read from storage.

- [ ] **4.1** Add `RequestListTimeEntriesJson` with `OperatorId?`, `DateFrom?`, `DateTo?`, `Status?`, `Mine?`, `Page`, and `PageSize`
- [ ] **4.2** Add lightweight list item response carrying `TimeEntry` summary, `OperatorName`, and the `IsInProgress` flag introduced in Phase 3.5. The summary's `TotalHours` / `BalanceHours` reflect the read-time live-running recompute described above and in 4.5 / 4.6, not the persisted snapshot.
- [ ] **4.3** Add `ResponseListTimeEntriesJson` envelope with `Items`, `TotalCount`, `TotalPages`, `HasNext`, and `HasPrevious`
- [ ] **4.4** Add `ListTimeEntriesFluentValidation` covering paging bounds, `DateFrom <= DateTo`, enum validity, and `Mine = true` plus explicit `OperatorId` mutex
- [ ] **4.5** Implement `GetTimeEntryUseCase`: missing/cross-branch id returns `404`; Member without linked operator returns `403 TIMEENTRY_REQUIRES_OPERATOR_LINK`; Member targeting another operator returns `403 TIMEENTRY_NOT_OWN_OPERATOR`; Manager/Admin may read any branch entry. The response applies the read-time live-running recompute: load the branch `Setting` once, capture `branchClock.LocalBusinessDateTime(branchClock.UtcNow())` once, and pass both to the shared mapper helper from 4.6.
- [ ] **4.6** Implement `ListTimeEntriesUseCase` with server-resolved Member operator scope. The use case loads the branch `Setting` once and captures `branchClock.LocalBusinessDateTime(branchClock.UtcNow())` once, then applies the same read-time live-running recompute to every projected row through a shared mapper helper (e.g., `TimeEntry.ToReadResponse(operatorName, setting, branchLocalNow, calculationService)`) so Get and List share exactly one recompute path.
- [ ] **4.7** For Member list with no linked operator, return an empty page without calling repository list/count
- [ ] **4.8** For `Mine = true`, resolve the caller's linked operator and set the server-side operator filter; combining `Mine = true` and explicit `OperatorId` returns `400`; for a Member with no linked operator, `Mine = true` falls through to the empty-scope short-circuit from 4.7 with no repository list/count call
- [ ] **4.9** Ensure repository list uses a single branch-scoped projection with deterministic ordering `Date DESC, CreatedAt DESC, Id DESC`
- [ ] **4.10** Add `GET /timeentry/{timeEntryId:guid}` and `GET /timeentry` to `TimeEntryController`
- [ ] **4.11** Register read use cases in Application DI
- [ ] **4.12** Add `Validators.Test` coverage for list validation
- [ ] **4.13** Add `UseCases.Test` coverage for get/list permissions, Member empty-scope short-circuit, `Mine` behavior, branch isolation, deterministic filter mapping, and pagination metadata. Include read-time live-running coverage: an in-progress current-day partial Present surfaces live-recomputed hours and `IsInProgress = true`; a forgotten-clock-out prior-day partial Present surfaces `(0, 0)` and `IsInProgress = true`; a closed shift surfaces stable persisted hours and `IsInProgress = false`. Assert the same mapper helper is invoked from both Get and List paths (same `Setting` and same `branchLocalNow` per request).
- [ ] **4.14** Add `WebApi.Test` happy-path and unhappy-path coverage for get/list, including Member scope, Manager/Admin visibility, empty-scope response, pagination metadata over multiple pages, `401`, `403`, and `404`. Include reload-vs-response divergence assertions for in-progress current-day partial Present rows: the row's persisted `TotalHours` may be stale (e.g., zero, written at clock-in time), but the read response carries the live-recomputed value; closed shifts and prior-day partials must match between persisted and response values.

### Phase 5 — Holiday-Aware Due Dates

Apply the branch holiday calendar to the existing transaction due-date behavior.

- [ ] **5.1** Update `CreateTransactionUseCase` / transaction create preamble to load active branch holidays once and pass them into due-date calculation
- [ ] **5.2** Update `CreateTransactionInstallmentUseCase` / `InstallmentPlanBuilder` to load active branch holidays once and pass them into generated installment due-date adjustment
- [ ] **5.3** Preserve manual `OperatorEnteredCheque` due-date behavior: explicit manual due dates are validated but not auto-moved
- [ ] **5.4** Mechanically update existing M3 due-date tests that assumed weekend-only behavior to pass an empty holiday set; this is test plumbing, not a redesign of those cases
- [ ] **5.5** Add tests proving `NextBusinessDay` skips a branch holiday
- [ ] **5.6** Add tests proving `TwoBusinessDays` skips one or more branch holidays
- [ ] **5.7** Add tests proving auto-generated installment due dates that land on holidays move to the next non-weekend, non-holiday business day
- [ ] **5.8** Add Web API integration coverage proving holiday-aware due dates persist through transaction create and installment create endpoints

### Phase 6 — Hardening + Spec Close-Out

Close the milestone with the same discipline used in M3 and M4.

- [ ] **6.1** Audit `[ProducesResponseType]` declarations on every `HolidayController` and `TimeEntryController` action; remove spurious codes and add missing ones
- [ ] **6.2** Confirm architecture tests resolve every new use case and public service under `Services/TimeEntries/`, `Services/Holidays/`, and holiday/calendar support
- [ ] **6.3** Confirm validator, use-case, Web API, and architecture tests cover every new endpoint and service
- [ ] **6.4** Run `dotnet test tests/Validators.Test`
- [ ] **6.5** Run `dotnet test tests/UseCases.Test`
- [ ] **6.6** Run `dotnet test tests/WebApi.Test`
- [ ] **6.7** Run `bash docs/check-loto-doc-sync.sh`
- [ ] **6.8** Update this milestone checklist with completion notes where implementation intentionally differs from the original plan

### Done criteria

- `TimeEntryStatus`, `TimeEntry`, and `Holiday` exist in Domain, Infrastructure, and the Milestone 5 migration/model snapshot
- `TimeEntry` enforces one active row per `(BranchId, OperatorId, Date)` through a filtered unique index
- `Holiday` enforces one active row per `(BranchId, Date)` through a filtered unique index
- API-level PostgreSQL unique-violation translation returns clean `409` responses for TimeEntry and Holiday uniqueness races
- `TimeEntry` carries `UpdatedAt` and `UpdatedByUserId`; successful TimeEntry upsert and deactivate stamp the audit pair consistently
- `PUT /timeentry` is the only TimeEntry write/upsert path and computes `TotalHours` / `BalanceHours` server-side
- For `Status = Present`, `ClockIn` is required and `ClockOut` is optional (Phase 3.5). Submitting `ClockOut` without `ClockIn` returns `400 TIMEENTRY_CLOCK_OUT_REQUIRES_CLOCK_IN`. Non-Present statuses still reject both clocks.
- Partial Present rows on the current branch-local business day carry server-computed live-running `TotalHours` / `BalanceHours` via `ITimeEntryCalculationService.CalculateLiveRunning`. Closed shifts (both clocks set) and prior-day partials (forgotten clock-outs, returning `(0, 0)`) are stable snapshots.
- Response DTOs include the server-computed `IsInProgress` flag (`true` iff `Status == Present && ClockIn != null && ClockOut == null`)
- TimeEntry read endpoints (`GET /timeentry/{id}` and `GET /timeentry`) recompute live-running values on every read using `IBranchClock.LocalBusinessDateTime` and `CalculateLiveRunning` through a shared mapper helper; `Setting` and `branchLocalNow` are captured once per request and reused across all rows in a list page
- Members can upsert only their linked operator's same-day `Present` entry — partial (`ClockIn`-only, in-progress) or complete (both clocks). Manager/Admin can upsert or deactivate any branch operator's entry on any date.
- TimeEntry writes are not blocked by `Setting.LockDate`
- TimeEntry read/list scope follows the agreed role rules: Members see only their linked operator; Manager/Admin see all branch entries; missing/cross-branch ids stay `404`
- Holiday CRUD is live: any branch role can read; only Manager/Admin can create/update/deactivate
- Holiday dates are immutable after creation; moving a holiday is handled by deactivate + create
- Holiday-aware due-date calculation is live for transaction create and auto-generated installment rows, skipping weekends and active branch holidays
- Manual cheque due dates remain explicit operator input and are not auto-adjusted by the holiday calendar
- Spec sync covers TimeEntry audit fields, Holiday constraints, TimeEntry calculation (including the Phase 3.5 open-shift Present and live-running rules), TimeEntry permission/read/write contract, Holiday contract, and holiday-aware due-date behavior; shared revision metadata is bumped and `check-loto-doc-sync.sh` passes
- Validator, use-case, Web API, and architecture tests pass with the new TimeEntry, Holiday, and due-date behavior

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
- Add a manager-facing open-cheque aging view grouped by `OriginTransactionId`, with per-group outstanding totals and age buckets, so multi-row cheque plans can be reviewed as one receivable group instead of isolated rows
- Add a non-persisting `POST /transaction/installment/preview` endpoint for operator confirmation before persisting cheque installments. It must reuse the exact same installment planner as create and return the computed rows without `AddRange` / `Commit`
- If bounced cheques become operationally important, handle them as a dedicated transaction-lifecycle concern (for example, `TransactionStatus.Bounced` or `BouncedAt`) rather than overloading `CancellationReason`, because bounced receivables must remain outstanding in totals and aging views
- If audit/legal requirements need to distinguish operator-entered versus auto-generated installment plans, persist that provenance as structured transaction metadata, ideally on the origin row, instead of encoding it only in `Description`
- Cash-variance summaries across accounts and date ranges
- Operator transaction summaries by date
- Monthly reconciliation views for manager review before lock-date advancement
- Add a manager-facing, read-only transaction edit impact preview endpoint for sensitive fields such as `DueDate`, `PaidAt`, and `ClientId`. It must reuse the real validation/permission path and return calculated effects on receivables, aging, and daily reconciliation without persisting changes
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
