using System.Reflection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using WebApi.Test.Infrastructure;

namespace WebApi.Test.OpenApi;

/// <summary>
/// A derived test host for observing which token an operation's authorization filters admit, without running the
/// action. It adds one global action filter ordered before every other action filter (model-state validation
/// included): MVC runs it only after all authorization filters let the request through, and it answers with
/// <see cref="ReachedHeader"/> instead of executing the action, so a probe has no side effects. Optional probe
/// controllers are added to this host only; they are nested types, which MVC's default discovery never picks up.
/// Nothing here changes the application's own pipeline or authorization filters.
/// </summary>
internal static class AuthTierProbeHost
{
    public const string ReachedHeader = "X-Auth-Tier-Probe";

    public static WebApplicationFactory<Program> Create(ServerWebApplicationFactory factory, params Type[] probeControllers) =>
        Create(factory, probeControllers, configureServices: null, shortCircuitActions: true);

    /// <param name="configureServices">Extra test-host registrations (for example a filter service or OpenAPI options).</param>
    /// <param name="shortCircuitActions">False keeps the probe controllers but lets actions run, for wire round-trips.</param>
    public static WebApplicationFactory<Program> Create(
        ServerWebApplicationFactory factory,
        IReadOnlyCollection<Type> probeControllers,
        Action<IServiceCollection>? configureServices,
        bool shortCircuitActions) =>
        factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            if (shortCircuitActions)
                services.Configure<MvcOptions>(options => options.Filters.Add(new ReachedActionFilter()));
            if (probeControllers.Count > 0)
            {
                services.AddControllers().ConfigureApplicationPartManager(manager =>
                    manager.FeatureProviders.Add(new ProbeControllerFeatureProvider(probeControllers)));
            }

            configureServices?.Invoke(services);
        }));

    private sealed class ReachedActionFilter : IAsyncActionFilter, IOrderedFilter
    {
        public int Order => int.MinValue;

        public Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            context.HttpContext.Response.Headers[ReachedHeader] = "reached";
            context.Result = new NoContentResult();
            return Task.CompletedTask;
        }
    }

    private sealed class ProbeControllerFeatureProvider(IEnumerable<Type> controllers) : IApplicationFeatureProvider<ControllerFeature>
    {
        public void PopulateFeature(IEnumerable<ApplicationPart> parts, ControllerFeature feature)
        {
            foreach (var controller in controllers.Select(type => type.GetTypeInfo()))
            {
                if (feature.Controllers.Contains(controller) is false)
                    feature.Controllers.Add(controller);
            }
        }
    }
}
