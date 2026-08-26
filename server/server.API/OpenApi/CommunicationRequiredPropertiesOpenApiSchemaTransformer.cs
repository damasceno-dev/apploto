using System.Reflection;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace server.OpenApi;

internal sealed class CommunicationRequiredPropertiesOpenApiSchemaTransformer : IOpenApiSchemaTransformer
{
    public Task TransformAsync(
        OpenApiSchema schema,
        OpenApiSchemaTransformerContext context,
        CancellationToken cancellationToken)
    {
        if (ContractNullability.IsCommunicationContract(context.JsonTypeInfo.Type) is false ||
            schema.Properties is null)
        {
            return Task.CompletedTask;
        }

        var isRequest = ContractNullability.IsCommunicationRequest(context.JsonTypeInfo.Type);
        foreach (var jsonProperty in context.JsonTypeInfo.Properties)
        {
            if (jsonProperty.AttributeProvider is not MemberInfo member ||
                (member is PropertyInfo property && RequestBodyPropertyDefaults.IsOptionalWhenOmitted(property)) ||
                ContractNullability.IsRequired(member, forInput: isRequest) is false ||
                schema.Properties.ContainsKey(jsonProperty.Name) is false)
            {
                continue;
            }

            schema.Required ??= new HashSet<string>();
            schema.Required.Add(jsonProperty.Name);
        }

        return Task.CompletedTask;
    }
}
