# Project Specification — .NET 10 LTS Clean Architecture / DDD

> Generic, reusable specification for bootstrapping new .NET 10 Web API projects.
> Replace `{ProjectName}` with your actual project name throughout.

| Item | Value |
|------|-------|
| Target Framework | .NET 10 (LTS) |
| Language | C# 13 |
| Database | PostgreSQL |
| ORM | Entity Framework Core 10 |
| Architecture | Clean Architecture + DDD |

---

## Table of Contents

1. [Solution Overview](#1-solution-overview)
2. [Domain Layer](#2-domain-layer)
3. [Exception Layer](#3-exception-layer)
4. [Communication Layer](#4-communication-layer)
5. [Application Layer](#5-application-layer)
6. [Infrastructure Layer](#6-infrastructure-layer)
7. [API Layer](#7-api-layer)
8. [Test Strategy](#8-test-strategy)
9. [Naming Conventions](#9-naming-conventions)
10. [NuGet Dependencies](#10-nuget-dependencies)
11. [New Feature Checklist](#11-new-feature-checklist)

---

## 1. Solution Overview

### Dependency Direction

Inner layers never reference outer layers. Domain is the core with zero dependencies.

```
API ──────────► Application ──────► Domain
 │                 │                   ▲
 │                 ├──► Communication ─┘
 │                 └──► Exception
 │
 └─► Infrastructure ──► Domain
                    └──► Exception
```

### Project References

| Project | SDK | Type | References |
|---------|-----|------|------------|
| `{ProjectName}.Domain` | `Microsoft.NET.Sdk` | Class Library | (none) |
| `{ProjectName}.Exception` | `Microsoft.NET.Sdk` | Class Library | (none) |
| `{ProjectName}.Communication` | `Microsoft.NET.Sdk` | Class Library | Domain |
| `{ProjectName}.Application` | `Microsoft.NET.Sdk` | Class Library | Domain, Communication, Exception |
| `{ProjectName}.Infrastructure` | `Microsoft.NET.Sdk` | Class Library | Domain, Exception |
| `{ProjectName}.API` | `Microsoft.NET.Sdk.Web` | Web API | Application, Communication, Infrastructure |

### Test Projects

| Project | Type | References | Key Packages |
|---------|------|------------|--------------|
| `CommonTestUtilities` | Class Library | Application, Communication, Infrastructure | Bogus, NSubstitute |
| `Validators.Test` | xUnit Test | Application, CommonTestUtilities | Shouldly, xUnit |
| `UseCases.Test` | xUnit Test | Application, CommonTestUtilities | Shouldly, xUnit |
| `WebApi.Test` | xUnit Test | API, CommonTestUtilities | Shouldly, Testcontainers.PostgreSql, xUnit |

---

## 2. Domain Layer

**Path:** `{ProjectName}.Domain/`

The Domain layer is the core of the application. It has **zero NuGet dependencies** — only .NET base class library types.

### 2.1. EntityBase

All domain entities inherit from this base class.

```csharp
namespace {ProjectName}.Domain.Entities;

public class EntityBase
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public bool Active { get; set; } = true;
}
```

### 2.2. Entities

Entities are plain POCOs with no behavior methods. They inherit from `EntityBase` and use file-scoped namespaces.

```csharp
namespace {ProjectName}.Domain.Entities;

public class {Entity} : EntityBase
{
    public string Name { get; set; } = string.Empty;
    // Navigation properties
    public IList<{RelatedEntity}> {RelatedEntities} { get; set; } = [];
}
```

### 2.3. Enums

Domain enums live in `Entities/` or a dedicated `Enums/` folder. Use `[Description]` attribute when a human-readable label is needed.

```csharp
namespace {ProjectName}.Domain.Entities;

public enum Role
{
    Admin = 0,
    Manager = 1,
    Member = 2
}
```

Optional enum extension for reading `[Description]`:

```csharp
namespace {ProjectName}.Domain.Utils;

public static class EnumExtension
{
    public static string GetDescription(this Enum value)
    {
        var field = value.GetType().GetField(value.ToString());
        var attribute = field?.GetCustomAttribute<DescriptionAttribute>();
        return attribute?.Description ?? value.ToString();
    }
}
```

### 2.4. Models

Domain-level models used as intermediary data structures (not entities, not DTOs).

```csharp
namespace {ProjectName}.Domain.Models;

public class TokenResultValidation
{
    public bool IsSuccess { get; init; }
    public Guid UserId { get; init; }
    public TokenErrorType Error { get; init; }
}

public enum TokenErrorType
{
    None,
    Expired,
    Invalid
}
```

### 2.5. Interfaces

All contracts live in `Interfaces/`. Implementations exist in Infrastructure or Application layers.

**Repository interfaces:**

```csharp
namespace {ProjectName}.Domain.Interfaces;

public interface I{Entity}Repository
{
    Task Register({Entity} entity);
    Task<{Entity}?> GetById(Guid id);
    Task<List<{Entity}>> GetAll();
    Task<bool> CheckIfExists({Entity} entity);
}
```

**Unit of Work:**

```csharp
public interface IUnitOfWork
{
    Task Commit();
}
```

**Token interfaces:**

```csharp
public interface ITokenProvider
{
    string? GetTokenValue();
    User? GetCachedUser();
    void CacheUser(User user);
}

public interface ITokenServices
{
    string Generate(User user);
    TokenResultValidation ValidateToken(string token);
}
```

**Authentication service:**

```csharp
public interface IAuthenticationService
{
    Task<User> GetAuthenticatedUser();
    Task<User> GetAuthorizedUser(Role requiredRole, params Role[] additionalRoles);
}
```

### 2.6. Conventions

| Item | Convention |
|------|-----------|
| Namespace | File-scoped: `namespace {ProjectName}.Domain.Entities;` |
| Entity base | Always inherit `EntityBase` |
| Interface prefix | `I` prefix: `IUsersRepository` |
| Primary key | `Guid` for all entities |
| Soft delete | `Active` boolean flag |
| NuGet packages | **None** — Domain must remain pure |

---

## 3. Exception Layer

**Path:** `{ProjectName}.Exception/`

### 3.1. Base Exception

All custom exceptions inherit from this abstract base class.

```csharp
namespace {ProjectName}.Exceptions.Exceptions;

public abstract class {ProjectName}Exception : SystemException
{
    public {ProjectName}Exception(string message) : base(message) { }
    public abstract int GetStatusCode { get; }
    public abstract List<string> GetErrorMessages { get; }
}
```

### 3.2. Concrete Exceptions

| Exception Class | HTTP Status | Constructor | Use Case |
|----------------|-------------|-------------|----------|
| `OnValidationException` | 400 Bad Request | `List<string> errorMessages` | FluentValidation failures |
| `InvalidLoginException` | 401 Unauthorized | `string message` | Wrong credentials |
| `ConflictException` | 409 Conflict | `string message` | Duplicate resources |
| `NotFoundException` | 404 Not Found | `string message` | Entity not found |
| `RefreshTokenException` | 401 Unauthorized | `string message` | Invalid/expired refresh token |

**OnValidationException** (accepts multiple error messages):

```csharp
namespace {ProjectName}.Exceptions.Exceptions;

public class OnValidationException : {ProjectName}Exception
{
    public List<string> ErrorMessages { get; }

    public OnValidationException(List<string> errorMessages) : base(string.Empty)
    {
        ErrorMessages = errorMessages;
    }

    public override int GetStatusCode => (int)HttpStatusCode.BadRequest;
    public override List<string> GetErrorMessages => ErrorMessages;
}
```

**Simple exception template** (for Conflict, NotFound, InvalidLogin):

```csharp
public class ConflictException(string message) : {ProjectName}Exception(message)
{
    public override int GetStatusCode => (int)HttpStatusCode.Conflict;
    public override List<string> GetErrorMessages => [Message];
}
```

### 3.3. Token Exception Hierarchy

Token exceptions share a common base for 401 responses. `TokenWithoutPermissionException` is separate (403).

```csharp
public class TokenUnauthorizedException(string message)
    : {ProjectName}Exception(message)
{
    public override int GetStatusCode => (int)HttpStatusCode.Unauthorized;
    public override List<string> GetErrorMessages => [Message];
}

public class TokenEmptyException(string errorMessage) : TokenUnauthorizedException(errorMessage);
public class TokenExpiredException(string errorMessage) : TokenUnauthorizedException(errorMessage);
public class TokenInvalidException(string errorMessage) : TokenUnauthorizedException(errorMessage);
public class TokenWithoutUserException(string errorMessage) : TokenUnauthorizedException(errorMessage);
```

```csharp
public class TokenWithoutPermissionException(string message)
    : {ProjectName}Exception(message)
{
    public override int GetStatusCode => (int)HttpStatusCode.Forbidden;
    public override List<string> GetErrorMessages => [Message];
}
```

### 3.4. Resource Error Messages

Use `.resx` files for localized error message constants. This enables multi-language support and prevents magic strings.

**`ResourcesErrorMessages.resx`** (default language):

| Key | Value |
|-----|-------|
| `NAME_EMPTY` | Name cannot be empty |
| `EMAIL_EMPTY` | Email cannot be empty |
| `EMAIL_INVALID` | Invalid email format |
| `PASSWORD_LENGTH` | Password must have at least {0} characters |
| `ROLE_INVALID` | Invalid role |
| `EMAIL_NOT_REGISTERED` | Email not registered |
| `PASSWORD_WRONG` | Wrong password |
| `TOKEN_EMPTY` | Token is required |
| `TOKEN_EXPIRED` | Token has expired |
| `TOKEN_INVALID` | Invalid token |
| `TOKEN_WITHOUT_USER` | User not found for this token |
| `TOKEN_WITHOUT_PERMISSION` | Insufficient permissions |
| `UNKNOWN_ERROR` | Unknown error |

Add `ResourcesErrorMessages.{culture}.resx` files for additional languages (e.g., `pt-BR`).

Usage in code: `ResourcesErrorMessages.NAME_EMPTY`

---

## 4. Communication Layer

**Path:** `{ProjectName}.Communication/`

The Communication layer defines the JSON contract between client and server. It references Domain only for shared enums.

### 4.1. Request DTOs

Naming convention: `Request{Feature}Json`. Located in `Requests/` folder.

```csharp
namespace {ProjectName}.Communication.Requests;

public class Request{Feature}Json
{
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public Role Role { get; set; }  // enum from Domain
}
```

### 4.2. Response DTOs

Naming convention: `Response{Feature}Json`. Located in `Responses/` folder.

```csharp
namespace {ProjectName}.Communication.Responses;

public class Response{Feature}Json
{
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public ResponseTokenJson ResponseToken { get; set; } = default!;
}

public class ResponseTokenJson
{
    public string Token { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;
}
```

### 4.3. ResponseErrorJson

Standard error response shape used by the exception filter.

```csharp
namespace {ProjectName}.Communication.Responses;

public class ResponseErrorJson
{
    public List<string> ErrorMessages { get; set; }

    public ResponseErrorJson(List<string> errorMessages)
    {
        ErrorMessages = errorMessages;
    }

    public ResponseErrorJson(string errorMessage)
    {
        ErrorMessages = [errorMessage];
    }
}
```

### 4.4. Conventions

| Item | Convention |
|------|-----------|
| Request naming | `Request{Feature}Json` |
| Response naming | `Response{Feature}Json` |
| Error response | Always `ResponseErrorJson` with `List<string>` |
| Domain dependency | Only for shared enums used in DTOs |
| No validation logic | Validation lives in Application layer |

---

## 5. Application Layer

**Path:** `{ProjectName}.Application/`

### 5.1. Use Case Structure

Each use case is organized by feature and operation. One folder per operation, containing three files:

```
UseCases/
  {Feature}/
    {Operation}/
      {Feature}{Operation}UseCase.cs
      {Feature}{Operation}FluentValidation.cs
      {Feature}{Operation}Mapper.cs
```

### 5.2. Use Case Template

Use cases follow a strict flow: **validate → map to domain → business checks → persist → map to response**.

```csharp
namespace {ProjectName}.Application.UseCases.{Feature}.{Operation};

public class {Feature}{Operation}UseCase(
    I{Feature}Repository repository,
    IUnitOfWork unitOfWork,
    ITokenServices tokenServices,
    IRefreshTokenRepository refreshTokenRepository,
    PasswordEncryption passwordEncryption)
{
    public async Task<Response{Feature}Json> Execute(Request{Feature}Json request)
    {
        // 1. Validate
        await Validate(request);

        // 2. Map request to domain entity
        var entity = request.ToDomain();

        // 3. Business-specific operations
        entity.Password = passwordEncryption.HashPassword(request.Password);
        var token = tokenServices.Generate(entity);
        var refreshToken = refreshTokenRepository.Generate();

        // 4. Persist
        await refreshTokenRepository.SaveRefreshToken(new RefreshToken
        {
            Value = refreshToken,
            UserId = entity.Id
        });
        await repository.Register(entity);
        await unitOfWork.Commit();

        // 5. Map to response
        return entity.ToResponse(token, refreshToken);
    }

    private async Task Validate(Request{Feature}Json request)
    {
        // Business rule checks (throw before validation)
        var emailExists = await repository.VerifyIfEmailAlreadyExists(request.Email);
        if (emailExists)
            throw new ConflictException(ResourcesErrorMessages.EMAIL_ALREADY_REGISTERED);

        // FluentValidation
        var result = new {Feature}{Operation}FluentValidation().Validate(request);
        if (!result.IsValid)
        {
            var errorMessages = result.Errors.Select(e => e.ErrorMessage).ToList();
            throw new OnValidationException(errorMessages);
        }
    }
}
```

### 5.3. FluentValidation Template

One validator per use case that needs input validation. Extends `AbstractValidator<Request{Feature}Json>`.

```csharp
namespace {ProjectName}.Application.UseCases.{Feature}.{Operation};

public class {Feature}{Operation}FluentValidation : AbstractValidator<Request{Feature}Json>
{
    public {Feature}{Operation}FluentValidation()
    {
        RuleFor(r => r.Name).NotEmpty()
            .WithMessage(ResourcesErrorMessages.NAME_EMPTY);
        RuleFor(r => r.Email).NotEmpty()
            .WithMessage(ResourcesErrorMessages.EMAIL_EMPTY);
        When(r => !string.IsNullOrWhiteSpace(r.Email), () =>
        {
            RuleFor(r => r.Email).EmailAddress()
                .WithMessage(ResourcesErrorMessages.EMAIL_INVALID);
        });
        RuleFor(r => r.Password).ValidatePassword();
        RuleFor(r => r.Role).IsInEnum()
            .WithMessage(ResourcesErrorMessages.ROLE_INVALID);
    }
}
```

### 5.4. Manual Mapper Extension Methods

Static extension methods for DTO/entity conversions. No AutoMapper — explicit, debuggable mappings.

```csharp
namespace {ProjectName}.Application.UseCases.{Feature}.{Operation};

public static class {Feature}{Operation}Mapper
{
    public static {Entity} ToDomain(this Request{Feature}Json request)
    {
        return new {Entity}
        {
            Name = request.Name,
            Email = request.Email,
            Role = request.Role
        };
    }

    public static Response{Feature}Json ToResponse(this {Entity} entity, string token, string refreshToken)
    {
        return new Response{Feature}Json
        {
            Name = entity.Name,
            Email = entity.Email,
            ResponseToken = new ResponseTokenJson
            {
                Token = token,
                RefreshToken = refreshToken
            }
        };
    }
}
```

### 5.5. SharedValidators

Reusable validation rules as `IRuleBuilderOptions` extension methods.

```csharp
namespace {ProjectName}.Application.UseCases;

public static class SharedValidators
{
    private const int MinimumPasswordLength = 8;

    public static IRuleBuilderOptions<T, string> ValidatePassword<T>(
        this IRuleBuilder<T, string> ruleBuilder)
    {
        return ruleBuilder
            .NotEmpty().WithMessage(ResourcesErrorMessages.PASSWORD_EMPTY)
            .MinimumLength(MinimumPasswordLength)
            .WithMessage(string.Format(ResourcesErrorMessages.PASSWORD_LENGTH, MinimumPasswordLength));
    }
}
```

### 5.6. AuthenticationService

Implements `IAuthenticationService`. Caches the authenticated user in `HttpContext.Items` to avoid repeated token validations within a single request.

```csharp
namespace {ProjectName}.Application.Services;

public class AuthenticationService(
    ITokenProvider tokenProvider,
    ITokenServices tokenServices,
    IUsersRepository usersRepository) : IAuthenticationService
{
    public async Task<User> GetAuthenticatedUser()
    {
        // 1. Check cache first
        var cachedUser = tokenProvider.GetCachedUser();
        if (cachedUser is not null)
            return cachedUser;

        // 2. Extract token
        var token = tokenProvider.GetTokenValue();
        if (string.IsNullOrWhiteSpace(token))
            throw new TokenEmptyException(ResourcesErrorMessages.TOKEN_EMPTY);

        // 3. Validate token
        var result = tokenServices.ValidateToken(token);
        if (result.IsSuccess is false)
        {
            throw result.Error switch
            {
                TokenErrorType.Expired => new TokenExpiredException(ResourcesErrorMessages.TOKEN_EXPIRED),
                _ => new TokenInvalidException(ResourcesErrorMessages.TOKEN_INVALID)
            };
        }

        // 4. Load user from DB
        var user = await usersRepository.GetById(result.UserId);

        // 5. Cache and return
        tokenProvider.CacheUser(user ?? throw new TokenWithoutUserException(
            ResourcesErrorMessages.TOKEN_WITHOUT_USER));
        return user;
    }

    public async Task<User> GetAuthorizedUser(Role requiredRole, params Role[] additionalRoles)
    {
        var user = await GetAuthenticatedUser();
        var roles = new List<Role> { requiredRole }.Union(additionalRoles).ToArray();
        return roles.Contains(user.Role) is false
            ? throw new TokenWithoutPermissionException(ResourcesErrorMessages.TOKEN_WITHOUT_PERMISSION)
            : user;
    }
}
```

### 5.7. PasswordEncryption

Uses BCrypt for password hashing. This is a concrete class (not an interface) — safe to use directly in tests.

```csharp
namespace {ProjectName}.Application.Services;

public class PasswordEncryption
{
    public string HashPassword(string password)
    {
        return BCrypt.Net.BCrypt.HashPassword(password);
    }

    public bool VerifyPassword(string password, string hashedPassword)
    {
        return BCrypt.Net.BCrypt.Verify(password, hashedPassword);
    }
}
```

### 5.8. DI Extension

```csharp
namespace {ProjectName}.Application;

public static class AppDependencyInjectionExtension
{
    public static void AddApplication(this IServiceCollection services)
    {
        AddUseCases(services);
        services.AddScoped<PasswordEncryption>();
        services.AddScoped<IAuthenticationService, AuthenticationService>();
    }

    private static void AddUseCases(IServiceCollection services)
    {
        services.AddScoped<{Feature}{Operation}UseCase>();
        // ... one line per use case
    }
}
```

---

## 6. Infrastructure Layer

**Path:** `{ProjectName}.Infrastructure/`

### 6.1. DbContext

```csharp
namespace {ProjectName}.Infrastructure;

public class {ProjectName}DbContext(DbContextOptions<{ProjectName}DbContext> options) : DbContext(options)
{
    public DbSet<User> Users { get; set; }
    public DbSet<RefreshToken> RefreshTokens { get; set; }
    // ... one DbSet per entity
}
```

> **Note:** For integration tests, either make the DbContext `public` or add `[assembly: InternalsVisibleTo("WebApi.Test")]` to expose it.

### 6.2. Repositories

All repository classes are **`internal`**. They implement Domain interfaces and are resolved only through DI.

```csharp
namespace {ProjectName}.Infrastructure.Repositories;

internal class {Entity}Repository(
    {ProjectName}DbContext dbContext) : I{Entity}Repository
{
    public async Task Register({Entity} entity)
    {
        await dbContext.{Entities}.AddAsync(entity);
    }

    public async Task<{Entity}?> GetById(Guid id)
    {
        return await dbContext.{Entities}
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == id && e.Active);
    }

    public async Task<List<{Entity}>> GetAll()
    {
        return await dbContext.{Entities}
            .AsNoTracking()
            .ToListAsync();
    }

    public async Task<bool> CheckIfExists({Entity} entity)
    {
        return await dbContext.{Entities}
            .AsNoTracking()
            .AnyAsync(e => e.Name == entity.Name);
    }
}
```

**Key patterns:**
- `AsNoTracking()` for all read operations (performance)
- `Register()` adds to context but does NOT call `SaveChangesAsync` — that's the UnitOfWork's job
- `Active` filter on queries for soft-deleted entities

### 6.3. UnitOfWork

```csharp
namespace {ProjectName}.Infrastructure;

internal class UnitOfWork({ProjectName}DbContext dbContext) : IUnitOfWork
{
    public async Task Commit() => await dbContext.SaveChangesAsync();
}
```

### 6.4. JwtTokenService

Generates and validates JWT tokens. Uses HMAC-SHA256 signing, zero clock skew, no audience/issuer validation.

```csharp
namespace {ProjectName}.Infrastructure.Services;

public class JwtTokenService(string signinKey, uint expirationTimeInMinutes) : ITokenServices
{
    private SymmetricSecurityKey PrivateKey => new(Encoding.UTF8.GetBytes(signinKey));

    private TokenValidationParameters TokenValidationParameters => new()
    {
        ClockSkew = TimeSpan.Zero,
        IssuerSigningKey = PrivateKey,
        ValidateAudience = false,
        ValidateIssuer = false,
    };

    public string Generate(User user)
    {
        var token = new JwtSecurityTokenHandler().CreateJwtSecurityToken(
            subject: new ClaimsIdentity([
                new Claim(ClaimTypes.Name, user.Name),
                new Claim(ClaimTypes.Sid, user.Id.ToString()),
                new Claim(ClaimTypes.Role, user.Role.ToString())
            ]),
            expires: DateTime.UtcNow.AddMinutes(expirationTimeInMinutes),
            signingCredentials: new SigningCredentials(PrivateKey, SecurityAlgorithms.HmacSha256Signature)
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public TokenResultValidation ValidateToken(string token)
    {
        try
        {
            var claimsPrincipal = new JwtSecurityTokenHandler()
                .ValidateToken(token, TokenValidationParameters, out _);
            var userId = Guid.Parse(
                claimsPrincipal.Claims.First(c => c.Type == ClaimTypes.Sid).Value);
            return new TokenResultValidation
                { IsSuccess = true, UserId = userId, Error = TokenErrorType.None };
        }
        catch (SecurityTokenExpiredException)
        {
            return new TokenResultValidation
                { IsSuccess = false, Error = TokenErrorType.Expired };
        }
        catch (Exception)
        {
            return new TokenResultValidation
                { IsSuccess = false, Error = TokenErrorType.Invalid };
        }
    }
}
```

### 6.5. RefreshTokenRepository

Generates refresh tokens as Base64-encoded GUIDs. Maintains one active token per user.

```csharp
namespace {ProjectName}.Infrastructure.Repositories;

internal class RefreshTokenRepository(
    {ProjectName}DbContext dbContext) : IRefreshTokenRepository
{
    public string Generate()
    {
        return Convert.ToBase64String(Guid.NewGuid().ToByteArray());
    }

    public async Task SaveRefreshToken(RefreshToken refreshToken)
    {
        // Remove all previous tokens for this user
        var existing = await dbContext.RefreshTokens
            .Where(rt => rt.UserId == refreshToken.UserId)
            .ToListAsync();
        dbContext.RefreshTokens.RemoveRange(existing);
        await dbContext.RefreshTokens.AddAsync(refreshToken);
        // Does NOT call SaveChangesAsync — UnitOfWork handles it
    }

    public async Task<RefreshToken?> GetRefreshTokenEntity(string token)
    {
        return await dbContext.RefreshTokens
            .FirstOrDefaultAsync(rt => rt.Value == token);
    }
}
```

### 6.6. DI Extension

```csharp
namespace {ProjectName}.Infrastructure;

public static class InfraDependencyInjectionExtension
{
    public static void AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        AddDbContext(services, configuration);
        AddToken(services, configuration);
        AddRepositories(services);
    }

    private static void AddToken(IServiceCollection services, IConfiguration configuration)
    {
        var signingKey = configuration.GetValue<string>("Token:SigningKey");
        var expirationTimeInMinutes = configuration.GetValue<uint>("Token:ExpirationTimeInMinutes");
        if (signingKey is null || expirationTimeInMinutes == 0)
            throw new Exception("Token configuration is invalid");

        services.AddScoped<ITokenServices>(_ =>
            new JwtTokenService(signingKey, expirationTimeInMinutes));
    }

    private static void AddRepositories(IServiceCollection services)
    {
        services.AddScoped<I{Entity}Repository, {Entity}Repository>();
        services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IUsersRepository, UsersRepository>();
    }

    private static void AddDbContext(IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection");
        services.AddDbContext<{ProjectName}DbContext>(options =>
            options.UseNpgsql(connectionString));
    }
}
```

### 6.7. Configuration (`appsettings.json`)

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Database={projectname};Username=postgres;Password=..."
  },
  "Token": {
    "SigningKey": "your-256-bit-signing-key-here",
    "ExpirationTimeInMinutes": 15
  }
}
```

### 6.8. Migration Commands

```bash
# Add a migration
dotnet ef migrations add {MigrationName} \
    --project {ProjectName}.Infrastructure \
    --startup-project {ProjectName}.API

# Apply migrations
dotnet ef database update \
    --project {ProjectName}.Infrastructure \
    --startup-project {ProjectName}.API
```

---

## 7. API Layer

**Path:** `{ProjectName}.API/`

### 7.1. Program.cs

```csharp
using {ProjectName};
using {ProjectName}.Application;
using {ProjectName}.Filters;
using {ProjectName}.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRouting(o => o.LowercaseUrls = true);
builder.Services.AddMvc(f => f.Filters.Add(typeof(ExceptionFilter)));

builder.Services.AddApi();
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddControllers();
builder.Services.AddOpenApi();  // .NET 10: replaces AddEndpointsApiExplorer()

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();           // serves /openapi/v1.json
    app.UseSwaggerUI(options => // Swashbuckle UI pointing to the OpenAPI endpoint
        options.SwaggerEndpoint("/openapi/v1.json", "v1"));
    // Alternative: app.MapScalarApiReference(); // if using Scalar instead of Swashbuckle
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();
app.Run();

public partial class Program { }  // enables WebApplicationFactory<Program> in tests
```

> **Note (.NET 10 change):** `AddEndpointsApiExplorer()` and `app.UseSwagger()` (Swashbuckle document generation) are replaced by the built-in `AddOpenApi()` + `MapOpenApi()`. Swashbuckle is still used for the UI only, or you can use [Scalar](https://github.com/scalar/scalar) as a modern alternative.

### 7.2. ExceptionFilter

Global exception handler registered in Program.cs via `options.Filters.Add(typeof(ExceptionFilter))`.

```csharp
namespace {ProjectName}.Filters;

public class ExceptionFilter(IApiExceptionHandler exceptionHandler) : IExceptionFilter
{
    public void OnException(ExceptionContext context)
    {
        context.Result = exceptionHandler.HandleException(context.Exception, context.HttpContext);
        context.ExceptionHandled = true;
    }
}
```

### 7.3. ApiExceptionHandler

Maps known exceptions to HTTP status codes. In development, unknown exceptions include detailed error info; in production, a generic message.

```csharp
namespace {ProjectName}.ExceptionHandling;

public interface IApiExceptionHandler
{
    ObjectResult HandleException(Exception exception, HttpContext httpContext);
}

public class ApiExceptionHandler : IApiExceptionHandler
{
    public ObjectResult HandleException(Exception exception, HttpContext httpContext)
    {
        var environment = httpContext.RequestServices.GetService<IWebHostEnvironment>();
        var errorMessage = environment?.EnvironmentName == "Development"
            ? GetErrorDetail(exception, httpContext)
            : ResourcesErrorMessages.UNKNOWN_ERROR;

        return exception is {ProjectName}Exception appException
            ? new ObjectResult(new ResponseErrorJson(appException.GetErrorMessages))
                { StatusCode = appException.GetStatusCode }
            : new ObjectResult(new ResponseErrorJson(errorMessage))
                { StatusCode = StatusCodes.Status500InternalServerError };
    }

    private static string GetErrorDetail(Exception exception, HttpContext httpContext)
    {
        var innerMessage = exception.InnerException?.Message ?? "No inner exception was thrown";
        var truncatedMessage = innerMessage.Length > 150
            ? innerMessage[..150] + "..."
            : innerMessage;
        return $"Method: {httpContext.Request.Method} {httpContext.Request.Path}, " +
               $"Error: {exception.Message}, Exception: {truncatedMessage}";
    }
}
```

### 7.4. TokenAuthenticateFilter

Verifies the request has a valid JWT token. Applied with `[{ProjectName}AuthenticateFilter]`.

```csharp
namespace {ProjectName}.Filters;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
public class {ProjectName}AuthenticateFilter() : TypeFilterAttribute(typeof(TokenAuthenticateFilter));

public class TokenAuthenticateFilter(
    IAuthenticationService authenticationService,
    IApiExceptionHandler exceptionHandler) : IAsyncAuthorizationFilter
{
    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        try
        {
            await authenticationService.GetAuthenticatedUser();
        }
        catch (Exception exception)
        {
            context.Result = exceptionHandler.HandleException(exception, context.HttpContext);
        }
    }
}
```

### 7.5. TokenAuthorizeFilter

Verifies the request has a valid token AND the user has the required role. Applied with `[{ProjectName}AuthorizeFilter(Role.Admin, Role.Manager)]`.

```csharp
namespace {ProjectName}.Filters;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
public class {ProjectName}AuthorizeFilter : TypeFilterAttribute
{
    public {ProjectName}AuthorizeFilter(Role requiredRole, params Role[] additionalRoles)
        : base(typeof(TokenAuthorizeFilter))
    {
        Arguments = [requiredRole, additionalRoles];
    }
}

public class TokenAuthorizeFilter(
    IApiExceptionHandler exceptionHandler,
    IAuthenticationService authenticationService,
    Role requiredRole,
    params Role[] additionalRoles) : IAsyncAuthorizationFilter
{
    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        try
        {
            await authenticationService.GetAuthorizedUser(requiredRole, additionalRoles);
        }
        catch (Exception exception)
        {
            context.Result = exceptionHandler.HandleException(exception, context.HttpContext);
        }
    }
}
```

### 7.6. HttpContextTokenProvider

Implements `ITokenProvider`. Extracts the Bearer token from the Authorization header and caches the authenticated user in `HttpContext.Items`.

```csharp
namespace {ProjectName}.Token;

public class HttpContextTokenProvider(IHttpContextAccessor contextAccessor) : ITokenProvider
{
    private const string CachedUserKey = "AuthenticatedUser";

    public string? GetTokenValue()
    {
        var context = contextAccessor.HttpContext
            ?? throw new ArgumentException("HttpContext cannot be null");
        var authHeader = context.Request.Headers.Authorization.ToString();
        if (string.IsNullOrWhiteSpace(authHeader) || authHeader.StartsWith("Bearer ") is false)
            return null;
        return authHeader["Bearer ".Length..];
    }

    public User? GetCachedUser()
    {
        var context = contextAccessor.HttpContext
            ?? throw new ArgumentException("HttpContext cannot be null");
        return context.Items[CachedUserKey] as User;
    }

    public void CacheUser(User user)
    {
        var context = contextAccessor.HttpContext
            ?? throw new ArgumentException("HttpContext cannot be null");
        context.Items[CachedUserKey] = user;
    }
}
```

### 7.7. OpenAPI + Swagger UI Configuration

**.NET 10** uses the built-in `Microsoft.AspNetCore.OpenApi` for document generation. The Swagger UI (or Scalar) is added separately for the interactive explorer.

```csharp
// Inside ApiDependencyInjectionExtension

// 1. Built-in OpenAPI document generation (.NET 10)
services.AddOpenApi(options =>
{
    options.AddDocumentTransformer((document, context, ct) =>
    {
        // Add Bearer security scheme to the generated OpenAPI document
        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes["Bearer"] = new OpenApiSecurityScheme
        {
            Description = "JWT Authorization header. Enter: Bearer {token}",
            Name = "Authorization",
            In = ParameterLocation.Header,
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT"
        };
        return Task.CompletedTask;
    });
});

// 2. Swagger UI (for interactive API explorer in Development)
services.AddSwaggerGen();  // only needed if using Swashbuckle UI
```

> **Alternative: Scalar** — A modern, faster API explorer UI. Install `Scalar.AspNetCore` and call `app.MapScalarApiReference()` instead of `app.UseSwaggerUI()` in Program.cs.

### 7.8. Controllers

Use `[FromServices]` for use case injection per action (not constructor injection). This keeps controllers thin.

```csharp
namespace {ProjectName}.Controllers;

[Route("[controller]")]
[ApiController]
public class {Feature}Controller : ControllerBase
{
    [HttpPost("register")]
    [ProducesResponseType(typeof(Response{Feature}Json), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Register(
        [FromServices] {Feature}RegisterUseCase useCase,
        [FromBody] Request{Feature}Json request)
    {
        var response = await useCase.Execute(request);
        return Created(string.Empty, response);
    }

    [{ProjectName}AuthenticateFilter]
    [HttpGet("getall")]
    [ProducesResponseType(typeof(List<Response{Feature}Json>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(
        [FromServices] {Feature}GetAllUseCase useCase)
    {
        var response = await useCase.Execute();
        return Ok(response);
    }

    [{ProjectName}AuthorizeFilter(Role.Manager, Role.Admin)]
    [HttpGet("getbyid/{id}")]
    [ProducesResponseType(typeof(Response{Feature}Json), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseErrorJson), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(
        [FromServices] {Feature}GetByIdUseCase useCase,
        [FromRoute] Guid id)
    {
        var response = await useCase.Execute(id);
        return Ok(response);
    }
}
```

### 7.9. API DI Extension

```csharp
namespace {ProjectName};

public static class ApiDependencyInjectionExtension
{
    public static void AddApi(this IServiceCollection services)
    {
        services.AddHttpContextAccessor();
        services.AddScoped<ITokenProvider, HttpContextTokenProvider>();
        services.AddSingleton<IApiExceptionHandler, ApiExceptionHandler>();

        // OpenAPI + Swagger UI (see 7.7)
        services.AddOpenApi(options => { /* ... */ });
        services.AddSwaggerGen();  // only if using Swashbuckle UI
    }
}
```

---

## 8. Test Strategy

### 8.1. Philosophy

Tests exist to give **confidence that the system behaves correctly**, not to achieve a coverage number. Every test decision should answer: *"does this test catch a real bug faster than a manual check would?"*

### 8.2. The Testing Trophy

```
         /   E2E   \          <-- Very few. Critical user journeys only.
        / Integration\        <-- MOST tests live here.
       /    Unit      \       <-- Only for pure logic (validators, calculations).
      /  Static/Lint   \      <-- Base: Roslyn analyzers, nullable warnings, arch tests.
```

A single integration test hitting `POST /user/register` covers the controller, validator, use case, repository, and database in one shot. That is more confidence per test than four separate unit tests.

| Tier | Project | What it tests | Dependencies | Database |
|------|---------|---------------|--------------|----------|
| Unit | `Validators.Test` | FluentValidation rules in isolation | Request builders | None |
| Unit | `UseCases.Test` | Business logic with mocked I/O | All builders, NSubstitute | None |
| Integration | `WebApi.Test` | Full HTTP pipeline with real database | Testcontainers PostgreSQL | Real PostgreSQL |

### 8.3. Library Choices

| Library | Purpose | Version |
|---------|---------|---------|
| **xUnit** | Test framework | 2.4.x |
| **Bogus** | Realistic fake data generation | 35.x |
| **NSubstitute** | Mocking | 5.x |
| **Shouldly** | Readable assertions | 4.3.x |
| **Testcontainers.PostgreSql** | Real PostgreSQL in Docker | 3.x |
| **Microsoft.AspNetCore.Mvc.Testing** | `WebApplicationFactory` host | 10.x |

**Why NSubstitute over Moq:** Moq shipped a dependency (SponsorLink) in 2023 that silently collected developer email hashes during builds. NSubstitute has a cleaner API and no such history.

```csharp
// NSubstitute — more natural, no .Object wrapper needed
var repo = Substitute.For<IUsersRepository>();
repo.GetByEmail(Arg.Any<string>()).Returns(user);
```

**Why Shouldly over FluentAssertions:** FluentAssertions moved to a paid commercial license starting at v7. Shouldly is free, actively maintained, and has similar readability.

**Why Testcontainers over InMemory:** EF Core InMemory does not enforce foreign key constraints, does not support transactions, does not support raw SQL, and query translation differs from real PostgreSQL. **Do not use InMemory for new projects.**

### 8.4. Project Structure

```
tests/
├── CommonTestUtilities/           <-- Shared builders, fakers, mock factories
│   ├── CommonTestUtilities.csproj
│   ├── Entities/
│   │   ├── {Entity}Builder.cs
│   │   └── RefreshTokenBuilder.cs
│   ├── Requests/
│   │   └── Request{Feature}JsonBuilder.cs
│   ├── Repositories/
│   │   ├── {Entity}RepositoryBuilder.cs
│   │   └── UnitOfWorkBuilder.cs
│   └── Services/
│       ├── TokenServicesBuilder.cs
│       ├── TokenProviderBuilder.cs
│       └── AuthenticationServiceBuilder.cs
├── Validators.Test/
│   └── {Feature}/
│       └── {Feature}{Operation}ValidatorTest.cs
├── UseCases.Test/
│   ├── {Feature}/
│   │   └── {Feature}{Operation}UseCaseTest.cs
│   └── Services/
│       └── AuthenticationServiceTest.cs
└── WebApi.Test/
    ├── MyContainerFactory.cs
    ├── {Feature}/
    │   └── {Feature}{Operation}Test.cs
    └── ArchitectureTests.cs
```

### 8.5. CommonTestUtilities

#### Entity Builders (Bogus)

`PasswordEncryption` is a concrete class — use the real implementation in builders (no I/O, safe for tests).

```csharp
public class UserBuilder
{
    public static (User user, string plainPassword) Build(Role role = Role.Member)
    {
        var plainPassword = new Faker().Internet.Password(prefix: "Ab1_", length: 12);
        var encryption = new PasswordEncryption();

        var user = new Faker<User>()
            .RuleFor(u => u.Name, f => f.Person.FullName)
            .RuleFor(u => u.Email, (f, u) => f.Internet.Email(u.Name))
            .RuleFor(u => u.Password, _ => encryption.HashPassword(plainPassword))
            .RuleFor(u => u.Role, role);

        return (user, plainPassword);
    }
}
```

```csharp
public class RefreshTokenBuilder
{
    public static RefreshToken Build(Guid userId, bool expired = false)
    {
        return new RefreshToken
        {
            Value = Guid.NewGuid().ToString(),
            UserId = userId,
            CreatedAt = expired
                ? DateTime.UtcNow.AddDays(-8)   // past the 7-day expiration window
                : DateTime.UtcNow
        };
    }
}
```

#### Repository Builders (NSubstitute Fluent)

Fluent interface: each method configures one behavior and returns `this`. Call `.Build()` at the end.

```csharp
public class UserRepositoryBuilder
{
    private readonly IUsersRepository _repository = Substitute.For<IUsersRepository>();

    public UserRepositoryBuilder EmailAlreadyExists(bool exists = true)
    {
        _repository.VerifyIfEmailAlreadyExists(Arg.Any<string>()).Returns(exists);
        return this;
    }

    public UserRepositoryBuilder GetByEmail(User? user)
    {
        _repository.GetByEmail(Arg.Any<string>()).Returns(user);
        return this;
    }

    public UserRepositoryBuilder GetById(User? user)
    {
        _repository.GetById(Arg.Any<Guid>()).Returns(user);
        return this;
    }

    public IUsersRepository Build() => _repository;
}
```

```csharp
public class UnitOfWorkBuilder
{
    public static IUnitOfWork Build() => Substitute.For<IUnitOfWork>();
}
```

#### Service Builders (NSubstitute Fluent)

```csharp
public class TokenServicesBuilder
{
    private readonly ITokenServices _service = Substitute.For<ITokenServices>();

    public TokenServicesBuilder()
    {
        _service.Generate(Arg.Any<User>()).Returns(_ => $"fake-token-{Guid.NewGuid()}");
    }

    public TokenServicesBuilder ValidateSuccess(Guid userId)
    {
        _service.ValidateToken(Arg.Any<string>())
            .Returns(new TokenResultValidation
                { IsSuccess = true, UserId = userId, Error = TokenErrorType.None });
        return this;
    }

    public TokenServicesBuilder ValidateExpired()
    {
        _service.ValidateToken(Arg.Any<string>())
            .Returns(new TokenResultValidation
                { IsSuccess = false, Error = TokenErrorType.Expired });
        return this;
    }

    public TokenServicesBuilder ValidateInvalid()
    {
        _service.ValidateToken(Arg.Any<string>())
            .Returns(new TokenResultValidation
                { IsSuccess = false, Error = TokenErrorType.Invalid });
        return this;
    }

    public ITokenServices Build() => _service;
}
```

```csharp
public class TokenProviderBuilder
{
    private readonly ITokenProvider _provider = Substitute.For<ITokenProvider>();

    public TokenProviderBuilder WithToken(string token)
    {
        _provider.GetCachedUser().Returns((User?)null);
        _provider.GetTokenValue().Returns(token);
        return this;
    }

    public TokenProviderBuilder WithCachedUser(User user)
    {
        _provider.GetCachedUser().Returns(user);
        return this;
    }

    public TokenProviderBuilder WithEmptyToken()
    {
        _provider.GetCachedUser().Returns((User?)null);
        _provider.GetTokenValue().Returns(string.Empty);
        return this;
    }

    public ITokenProvider Build() => _provider;
}
```

```csharp
public class AuthenticationServiceBuilder
{
    private readonly IAuthenticationService _service = Substitute.For<IAuthenticationService>();

    public AuthenticationServiceBuilder AuthenticatedAs(User user)
    {
        _service.GetAuthenticatedUser().Returns(user);
        _service.GetAuthorizedUser(Arg.Any<Role>(), Arg.Any<Role[]>()).Returns(user);
        return this;
    }

    public IAuthenticationService Build() => _service;
}
```

### 8.6. Validators.Test

Tests FluentValidation rules in complete isolation. No mocks, no DI — just instantiate the validator and call `.ValidateAsync()`.

**Pattern:**
1. Build a **valid** request with the builder
2. Break **exactly one** field
3. Assert `IsValid == false` and the correct error message
4. Always include one `Success()` test with an unmodified request

```csharp
public class UserRegisterValidatorTest
{
    private readonly UserRegisterFluentValidation _validator = new();

    [Fact]
    public async Task Success()
    {
        var request = RequestUserRegisterJsonBuilder.Build();
        var result = await _validator.ValidateAsync(request);
        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public async Task Error_Name_Empty()
    {
        var request = RequestUserRegisterJsonBuilder.Build();
        request.Name = string.Empty;

        var result = await _validator.ValidateAsync(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e =>
            e.ErrorMessage == ResourcesErrorMessages.NAME_EMPTY);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(4)]
    [InlineData(7)]
    public async Task Error_Password_Too_Short(int length)
    {
        var request = RequestUserRegisterJsonBuilder.Build(passwordLength: length);

        var result = await _validator.ValidateAsync(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e =>
            e.ErrorMessage == string.Format(ResourcesErrorMessages.PASSWORD_LENGTH, 8));
    }

    [Fact]
    public async Task Error_Role_Invalid()
    {
        var request = RequestUserRegisterJsonBuilder.Build();
        request.Role = (Role)99;

        var result = await _validator.ValidateAsync(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e =>
            e.ErrorMessage == ResourcesErrorMessages.ROLE_INVALID);
    }
}
```

### 8.7. UseCases.Test

Tests business logic. All I/O is mocked. Each test class has a private `CreateUseCase()` factory method.

```csharp
public class UserRegisterUseCaseTest
{
    [Fact]
    public async Task Success()
    {
        var request = RequestUserRegisterJsonBuilder.Build();
        var response = await CreateUseCase().Execute(request);

        response.Name.ShouldBe(request.Name);
        response.Email.ShouldBe(request.Email);
        response.ResponseToken.Token.ShouldNotBeNullOrEmpty();
        response.ResponseToken.RefreshToken.ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public async Task Error_Email_Already_Registered()
    {
        var request = RequestUserRegisterJsonBuilder.Build();
        var act = async () => await CreateUseCase(emailAlreadyExists: true).Execute(request);

        await act.ShouldThrowAsync<ConflictException>();
    }

    [Fact]
    public async Task Error_Invalid_Request()
    {
        var request = RequestUserRegisterJsonBuilder.Build();
        request.Name = string.Empty;

        var act = async () => await CreateUseCase().Execute(request);

        var exception = await act.ShouldThrowAsync<OnValidationException>();
        exception.ErrorMessages.Count.ShouldBeGreaterThan(0);
    }

    private static UserRegisterUseCase CreateUseCase(bool emailAlreadyExists = false)
    {
        var usersRepo = new UserRepositoryBuilder()
            .EmailAlreadyExists(emailAlreadyExists)
            .Build();
        var refreshTokenRepo = new RefreshTokenRepositoryBuilder().Build();
        var unitOfWork = UnitOfWorkBuilder.Build();
        var tokenServices = new TokenServicesBuilder().Build();
        var passwordEncryption = new PasswordEncryption();

        return new UserRegisterUseCase(
            usersRepo, tokenServices, refreshTokenRepo, unitOfWork, passwordEncryption);
    }
}
```

#### AuthenticationService Tests

`AuthenticationService` has its own logic tree and deserves dedicated unit tests.

```csharp
public class AuthenticationServiceTest
{
    [Fact]
    public async Task Success_Returns_Cached_User_Without_Calling_Repository()
    {
        var (user, _) = UserBuilder.Build();
        var tokenProvider = new TokenProviderBuilder().WithCachedUser(user).Build();
        var service = new AuthenticationService(
            tokenProvider, new TokenServicesBuilder().Build(), new UserRepositoryBuilder().Build());

        var result = await service.GetAuthenticatedUser();
        result.ShouldBe(user);
    }

    [Fact]
    public async Task Error_Token_Empty()
    {
        var tokenProvider = new TokenProviderBuilder().WithEmptyToken().Build();
        var service = new AuthenticationService(
            tokenProvider, new TokenServicesBuilder().Build(), new UserRepositoryBuilder().Build());

        var act = async () => await service.GetAuthenticatedUser();
        await act.ShouldThrowAsync<TokenEmptyException>();
    }

    [Fact]
    public async Task Error_Token_Expired()
    {
        var tokenProvider = new TokenProviderBuilder().WithToken("expired").Build();
        var tokenServices = new TokenServicesBuilder().ValidateExpired().Build();
        var service = new AuthenticationService(
            tokenProvider, tokenServices, new UserRepositoryBuilder().Build());

        var act = async () => await service.GetAuthenticatedUser();
        await act.ShouldThrowAsync<TokenExpiredException>();
    }

    [Fact]
    public async Task Error_Token_Valid_But_User_Deleted()
    {
        var tokenProvider = new TokenProviderBuilder().WithToken("valid").Build();
        var tokenServices = new TokenServicesBuilder().ValidateSuccess(Guid.NewGuid()).Build();
        var usersRepo = new UserRepositoryBuilder().GetById(null).Build();
        var service = new AuthenticationService(tokenProvider, tokenServices, usersRepo);

        var act = async () => await service.GetAuthenticatedUser();
        await act.ShouldThrowAsync<TokenWithoutUserException>();
    }

    [Fact]
    public async Task Error_Insufficient_Role()
    {
        var (user, _) = UserBuilder.Build(role: Role.Member);
        var tokenProvider = new TokenProviderBuilder().WithToken("valid").Build();
        var tokenServices = new TokenServicesBuilder().ValidateSuccess(user.Id).Build();
        var usersRepo = new UserRepositoryBuilder().GetById(user).Build();
        var service = new AuthenticationService(tokenProvider, tokenServices, usersRepo);

        var act = async () => await service.GetAuthorizedUser(Role.Admin, Role.Manager);
        await act.ShouldThrowAsync<TokenWithoutPermissionException>();
    }
}
```

### 8.8. WebApi.Test (Integration)

Tests the full HTTP pipeline. **Only integration tests can catch:**
1. Auth filter behavior (401/403 HTTP responses)
2. Database constraints (duplicate keys, FK violations, migration correctness)

#### Prerequisites

1. Make `{ProjectName}DbContext` accessible: add `[assembly: InternalsVisibleTo("WebApi.Test")]` or make it `public`
2. Add `public partial class Program { }` as the last line of `Program.cs`

#### MyContainerFactory

```csharp
public class MyContainerFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _dbContainer =
        new PostgreSqlBuilder().WithImage("postgres:17-alpine").Build();

    private User _memberUser = default!;
    private User _adminUser = default!;
    private string _memberToken = string.Empty;
    private string _adminToken = string.Empty;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            var descriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbContextOptions<{ProjectName}DbContext>));
            if (descriptor is not null)
                services.Remove(descriptor);

            services.AddDbContext<{ProjectName}DbContext>(options =>
                options.UseNpgsql(_dbContainer.GetConnectionString()));
        });
    }

    public async Task InitializeAsync()
    {
        await _dbContainer.StartAsync();

        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<{ProjectName}DbContext>();
        var tokenServices = scope.ServiceProvider.GetRequiredService<ITokenServices>();
        await db.Database.MigrateAsync();

        // Seed test users
        var (member, _) = UserBuilder.Build(role: Role.Member);
        var (admin, _) = UserBuilder.Build(role: Role.Admin);
        db.Users.AddRange(member, admin);
        await db.SaveChangesAsync();

        _memberUser = member;
        _adminUser = admin;
        _memberToken = tokenServices.Generate(member);
        _adminToken = tokenServices.Generate(admin);
    }

    public new async Task DisposeAsync() => await _dbContainer.DisposeAsync();

    // Accessors
    public User GetMemberUser() => _memberUser;
    public User GetAdminUser() => _adminUser;
    public string GetMemberToken() => _memberToken;
    public string GetAdminToken() => _adminToken;

    // HTTP helpers
    public async Task<HttpResponseMessage> DoPost(string endpoint, object body)
        => await CreateClient().PostAsJsonAsync(endpoint, body);

    public async Task<HttpResponseMessage> DoGet(string endpoint)
        => await CreateClient().GetAsync(endpoint);

    public async Task<HttpResponseMessage> DoPostAuthenticated(
        string endpoint, object body, string token)
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);
        return await client.PostAsJsonAsync(endpoint, body);
    }

    public async Task<HttpResponseMessage> DoGetAuthenticated(string endpoint, string token)
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);
        return await client.GetAsync(endpoint);
    }
}
```

#### Integration Test Examples

```csharp
public class RegisterUserTest : IClassFixture<MyContainerFactory>
{
    private readonly MyContainerFactory _factory;
    public RegisterUserTest(MyContainerFactory factory) => _factory = factory;

    [Fact]
    public async Task Success()
    {
        var request = RequestUserRegisterJsonBuilder.Build();
        var response = await _factory.DoPost("user/register", request);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<JsonDocument>();
        body!.RootElement.GetProperty("name").GetString().ShouldBe(request.Name);
        body.RootElement.GetProperty("responseToken")
            .GetProperty("token").GetString().ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public async Task Error_Email_Already_Registered()
    {
        var request = RequestUserRegisterJsonBuilder.Build();
        request.Email = _factory.GetMemberUser().Email;

        var response = await _factory.DoPost("user/register", request);
        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }
}
```

#### Auth Filter Integration Tests

Auth filter behavior (401/403) is **only** tested here — it cannot be verified in unit tests.

```csharp
public class {Feature}GetByIdTest : IClassFixture<MyContainerFactory>
{
    private readonly MyContainerFactory _factory;
    public {Feature}GetByIdTest(MyContainerFactory factory) => _factory = factory;

    [Fact]
    public async Task Error_No_Token_Returns_Unauthorized()
    {
        var response = await _factory.DoGet($"{feature}/{Guid.NewGuid()}/getbyid");
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Error_Member_Cannot_Access_Returns_Forbidden()
    {
        var response = await _factory.DoGetAuthenticated(
            $"{feature}/{Guid.NewGuid()}/getbyid", _factory.GetMemberToken());
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Success_Admin_Can_Access()
    {
        // Assumes data was seeded in InitializeAsync
        var response = await _factory.DoGetAuthenticated(
            $"{feature}/{{seededId}}/getbyid", _factory.GetAdminToken());
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }
}
```

### 8.9. Architecture Tests

One file. Catches structural mistakes automatically on every CI run.

```csharp
public class ArchitectureTests : IClassFixture<MyContainerFactory>
{
    private readonly MyContainerFactory _factory;
    public ArchitectureTests(MyContainerFactory factory) => _factory = factory;

    [Fact]
    public void All_UseCases_Must_Be_Registered_In_DI()
    {
        using var scope = _factory.Services.CreateScope();
        var useCaseTypes = typeof({Feature}RegisterUseCase).Assembly.GetTypes()
            .Where(t => t.Name.EndsWith("UseCase") && !t.IsAbstract && !t.IsInterface);

        foreach (var useCaseType in useCaseTypes)
        {
            var resolved = scope.ServiceProvider.GetService(useCaseType);
            resolved.ShouldNotBeNull(
                $"{useCaseType.Name} must be registered in DI");
        }
    }

    [Fact]
    public void All_Controllers_Must_Declare_Authorization_Intent()
    {
        var controllers = typeof(Program).Assembly.GetTypes()
            .Where(t => typeof(ControllerBase).IsAssignableFrom(t) && !t.IsAbstract);

        foreach (var controller in controllers)
        {
            var hasClassLevelAuth =
                controller.GetCustomAttribute<AuthorizeAttribute>() != null ||
                controller.GetCustomAttribute<AllowAnonymousAttribute>() != null;
            var hasEndpointLevelAuth = controller.GetMethods()
                .Any(m => m.GetCustomAttributes()
                    .Any(a => a is {ProjectName}AuthenticateFilter or {ProjectName}AuthorizeFilter));

            (hasClassLevelAuth || hasEndpointLevelAuth).ShouldBeTrue(
                $"{controller.Name} must declare auth intent at class or endpoint level");
        }
    }
}
```

### 8.10. Mutation Testing with Stryker.NET

Code coverage measures which lines were executed. Stryker measures whether your tests would **catch a bug** by modifying production code and checking if tests go red.

```bash
# Install
dotnet tool install -g dotnet-stryker

# Run against use cases
cd tests/UseCases.Test
dotnet stryker --project "../../{ProjectName}.Application/{ProjectName}.Application.csproj"

# Run against a specific class
dotnet stryker --project "../../{ProjectName}.Application/{ProjectName}.Application.csproj" \
               --mutate "**/AuthenticationService.cs"
```

| Result | Meaning | Action |
|--------|---------|--------|
| **Killed** | Your tests caught it | Good |
| **Surviving** | Your tests missed it | Add a test for that branch |
| **Timeout** | Usually an infinite loop from the mutation | Safe to ignore |

### 8.11. What NOT to Do

**Do not test HTTP status codes in use case tests:**
```csharp
// Wrong — use cases don't return status codes
result.StatusCode.ShouldBe(200);

// Right — test the exception; test the status code in WebApi.Test
await act.ShouldThrowAsync<ConflictException>();
```

**Do not test auth filter behavior in use case tests.** The 401/403 responses come from filters in the HTTP pipeline. Only `WebApi.Test` can verify these.

**Do not use EF Core InMemory for integration tests.** It does not enforce constraints and behaves differently from real PostgreSQL. Use Testcontainers.

**Do not share factory state carelessly.** `IClassFixture<MyContainerFactory>` gives all tests the same instance. Either use unique data (Bogus generates random data) or seed all data once in `InitializeAsync`.

**Do not magic-string error messages:**
```csharp
// Wrong
result.Errors[0].ErrorMessage.ShouldBe("Name cannot be empty");

// Right — use the same constants the production code uses
result.Errors[0].ErrorMessage.ShouldBe(ResourcesErrorMessages.NAME_EMPTY);
```

### 8.12. Test Execution Order (for new projects)

```
Step 1 — Prerequisites
  |- Add `public partial class Program {}` to Program.cs
  |- Expose DbContext to WebApi.Test (InternalsVisibleTo or make it public)

Step 2 — CommonTestUtilities
  |- Entity builders
  |- Request builders
  |- Repository builders
  |- Service builders

Step 3 — Validators.Test
  -> Run: dotnet test tests/Validators.Test

Step 4 — UseCases.Test
  -> Run: dotnet test tests/UseCases.Test

Step 5 — WebApi.Test
  -> Run: dotnet test tests/WebApi.Test  (requires Docker)

Step 6 — Mutation Testing
  -> dotnet stryker (run after all tests are green)
```

---

## 9. Naming Conventions

| Category | Pattern | Example |
|----------|---------|---------|
| Solution | `{ProjectName}.sln` | `MyApp.sln` |
| Project namespace | `{ProjectName}.{Layer}` | `MyApp.Domain` |
| Entity | PascalCase noun, inherits `EntityBase` | `User`, `Recipe` |
| Use case class | `{Entity}{Operation}UseCase` | `UserRegisterUseCase` |
| Validator class | `{Entity}{Operation}FluentValidation` | `UserRegisterFluentValidation` |
| Mapper class | `{Entity}{Operation}Mapper` | `UserRegisterMapper` |
| Repository class | `{Entity}Repository` (internal) | `UsersRepository` |
| Repository interface | `I{Entity}Repository` | `IUsersRepository` |
| Controller | `{Feature}Controller` | `UserController` |
| Auth filter (authenticate) | `{ProjectName}AuthenticateFilter` | `MyAppAuthenticateFilter` |
| Auth filter (authorize) | `{ProjectName}AuthorizeFilter` | `MyAppAuthorizeFilter` |
| Request DTO | `Request{Feature}Json` | `RequestUserRegisterJson` |
| Response DTO | `Response{Feature}Json` | `ResponseUserRegisterJson` |
| DI extension | `{Layer}DependencyInjectionExtension` | `InfraDependencyInjectionExtension` |
| Test class | `{Feature}{Operation}Test` | `UserRegisterUseCaseTest` |
| Builder (entities) | `{Entity}Builder` | `UserBuilder` |
| Builder (requests) | `Request{Feature}JsonBuilder` | `RequestUserRegisterJsonBuilder` |
| Builder (repos) | `{Entity}RepositoryBuilder` | `UserRepositoryBuilder` |
| Namespaces | File-scoped (`;` not `{ }`) | `namespace MyApp.Domain.Entities;` |
| Constructors | Primary constructors (C# 13) | `public class X(IDep dep)` |

---

## 10. NuGet Dependencies

| Project | Package | Purpose |
|---------|---------|---------|
| **Domain** | (none) | Pure domain, zero dependencies |
| **Exception** | (none) | Only .resx resources |
| **Communication** | (none) | Shared DTOs, FrameworkReference only if needed |
| **Application** | FluentValidation | Input validation |
| **Application** | BCrypt.Net-Next | Password hashing |
| **Infrastructure** | Npgsql.EntityFrameworkCore.PostgreSQL 10.x | PostgreSQL provider |
| **Infrastructure** | System.IdentityModel.Tokens.Jwt | JWT generation/validation |
| **Infrastructure** | Microsoft.EntityFrameworkCore 10.x | ORM |
| **Infrastructure** | Microsoft.EntityFrameworkCore.Tools 10.x | Migrations CLI |
| **API** | Microsoft.AspNetCore.OpenApi 10.x | OpenAPI document generation (built-in) |
| **API** | Swashbuckle.AspNetCore (or Scalar.AspNetCore) | Swagger UI / API explorer |
| **CommonTestUtilities** | Bogus | Fake data generation |
| **CommonTestUtilities** | NSubstitute | Mocking |
| **All test projects** | Shouldly | Assertions |
| **All test projects** | xunit + xunit.runner.visualstudio | Test framework |
| **All test projects** | Microsoft.NET.Test.Sdk | Test runner |
| **WebApi.Test** | Testcontainers.PostgreSql | Integration test DB |
| **WebApi.Test** | Microsoft.AspNetCore.Mvc.Testing | WebApplicationFactory |

---

## 11. New Feature Checklist

End-to-end steps for adding a new feature:

1. **Domain** — Create entity (extend `EntityBase`), add repository interface in `Interfaces/`
2. **Exception** — Add any new exception types if needed
3. **Communication** — Create `Request{Feature}Json` and `Response{Feature}Json`
4. **Application — Use Case** — Create `UseCases/{Feature}/{Operation}/` with UseCase, FluentValidation, Mapper
5. **Application — DI** — Register use case in `AppDependencyInjectionExtension`
6. **Infrastructure — Repository** — Implement repository (`internal`), add `DbSet<T>` to DbContext
7. **Infrastructure — DI** — Register repository in `InfraDependencyInjectionExtension`
8. **Infrastructure — Migration** — `dotnet ef migrations add {MigrationName} ...`
9. **API — Controller** — Add action with `[FromServices]` use case injection, `[ProducesResponseType]` attributes, auth filter if needed
10. **Tests — CommonTestUtilities** — Create entity builder, request builder, repository builder
11. **Tests — Validators.Test** — Test each validation rule (success + one broken field per test)
12. **Tests — UseCases.Test** — Test success + each error path with `CreateUseCase()` factory
13. **Tests — WebApi.Test** — Add integration test with `MyContainerFactory` (include auth filter tests if endpoint is protected)
14. **Mutation Testing** — Run `dotnet stryker` after all tests are green
