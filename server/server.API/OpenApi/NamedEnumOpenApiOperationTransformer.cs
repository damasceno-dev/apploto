using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;
using server.Serialization;

namespace server.OpenApi;

internal sealed class NamedEnumOpenApiOperationTransformer : IOpenApiOperationTransformer
{
    public Task TransformAsync(
        OpenApiOperation operation,
        OpenApiOperationTransformerContext context,
        CancellationToken cancellationToken)
    {
        // The current contract declares defaulted enum operation parameters only in query strings.
        // If a defaulted route or header enum is introduced, extend this scope together with
        // location-aware parameter lookup tests instead of treating it as a query parameter.
        foreach (var description in context.Description.ParameterDescriptions
                     .Where(description => description.Source == BindingSource.Query))
        {
            var enumType = Nullable.GetUnderlyingType(description.Type) ?? description.Type;
            if (enumType.IsEnum is false || description.DefaultValue is null)
                continue;

            var parameter = RequiredQueryParametersOpenApiOperationTransformer.FindQueryParameter(
                operation.Parameters,
                description.Name);
            if (parameter is null)
                continue;

            var defaultValue = description.DefaultValue.GetType() == enumType
                ? description.DefaultValue
                : Enum.ToObject(enumType, description.DefaultValue);
            var defaultName = ContractEnumPolicy.GetDeclaredName(enumType, defaultValue);
            if (defaultName is not null)
            {
                var namedDefault = JsonValue.Create(defaultName);
                switch (parameter.Schema)
                {
                    case OpenApiSchemaReference schemaReference:
                        schemaReference.Default = namedDefault;
                        break;
                    case OpenApiSchema schema:
                        schema.Default = namedDefault;
                        break;
                }
            }
        }

        return Task.CompletedTask;
    }
}
