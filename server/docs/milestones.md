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

- [ ] **2.1** Update `TargetFramework` from `net8.0` to `net10.0` in all 6 `.csproj` files
- [ ] **2.2** Bump `Microsoft.EntityFrameworkCore` packages to 10.x in `server.Infrastructure`
- [ ] **2.3** Bump `Npgsql.EntityFrameworkCore.PostgreSQL` to 10.x in `server.Infrastructure`
- [ ] **2.4** Bump `System.IdentityModel.Tokens.Jwt` to latest stable in `server.Infrastructure`
- [ ] **2.5** Bump `FluentValidation` to latest stable in `server.Application`
- [ ] **2.6** Bump `BCrypt.Net-Next` to latest stable in `server.Application`
- [ ] **2.7** Replace `Swashbuckle.AspNetCore` with built-in `AddOpenApi()` + `MapOpenApi()` in `server.API`; update `Program.cs` and `ApiDependencyInjection`; ensure bearer security scheme is included in generated document
- [ ] **2.8** Remove `Microsoft.VisualStudio.Web.CodeGeneration.Design` from `server.API` (unused scaffolding package)
- [ ] **2.9** Add `public partial class Program { }` at the end of `Program.cs` (required for `WebApi.Test` integration testing)
- [ ] **2.10** Add `InternalsVisibleTo("WebApi.Test")` to `server.Infrastructure` so `ServerDbContext` is accessible to integration tests
- [ ] **2.11** Update `launchSettings.json` to replace `"launchUrl": "swagger"` with the correct route for the chosen interactive API explorer
- [ ] **2.12** Create fresh initial migration: `dotnet ef migrations add InitialCreate`
- [ ] **2.13** Verify the solution builds and the OpenAPI document is generated and accessible at `/openapi/v1.json`

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

## Milestone 1 — (upcoming)

> PRD-driven feature milestones will be defined here after Ground Zero is complete.
> Expected first features: Branch + BranchUser multi-tenancy, then Account + Operator setup.
