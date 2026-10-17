using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using server.Domain.Entities.Enums;
using server.Filters;
using server.OpenApi;
using Shouldly;
using Xunit;

namespace WebApi.Test.OpenApi;

/// <summary>
/// M7.6 follow-up Phase 4.3 (Shared M2 H4): the resolver's decision table on synthetic action descriptors. The real
/// MVC-built descriptors (controller-scope inheritance) and the runtime behavior they describe are covered by
/// <see cref="OperationSecurityPrecedenceTest"/> and <see cref="OperationSecurityOpenApiContractTest"/>.
/// </summary>
public sealed class OperationTokenRequirementResolverTest
{
    [Fact]
    public void AllowAnonymousWithoutTokenFilter_ShouldBeAnonymous()
    {
        var requirement = OperationTokenRequirementResolver.Resolve(Action([], new AllowAnonymousAttribute()));

        requirement.Tier.ShouldBe(TokenTier.Anonymous);
        requirement.Roles.ShouldBeEmpty();
    }

    [Fact]
    public void IdentityFilter_ShouldBeIdentity()
    {
        var requirement = OperationTokenRequirementResolver.Resolve(Action([new TokenAuthenticateAttribute()]));

        requirement.Tier.ShouldBe(TokenTier.Identity);
        requirement.Roles.ShouldBeEmpty();
    }

    [Fact]
    public void BranchFilter_ShouldBeAnyBranchRole()
    {
        var requirement = OperationTokenRequirementResolver.Resolve(Action([new TokenAuthenticateBranchAttribute()]));

        requirement.Tier.ShouldBe(TokenTier.Branch);
        requirement.Roles.ShouldBeEmpty();
    }

    [Fact]
    public void AuthorizeFilters_ShouldAdmitTheIntersectionOfTheirRoles_InDeclarationOrderOfTheEnum()
    {
        var requirement = OperationTokenRequirementResolver.Resolve(Action(
        [
            new TokenAuthenticateBranchAttribute(),
            new TokenAuthorizeAttribute(Role.Member, Role.Manager, Role.Admin),
            new TokenAuthorizeAttribute(Role.Manager, Role.Admin)
        ]));

        requirement.Tier.ShouldBe(TokenTier.Branch);
        requirement.Roles.ShouldBe(Enum.GetValues<Role>().Where(role => role is Role.Admin or Role.Manager).ToList());
    }

    [Fact]
    public void Undeclared_ShouldFail()
    {
        var exception = Should.Throw<InvalidOperationException>(() => OperationTokenRequirementResolver.Resolve(Action([])));

        exception.Message.ShouldContain("declares no auth intent");
    }

    [Fact]
    public void AllowAnonymousWithTokenFilter_ShouldFail_BecauseTheFilterStillRunsAtRuntime()
    {
        var exception = Should.Throw<InvalidOperationException>(() => OperationTokenRequirementResolver.Resolve(
            Action([new TokenAuthenticateBranchAttribute()], new AllowAnonymousAttribute())));

        exception.Message.ShouldContain("[AllowAnonymous] but also carries a token filter");
    }

    /// <summary>
    /// The runtime outcome of a mix depends on filter order and the per-request user cache
    /// (<see cref="OperationSecurityPrecedenceTest"/> observes both orders), so the contract refuses to state a tier.
    /// </summary>
    [Fact]
    public void IdentityAndBranchFilters_ShouldFail_BecauseTheirOutcomeDependsOnFilterOrder()
    {
        var exception = Should.Throw<InvalidOperationException>(() => OperationTokenRequirementResolver.Resolve(
            Action([new TokenAuthenticateAttribute(), new TokenAuthorizeAttribute(Role.Admin)])));

        exception.Message.ShouldContain("mixes identity-token and branch-token filters");
        exception.Message.ShouldContain("filter order");
    }

    [Fact]
    public void DisjointAuthorizeFilters_ShouldFail()
    {
        var exception = Should.Throw<InvalidOperationException>(() => OperationTokenRequirementResolver.Resolve(
            Action([new TokenAuthorizeAttribute(Role.Admin), new TokenAuthorizeAttribute(Role.Member)])));

        exception.Message.ShouldContain("admit no common role");
    }

