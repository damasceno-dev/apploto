using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using server.Application.Services.Security;
using server.Application.Services.Transactions;
using server.Domain.Interfaces.Holidays;
using server.Domain.Interfaces;
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
    private string? _previousConnection;
    private string? _previousSigningKey;
    private string? _previousTokenExpiration;

    private readonly PostgreSqlContainer _postgresContainer = new PostgreSqlBuilder("postgres:16-alpine")
         .WithCreateParameterModifier(parameters =>
        {
            foreach (var bindings in parameters.HostConfig!.PortBindings!.Values)
                foreach (var binding in bindings) binding.HostIP = "127.0.0.1";
        })
        .WithDatabase("loto_test")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .Build();

    public string DisposableConnectionString => _postgresContainer.GetConnectionString();

    public async Task InitializeAsync()
    {
        await _postgresContainer.StartAsync();

        _previousConnection = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection");
        _previousSigningKey = Environment.GetEnvironmentVariable("Token__SigningKey");
        _previousTokenExpiration = Environment.GetEnvironmentVariable("Token__ExpirationTimeInMinutes");

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
        Environment.SetEnvironmentVariable("ConnectionStrings__DefaultConnection", _previousConnection);
        Environment.SetEnvironmentVariable("Token__SigningKey", _previousSigningKey);
        Environment.SetEnvironmentVariable("Token__ExpirationTimeInMinutes", _previousTokenExpiration);

        await _postgresContainer.DisposeAsync();
        await base.DisposeAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // OpenAPI is intentionally exposed only in Development by Program.cs. The test
        // host uses that supported environment so contract tests exercise the production
        // predicate without adding a test-only branch to the application pipeline.
        builder.UseEnvironment("Development");

        builder.ConfigureServices(services =>
        {
            // Security tests never deliver to an external provider, regardless of inherited configuration.
            services.RemoveAll<ISecurityMailSender>();
            services.AddSingleton<SecurityMailCapture>();
            services.AddSingleton<ISecurityMailSender>(provider => provider.GetRequiredService<SecurityMailCapture>());
            // Replace the typed-HttpClient external holiday providers with stubbed
            // unavailable instances so the test suite never reaches BrasilAPI or
            // Nager.Date over the network. The composite resolver swallows these
            // failures and backfills every concept from the canonical calendar.
            services.RemoveAll<IBrasilApiHolidayProvider>();
            services.RemoveAll<INagerDateHolidayProvider>();
            services.AddSingleton<IBrasilApiHolidayProvider, UnavailableBrasilApiHolidayProvider>();
            services.AddSingleton<INagerDateHolidayProvider, UnavailableNagerDateHolidayProvider>();
            // M11 Phase 7.5: a settable instant for time-dependent scenarios. Unfrozen it reads real time, and the
            // production registration in AddApplication is left as it is.
            services.RemoveAll<IBranchClock>();
            services.AddSingleton<FrozenClockState>();
            services.AddScoped<IBranchClock, FrozenBranchClock>();
            // M11 Phase 7.5: an armable command interceptor (inactive unless a test arms it) for the snapshot,
            // cancellation and atomic-failure scenarios.
            services.AddSingleton<ArmableCommandInterceptor>();
            services.ConfigureDbContext<ServerDbContext>((provider, options) =>
                options.AddInterceptors(provider.GetRequiredService<ArmableCommandInterceptor>()));
            // M8 Phase 8: a test may choose the connection's peer address (TestServer has none).
            services.AddTransient<IStartupFilter, TestPeerStartupFilter>();
            // Every shared-host request comes from one peer, so the per-network request limiter would throttle the
            // whole suite. Only that process-local limit is raised; the persistent login budgets keep their defaults.
            services.RemoveAll<LoginProtectionOptions>();
            services.AddSingleton(LoginProtectionOptionsFactory.Create(
                LoginProtectionOptionsFactory.MaxRequestsPerMinute.ToString(), null, null, null, null));
        });
    }
}
