# Server Agent Notes

This file is the only source of truth for work inside `server/`.

Use it together with the repo root [AGENTS.md](../AGENTS.md).

## Stack

| Item               | Value                                                         |
|--------------------|---------------------------------------------------------------|
| Target framework   | .NET 10 LTS                                                   |
| Language           | C# 13                                                         |
| Database           | PostgreSQL                                                    |
| ORM                | Entity Framework Core 10                                      |
| Architecture       | Clean Architecture + DDD                                      |
| API description    | Built-in OpenAPI in .NET 10                                   |
| Interactive API UI | Swagger UI by default, Scalar allowed if chosen intentionally |

## Naming map

| Concern                    | Rule                                                                                                                                            | Example                                               |
|----------------------------|-------------------------------------------------------------------------------------------------------------------------------------------------|-------------------------------------------------------|
| Solution and project names | Keep the DDD split as `server.API`, `server.Application`, `server.Communication`, `server.Domain`, `server.Exceptions`, `server.Infrastructure` | `server.Application`                                  |
| C# namespace root          | Use `server` as the code prefix in namespaces and type names                                                                                    | `server.Domain.Entities`                              |
| Base exception prefix      | Use `Server` for exception                                                                                                                      | `ServerException`                                     |
| DbContext naming           | Use the `server` prefix                                                                                                                         | `ServerDbContext`                                     |
| Feature DTO naming         | Keep request and response DTO names explicit                                                                                                    | `RequestUserRegisterJson`, `ResponseUserRegisterJson` |

## Core decisions

- The backend owns the API contract.
- `server.Communication` is the DTO boundary.
- `server.Exceptions` stays backend-only.
- Frontend and mobile consumers depend on generated TypeScript artifacts, not shared C# DTO assemblies.
- Emit a stable OpenAPI spec for frontend and mobile generation.
- Fix nullability and required metadata so required C# fields stay required in generated TypeScript.
- Keep the serialized API error contract stable for both web and mobile.
- Manual backend auth remains the preferred direction for now.
- Tests should optimize for confidence, not for a coverage target.

## Solution overview

### Dependency direction

Inner layers never reference outer layers. Domain stays at the center with zero infrastructure or framework coupling.

```text
server.API ----------> server.Application ----------> server.Domain
   |                          |                           ^
   |                          +--> server.Communication --+
   |                          +--> server.Exceptions
   |
   +--> server.Infrastructure -----> server.Domain
```

If `server.API` needs direct compile-time access to `ServerException` or exception resources, add an explicit reference to `server.Exceptions` instead of depending on transitive access.

### Preferred project references

| Project                 | SDK                     | Type          | References                                                                                                                            |
|-------------------------|-------------------------|---------------|---------------------------------------------------------------------------------------------------------------------------------------|
| `server.Domain`         | `Microsoft.NET.Sdk`     | Class Library | none                                                                                                                                  |
| `server.Exceptions`     | `Microsoft.NET.Sdk`     | Class Library | none                                                                                                                                  |
| `server.Communication`  | `Microsoft.NET.Sdk`     | Class Library | `server.Domain` only when shared enums are needed                                                                                     |
| `server.Application`    | `Microsoft.NET.Sdk`     | Class Library | `server.Domain`, `server.Communication`, `server.Exceptions`                                                                          |
| `server.Infrastructure` | `Microsoft.NET.Sdk`     | Class Library | `server.Domain`                                                                                                                       |
| `server.API`            | `Microsoft.NET.Sdk.Web` | Web API       | `server.Application`, `server.Communication`, `server.Infrastructure`, and `server.Exceptions` when direct exception access is needed |

### Test projects

| Project               | Type          | References                                 | Key packages                               |
|-----------------------|---------------|--------------------------------------------|--------------------------------------------|
| `CommonTestUtilities` | Class Library | Application, Communication, Infrastructure | Bogus, NSubstitute                         |
| `Validators.Test`     | xUnit         | Application, CommonTestUtilities           | Shouldly, xUnit                            |
| `UseCases.Test`       | xUnit         | Application, CommonTestUtilities           | Shouldly, xUnit                            |
| `WebApi.Test`         | xUnit         | API, CommonTestUtilities                   | Shouldly, Testcontainers.PostgreSql, xUnit |

