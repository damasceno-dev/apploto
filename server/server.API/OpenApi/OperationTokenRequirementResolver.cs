using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using server.Domain.Entities.Enums;
using server.Filters;

namespace server.OpenApi;

/// <summary>Which token an operation's effective filters require (M7.6 follow-up Phase 4.3, Shared M2 H4).</summary>
internal enum TokenTier
{
    /// <summary>No token filter runs and the action is explicitly <c>[AllowAnonymous]</c>.</summary>
    Anonymous,

    /// <summary><c>TokenAuthenticateFilter</c>: the identity (global-scope) token only; a branch token is refused.</summary>
    Identity,

    /// <summary><c>TokenAuthenticateBranchFilter</c> / <c>TokenAuthorizeFilter</c>: a branch-scope token.</summary>
    Branch
}

/// <param name="Roles">For <see cref="TokenTier.Branch"/>: the roles every <c>[TokenAuthorize]</c> filter admits
/// (empty = any branch role).</param>
internal sealed record OperationTokenRequirement(TokenTier Tier, IReadOnlyList<Role> Roles);

/// <summary>
/// Derives an operation's token requirement from the filters MVC actually runs for the action (global, controller and
/// action scopes, inherited attributes included), never from a method/path table. Every filter descriptor is
/// classified without instantiating it: a <see cref="TypeFilterAttribute"/> by its implementation type and arguments,
/// a <see cref="ServiceFilterAttribute"/> by its service type, and a direct filter instance by its own type. Any other
/// <see cref="IFilterFactory"/> is opaque — what it creates is only known at runtime — so it stops generation unless it
/// is one of the framework factories MVC adds to every action, which never create an authorization filter.
/// <para>
/// The custom token filters do not consult <see cref="IAllowAnonymous"/>, so an <c>[AllowAnonymous]</c> action that
/// also carries a token filter still requires the token at runtime. That contradictory declaration, an undeclared auth
/// intent, a mix of identity and branch token filters, <c>[TokenAuthorize]</c> filters with no common role, and any
/// authorization mechanism this resolver does not model all stop document generation instead of being published as
/// something they are not.
/// </para>
/// </summary>
internal static class OperationTokenRequirementResolver
{
    /// <summary>
    /// Factories MVC itself adds to every controller action (observed on all 101 production actions). They create
    /// temp-data, client-error and model-state filters — never an authorization filter. Internal framework types are
    /// matched by full name; a renamed or new factory fails generation until it is reviewed here.
    /// </summary>
    private static readonly HashSet<string> NonAuthorizationFrameworkFactories = new(StringComparer.Ordinal)
    {
        typeof(SaveTempDataAttribute).FullName!,
        "Microsoft.AspNetCore.Mvc.Infrastructure.ClientErrorResultFilterFactory",
        "Microsoft.AspNetCore.Mvc.Infrastructure.ModelStateInvalidFilterFactory"
    };

    public static OperationTokenRequirement Resolve(ActionDescriptor action)
    {
        var name = action.DisplayName ?? action.Id;
        if (action.EndpointMetadata.OfType<IAuthorizeData>().Any())
        {
            throw new InvalidOperationException(
                $"{name} carries ASP.NET [Authorize] metadata, which this contract does not model; declare a token filter instead.");
        }

        var identity = 0;
        var branch = 0;
        var authorizedRoleSets = new List<HashSet<Role>>();
        foreach (var filter in action.FilterDescriptors.Select(descriptor => descriptor.Filter))
        {
            switch (Classify(filter, name))
            {
                case TokenFilter.Identity:
                    identity++;
                    break;
                case TokenFilter.Branch:
                    branch++;
                    break;
                case TokenFilter.Authorize:
                    authorizedRoleSets.Add(AuthorizedRoles((TypeFilterAttribute)filter, name));
                    break;
                case TokenFilter.None:
                default:
                    break;
            }
        }

        var allowAnonymous = action.EndpointMetadata.OfType<IAllowAnonymous>().Any();
        if (identity + branch + authorizedRoleSets.Count == 0)
        {
            return allowAnonymous
                ? new OperationTokenRequirement(TokenTier.Anonymous, [])
                : throw new InvalidOperationException(
                    $"{name} declares no auth intent: mark it [AllowAnonymous] or add a token filter.");
        }

        if (allowAnonymous)
        {
            throw new InvalidOperationException(
                $"{name} is [AllowAnonymous] but also carries a token filter. The token filters do not consult " +
                "[AllowAnonymous], so the token is still required; remove one of the two declarations.");
        }

        if (identity > 0 && branch + authorizedRoleSets.Count > 0)
        {
            // Observed, not assumed: the outcome depends on filter order and the per-request user cache (a branch filter
            // that runs first lets a branch token through the identity filter; the opposite order refuses both tokens).
            throw new InvalidOperationException(
                $"{name} mixes identity-token and branch-token filters. Which token passes depends on filter order and " +
                "the per-request user cache, so the contract cannot state one tier; declare a single token tier.");
        }

        if (identity > 0)
            return new OperationTokenRequirement(TokenTier.Identity, []);

        if (authorizedRoleSets.Count == 0)
            return new OperationTokenRequirement(TokenTier.Branch, []);

        // Every [TokenAuthorize] filter must pass, so the admitted roles are their intersection.
        var roles = authorizedRoleSets.Aggregate((left, right) => { left.IntersectWith(right); return left; });
        if (roles.Count == 0)
            throw new InvalidOperationException($"{name} combines [TokenAuthorize] filters that admit no common role.");

        return new OperationTokenRequirement(TokenTier.Branch, Enum.GetValues<Role>().Where(roles.Contains).ToList());
    }

