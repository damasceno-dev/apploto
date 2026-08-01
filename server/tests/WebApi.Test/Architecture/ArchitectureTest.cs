using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using server;
using server.Application;
using server.Application.UseCases.TimeEntries.AddSegment;
using server.Application.UseCases.TimeEntries.DeactivateSegment;
using server.Application.UseCases.TimeEntries.UpdateSegment;
using server.Controllers;
using server.Domain.Interfaces;
using server.Domain.Interfaces.Holidays;
using server.ExceptionHandling;
using server.Filters;
using server.Infrastructure;
using Shouldly;
using Xunit;

namespace WebApi.Test.Architecture;

/// <summary>
/// Project-wide architecture guardrails. These tests are pure reflection /
/// DI-container inspection and intentionally do not boot the Web host or the
/// PostgreSQL Testcontainer, so they run cheaply and in parallel with the
/// integration-test collection.
///
/// Two invariants are enforced across the entire backend — they are NOT scoped
/// to any specific milestone, so new use cases and controllers that are added
/// in later milestones automatically fall under these checks without having to
/// edit the test itself.
///
/// 1. Every concrete <c>*UseCase</c> class in the <c>server.Application</c>
///    assembly is explicitly registered in
///    <see cref="AppDependencyInjection.AddApplication"/>. Missing a
///    registration would only blow up at runtime the first time an endpoint
///    is hit, so the static check is worth it.
///
/// 2. Every action on every <see cref="ControllerBase"/>-derived class in the
///    <c>server.API</c> assembly declares an explicit auth intent — either a
///    protection attribute (<see cref="TokenAuthenticateAttribute"/>,
///    <see cref="TokenAuthenticateBranchAttribute"/>, or
///    <see cref="TokenAuthorizeAttribute"/>) or an explicit anonymous marker
///    (<see cref="AllowAnonymousAttribute"/>), declared at the action or class
///    level. Forgetting to declare any intent would silently expose a protected
///    endpoint to anonymous callers or leave ambiguous whether the omission was
///    intentional.
/// </summary>
public class ArchitectureTest
{
    /// <summary>
    /// Every attribute type that counts as an explicit auth-intent declaration.
    /// An endpoint must carry at least one of these (at the action or class level)
    /// to be considered compliant.
    /// </summary>
    private static readonly Type[] AuthIntentAttributeTypes =
    [
        typeof(TokenAuthenticateAttribute),
        typeof(TokenAuthenticateBranchAttribute),
        typeof(TokenAuthorizeAttribute),
        typeof(AllowAnonymousAttribute),
    ];

    private static readonly string[] PublicServiceNamespacePrefixes =
    [
        "server.Application.Services",
        "server.ExceptionHandling"
    ];

    /// <summary>
    /// Use-case namespaces whose members must never be able to commit. Every read/preview surface
    /// either reads (<c>GET</c>) or computes without persisting (<c>POST … /preview</c>); none may take
    /// an <see cref="IUnitOfWork"/> dependency. Reflection over the constructor signature is the
    /// regression guard — it is immune to the doc-comment mentions of <c>IUnitOfWork</c> these classes
    /// carry to explain the invariant.
    /// </summary>
    private static readonly string[] PreviewNeverCommitsNamespacePrefixes =
    [
        "server.Application.UseCases.Reports",
        "server.Application.UseCases.Transactions.InstallmentPreview",
        "server.Application.UseCases.Transactions.EditPreview",
        "server.Application.UseCases.Transactions.CreatePreview",
        // M7.5 Phase 4 catch-up: the daily-close review context (GET /dailyclose/{id}/review)
        // is a read surface like the report use cases — it must never be able to commit.
        "server.Application.UseCases.DailyCloses.Review",
        "server.Application.UseCases.DailyCloses.VariancePreview"
    ];