## Cross-cutting rules

- Use file-scoped namespaces.
- Prefer primary constructors for services, use cases, repositories, and filters.
- Keep record constructors, primary constructors, and method signatures on one line when they have four or fewer parameters and fit comfortably. Split across multiple lines at five or more parameters, or earlier when the line would be hard to read.
- Use `Guid` as the primary key for all entities.
- Use `Active` as the default soft-delete flag when soft deletion is needed.
- Keep backend-only concerns inside the backend. Do not expose `server.Exceptions` to web or mobile.
- Keep controllers thin. Business logic belongs in use cases and domain-facing services.
- Keep DTOs simple and serialization-focused. Validation does not live in DTOs.
- Prefer explicit mappings over reflection-based or convention-based mapper libraries.
- Prefer stable, named patterns over ad hoc feature-specific structure.
- Spec changes must keep the project-level `docs/` doc sync group aligned and pass `docs/check-loto-doc-sync.sh` (the sync group and its rule are defined in the root `AGENTS.md`).

## Layer guide

### `server.Domain`

Purpose:
The domain layer is the core of the system and must stay free of application, infrastructure, transport, and framework concerns.

Rules:

- Keep zero NuGet dependencies. Only the .NET base class library is allowed here.
- Keep entities as plain POCOs. Do not add framework annotations or persistence-specific behavior.
- All entities inherit from `EntityBase`.
- `EntityBase` should provide:
  - `Guid Id`
  - `DateTime CreatedAt = DateTime.UtcNow`
  - `bool Active = true`
- Navigation properties should default to empty collections.
- Prefer `init` setters for entity properties that should never change after construction, especially foreign keys and required navigations on join entities.
- Do not use `init` on properties that later use cases must mutate, such as `Role`, `Active`, status fields, or values that participate in reactivation/update flows.
- Example for `BranchUser`: `UserId`, `User`, `BranchId`, and `Branch` may be `init`; keep `Role` settable because branch memberships are updated later.
- Enums live in `Entities/Enums`.
- Use `[Description]` on enums only when a human-readable label is required.
- Domain models are allowed for intermediary structures that are not entities and not DTOs.
- All domain contracts live in `Interfaces/`.
- Repository interfaces belong here.
- `IUnitOfWork` belongs here.
- Token and authentication contracts belong here, including `ITokenProvider`, `ITokenServices`, and `IAuthenticationService`.
- Any refresh-token persistence contract used by Application also belongs here.

Conventions:

- Namespace example: `namespace server.Domain.Entities;`
- Interface naming: `IUsersRepository`
- Entity naming: singular PascalCase nouns such as `User`, `Recipe`
- File naming should match the type name
- Do not add validation libraries or ASP.NET types here

### `server.Exceptions`

Purpose:
The exception layer owns custom backend exceptions and reusable error-message resources.

Rules:

- Keep this project backend-only.
- Put localized or reusable error message constants in `.resx` files.
- The default resource file should be `ResourcesErrorMessages.resx`.
- Add culture-specific resource files such as `ResourcesErrorMessages.pt-BR.resx` when localization is required.
- Avoid magic-string error messages in application or test code.

Base exception contract:

- All custom exceptions inherit from `ServerException : SystemException`.
- `ServerException` exposes:
  - an HTTP status code getter
  - a list of serialized error messages

Standard exception set:

| Exception               | Status code | Use case                           |
|-------------------------|-------------|------------------------------------|
| `OnValidationException` | 400         | FluentValidation failures          |
| `InvalidLoginException` | 401         | Wrong credentials                  |
| `RefreshTokenException` | 401         | Invalid or expired refresh token   |
| `ConflictException`     | 409         | Duplicate or conflicting resources |
| `NotFoundException`     | 404         | Missing entities                   |

Token exception hierarchy:

- Use a shared unauthorized base such as `TokenUnauthorizedException` for 401 token failures.
- Use specialized token exceptions for empty, expired, invalid, and user-not-found token states.
- Use `TokenWithoutPermissionException` for 403 forbidden cases.

Serialization rules:

- Exceptions must serialize cleanly into the shared API error contract.
- `OnValidationException` should surface multiple error messages.
- Single-message exceptions should still serialize as a `List<string>` with one item.

### `server.Communication`

