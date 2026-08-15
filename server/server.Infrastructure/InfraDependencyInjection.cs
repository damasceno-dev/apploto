using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using server.Domain.Interfaces;
using server.Domain.Interfaces.Holidays;
using server.Infrastructure.Holidays.External;
using server.Infrastructure.Repositories;
using server.Infrastructure.Services;

namespace server.Infrastructure;

public static class InfraDependencyInjection
{
    private const string BrasilApiDefaultBaseUrl = "https://brasilapi.com.br";
    private const string NagerDateDefaultBaseUrl = "https://date.nager.at";
    private const int DefaultExternalHolidayTimeoutSeconds = 5;
    private const int DefaultDailyCloseAccountCoordinationLockTimeoutSeconds = 5;

    public static void AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        AddDbContext(services, configuration);
        AddToken(services, configuration);
        AddRepositories(services, configuration);
        AddExternalHolidayProviders(services, configuration);
    }

    private static void AddExternalHolidayProviders(IServiceCollection services, IConfiguration configuration)
    {
        var brasilApiBaseUrl = configuration.GetValue<string>("ExternalHolidaySources:BrasilApi:BaseUrl");
        var brasilApiTimeoutSeconds = configuration.GetValue<int?>("ExternalHolidaySources:BrasilApi:TimeoutSeconds")
            ?? DefaultExternalHolidayTimeoutSeconds;

        var nagerBaseUrl = configuration.GetValue<string>("ExternalHolidaySources:NagerDate:BaseUrl");
        var nagerTimeoutSeconds = configuration.GetValue<int?>("ExternalHolidaySources:NagerDate:TimeoutSeconds")
            ?? DefaultExternalHolidayTimeoutSeconds;

        services.AddHttpClient<IBrasilApiHolidayProvider, BrasilApiHolidayProvider>(client =>
        {
            var baseUrl = string.IsNullOrWhiteSpace(brasilApiBaseUrl)
                ? BrasilApiDefaultBaseUrl
                : brasilApiBaseUrl;
            client.BaseAddress = new Uri(EnsureTrailingSlash(baseUrl));
            client.Timeout = TimeSpan.FromSeconds(brasilApiTimeoutSeconds);
        });

        services.AddHttpClient<INagerDateHolidayProvider, NagerDateHolidayProvider>(client =>
        {
            var baseUrl = string.IsNullOrWhiteSpace(nagerBaseUrl)
                ? NagerDateDefaultBaseUrl
                : nagerBaseUrl;
            client.BaseAddress = new Uri(EnsureTrailingSlash(baseUrl));
            client.Timeout = TimeSpan.FromSeconds(nagerTimeoutSeconds);
        });
    }

    private static string EnsureTrailingSlash(string baseUrl) =>
        baseUrl.EndsWith('/') ? baseUrl : baseUrl + "/";

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

    private static void AddRepositories(IServiceCollection services, IConfiguration configuration)
    {
        var coordinationLockTimeoutSeconds = configuration.GetValue<int?>(
            "DailyCloseAccountCoordination:LockTimeoutSeconds")
            ?? DefaultDailyCloseAccountCoordinationLockTimeoutSeconds;
        if (coordinationLockTimeoutSeconds <= 0)
        {
            throw new InvalidOperationException(
                "DailyCloseAccountCoordination:LockTimeoutSeconds must be greater than zero.");
        }

        services.AddSingleton(new DailyCloseAccountCoordinationOptions(
            TimeSpan.FromSeconds(coordinationLockTimeoutSeconds)));
        services.AddScoped<IUsersRepository, UsersRepository>();
        services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
        services.AddScoped<IBranchesRepository, BranchesRepository>();
        services.AddScoped<IBranchUsersRepository, BranchUsersRepository>();
        services.AddScoped<ICategoriesRepository, CategoriesRepository>();
        services.AddScoped<ITransactionTypesRepository, TransactionTypesRepository>();
        services.AddScoped<IProductsRepository, ProductsRepository>();
        services.AddScoped<ISettingsRepository, SettingsRepository>();
        services.AddScoped<IOperatorsRepository, OperatorsRepository>();
        services.AddScoped<IAccountsRepository, AccountsRepository>();
        services.AddScoped<IOperatorAccountsRepository, OperatorAccountsRepository>();
        services.AddScoped<IClientsRepository, ClientsRepository>();
        services.AddScoped<ITransactionsRepository, TransactionsRepository>();
        services.AddScoped<IDailyClosesRepository, DailyClosesRepository>();
        services.AddScoped<IDailyCloseItemsRepository, DailyCloseItemsRepository>();
        services.AddScoped<ITimeEntriesRepository, TimeEntriesRepository>();
        services.AddScoped<ITimeEntrySegmentsRepository, TimeEntrySegmentsRepository>();
        services.AddScoped<IHolidaysRepository, HolidaysRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IDailyCloseAccountCoordination, DailyCloseAccountCoordination>();
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
