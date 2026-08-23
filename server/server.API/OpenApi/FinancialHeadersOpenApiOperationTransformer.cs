using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;
using server.Application.Services.Idempotency;
using server.Headers;

namespace server.OpenApi;

internal sealed class FinancialHeadersOpenApiOperationTransformer : IOpenApiOperationTransformer
{
    public Task TransformAsync(
        OpenApiOperation operation,
        OpenApiOperationTransformerContext context,
        CancellationToken cancellationToken)
    {
        foreach (var parameter in operation.Parameters?.OfType<OpenApiParameter>() ?? [])
        {
            switch (parameter.Name)
            {
                case FinancialCommandIdempotency.HeaderName:
                    parameter.Required = true;
                    parameter.Description =
                        "Required printable-ASCII idempotency key, scoped by endpoint, authenticated branch, and user.";
                    break;
                case EntityTagHeader.IfMatchName:
                    parameter.Required = true;
                    parameter.Description = "Required strong ETag containing the quoted decimal PostgreSQL xmin value (for example \"123\").";
                    break;
            }
        }

        if (ReturnsVersionedAggregate(context.Description.RelativePath, context.Description.HttpMethod) is false) return Task.CompletedTask;
        foreach (var (statusCode, responseValue) in operation.Responses ?? [])
        {
            if (statusCode is not ("200" or "201") || responseValue is not OpenApiResponse response)
                continue;

            response.Headers ??= new Dictionary<string, IOpenApiHeader>();
            response.Headers["ETag"] = new OpenApiHeader
            {
                Description = "Strong ETag containing the quoted decimal PostgreSQL xmin value.",
                Schema = new OpenApiSchema { Type = JsonSchemaType.String }
            };
        }

        return Task.CompletedTask;
    }

    private static bool ReturnsVersionedAggregate(string? relativePath, string? method)
    {
        if (relativePath is null || method is not ("GET" or "POST" or "PUT"))
            return false;

        var path = relativePath.Split('?', 2)[0];
        return path switch
        {
            "setting" or "setting/lock-month" => true,
            "dailyclose" => method == "POST",
            "transaction" or "transaction/installment" => method == "POST",
            "transaction/{transactionId}" or
                "transaction/{transactionId}/finalize" or
                "transaction/{transactionId}/cancel" => true,
            _ => path.StartsWith("dailyclose/{dailyCloseId}", StringComparison.Ordinal) && path != "dailyclose/{dailyCloseId}/variance-preview"
        };
    }
}