Purpose:
This project defines the JSON contract between clients and the backend.

Rules:

- `server.Communication` remains the DTO boundary.
- It may reference `server.Domain` only for shared enums that must appear in the contract.
- Do not put validation logic here.
- Do not put persistence logic here.
- Keep request and response types flat, explicit, and stable.

Naming conventions:

- Request DTOs use `Request{Feature}Json`
- Response DTOs use `Response{Feature}Json`
- Shared token payloads should use explicit names such as `ResponseTokenJson`

Error response contract:

- Standardize on `ResponseErrorJson`
- It should serialize `ErrorMessages` as `List<string>`
- All API errors returned to web and mobile must fit this shape

### `server.Application`

Purpose:
The application layer orchestrates use cases, validation, mapping, and application services.

Use case structure:

```text
UseCases/
  {Feature}/
    {Operation}/
      {Feature}{Operation}UseCase.cs
      {Feature}{Operation}FluentValidation.cs
      {Feature}{Operation}Mapper.cs
      {Feature}{Operation}SeedFactory.cs   # optional, only when bootstrap/seed data is needed
```

Use case rules:

- Organize use cases by feature and operation.
- One operation folder contains the use case, its validator, and any mapper or seed factory the operation needs.
- Use cases follow this flow:
  1. Validate request and business preconditions
  2. Map request DTOs to domain models or entities
  3. Execute business-specific operations
  4. Persist through repositories
  5. Commit through `IUnitOfWork`
  6. Map to response DTOs
- Throw domain-specific or application-specific exceptions, not raw HTTP responses.
- Do not return HTTP status codes from use cases.

Validation rules:

- Use FluentValidation.
- Create one validator per input-bearing use case.
- When FluentValidation rules repeat within a feature slice (e.g. both Create and Update share the same field rules), extract a `{Feature}ValidationExtensions.cs` in that slice's folder and expose the rules as C# 13 `extension<T>(IRuleBuilder<T, TValue> rule)` block methods. Reserve a cross-feature `SharedValidators` only for rules that genuinely span multiple unrelated features.
- Validation checks raw input shape only. Normalization and canonicalization (e.g. stripping non-digit characters, trimming) happen in the slice-shared mapper or normalizer helpers before persistence — never inside the validator.
- Validators are role-agnostic: they are instantiated with no dependencies and check raw input shape only. Any rule that varies by the caller's role or scope — including conditional requiredness — belongs in the use case after authentication.
- If optional input is treated as absent when `string.IsNullOrWhiteSpace` is true, the normalization helper must return `null` for whitespace-only values, not `""`. Returning `""` would bypass nullable column semantics and filtered unique indexes.
- Business rule checks that require repository access should happen before or around validator execution inside the use case flow, not inside DTO classes.
- When a shared validation extension method has configurable bounds (e.g. `PageSizeBounds(int min = 1, int max = 200)`, `DateRangeWithinCap(int maxDays = 366)`), declare the defaults as `internal const` fields directly inside the same `internal static class` that holds the extension methods — do not create a separate companion class. Follow the same pattern as `AccountValidationExtensions`, which keeps its `NameMaxLength`, `InstitutionMaxLength`, and `NumberMaxLength` constants alongside the extension methods in the same file. Test projects can reference these `internal` constants because `server.Application.csproj` already grants all three test assemblies friend access via `<InternalsVisibleTo>`. Tests must reference the constants by name — never duplicate the literal value.

Mapping rules:

- Use C# 13 `extension(Type instance)` blocks for instance-based mapper methods. Do not mix with the older `public static ReturnType Method(this Type param)` style in the same codebase.
- The `this`-parameter style (`public static Branch ToDomain(this RequestDto request)`) is still used for the initial DTO-to-domain conversion where the source is a request DTO, not a domain entity.
- Do not use AutoMapper.
- Mappings must stay obvious and debuggable.
- Keep mappers focused on shape transformation (DTO ↔ Entity). Do not embed seed data, default catalogs, or business-rule knowledge in mappers.
- For simple in-place updates on an already-loaded entity, direct assignment inside the use case is acceptable when a separate mapper would add no clarity.
- When a use case needs to generate default seed data or bootstrap entities beyond simple DTO mapping, use a dedicated seed factory class (e.g., `CreateBranchSeedFactory`) in the same operation folder.

