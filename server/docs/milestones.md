# LottoGest — Backend Milestones

> **Status:** Active
> **Started:** 2026-04-06
> **Approach:** Use-case-driven development with test pyramid

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
- [ ] **3.10** Verify local boot works with `Development` and `loto_dev_local`
- [ ] **3.11** Verify the User auth endpoints (register, login, renew-token) respond correctly against the local Development database

### Phase 4 — Test Infrastructure

Scaffold the test projects per AGENTS.md test strategy (empty but wired).

- [ ] **4.1** Create `tests/CommonTestUtilities` class library; add references to Application, Communication, Infrastructure; add Bogus + NSubstitute packages
- [ ] **4.2** Create `tests/Validators.Test` xUnit project; add references to Application, CommonTestUtilities; add Shouldly + xUnit packages
- [ ] **4.3** Create `tests/UseCases.Test` xUnit project; add references to Application, CommonTestUtilities; add Shouldly + xUnit packages
- [ ] **4.4** Create `tests/WebApi.Test` xUnit project; add references to API, CommonTestUtilities; add Shouldly + Testcontainers.PostgreSql + Microsoft.AspNetCore.Mvc.Testing + xUnit packages
- [ ] **4.5** Add all 4 test projects to `server.sln`
- [ ] **4.6** Verify `dotnet build` succeeds for the full solution including test projects
- [ ] **4.7** Verify `dotnet test` runs (even if zero tests exist yet)

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

**Scope boundary:** This milestone includes only `Branch`, `BranchUser`, branch-scoped auth/session, membership management for already-registered users, and the default branch bootstrap seeds from `loto-specs.md` section 5. Invitations and email delivery are explicitly deferred.

**Precondition:** Milestone 0 remains the foundation. In practice, Phase 4 test-project scaffolding from Ground Zero should be completed before the first Milestone 1 tests are written.

---

### Phase 1 — Domain and Authorization Model

Replace the remaining global-role assumptions with the branch-scoped tenancy model described in the spec.

- [ ] **1.1** Add `Branch` entity to `server.Domain` with `Name`, optional `Cnpj`, optional `Address`, optional `Phone`, and navigation to `BranchUsers`
- [ ] **1.2** Add `BranchUser` entity to `server.Domain` with `UserId`, `BranchId`, `Role`, and `Active`
- [ ] **1.3** Remove `Role` from `User`; keep `User` as global authentication identity only
- [ ] **1.4** Update `User` and `RefreshToken` navigations so the auth model still maps cleanly after the `Role` removal
- [ ] **1.5** Add the required repository interfaces for `Branch` and `BranchUser` to `server.Domain/Interfaces/`
- [ ] **1.6** Add the required EF Core mappings, `DbSet`s, and relational constraints for `Branch` and `BranchUser` in `server.Infrastructure`
- [ ] **1.7** Enforce active-membership uniqueness on `(UserId, BranchId)` for `BranchUser`
- [ ] **1.8** Add a branch-scoped token/session model that can carry `UserId`, `BranchId`, `BranchUserId`, and `Role`
- [ ] **1.9** Update token validation/authentication services so they can distinguish global user auth from branch-scoped auth
- [ ] **1.10** Update authorization filters/attributes so branch-protected endpoints authorize against `BranchUser.Role`, not `User.Role`
- [ ] **1.11** Preserve the existing global auth flow for `register`, `login`, and `renew-token`
- [ ] **1.12** Add regression coverage to prove a global token cannot satisfy branch-only authorization

### Phase 2 — Branch Bootstrap Flow

Implement the tenant-creation and branch-session flows that unlock later milestones.

- [ ] **2.1** Add `CreateBranch` request/response DTOs to `server.Communication`
- [ ] **2.2** Add `CreateBranch` validator covering required `Name` and optional-field length limits
- [ ] **2.3** Implement `CreateBranchUseCase` to create the `Branch`, create the creator's `BranchUser` as `Admin`, and commit atomically
- [ ] **2.4** Seed the new branch with the default `Category` rows defined in `loto-specs.md` section 5
- [ ] **2.5** Seed the new branch with the default `TransactionType` rows defined in `loto-specs.md` section 5
- [ ] **2.6** Seed the new branch with the default `Product` rows defined in `loto-specs.md` section 5
- [ ] **2.7** Seed the new branch with the default `Setting` row defined in `loto-specs.md`
- [ ] **2.8** Add `ListMyBranches` request/response contract and use case to return the authenticated user's active branch memberships with branch summary + caller role
- [ ] **2.9** Add `CreateBranchSession` request/response contract and use case so a user can select one of their branches and receive a branch-scoped token
- [ ] **2.10** Reject branch-session creation when the authenticated user is not an active member of the requested branch
- [ ] **2.11** Add `GetCurrentBranchSummary` response contract and use case to resolve the current branch from the branch-scoped token
- [ ] **2.12** Add the corresponding `BranchController` endpoints for create/list/session/current-branch summary