    private enum TokenFilter
    {
        None,
        Identity,
        Branch,
        Authorize
    }

    private static TokenFilter Classify(IFilterMetadata filter, string name) => filter switch
    {
        TypeFilterAttribute typeFilter => ClassifyType(typeFilter.ImplementationType, name, "[TypeFilter]", rolesReadable: true),
        ServiceFilterAttribute serviceFilter => ClassifyType(serviceFilter.ServiceType, name, "[ServiceFilter]", rolesReadable: false),
        IFilterFactory factory when NonAuthorizationFrameworkFactories.Contains(factory.GetType().FullName!) => TokenFilter.None,
        IFilterFactory factory => throw new InvalidOperationException(
            $"{name} runs filter factory {factory.GetType().FullName}, whose filters are only known at runtime; " +
            "its token requirement cannot be published. Use the token filter attributes directly."),
        _ => ClassifyType(filter.GetType(), name, "a filter instance", rolesReadable: false)
    };

    private static TokenFilter ClassifyType(Type type, string name, string origin, bool rolesReadable)
    {
        if (type == typeof(TokenAuthenticateFilter))
            return TokenFilter.Identity;
        if (type == typeof(TokenAuthenticateBranchFilter))
            return TokenFilter.Branch;
        if (type == typeof(TokenAuthorizeFilter))
        {
            return rolesReadable
                ? TokenFilter.Authorize
                : throw new InvalidOperationException(
                    $"{name} runs {nameof(TokenAuthorizeFilter)} through {origin}, which does not declare its roles; use [TokenAuthorize].");
        }

        if (type.IsInterface || type.IsAbstract)
        {
            throw new InvalidOperationException(
                $"{name} resolves {origin} {type.FullName} at runtime; its token requirement cannot be published.");
        }

        if (typeof(IAuthorizationFilter).IsAssignableFrom(type) || typeof(IAsyncAuthorizationFilter).IsAssignableFrom(type))
        {
            throw new InvalidOperationException(
                $"{name} runs authorization filter {type.FullName} through {origin}, whose token requirement is not modeled.");
        }

        if (typeof(IFilterFactory).IsAssignableFrom(type))
        {
            throw new InvalidOperationException(
                $"{name} runs filter factory {type.FullName} through {origin}, whose filters are only known at runtime.");
        }

        return TokenFilter.None;
    }

    /// <summary>The roles one <c>[TokenAuthorize]</c> (or equivalent <c>[TypeFilter]</c>) admits, read from its arguments.</summary>
    private static HashSet<Role> AuthorizedRoles(TypeFilterAttribute filter, string name)
    {
        if (filter.Arguments is not [Role requiredRole, Role[] additionalRoles])
        {
            throw new InvalidOperationException(
                $"{name} runs {nameof(TokenAuthorizeFilter)} with arguments that are not (Role, Role[]); its roles cannot be read.");
        }

        return [requiredRole, .. additionalRoles];
    }
}
