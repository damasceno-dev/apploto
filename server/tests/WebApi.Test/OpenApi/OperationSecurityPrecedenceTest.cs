using System.Net;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using server.Domain.Entities.Enums;
using server.Exceptions;
using server.Filters;
using server.OpenApi;
using Shouldly;
using WebApi.Test.Infrastructure;
using Xunit;
using static WebApi.Test.OpenApi.OperationSecurityOpenApiContractTest;

namespace WebApi.Test.OpenApi;

/// <summary>
/// M7.6 follow-up Phase 4.3 (Shared M2 H4): the runtime precedence the resolver models, observed on probe controllers
/// added to a derived test host only.
/// <list type="bullet">
/// <item>The token filters do not consult <c>[AllowAnonymous]</c>: an <c>[AllowAnonymous]</c> action that also
/// carries a token filter still answers 401 without a token, so publishing it as public would be false. The resolver
/// refuses it, and document generation fails instead of guessing.</item>
/// <item>Controller-scope filters apply to every action and add to action-scope ones: every
/// <c>[TokenAuthorize]</c> must pass, so the admitted roles are their intersection.</item>
/// <item>A token filter wrapped in <c>[ServiceFilter]</c> runs exactly like the attribute; the resolver reads its service
/// type. A custom filter factory hides what it creates, so it stops generation.</item>
/// <item>Identity plus branch filters have no single runtime answer: with the branch filter first a branch token passes
/// (the identity filter returns the user the branch filter cached), with the identity filter first both tokens are
/// refused. The resolver refuses the mix rather than publish either order's outcome.</item>
/// </list>
/// Production controllers declare every auth attribute at action scope today; these probes pin the behavior a future
/// controller-scope or mixed declaration would get.
/// </summary>
[Collection(ServerApiCollection.Name)]
public sealed class OperationSecurityPrecedenceTest(ServerWebApplicationFactory factory)
{
    [Fact]
    public async Task AllowAnonymousWithATokenFilter_ShouldStillRequireTheToken_AndStopDocumentGeneration()
    {
        await using var host = AuthTierProbeHost.Create(factory, typeof(AnonymousWithTokenFilterProbeController));
        using var client = host.CreateClient();
        var (_, _, _, branchToken) = await factory.SeedFullBranchContextAsync("H4Precedence", Role.Member);

        using var withoutToken = await client.GetAsync("/h4-probe/anonymous-with-filter");
        withoutToken.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await withoutToken.ReadContentAsync<TestResponseErrorJson>()).ErrorMessages
            .ShouldContain(ResourcesErrorMessages.TOKEN_EMPTY);
        using var withToken = await client.GetAuthAsync("/h4-probe/anonymous-with-filter", branchToken);
        withToken.Headers.Contains(AuthTierProbeHost.ReachedHeader).ShouldBeTrue();

        var action = Descriptor(host.Services, nameof(AnonymousWithTokenFilterProbeController));
        Should.Throw<InvalidOperationException>(() => OperationTokenRequirementResolver.Resolve(action))
            .Message.ShouldContain("[AllowAnonymous] but also carries a token filter");

