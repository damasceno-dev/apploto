using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using server;
using server.Application;
using server.Controllers;
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
