using System.Text.Json.Nodes;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;
using server.Serialization;

namespace server.OpenApi;

internal sealed class NamedEnumOpenApiSchemaTransformer : IOpenApiSchemaTransformer
{
    public Task TransformAsync(
        OpenApiSchema schema,
        OpenApiSchemaTransformerContext context,
        CancellationToken cancellationToken)
    {
        // The generator can first materialize and cache an enum component through Nullable<TEnum>.
        // Normalize the underlying enum so DTO property order cannot decide the component's wire type.
        var enumType = Nullable.GetUnderlyingType(context.JsonTypeInfo.Type) ?? context.JsonTypeInfo.Type;
        if (enumType.IsEnum is false)
            return Task.CompletedTask;

        ApplyDeclaredNames(schema, enumType);

        return Task.CompletedTask;
    }

    internal static void ApplyDeclaredNames(OpenApiSchema schema, Type enumType)
    {
        schema.Type = JsonSchemaType.String;
        schema.Format = null;
        schema.Enum =
        [
            .. ContractEnumPolicy.GetDeclaredNames(enumType)
                .Select(static JsonNode (name) => JsonValue.Create(name))
        ];
    }
}
