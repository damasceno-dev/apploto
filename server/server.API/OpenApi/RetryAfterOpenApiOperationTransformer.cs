using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;
using server.Headers;

namespace server.OpenApi;

/// <summary>
/// Every declared 429 publishes <c>Retry-After</c>: the request limiters and the persistent login budgets both set it,
/// and forgot-password never answers 429 (its suppressed request is the same empty 202).
/// </summary>
internal sealed class RetryAfterOpenApiOperationTransformer : IOpenApiOperationTransformer
{
    public Task TransformAsync(
        OpenApiOperation operation,
        OpenApiOperationTransformerContext context,
        CancellationToken cancellationToken)
    {
        if (operation.Responses?.TryGetValue("429", out var value) is true && value is OpenApiResponse response)
        {
            response.Headers ??= new Dictionary<string, IOpenApiHeader>();
            response.Headers[RetryAfterHeader.Name] = new OpenApiHeader
            {
                Description = "Whole seconds to wait before trying again (at least 1). Advisory: clients do not retry automatically.",
                Required = true,
                Schema = new OpenApiSchema { Type = JsonSchemaType.Integer, Format = "int64", Minimum = "1" }
            };
        }

        return Task.CompletedTask;
    }
}