Authentication rules:

- `AuthenticationService` implements `IAuthenticationService`.
- It should:
  - read the token through `ITokenProvider`
  - validate the token through `ITokenServices`
  - load the authenticated user from the repository when needed
  - cache the resolved user for the lifetime of the request
  - enforce role checks for authorized access

Service rules:

- `PasswordEncryption` is a concrete service, not an interface.
- It uses BCrypt for password hashing and verification.
- It is safe to use directly in tests because it has no I/O.

Dependency injection rules:

- `AddApplication` registers:
  - all use cases
  - `PasswordEncryption`
  - `IAuthenticationService` -> `AuthenticationService`
- Keep use case registration explicit and reviewable.

### `server.Infrastructure`

Purpose:
Infrastructure owns persistence, repository implementations, database access, and token implementations.

Dependency rules:

- Infrastructure depends on `server.Domain` only. It does not reference `server.Exceptions`.
- Infrastructure should return result objects (e.g. `TokenResultValidation`) for recoverable failures instead of throwing custom exceptions.
- The Application layer is the proper place to translate infrastructure results into backend exceptions.

DbContext rules:

- Use `ServerDbContext`.
- Add one `DbSet<T>` per persisted entity.
- Use `UseNpgsql` for PostgreSQL.
- Make the DbContext accessible to `WebApi.Test` either by making it `public` or by using `InternalsVisibleTo`.

Repository rules:

- Repository implementations are `internal`.
- They implement interfaces defined in `server.Domain`.
- Read queries should use `AsNoTracking()` by default.
- Apply the `Active` filter on reads when soft deletion is part of the entity contract.
- Repository methods stage changes in the DbContext but do not call `SaveChangesAsync()` directly.
- When a repository needs two overloads of the same query that differ only in EF Core tracking — one tracked for use-case mutations, one read-only for queries — suffix the read-only variant with `AsNoTracking`. Example: `GetActiveByIdAndBranchId` (tracked, used by Update/Deactivate) and `GetActiveByIdAndBranchIdAsNoTracking` (no tracking, used by Get). This makes the contract explicit and prevents accidental use of a non-tracked query in a write path.
- Persisted idempotency reservations use a tracked repository read because the replay envelope and expiry may be updated in the same database transaction as the business write. Never implement financial idempotency with process memory.
- PostgreSQL optimistic-concurrency roots (`DailyClose`, `Transaction`, and `Setting`) map a `uint Version` property to the `xmin` system column with `IsRowVersion()`; do not add a user-defined `Version` column or a second close-item concurrency mechanism.

Unit of work rule:

- `UnitOfWork` owns `SaveChangesAsync()` and is the only normal commit boundary for application writes.

JWT rules:

- `JwtTokenService` implements `ITokenServices`.
- Use HMAC-SHA256 signing.
- Use zero clock skew unless a real requirement appears.
- Do not validate issuer or audience unless a concrete integration requires it.
- Token validation should map failures into domain-level token error states that `AuthenticationService` can translate into backend exceptions.

Refresh token rules:

- Keep refresh-token persistence in Infrastructure.
- Generate refresh tokens server-side.
- Maintain one active refresh token per user unless a future requirement explicitly changes that model.

Infrastructure DI rules:

- `AddInfrastructure` should register:
  - DbContext
  - token services
  - repositories
  - unit of work
- Validate database and token configuration during startup.

Configuration rules:

- Keep the default database connection string under `ConnectionStrings:DefaultConnection`.
- Keep token settings under `Token`.
- Treat `Development` as the local developer environment, `Staging` as the published non-production environment, and `Production` as the live environment.
- Use these database names consistently:
  - `Development` -> `loto_dev_local`
  - `Staging` -> `loto_staging`
  - `Production` -> `loto_prod`