### Phase 3 — Branch Membership Management

Implement branch-member administration without introducing invitations yet.

- [ ] **3.1** Limit onboarding in this milestone to users that already exist in `User`
- [ ] **3.2** Add `ListBranchUsers` response contract and use case to return active members from the current branch only
- [ ] **3.3** Add `AddBranchUser` request/response DTOs using an existing registered user identifier or email plus the target branch role
- [ ] **3.4** Add `AddBranchUser` validator covering required user identifier/email, required role, valid role enum, and email format when email is used
- [ ] **3.5** Implement `AddBranchUserUseCase` so `Admin` can add `Admin`/`Manager`/`Member`, while `Manager` can add only `Manager`/`Member`
- [ ] **3.6** Reject `AddBranchUser` when the target user does not exist or already has an active membership in the branch
- [ ] **3.7** Add `UpdateBranchUserRole` request/response DTOs and validator
- [ ] **3.8** Implement `UpdateBranchUserRoleUseCase` so `Admin` can manage any membership role, while `Manager` can manage only `Manager`/`Member`
- [ ] **3.9** Reject any role update that would leave the branch without at least one active `Admin`
- [ ] **3.10** Add `RemoveBranchUser` request/response contract
- [ ] **3.11** Implement `RemoveBranchUserUseCase` as soft deactivation (`Active = false`), not hard delete
- [ ] **3.12** Allow `Manager` to remove only `Manager`/`Member`; allow `Admin` to remove any non-last-admin membership
- [ ] **3.13** Reject any removal that would leave the branch without at least one active `Admin`
- [ ] **3.14** Add the corresponding branch-membership endpoints to `BranchController` or a dedicated `BranchUserController`

### Phase 4 — Tests for the Tenancy Slice

Write tests for validators, use cases, and API behavior as part of the milestone, not as a follow-up.

- [ ] **4.1** Ensure the Milestone 0 test-project scaffold exists before adding Milestone 1 tests
- [ ] **4.2** Add `Validators.Test` coverage for `CreateBranch`, `CreateBranchSession`, `AddBranchUser`, and `UpdateBranchUserRole`
- [ ] **4.3** Add `UseCases.Test` coverage for `CreateBranchUseCase`
- [ ] **4.4** In `CreateBranchUseCase` tests, assert branch creation, creator membership as `Admin`, exact default seeds from spec section 5, and atomic rollback on failure
- [ ] **4.5** Add `UseCases.Test` coverage for `ListMyBranchesUseCase`, `CreateBranchSessionUseCase`, and `GetCurrentBranchSummaryUseCase`
- [ ] **4.6** Add `UseCases.Test` coverage for `ListBranchUsersUseCase`, `AddBranchUserUseCase`, `UpdateBranchUserRoleUseCase`, and `RemoveBranchUserUseCase`
- [ ] **4.7** Add use-case tests for permission rules: `Manager` may manage only `Manager`/`Member`; `Admin` may manage all memberships
- [ ] **4.8** Add use-case tests for the "must retain one active Admin" invariant
- [ ] **4.9** Add use-case tests proving branch isolation: no membership read/write may target another branch through a valid token
- [ ] **4.10** Add global-auth regression tests proving `register`, `login`, and `renew-token` still work after `User.Role` removal
- [ ] **4.11** Add `WebApi.Test` happy-path coverage for all Milestone 1 endpoints
- [ ] **4.12** Add `WebApi.Test` coverage for `401` unauthenticated, `403` unauthorized by branch role, `404` missing entity in branch scope, and `409` membership conflicts / last-admin violations
- [ ] **4.13** Add `WebApi.Test` coverage proving a global token is rejected by branch-only endpoints

### Done criteria

- `User` no longer carries a `Role`; branch authorization is driven by `BranchUser.Role`
- `Branch` and `BranchUser` exist in Domain, Infrastructure, and the initial migration/model snapshot
- `BranchUser` enforces one active membership per `(UserId, BranchId)`
- Branch-scoped tokens carry enough data to resolve `UserId`, `BranchId`, `BranchUserId`, and `Role`
- `POST /User/register`, `POST /User/login`, and `POST /User/renew-token` still work with the new auth model
- There is a branch-selection/session endpoint that turns a valid global session into a branch-scoped session
- `CreateBranch` creates the branch, the creator membership as `Admin`, and the default `Category`, `TransactionType`, `Product`, and `Setting` seeds
- Membership management is limited to already-registered users; no invitation or email flow exists yet
- The branch always retains at least one active `Admin`
- `Manager` can manage only `Manager` and `Member` memberships; `Admin` can manage all memberships
- Validator, use-case, and Web API tests exist for the Milestone 1 flows and permissions
- Global tokens are rejected by branch-only endpoints
