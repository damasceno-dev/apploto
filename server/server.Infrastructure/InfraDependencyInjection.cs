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
    private const int DefaultMonthLockCoordinationLockTimeoutSeconds = 5;
    private const int DefaultAccountPairingCoordinationLockTimeoutSeconds = 5;
    private const int DefaultIdempotencyRequestCoordinationLockTimeoutSeconds = 5;
    private const int DefaultIdempotencyRequestCleanupIntervalHours = 24;
    private const int DefaultIdempotencyRequestCleanupBatchSize = 500;
    private const int DefaultSecurityThrottleCleanupIntervalMinutes = 15;
    private const int DefaultSecurityThrottleCleanupBatchSize = 500;

    public static void AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        AddDbContext(services, configuration);
        AddToken(services, configuration);
        services.AddSingleton(SecurityMailOptions.FromConfiguration(configuration));
        services.AddSingleton<ISecurityMailSender, SmtpSecurityMailSender>();
        services.AddSingleton<IGoogleIdentityVerifier>(_ => new GoogleIdentityVerifier(
            configuration.GetSection("GoogleIdentity:ClientIds").Get<string[]>() ?? []));
        services.AddSingleton<RecoveryMailQueue>();
        services.AddSingleton<IRecoveryMailQueue>(provider => provider.GetRequiredService<RecoveryMailQueue>());
        services.AddHostedService(provider => provider.GetRequiredService<RecoveryMailQueue>());
        AddRepositories(services, configuration);
        AddIdempotencyRequestCleanup(services, configuration);
        AddSecurityThrottleCleanup(services, configuration);
        AddExternalHolidayProviders(services, configuration);
    }

    private static void AddIdempotencyRequestCleanup(
        IServiceCollection services,
        IConfiguration configuration)
    {
        var intervalHours = configuration.GetValue<int?>(
            "IdempotencyRequestCleanup:SweepIntervalHours")
            ?? DefaultIdempotencyRequestCleanupIntervalHours;
        if (intervalHours <= 0)
        {
            throw new InvalidOperationException(
                "IdempotencyRequestCleanup:SweepIntervalHours must be greater than zero.");
        }

        var batchSize = configuration.GetValue<int?>("IdempotencyRequestCleanup:BatchSize")
            ?? DefaultIdempotencyRequestCleanupBatchSize;
        if (batchSize <= 0)
        {
            throw new InvalidOperationException(
                "IdempotencyRequestCleanup:BatchSize must be greater than zero.");
        }

        services.AddSingleton(new IdempotencyRequestCleanupOptions(
            TimeSpan.FromHours(intervalHours),
            batchSize));
        services.AddSingleton(TimeProvider.System);
        services.AddScoped<IdempotencyRequestCleanup>();
        services.AddHostedService<IdempotencyRequestCleanupHostedService>();
    }

    private static void AddSecurityThrottleCleanup(IServiceCollection services, IConfiguration configuration)
    {
        var intervalMinutes = configuration.GetValue<int?>("SecurityThrottleCleanup:SweepIntervalMinutes")
            ?? DefaultSecurityThrottleCleanupIntervalMinutes;
        if (intervalMinutes <= 0)
            throw new InvalidOperationException("SecurityThrottleCleanup:SweepIntervalMinutes must be greater than zero.");

        var batchSize = configuration.GetValue<int?>("SecurityThrottleCleanup:BatchSize")
            ?? DefaultSecurityThrottleCleanupBatchSize;
        if (batchSize <= 0)
            throw new InvalidOperationException("SecurityThrottleCleanup:BatchSize must be greater than zero.");

        services.AddSingleton(new SecurityThrottleCleanupOptions(TimeSpan.FromMinutes(intervalMinutes), batchSize));
        services.AddScoped<SecurityThrottleCleanup>();
        services.AddHostedService<SecurityThrottleCleanupHostedService>();
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

        var monthLockTimeoutSeconds = configuration.GetValue<int?>(
            "MonthLockCoordination:LockTimeoutSeconds")
            ?? DefaultMonthLockCoordinationLockTimeoutSeconds;
        if (monthLockTimeoutSeconds <= 0)
        {
            throw new InvalidOperationException(
                "MonthLockCoordination:LockTimeoutSeconds must be greater than zero.");
        }

        services.AddSingleton(new MonthLockCoordinationOptions(
            TimeSpan.FromSeconds(monthLockTimeoutSeconds)));

        var idempotencyLockTimeoutSeconds = configuration.GetValue<int?>(
            "IdempotencyRequestCoordination:LockTimeoutSeconds")
            ?? DefaultIdempotencyRequestCoordinationLockTimeoutSeconds;
        if (idempotencyLockTimeoutSeconds <= 0)
        {
            throw new InvalidOperationException(
                "IdempotencyRequestCoordination:LockTimeoutSeconds must be greater than zero.");
        }

        services.AddSingleton(new IdempotencyRequestCoordinationOptions(
            TimeSpan.FromSeconds(idempotencyLockTimeoutSeconds)));

        var accountPairingLockTimeoutSeconds = configuration.GetValue<int?>(
            "AccountPairingCoordination:LockTimeoutSeconds")
            ?? DefaultAccountPairingCoordinationLockTimeoutSeconds;
        if (accountPairingLockTimeoutSeconds <= 0)
        {
            throw new InvalidOperationException(
                "AccountPairingCoordination:LockTimeoutSeconds must be greater than zero.");
        }

        services.AddSingleton(new AccountPairingCoordinationOptions(
            TimeSpan.FromSeconds(accountPairingLockTimeoutSeconds)));
        services.AddScoped<ISecurityCoordination, SecurityCoordination>();
        services.AddScoped<IAccountSecurityRepository, AccountSecurityRepository>();
        services.AddScoped<ILoginThrottleRepository, LoginThrottleRepository>();
        services.AddScoped<IOnboardingRepository, OnboardingRepository>();
        services.AddScoped<IOperatedOnboardingRepository, OperatedOnboardingRepository>();
        services.AddScoped<IGoogleIdentityRepository, GoogleIdentityRepository>();
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
        services.AddScoped<IFiadoProjectionRepository, FiadoProjectionRepository>();
        services.AddScoped<IDailyClosesRepository, DailyClosesRepository>();
        services.AddScoped<IDailyCloseItemsRepository, DailyCloseItemsRepository>();
        services.AddScoped<IDailyCloseReviewEventsRepository, DailyCloseReviewEventsRepository>();
        services.AddScoped<ITerminalsRepository, TerminalsRepository>();
        services.AddScoped<ISessionOpeningsRepository, SessionOpeningsRepository>();
        services.AddScoped<ISessionHandoversRepository, SessionHandoversRepository>();
        services.AddScoped<ISessionOpeningDiscrepanciesRepository, SessionOpeningDiscrepanciesRepository>();
        services.AddScoped<ISessionEventsRepository, SessionEventsRepository>();
        services.AddScoped<IOpeningCountsRepository, OpeningCountsRepository>();
        services.AddScoped<ICustodyMappingIssuesRepository, CustodyMappingIssuesRepository>();
        services.AddScoped<ITimeEntriesRepository, TimeEntriesRepository>();
        services.AddScoped<ITimeEntrySegmentsRepository, TimeEntrySegmentsRepository>();
        services.AddScoped<ITimeEntryPoliciesRepository, TimeEntryPoliciesRepository>();
        services.AddScoped<IHolidaysRepository, HolidaysRepository>();
        services.AddScoped<IIdempotencyRequestsRepository, IdempotencyRequestsRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IReportReadSnapshot, ReportReadSnapshot>();
        services.AddScoped<IIdempotencyRequestCoordination, IdempotencyRequestCoordination>();
        services.AddScoped<IMonthLockCoordination, MonthLockCoordination>();
        services.AddScoped<IDailyCloseAccountCoordination, DailyCloseAccountCoordination>();
        services.AddScoped<IAccountPairingCoordination, AccountPairingCoordination>();
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
