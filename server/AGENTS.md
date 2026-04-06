# Server Agent Notes

This file is the only source of truth for work inside `server/`.

Use it together with the repo root [AGENTS.md](../AGENTS.md).

## Stack

| Item | Value |
|------|-------|
| Target framework | .NET 10 LTS |
| Language | C# 13 |
| Database | PostgreSQL |
| ORM | Entity Framework Core 10 |
| Architecture | Clean Architecture + DDD |
| API description | Built-in OpenAPI in .NET 10 |
| Interactive API UI | Swagger UI by default, Scalar allowed if chosen intentionally |

## Naming map

| Concern | Rule | Example |
|--------|------|---------|
| Solution and project names | Keep the DDD split as `server.API`, `server.Application`, `server.Communication`, `server.Domain`, `server.Exceptions`, `server.Infrastructure` | `server.Application` |
| C# namespace root | Use `Server` as the code prefix in namespaces and type names | `Server.Domain.Entities` |
| Base exception prefix | Use `Server` for exception and filter naming | `ServerException`, `ServerAuthenticateFilter` |
| DbContext naming | Use the `Server` prefix | `ServerDbContext` |
| Feature DTO naming | Keep request and response DTO names explicit | `RequestUserRegisterJson`, `ResponseUserRegisterJson` |

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
                                  +-> server.Exceptions
```

If `server.API` needs direct compile-time access to `ServerException` or exception resources, add an explicit reference to `server.Exceptions` instead of depending on transitive access.

### Preferred project references

| Project | SDK | Type | References |
|---------|-----|------|------------|
| `server.Domain` | `Microsoft.NET.Sdk` | Class Library | none |
| `server.Exceptions` | `Microsoft.NET.Sdk` | Class Library | none |
| `server.Communication` | `Microsoft.NET.Sdk` | Class Library | `server.Domain` only when shared enums are needed |
| `server.Application` | `Microsoft.NET.Sdk` | Class Library | `server.Domain`, `server.Communication`, `server.Exceptions` |
| `server.Infrastructure` | `Microsoft.NET.Sdk` | Class Library | `server.Domain`, `server.Exceptions` |
| `server.API` | `Microsoft.NET.Sdk.Web` | Web API | `server.Application`, `server.Communication`, `server.Infrastructure`, and `server.Exceptions` when direct exception access is needed |

### Test projects

| Project | Type | References | Key packages |
|---------|------|------------|--------------|
| `CommonTestUtilities` | Class Library | Application, Communication, Infrastructure | Bogus, NSubstitute |
| `Validators.Test` | xUnit | Application, CommonTestUtilities | Shouldly, xUnit |
| `UseCases.Test` | xUnit | Application, CommonTestUtilities | Shouldly, xUnit |
| `WebApi.Test` | xUnit | API, CommonTestUtilities | Shouldly, Testcontainers.PostgreSql, xUnit |

## Cross-cutting rules

- Use file-scoped namespaces.
- Prefer primary constructors for services, use cases, repositories, and filters.
- Use `Guid` as the primary key for all entities.
- Use `Active` as the default soft-delete flag when soft deletion is needed.
- Keep backend-only concerns inside the backend. Do not expose `server.Exceptions` to web or mobile.
- Keep controllers thin. Business logic belongs in use cases and domain-facing services.
- Keep DTOs simple and serialization-focused. Validation does not live in DTOs.
- Prefer explicit mappings over reflection-based or convention-based mapper libraries.
- Prefer stable, named patterns over ad hoc feature-specific structure.
- Treat `server/docs/loto-specs.md`, `server/docs/loto_presentation.html`, and `server/docs/loto_entity_relationship_diagram.html` as one LottoGest backend doc sync group. Factual changes must update the affected files together, keep the shared sync metadata aligned, and pass `server/docs/check-loto-doc-sync.sh`.

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
- Enums live in `Entities/` or a dedicated `Enums/` folder.
- Use `[Description]` on enums only when a human-readable label is required.
- Domain models are allowed for intermediary structures that are not entities and not DTOs.
- All domain contracts live in `Interfaces/`.
- Repository interfaces belong here.
- `IUnitOfWork` belongs here.
- Token and authentication contracts belong here, including `ITokenProvider`, `ITokenServices`, and `IAuthenticationService`.
- Any refresh-token persistence contract used by Application also belongs here.

Conventions:

- Namespace example: `namespace Server.Domain.Entities;`
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

| Exception | Status code | Use case |
|----------|-------------|----------|
| `OnValidationException` | 400 | FluentValidation failures |
| `InvalidLoginException` | 401 | Wrong credentials |
| `RefreshTokenException` | 401 | Invalid or expired refresh token |
| `ConflictException` | 409 | Duplicate or conflicting resources |
| `NotFoundException` | 404 | Missing entities |

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
```

Use case rules:

- Organize use cases by feature and operation.
- One operation folder contains the use case, its validator, and its mapper.
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
- Shared validation helpers live in a shared validator extension class.
- Business rule checks that require repository access should happen before or around validator execution inside the use case flow, not inside DTO classes.

