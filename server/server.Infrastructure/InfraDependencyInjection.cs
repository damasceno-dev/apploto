using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using server.Domain.Interfaces;
using server.Infrastructure.Repositories;
using server.Infrastructure.Services;

namespace server.Infrastructure;

public static class InfraDependencyInjection
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
        var expirationTimeInMinutes = configuration.GetValue<uint?>("Token:ExpirationTimeInMinutes");

        if (string.IsNullOrWhiteSpace(signingKey))
        {
            throw new InvalidOperationException("Missing required configuration: Token:SigningKey");
        }

        if (expirationTimeInMinutes is null or 0)
        {
            throw new InvalidOperationException("Missing required configuration: Token:ExpirationTimeInMinutes");
        }

        services.AddScoped<ITokenServices>(_ => new JwtTokenService(signingKey, expirationTimeInMinutes.Value));
    }

    private static void AddRepositories(IServiceCollection services)
    {
        services.AddScoped<IUsersRepository, UsersRepository>();
        services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();
    }

    private static void AddDbContext(IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("Missing required configuration: ConnectionStrings:DefaultConnection");
        }

        services.AddDbContext<ServerDbContext>(options => options.UseNpgsql(connectionString));
    }
}