    [Fact]
    public void AspNetAuthorizeMetadata_ShouldFail()
    {
        var exception = Should.Throw<InvalidOperationException>(() => OperationTokenRequirementResolver.Resolve(
            Action([new TokenAuthenticateBranchAttribute()], new AuthorizeAttribute())));

        exception.Message.ShouldContain("[Authorize]");
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void UnknownAuthorizationFilter_ShouldFail(bool asTypeFilter)
    {
        IFilterMetadata unknown = asTypeFilter
            ? new TypeFilterAttribute(typeof(UnmodeledAuthorizationFilter))
            : new UnmodeledAuthorizationFilter();

        var exception = Should.Throw<InvalidOperationException>(() => OperationTokenRequirementResolver.Resolve(
            Action([new TokenAuthenticateBranchAttribute(), unknown])));

        exception.Message.ShouldContain(nameof(UnmodeledAuthorizationFilter));
    }

    [Theory]
    [InlineData(typeof(TokenAuthenticateFilter), true)]
    [InlineData(typeof(TokenAuthenticateBranchFilter), false)]
    public void ServiceFilterOfAKnownTokenFilter_ShouldResolveToItsTier(Type service, bool identity)
    {
        OperationTokenRequirementResolver.Resolve(Action([new ServiceFilterAttribute(service)])).Tier
            .ShouldBe(identity ? TokenTier.Identity : TokenTier.Branch);
    }

    [Fact]
    public void ServiceFilterOfAKnownTokenFilter_ShouldStillConflictWithAllowAnonymous()
    {
        Should.Throw<InvalidOperationException>(() => OperationTokenRequirementResolver.Resolve(
                Action([new ServiceFilterAttribute(typeof(TokenAuthenticateFilter))], new AllowAnonymousAttribute())))
            .Message.ShouldContain("[AllowAnonymous] but also carries a token filter");
    }

    [Fact]
    public void ServiceFilterOfTokenAuthorizeFilter_ShouldFail_BecauseItsRolesAreNotDeclared()
    {
        Should.Throw<InvalidOperationException>(() => OperationTokenRequirementResolver.Resolve(
                Action([new ServiceFilterAttribute(typeof(TokenAuthorizeFilter))])))
            .Message.ShouldContain("does not declare its roles");
    }

    [Theory]
    [InlineData(typeof(IAsyncAuthorizationFilter))]
    [InlineData(typeof(UnmodeledAuthorizationFilter))]
    [InlineData(typeof(OpaqueFilterFactory))]
    public void ServiceFilterOfAnUnmodeledOrOpaqueService_ShouldFail(Type service)
    {
        Should.Throw<InvalidOperationException>(() => OperationTokenRequirementResolver.Resolve(
            Action([new ServiceFilterAttribute(service)], new AllowAnonymousAttribute())));
    }

    [Fact]
    public void CustomFilterFactory_ShouldFail_EvenOnAnAnonymousAction()
    {
        Should.Throw<InvalidOperationException>(() => OperationTokenRequirementResolver.Resolve(
                Action([new OpaqueFilterFactory()], new AllowAnonymousAttribute())))
            .Message.ShouldContain("filter factory");
    }

    [Fact]
    public void PlainTypeFilterOfTokenAuthorizeFilter_ShouldReadItsRoles()
    {
        var filter = new TypeFilterAttribute(typeof(TokenAuthorizeFilter)) { Arguments = [Role.Manager, new[] { Role.Admin }] };

        var requirement = OperationTokenRequirementResolver.Resolve(Action([filter]));

        requirement.Tier.ShouldBe(TokenTier.Branch);
        requirement.Roles.ShouldBe([Role.Admin, Role.Manager]);
    }

    [Fact]
    public void TypeFilterOfTokenAuthorizeFilterWithUnreadableArguments_ShouldFail()
    {
        var filter = new TypeFilterAttribute(typeof(TokenAuthorizeFilter)) { Arguments = [Role.Manager] };

        Should.Throw<InvalidOperationException>(() => OperationTokenRequirementResolver.Resolve(Action([filter])))
            .Message.ShouldContain("roles cannot be read");
    }

    [Fact]
    public void NonAuthorizationFilters_ShouldBeIgnored()
    {
        var requirement = OperationTokenRequirementResolver.Resolve(Action(
            [new TokenAuthenticateBranchAttribute(), new TypeFilterAttribute(typeof(ExceptionFilter))]));

        requirement.Tier.ShouldBe(TokenTier.Branch);
    }

    private static ActionDescriptor Action(IReadOnlyList<IFilterMetadata> filters, params object[] metadata) => new()
    {
        DisplayName = "Synthetic.Action",
        FilterDescriptors = filters.Select(filter => new FilterDescriptor(filter, FilterScope.Action)).ToList(),
        EndpointMetadata = metadata.ToList()
    };

    private sealed class UnmodeledAuthorizationFilter : IAuthorizationFilter
    {
        public void OnAuthorization(AuthorizationFilterContext context)
        {
        }
    }

    /// <summary>A factory whose created filter is only known at runtime.</summary>
    private sealed class OpaqueFilterFactory : IFilterFactory
    {
        public bool IsReusable => false;

        public IFilterMetadata CreateInstance(IServiceProvider serviceProvider) => new UnmodeledAuthorizationFilter();
    }
}