- Track only `appsettings.json` and `appsettings.Development.json` in git.
- Treat `appsettings.Staging.json` and `appsettings.Production.json` as generated outside git.
- `appsettings.Development.json` may contain fixed local-only bootstrap credentials for `loto_dev_local`.
- Never reuse local Development credentials in `Staging` or `Production`.
- Hosted `Staging` and `Production` secrets come from a non-tracked server-side `.env`.
- Hosted deploy flow generates `appsettings.Staging.json` or `appsettings.Production.json` on the host before app restart.
- Never commit hosted secrets or bake hosted secrets into build artifacts or container images.
- Keep local service containers such as PostgreSQL Docker Compose under `infra/`, not under `server/`.
- No local workflow may point to the online `loto_prod`.
- `ConnectionStrings:DefaultConnection`, `Token:SigningKey`, and `Token:ExpirationTimeInMinutes` are required startup settings.
- Minimum token settings:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Database=loto_dev_local;Username=postgres;Password=..."
  },
  "Token": {
    "SigningKey": "your-256-bit-signing-key-here",
    "ExpirationTimeInMinutes": 60
  }
}
```

Local development workflow:

```bash
cd infra
docker compose up -d

cd ../server
dotnet ef database update --project server.Infrastructure --startup-project server.API
dotnet run --project server.API
```

Migration commands:

```bash
dotnet ef migrations add {MigrationName} \
  --project server.Infrastructure \
  --startup-project server.API

dotnet ef database update \
  --project server.Infrastructure \
  --startup-project server.API
```

### `server.API`

Purpose:
The API layer exposes the HTTP interface, wires dependency injection, and maps exceptions into the stable response contract.

Program rules:

- Keep lowercase URLs enabled.
- Register a global exception filter.
- Call `AddApi()`, `AddApplication()`, and `AddInfrastructure(...)`.
- Register controllers normally.
- Use `AddOpenApi()` for document generation in .NET 10.
- In development, expose the OpenAPI document with `MapOpenApi()`.
- Use Swagger UI by default for the interactive explorer. Scalar is allowed if adopted intentionally.
- Keep `public partial class Program { }` for integration testing.

Error handling rules:

- Use a global `ExceptionFilter`.
- Route exception mapping through `IApiExceptionHandler`.
- Known `ServerException` types map to their declared status code and `ResponseErrorJson`.
- Unknown exceptions return detailed diagnostics in development and a generic unknown-error message outside development.

Auth filter rules:

- Use a TypeFilter-backed `TokenAuthenticateFilter` to require a valid token.
- Use a TypeFilter-backed `TokenAuthorizeFilter` to require a valid token plus a role check.
- Controllers or endpoints must declare auth intent explicitly.

Token provider rules:

- `HttpContextTokenProvider` implements `ITokenProvider`.
- It extracts the bearer token from the `Authorization` header.
- It caches the authenticated user in `HttpContext.Items`.

OpenAPI rules:

- Use the built-in OpenAPI generator in .NET 10.
- Add the bearer security scheme to the generated document.
- Keep the generated spec stable enough for downstream web and mobile code generation.
- Keep cross-cutting financial headers explicit and required in generated OpenAPI: `Idempotency-Key` on supported financial creates, `If-Match` on guarded mutations, and `ETag` on successful versioned responses.

Controller rules:

- Use `[ApiController]`.
- Keep route names clear and consistent.
- Declare an explicit `[Route("...")]` on every action instead of relying on the controller-level route plus verb attribute alone. Use `[Route("")]` for collection-root actions when the final URL should stay at the controller root.
- Use `[FromServices]` per action for use case injection rather than heavy constructor injection.
- Keep action methods thin.
- Declare response types with `[ProducesResponseType]`.
- Protected endpoints must use the explicit auth filters.
- `If-Match` uses one exact strong ETag representation: a quoted positive decimal PostgreSQL `xmin` value such as `"123"`. Missing or malformed preconditions are 400 resource-key errors; stale roots are 409 resource-key errors. Response DTOs expose `Version`, while mutation request bodies do not duplicate that precondition.
- `Idempotency-Key` is a required header on supported financial creates. The application scopes it by endpoint plus authenticated branch/user, hashes the typed request through the shared canonical JSON hasher, and persists the resource id and response envelope atomically with the business write.

API DI rules:

- `AddApi()` registers:
  - `IHttpContextAccessor`
  - `ITokenProvider` -> `HttpContextTokenProvider`
  - `IApiExceptionHandler`
  - OpenAPI configuration
  - Swagger UI services if Swagger UI is the chosen explorer

## Test strategy

### Philosophy

Tests exist to provide confidence that the system behaves correctly. Prefer the smallest set of tests that catches real bugs quickly. Coverage is not the goal.

### Testing trophy

```text
        /   E2E   \
       / Integration\   <- most tests live here
      /    Unit      \
     /  Static/Lint   \