    [Fact]
    public void AllUseCases_AreRegisteredInApplicationDi()
    {
        var services = new ServiceCollection();
        services.AddApplication();

        var applicationAssembly = typeof(AppDependencyInjection).Assembly;
        var useCases = applicationAssembly.GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false })
            .Where(t => t.Name.EndsWith("UseCase", StringComparison.Ordinal))
            .ToList();

        useCases.ShouldNotBeEmpty(
            "No use cases were discovered in the server.Application assembly — the reflection filter is probably stale."
        );

        var registeredTypes = services
            .Select(descriptor => descriptor.ServiceType)
            .ToHashSet();

        var unregistered = useCases
            .Where(useCaseType => registeredTypes.Contains(useCaseType) is false)
            .Select(useCaseType => useCaseType.FullName)
            .ToList();

        unregistered.ShouldBeEmpty(
            "The following use cases are not registered in AppDependencyInjection.AddApplication(): "
            + string.Join(", ", unregistered)
        );
    }

    /// <summary>
    /// Complements <see cref="AllUseCases_AreRegisteredInApplicationDi"/>. That check only proves each
    /// <c>*UseCase</c> service type is present in the DI collection — it never builds the provider, so a
    /// use case that is registered but whose constructor takes an <em>unregistered</em> dependency would
    /// still pass and only fail at runtime the first time its endpoint is hit. This test builds the
    /// fully-configured root container (API + Application + Infrastructure) and actually resolves every
    /// <c>*UseCase</c>, turning "registered but not constructible" into a build-time failure instead of a
    /// first-request 500.
    /// </summary>
    [Fact]
    public void AllUseCases_AreResolvableFromConfiguredRootContainer()
    {
        var services = new ServiceCollection();
        services.AddApi();
        services.AddApplication();
        services.AddInfrastructure(BuildArchitectureConfiguration());

        var useCases = typeof(AppDependencyInjection).Assembly.GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false })
            .Where(t => t.Name.EndsWith("UseCase", StringComparison.Ordinal))
            .ToList();

        useCases.ShouldNotBeEmpty(
            "No use cases were discovered in the server.Application assembly — the reflection filter is probably stale."
        );

        using var provider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateScopes = true
        });
        using var scope = provider.CreateScope();

        var failures = useCases
            .Select(useCaseType => TryResolve(scope.ServiceProvider, useCaseType, useCaseType))
            .Where(message => message is not null)
            .ToList();

        failures.ShouldBeEmpty(
            "The following use cases are registered but could not be resolved from the configured container "
            + "(a constructor dependency is missing a registration): "
            + string.Join(" | ", failures)
        );
    }

    [Fact]
    public void PreviewAndReportUseCases_DoNotDependOnUnitOfWork()
    {
        // A reflection check on constructor signatures
        // makes "a preview/report cannot persist" a build-time guarantee, not just a per-test
        // reload/row-count assertion.
        var applicationAssembly = typeof(AppDependencyInjection).Assembly;

        var useCases = applicationAssembly.GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false })
            .Where(t => t.Name.EndsWith("UseCase", StringComparison.Ordinal))
            .Where(t => PreviewNeverCommitsNamespacePrefixes.Any(prefix =>
                t.Namespace?.StartsWith(prefix, StringComparison.Ordinal) is true))
            .ToList();

        useCases.ShouldNotBeEmpty(
            "No report or preview use cases were discovered under the preview-never-commits namespaces — "
            + "the reflection filter is probably stale."
        );

        var committers = useCases
            .Where(useCaseType => useCaseType
                .GetConstructors()
                .SelectMany(constructor => constructor.GetParameters())
                .Any(parameter => parameter.ParameterType == typeof(IUnitOfWork)))
            .Select(useCaseType => useCaseType.FullName)
            .ToList();

        committers.ShouldBeEmpty(
            "The following report/preview use cases declare an IUnitOfWork constructor parameter, so they "
            + "could commit — preview/report surfaces must never be able to persist: "
            + string.Join(", ", committers)
        );
    }

    [Fact]
    public void AllPublicServicesAndExceptionHandlers_AreResolvableFromConfiguredRootContainer()
    {
        var services = new ServiceCollection();
        services.AddApi();
        services.AddApplication();
        services.AddInfrastructure(BuildArchitectureConfiguration());

        var serviceTypes = DiscoverPublicServiceTypes().ToList();
        serviceTypes.ShouldNotBeEmpty(
            "No public services were discovered under server.Application/Services or server.API/ExceptionHandling."
        );

        using var provider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateScopes = true
        });
        using var scope = provider.CreateScope();

        var failures = serviceTypes
            .Select(type => (ServiceType: type, RegistrationType: FindRegistrationType(services, type)))
            .Select(entry => TryResolve(scope.ServiceProvider, entry.ServiceType, entry.RegistrationType))
            .Where(message => message is not null)
            .ToList();

        failures.ShouldBeEmpty(
            "The following public services or exception handlers could not be resolved from the configured container: "
            + string.Join(" | ", failures)
        );
    }

    [Fact]
    public void ExternalBrazilianHolidayProviders_AreResolvableFromConfiguredRootContainer()
    {
        // Phase 7 catch-up: IBrasilApiHolidayProvider and INagerDateHolidayProvider live
        // under server.Domain.Interfaces.Holidays — outside the namespace prefixes scanned
        // by AllPublicServicesAndExceptionHandlers_AreResolvableFromConfiguredRootContainer.
        // Add explicit resolution so a missing AddHttpClient<...> registration in
        // InfraDependencyInjection.AddExternalHolidayProviders breaks the build, not
        // a runtime composite-import call.
        var services = new ServiceCollection();
        services.AddApi();
        services.AddApplication();
        services.AddInfrastructure(BuildArchitectureConfiguration());

        using var provider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateScopes = true
        });
        using var scope = provider.CreateScope();

        scope.ServiceProvider.GetRequiredService<IBrasilApiHolidayProvider>().ShouldNotBeNull();
        scope.ServiceProvider.GetRequiredService<INagerDateHolidayProvider>().ShouldNotBeNull();
    }

    [Fact]
    public void TimeEntrySegmentMutationServices_AreResolvableFromConfiguredRootContainer()
    {
        var services = new ServiceCollection();
        services.AddApi();
        services.AddApplication();
        services.AddInfrastructure(BuildArchitectureConfiguration());

        using var provider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateScopes = true
        });
        using var scope = provider.CreateScope();

        scope.ServiceProvider.GetRequiredService<AddTimeEntrySegmentUseCase>().ShouldNotBeNull();
        scope.ServiceProvider.GetRequiredService<UpdateTimeEntrySegmentUseCase>().ShouldNotBeNull();
        scope.ServiceProvider.GetRequiredService<DeactivateTimeEntrySegmentUseCase>().ShouldNotBeNull();
        scope.ServiceProvider.GetRequiredService<ITimeEntrySegmentsRepository>().ShouldNotBeNull();
    }

    [Fact]
    public void AllControllerEndpoints_DeclareExplicitAuthIntent()
    {
        var apiAssembly = typeof(BranchController).Assembly;
        var controllers = apiAssembly.GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false })
            .Where(t => typeof(ControllerBase).IsAssignableFrom(t))
            .ToList();

        controllers.ShouldNotBeEmpty(
            "No controllers were discovered in the server.API assembly — the reflection filter is probably stale."
        );

        var actionMethods = controllers
            .SelectMany(controller => controller
                .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Where(method => method.GetCustomAttributes<HttpMethodAttribute>(inherit: true).Any())
                .Select(method => (Controller: controller, Method: method)))
            .ToList();

        actionMethods.ShouldNotBeEmpty(
            "No HTTP action methods were discovered on the server.API controllers — the reflection filter is probably stale."
        );

        var endpointsMissingAuth = actionMethods
            .Where(entry =>
            {
                var declaredOnClass = AuthIntentAttributeTypes.Any(authType =>
                    entry.Controller.GetCustomAttributes(authType, inherit: true).Length > 0);
                var declaredOnMethod = AuthIntentAttributeTypes.Any(authType =>
                    entry.Method.GetCustomAttributes(authType, inherit: true).Length > 0);

                return declaredOnClass is false && declaredOnMethod is false;
            })
            .Select(entry => $"{entry.Controller.Name}.{entry.Method.Name}")
            .ToList();

        endpointsMissingAuth.ShouldBeEmpty(
            "The following controller endpoints do not declare explicit auth intent via "
            + "TokenAuthenticate, TokenAuthenticateBranch, TokenAuthorize, or AllowAnonymous: "
            + string.Join(", ", endpointsMissingAuth)
        );
    }

    private static IConfiguration BuildArchitectureConfiguration()
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] =
                    "Host=localhost;Database=loto_architecture_test;Username=postgres;Password=postgres",
                ["Token:SigningKey"] =
                    "architecture-test-signing-key-with-at-least-256-bits",
                ["Token:ExpirationTimeInMinutes"] = "60"
            })
            .Build();
    }

    private static IEnumerable<Type> DiscoverPublicServiceTypes()
    {
        return new[] { typeof(AppDependencyInjection).Assembly, typeof(IApiExceptionHandler).Assembly }
            .SelectMany(assembly => assembly.GetTypes())
            .Where(type => type.IsPublic)
            .Where(type => type.IsInterface || type is { IsClass: true, IsAbstract: false })
            .Where(type => PublicServiceNamespacePrefixes.Any(prefix =>
                type.Namespace?.StartsWith(prefix, StringComparison.Ordinal) is true))
            .Where(type => IsRecordType(type) is false);
    }

    private static Type FindRegistrationType(IServiceCollection services, Type serviceType)
    {
        if (services.Any(descriptor => descriptor.ServiceType == serviceType))
        {
            return serviceType;
        }

        var implementationRegistration = services.FirstOrDefault(descriptor =>
            descriptor.ImplementationType == serviceType);

        return implementationRegistration?.ServiceType ?? serviceType;
    }

    private static string? TryResolve(IServiceProvider serviceProvider, Type serviceType, Type registrationType)
    {
        try
        {
            _ = serviceProvider.GetRequiredService(registrationType);
            return null;
        }
        catch (Exception exception)
        {
            return $"{serviceType.FullName} via {registrationType.FullName}: {exception.GetType().Name} - {exception.Message}";
        }
    }

    private static bool IsRecordType(Type type)
    {
        return type.GetMethod("<Clone>$", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
            is not null;
    }
}
