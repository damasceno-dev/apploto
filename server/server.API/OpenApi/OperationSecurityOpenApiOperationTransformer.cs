using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace server.OpenApi;

/// <summary>
/// Publishes each operation's own <c>security</c> from <see cref="OperationTokenRequirementResolver"/>: <c>[]</c> for an
/// anonymous operation, <see cref="SecuritySchemeNames.IdentityBearer"/> for the identity token, and
/// <see cref="SecuritySchemeNames.BranchBearer"/> for a branch token, listing the admitted role names when
/// <c>[TokenAuthorize]</c> restricts them (OpenAPI 3.1 allows role names on non-OAuth schemes). Both schemes are the
/// same <c>Authorization: Bearer</c> header at runtime; the names tell a client which of its tokens to send.
/// </summary>
internal sealed class OperationSecurityOpenApiOperationTransformer : IOpenApiOperationTransformer
{
    public Task TransformAsync(
        OpenApiOperation operation,
        OpenApiOperationTransformerContext context,
        CancellationToken cancellationToken)
    {
        var requirement = OperationTokenRequirementResolver.Resolve(context.Description.ActionDescriptor);
        operation.Security = requirement.Tier switch
        {
            TokenTier.Anonymous => [],
            TokenTier.Identity => [Requirement(SecuritySchemeNames.IdentityBearer, [], context)],
            _ => [Requirement(SecuritySchemeNames.BranchBearer, requirement.Roles.Select(role => role.ToString()).ToList(), context)]
        };

        return Task.CompletedTask;
    }

    private static OpenApiSecurityRequirement Requirement(
        string scheme,
        List<string> roles,
        OpenApiOperationTransformerContext context)
    {
        return new OpenApiSecurityRequirement
        {
            [new OpenApiSecuritySchemeReference(scheme, context.Document)] = roles
        };
    }
}

internal static class SecuritySchemeNames
{
    public const string IdentityBearer = "IdentityBearer";
    public const string BranchBearer = "BranchBearer";
}