```

| Tier | Project | What it verifies | Database |
|------|---------|------------------|----------|
| Unit | `Validators.Test` | FluentValidation rules in isolation | none |
| Unit | `UseCases.Test` | Business logic with mocked I/O | none |
| Integration | `WebApi.Test` | Full HTTP pipeline with real database | real PostgreSQL via Testcontainers |

### Project structure

```text
tests/
├── CommonTestUtilities/
│   ├── Entities/
│   ├── Requests/
│   ├── Repositories/
│   └── Services/
├── Validators.Test/
│   └── {Feature}/
│       └── {Operation}/
│           └── {Feature}{Operation}FluentValidationTest.cs
├── UseCases.Test/
│   ├── UseCases/
│   │   └── {Feature}/
│   │       └── {Operation}/
│   │           └── {Feature}{Operation}UseCaseTest.cs
│   └── Services/
│       └── {Service}/
│           └── {Service}Test.cs
└── WebApi.Test/
    └── {Feature}/
        ├── {Feature}ControllerHappyPathTest.cs
        ├── {Feature}ControllerUnhappyPathTest.cs
        ├── {Feature}Controller{Operation}HappyPathTest.cs
        └── {Feature}Controller{Operation}UnhappyPathTest.cs
```

### Common test utilities

- Keep builders, fakers, and mock factories in `CommonTestUtilities`.
- Add or extend shared builders and mock helpers in `CommonTestUtilities` before adding repeated inline setup to a test class.
- Use Bogus for realistic fake data generation.
- Use NSubstitute for mocks.
- Use the real `PasswordEncryption` inside builders when a hashed password is needed.
- Prefer fluent builders that configure one behavior at a time and end with `.Build()`.
- Keep request builders under `Requests/`, entity builders under `Entities/`, repository substitutes under `Repositories/`, and service or token substitutes under `Services/`.
- Name shared test helpers after the concrete type they build, such as `UserBuilder`, `RequestUserRegisterJsonBuilder`, `UsersRepositoryBuilder`, or `TokenServicesBuilder`.
- Default builder output should already be valid for the happy path so tests only override the fields relevant to the scenario.
- Mock builders should return the actual NSubstitute substitute from `.Build()` so the test can still assert `Received()` and `DidNotReceive()`.
- Do not leave reusable builders or helper mocks embedded inside individual test classes once a second test needs the same setup.

### Validator tests

- Instantiate validators directly.
- Do not involve DI or repository mocks.
- Place validator tests under `Validators.Test/{Feature}/{Operation}/`.
- Every validator test class should include:
  - one success test
  - targeted failure tests that break exactly one field or rule per test
- Assert against resource-based error messages, not hardcoded strings.

### Use case tests

- Mock all external I/O.
- Place use case tests under `UseCases.Test/UseCases/{Feature}/{Operation}/`.
- Place service-level unit tests under `UseCases.Test/Services/{ServiceName}/`.
- Each test class should use a local `CreateUseCase()` helper to keep setup readable.
- Assert on returned DTOs for success cases.
- Assert on thrown exceptions for error cases.
- When a use case depends on branch-scoped, tenant-scoped, or id-scoped repository queries, assert the exact repository arguments with `Received()` / `DidNotReceive()` for the critical calls. This proves the use case passed the authenticated scope correctly instead of only relying on the repository contract.
- Do not treat an `Arg.Any(...)` repository setup by itself as proof of branch isolation. A mock configured with broad argument matching can still return the expected value even when the use case passes the wrong branch or entity id.
- Prefer mock-builder helpers that can configure exact-argument returns for scoped repository queries such as `ListActiveByBranchId`, `GetActiveByIdAndBranchId`, and `GetActiveByIdAndBranchIdAsNoTracking`.
- Do not assert HTTP status codes in use case tests.

### Authentication service tests

- Give `AuthenticationService` dedicated unit tests.
- Cover:
  - cached user flow
  - missing token
  - expired token
  - invalid token
  - token for deleted user
  - insufficient role

### Web API integration tests

- Use `Microsoft.AspNetCore.Mvc.Testing` plus `Testcontainers.PostgreSql`.
- Do not use EF Core InMemory for integration tests.
- `WebApi.Test` is the only place that should verify:
  - auth filter behavior
  - actual HTTP status codes
  - database constraints
  - migration correctness
- Seed known users or entities in the test factory when needed.
- Share factory state carefully. Prefer unique test data or deterministic seed data.
- Reuse shared Web API test setup from `WebApi.Test/Infrastructure` once it repeats across classes or scenarios.
- Use thin authenticated-request helpers when they reduce boilerplate without obscuring the HTTP verb, route, payload, or asserted outcome.
- Use request builders in `WebApi.Test` when the request payload is generic setup data and the builder does not get in the way of the scenario being exercised.
- Verify persistence through database reload/query helpers from the test infrastructure, not by assuming request input equals persisted state.
- When reading rows back in `WebApi.Test` (list, count, sum), prefer repository interface methods over raw `DbContext` / EF Core queries. Resolve the repository from the test scope via `scope.ServiceProvider.GetRequiredService<IRepository>()` — the same pattern used for `ITransactionsRepository` in `TransactionControllerCancelHappyPathTest`. Only fall back to `DbContext` directly for operations the repository does not expose.
- In `tests/WebApi.Test/{Feature}/`, keep file names and class names aligned with the existing happy/unhappy path convention. Broad feature coverage uses `{Feature}ControllerHappyPathTest` and `{Feature}ControllerUnhappyPathTest`; operation-specific slices use `{Feature}Controller{Operation}HappyPathTest` and `{Feature}Controller{Operation}UnhappyPathTest`, such as `TransactionControllerUpdateHappyPathTest`.
- Standardize the shared integration-test host fixture as `tests/WebApi.Test/Infrastructure/ServerWebApplicationFactory.cs`.
- Standardize the shared xUnit collection for API integration tests as `tests/WebApi.Test/Infrastructure/ServerApiCollection.cs`.

Prerequisites for `WebApi.Test`:

- `Program.cs` must end with `public partial class Program { }`
- `ServerDbContext` must be accessible to the test project

### Architecture tests

Keep a small architecture test suite that at minimum verifies:

- all use cases are registered in DI
- all controllers declare auth intent at the class or endpoint level

### Mutation testing

Use Stryker after the normal test suite is green.

```bash
dotnet tool install -g dotnet-stryker
cd tests/UseCases.Test
dotnet stryker --project "../../server.Application/server.Application.csproj"
```

Use mutation testing to find missing assertions or uncovered branches, especially in use cases and authentication logic.

### What not to do

- Do not test HTTP status codes in use case tests.
- Do not test auth filters in use case tests.
- Do not use EF Core InMemory for integration tests.
- Do not hardcode error strings in tests.
- Do not let repositories call `SaveChangesAsync()` directly during normal write flows.
- Do not hide mapping behavior inside AutoMapper or similar tools.
- Do not query `DbContext` directly in `WebApi.Test` when a repository interface method already covers the needed read; reach for the repository first.

### Recommended execution order for new features

1. Add shared builders and mock helpers in `CommonTestUtilities`
2. Write validator tests
3. Write use case tests
4. Write or update integration tests in `WebApi.Test`
5. Run mutation testing for critical logic after the suite is green

### Common test commands

```bash
dotnet test tests/Validators.Test
dotnet test tests/UseCases.Test
dotnet test tests/WebApi.Test
```

`tests/WebApi.Test` requires Docker because it relies on Testcontainers.

## Naming conventions

| Category               | Pattern                                                         | Example                             |
|------------------------|-----------------------------------------------------------------|-------------------------------------|
| Solution               | `server.sln` or equivalent server solution name                 | `server.sln`                        |
| Project namespace root | `server.{Layer}`                                                | `server.Domain`                     |
| Entity                 | PascalCase singular noun                                        | `User`                              |
| Use case class         | `{Entity}{Operation}UseCase`                                    | `UserRegisterUseCase`               |
| Validator class        | `{Entity}{Operation}FluentValidation`                           | `UserRegisterFluentValidation`      |
| Mapper class           | `{Entity}{Operation}Mapper`                                     | `UserRegisterMapper`                |
| Seed factory class     | `{Entity}{Operation}SeedFactory`                                | `CreateBranchSeedFactory`           |
| Repository class       | `{Entity}Repository`                                            | `UsersRepository`                   |
| Repository interface   | `I{Entity}Repository`                                           | `IUsersRepository`                  |
| Controller             | `{Feature}Controller`                                           | `UserController`                    |
| Request DTO            | `Request{Feature}Json`                                          | `RequestUserRegisterJson`           |
| Response DTO           | `Response{Feature}Json`                                         | `ResponseUserRegisterJson`          |
| Auth filter attribute  | `TokenAuthenticateFilter`, `TokenAuthorizeFilter`               | `TokenAuthorizeFilter`              |
| DI extension           | `{Layer}DependencyInjectionExtension`                           | `InfraDependencyInjectionExtension` |
| Test class             | `{Feature}{Operation}Test` or `{Feature}{Operation}UseCaseTest` | `UserRegisterUseCaseTest`           |
| Entity builder         | `{Entity}Builder`                                               | `UserBuilder`                       |
| Request builder        | `Request{Feature}JsonBuilder`                                   | `RequestUserRegisterJsonBuilder`    |
| Repository builder     | `{Entity}RepositoryBuilder`                                     | `UserRepositoryBuilder`             |
| Namespace style        | File-scoped namespaces                                          | `namespace server.Domain.Entities;` |
| Constructor style      | Primary constructors where helpful                              | `public class X(IDep dep)`          |
| Mapper extension style | C# 13 `extension(Type instance)` blocks for instance methods   | `extension(Branch branch) { ... }`  |

## NuGet and package guidance

| Project                 | Package guidance                                                                                                                                          |
|-------------------------|-----------------------------------------------------------------------------------------------------------------------------------------------------------|
| `server.Domain`         | No packages                                                                                                                                               |
| `server.Exceptions`     | No packages besides resource support already included by the SDK                                                                                          |
| `server.Communication`  | No runtime mapping or validation packages                                                                                                                 |
| `server.Application`    | FluentValidation, BCrypt.Net-Next                                                                                                                         |
| `server.Infrastructure` | Npgsql.EntityFrameworkCore.PostgreSQL 10.x, Microsoft.EntityFrameworkCore 10.x, Microsoft.EntityFrameworkCore.Tools 10.x, System.IdentityModel.Tokens.Jwt |
| `server.API`            | Microsoft.AspNetCore.OpenApi 10.x plus Swagger UI or Scalar package for the chosen explorer                                                               |
| `CommonTestUtilities`   | Bogus, NSubstitute                                                                                                                                        |
| All test projects       | Shouldly, xUnit, Microsoft.NET.Test.Sdk                                                                                                                   |
| `WebApi.Test`           | Testcontainers.PostgreSql, Microsoft.AspNetCore.Mvc.Testing                                                                                               |

## New feature checklist

1. Domain: create or update entities, enums, models, and repository interfaces as needed.
2. Exceptions: add new backend exception types or resource messages if the feature introduces new failure states.
3. Communication: add `Request{Feature}Json` and `Response{Feature}Json` contracts.
4. Application: add the use case, validator, and mapper under `UseCases/{Feature}/{Operation}/`.
5. Application DI: register the new use case explicitly.
6. Infrastructure: implement repositories, token helpers, or other infrastructure pieces required by the feature.
7. Infrastructure DI: register new implementations.
8. Infrastructure persistence: add or update DbSets, entity configuration, and EF migrations.
9. API: add controller actions, response metadata, and auth filters where needed.
10. OpenAPI: confirm the new endpoint or contract is represented correctly in the generated spec.
11. CommonTestUtilities: add or update builders and fakes.
12. Validators.Test: cover success and each important validation failure.
13. UseCases.Test: cover success and each important business failure path.
14. WebApi.Test: cover the real HTTP flow, including auth and persistence behavior where relevant.
15. Mutation testing: run Stryker for critical logic after the standard suite is green.

## Final reminders

- Keep `server` as the source of truth for backend contracts.
- Keep web and mobile aligned through generated TypeScript from OpenAPI, not through shared C# assemblies.
- Keep `server.Exceptions` internal to the backend and expose only the serialized error contract.
- When adding a new server pattern, update this file so it stays the single authoritative guide for the folder.