Mapping rules:

- Use explicit mapper extension methods.
- Do not use AutoMapper.
- Mappings must stay obvious and debuggable.

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
- Validate token configuration during startup.

Configuration rules:

- Keep the default database connection string under `ConnectionStrings:DefaultConnection`.
- Keep token settings under `Token`.
- Minimum token settings:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Database=lotoapp;Username=postgres;Password=..."
  },
  "Token": {
    "SigningKey": "your-256-bit-signing-key-here",
    "ExpirationTimeInMinutes": 15
  }
}
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

- Use a TypeFilter-backed `ServerAuthenticateFilter` to require a valid token.
- Use a TypeFilter-backed `ServerAuthorizeFilter` to require a valid token plus a role check.
- Controllers or endpoints must declare auth intent explicitly.

Token provider rules:

- `HttpContextTokenProvider` implements `ITokenProvider`.
- It extracts the bearer token from the `Authorization` header.
- It caches the authenticated user in `HttpContext.Items`.

OpenAPI rules:

- Use the built-in OpenAPI generator in .NET 10.
- Add the bearer security scheme to the generated document.
- Keep the generated spec stable enough for downstream web and mobile code generation.

Controller rules:

- Use `[ApiController]`.
- Keep route names clear and consistent.
- Use `[FromServices]` per action for use case injection rather than heavy constructor injection.
- Keep action methods thin.
- Declare response types with `[ProducesResponseType]`.
- Protected endpoints must use the explicit auth filters.

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
├── UseCases.Test/
└── WebApi.Test/
```

### Common test utilities

- Keep builders, fakers, and mock factories in `CommonTestUtilities`.
- Use Bogus for realistic fake data generation.
- Use NSubstitute for mocks.
- Use the real `PasswordEncryption` inside builders when a hashed password is needed.
- Prefer fluent builders that configure one behavior at a time and end with `.Build()`.

### Validator tests

- Instantiate validators directly.
- Do not involve DI or repository mocks.
- Every validator test class should include:
  - one success test
  - targeted failure tests that break exactly one field or rule per test
- Assert against resource-based error messages, not hardcoded strings.

### Use case tests

- Mock all external I/O.
- Each test class should use a local `CreateUseCase()` helper to keep setup readable.
- Assert on returned DTOs for success cases.
- Assert on thrown exceptions for error cases.
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

| Category | Pattern | Example |
|----------|---------|---------|
| Solution | `server.sln` or equivalent server solution name | `server.sln` |
| Project namespace root | `Server.{Layer}` | `Server.Domain` |
| Entity | PascalCase singular noun | `User` |
| Use case class | `{Entity}{Operation}UseCase` | `UserRegisterUseCase` |
| Validator class | `{Entity}{Operation}FluentValidation` | `UserRegisterFluentValidation` |
| Mapper class | `{Entity}{Operation}Mapper` | `UserRegisterMapper` |
| Repository class | `{Entity}Repository` | `UsersRepository` |
| Repository interface | `I{Entity}Repository` | `IUsersRepository` |
| Controller | `{Feature}Controller` | `UserController` |
| Request DTO | `Request{Feature}Json` | `RequestUserRegisterJson` |
| Response DTO | `Response{Feature}Json` | `ResponseUserRegisterJson` |
| Auth filter attribute | `ServerAuthenticateFilter`, `ServerAuthorizeFilter` | `ServerAuthorizeFilter` |
| DI extension | `{Layer}DependencyInjectionExtension` | `InfraDependencyInjectionExtension` |
| Test class | `{Feature}{Operation}Test` or `{Feature}{Operation}UseCaseTest` | `UserRegisterUseCaseTest` |
| Entity builder | `{Entity}Builder` | `UserBuilder` |
| Request builder | `Request{Feature}JsonBuilder` | `RequestUserRegisterJsonBuilder` |
| Repository builder | `{Entity}RepositoryBuilder` | `UserRepositoryBuilder` |
| Namespace style | File-scoped namespaces | `namespace Server.Domain.Entities;` |
| Constructor style | Primary constructors where helpful | `public class X(IDep dep)` |

## NuGet and package guidance

| Project | Package guidance |
|---------|------------------|
| `server.Domain` | No packages |
| `server.Exceptions` | No packages besides resource support already included by the SDK |
| `server.Communication` | No runtime mapping or validation packages |
| `server.Application` | FluentValidation, BCrypt.Net-Next |
| `server.Infrastructure` | Npgsql.EntityFrameworkCore.PostgreSQL 10.x, Microsoft.EntityFrameworkCore 10.x, Microsoft.EntityFrameworkCore.Tools 10.x, System.IdentityModel.Tokens.Jwt |
| `server.API` | Microsoft.AspNetCore.OpenApi 10.x plus Swagger UI or Scalar package for the chosen explorer |
| `CommonTestUtilities` | Bogus, NSubstitute |
| All test projects | Shouldly, xUnit, Microsoft.NET.Test.Sdk |
| `WebApi.Test` | Testcontainers.PostgreSql, Microsoft.AspNetCore.Mvc.Testing |

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
