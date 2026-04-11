using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using server.Infrastructure;
using Testcontainers.PostgreSql;
using Xunit;

namespace WebApi.Test.Infrastructure;

/// <summary>
/// Boots the real <c>Program</c> host against a short-lived PostgreSQL Testcontainer,
/// applies the production EF migrations, and exposes an <see cref="HttpClient"/> plus
/// service provider that integration tests can drive like the real API.
/// </summary>
public class ServerWebApplicationFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private const string TestSigningKey = "LotoTestSigningKey-0123456789ABCDEF-TestOnly";
    private const int TestTokenExpirationMinutes = 60;

    private readonly PostgreSqlContainer _postgresContainer = new PostgreSqlBuilder("postgres:16-alpine")
        .WithDatabase("loto_test")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .Build();

    public async Task InitializeAsync()
    {
        await _postgresContainer.StartAsync();

        // Program.cs consumes `builder.Configuration` eagerly at service registration time
        // (before `builder.Build()`), so `ConfigureWebHost` → `ConfigureAppConfiguration`
        // overrides arrive too late. Environment variables are read by
        // `WebApplication.CreateBuilder` via `AddEnvironmentVariables()` at construction,
        // which is the earliest supported hook for Testcontainers-provided values.
        Environment.SetEnvironmentVariable("ConnectionStrings__DefaultConnection", _postgresContainer.GetConnectionString());
        Environment.SetEnvironmentVariable("Token__SigningKey", TestSigningKey);
        Environment.SetEnvironmentVariable("Token__ExpirationTimeInMinutes", TestTokenExpirationMinutes.ToString());

        // Touch Services so the host gets built with the container connection string
        // already in configuration, then apply the real EF migrations to the container.
        using var scope = Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ServerDbContext>();
        await dbContext.Database.MigrateAsync();
    }

    public new async Task DisposeAsync()
    {
        Environment.SetEnvironmentVariable("ConnectionStrings__DefaultConnection", null);
        Environment.SetEnvironmentVariable("Token__SigningKey", null);
        Environment.SetEnvironmentVariable("Token__ExpirationTimeInMinutes", null);

        await _postgresContainer.DisposeAsync();
        await base.DisposeAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
    }
}