        using var document = await client.GetAsync("/openapi/v1.json");
        document.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
        (await document.Content.ReadAsStringAsync()).ShouldContain("[AllowAnonymous] but also carries a token filter");
    }

    [Fact]
    public async Task ControllerAndActionScopeFilters_ShouldCombine_InTheDocumentAndAtRuntime()
    {
        await using var host = AuthTierProbeHost.Create(factory, typeof(ControllerScopeRoleProbeController));
        using var client = host.CreateClient();

        var inherited = OperationTokenRequirementResolver.Resolve(
            Descriptor(host.Services, nameof(ControllerScopeRoleProbeController), nameof(ControllerScopeRoleProbeController.Inherited)));
        inherited.Tier.ShouldBe(TokenTier.Branch);
        inherited.Roles.ShouldBe([Role.Admin, Role.Manager]);
        var narrowed = OperationTokenRequirementResolver.Resolve(
            Descriptor(host.Services, nameof(ControllerScopeRoleProbeController), nameof(ControllerScopeRoleProbeController.Narrowed)));
        narrowed.Tier.ShouldBe(TokenTier.Branch);
        narrowed.Roles.ShouldBe([Role.Manager]);

        using var document = await LoadDocumentAsync(host);
        var probes = PublishedOperations(document.RootElement)
            .Where(operation => operation.Path.StartsWith("/h4-probe/", StringComparison.Ordinal))
            .ToDictionary(operation => operation.Path);
        probes["/h4-probe/inherited"].Roles.ShouldBe([Role.Admin, Role.Manager]);
        probes["/h4-probe/narrowed"].Roles.ShouldBe([Role.Manager]);

        var credentials = await SeedCredentialsAsync(factory);
        foreach (var operation in probes.Values)
        {
            foreach (var credential in credentials)
            {
                (await ObserveAsync(client, operation, credential))
                    .ShouldBe(Expected(operation, credential), $"{operation.Key} with {credential.Name}");
            }
        }
    }

    [Fact]
    public async Task ServiceWrappedTokenFilter_ShouldBePublishedAsItsTier_AndConflictWithAllowAnonymous()
    {
        static void RegisterFilter(IServiceCollection services) => services.AddScoped<TokenAuthenticateFilter>();
        await using var host = AuthTierProbeHost.Create(factory, [typeof(ServiceWrappedProbeController)], RegisterFilter, shortCircuitActions: true);
        using var client = host.CreateClient();

        using var document = await LoadDocumentAsync(host);
        var operation = PublishedOperations(document.RootElement).Single(candidate => candidate.Path == "/h4-probe/service");
        operation.Tier.ShouldBe(TokenTierName.Identity);
        foreach (var credential in await SeedCredentialsAsync(factory))
        {
            (await ObserveAsync(client, operation, credential))
                .ShouldBe(Expected(operation, credential), $"{operation.Key} with {credential.Name}");
        }

        await using var anonymousHost = AuthTierProbeHost.Create(
            factory, [typeof(AnonymousServiceWrappedProbeController)], RegisterFilter, shortCircuitActions: true);
        using var anonymousClient = anonymousHost.CreateClient();
        using var withoutToken = await anonymousClient.GetAsync("/h4-probe/anonymous-service");
        withoutToken.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        await AssertGenerationFailsAsync(anonymousClient, "[AllowAnonymous] but also carries a token filter");
    }

    [Fact]
    public async Task CustomFilterFactory_ShouldStopDocumentGeneration_WhateverItCreates()
    {
        await using var host = AuthTierProbeHost.Create(factory, typeof(CustomFactoryProbeController));
        using var client = host.CreateClient();

        using var withoutToken = await client.GetAsync("/h4-probe/factory");
        withoutToken.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        await AssertGenerationFailsAsync(client, "filter factory");
    }

    [Fact]
    public async Task MixedIdentityAndBranchFilters_ShouldDependOnOrderAtRuntime_AndStopDocumentGeneration()
    {
        var (user, _, _, branchToken) = await factory.SeedFullBranchContextAsync("H4MixedOrder", Role.Admin);
        var identityToken = factory.IssueGlobalToken(user);

        await using var branchFirst = AuthTierProbeHost.Create(factory, typeof(BranchFirstProbeController));
        using var branchFirstClient = branchFirst.CreateClient();
        using (var response = await branchFirstClient.GetAuthAsync("/h4-probe/branch-first", branchToken))
            response.Headers.Contains(AuthTierProbeHost.ReachedHeader).ShouldBeTrue("a branch token passes when the branch filter runs first");
        using (var response = await branchFirstClient.GetAuthAsync("/h4-probe/branch-first", identityToken))
            response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        await AssertGenerationFailsAsync(branchFirstClient, "mixes identity-token and branch-token filters");

        await using var identityFirst = AuthTierProbeHost.Create(factory, typeof(IdentityFirstProbeController));
        using var identityFirstClient = identityFirst.CreateClient();
        foreach (var token in new[] { branchToken, identityToken })
        {
            using var response = await identityFirstClient.GetAuthAsync("/h4-probe/identity-first", token);
            response.StatusCode.ShouldBe(HttpStatusCode.Forbidden, "both tokens are refused when the identity filter runs first");
        }

        await AssertGenerationFailsAsync(identityFirstClient, "mixes identity-token and branch-token filters");
    }

    private static async Task AssertGenerationFailsAsync(HttpClient client, string message)
    {
        using var document = await client.GetAsync("/openapi/v1.json");
        document.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
        (await document.Content.ReadAsStringAsync()).ShouldContain(message);
    }

    /// <summary>The descriptor MVC itself built for the probe action, controller-scope filters included.</summary>
    private static ControllerActionDescriptor Descriptor(IServiceProvider services, string controller, string? action = null) =>
        services.GetRequiredService<IActionDescriptorCollectionProvider>().ActionDescriptors.Items
            .OfType<ControllerActionDescriptor>()
            .Single(descriptor => descriptor.ControllerTypeInfo.Name == controller &&
                                  (action is null || descriptor.ActionName == action));

    [ApiController]
    [Route("h4-probe")]
    public sealed class AnonymousWithTokenFilterProbeController : ControllerBase
    {
        [HttpGet]
        [Route("anonymous-with-filter")]
        [AllowAnonymous]
        [TokenAuthenticateBranch]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        public IActionResult Get() => NoContent();
    }

    [ApiController]
    [Route("h4-probe")]
    public sealed class ServiceWrappedProbeController : ControllerBase
    {
        [HttpGet]
        [Route("service")]
        [ServiceFilter(typeof(TokenAuthenticateFilter))]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        public IActionResult Get() => NoContent();
    }

    [ApiController]
    [Route("h4-probe")]
    public sealed class AnonymousServiceWrappedProbeController : ControllerBase
    {
        [HttpGet]
        [Route("anonymous-service")]
        [AllowAnonymous]
        [ServiceFilter(typeof(TokenAuthenticateFilter))]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        public IActionResult Get() => NoContent();
    }

    [ApiController]
    [Route("h4-probe")]
    public sealed class CustomFactoryProbeController : ControllerBase
    {
        [HttpGet]
        [Route("factory")]
        [AllowAnonymous]
        [IdentityFilterFactory]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        public IActionResult Get() => NoContent();
    }

    /// <summary>Creates the real identity filter at runtime; nothing about that is visible to the resolver.</summary>
    [AttributeUsage(AttributeTargets.Method)]
    public sealed class IdentityFilterFactoryAttribute : Attribute, IFilterFactory
    {
        public bool IsReusable => false;

        public IFilterMetadata CreateInstance(IServiceProvider serviceProvider) =>
            ActivatorUtilities.CreateInstance<TokenAuthenticateFilter>(serviceProvider);
    }

    [ApiController]
    [Route("h4-probe")]
    [TokenAuthenticateBranch]
    public sealed class BranchFirstProbeController : ControllerBase
    {
        [HttpGet]
        [Route("branch-first")]
        [TokenAuthenticate]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        public IActionResult Get() => NoContent();
    }

    [ApiController]
    [Route("h4-probe")]
    [TokenAuthenticate]
    public sealed class IdentityFirstProbeController : ControllerBase
    {
        [HttpGet]
        [Route("identity-first")]
        [TokenAuthenticateBranch]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        public IActionResult Get() => NoContent();
    }

    [ApiController]
    [Route("h4-probe")]
    [TokenAuthorize(Role.Admin, Role.Manager)]
    public sealed class ControllerScopeRoleProbeController : ControllerBase
    {
        [HttpGet]
        [Route("inherited")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        public IActionResult Inherited() => NoContent();

        [HttpGet]
        [Route("narrowed")]
        [TokenAuthorize(Role.Manager, Role.Member)]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        public IActionResult Narrowed() => NoContent();
    }
}
