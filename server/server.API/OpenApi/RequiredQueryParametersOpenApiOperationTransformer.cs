using System.Reflection;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace server.OpenApi;

internal sealed class RequiredQueryParametersOpenApiOperationTransformer : IOpenApiOperationTransformer
{
    public Task TransformAsync(
        OpenApiOperation operation,
        OpenApiOperationTransformerContext context,
        CancellationToken cancellationToken)
    {
        foreach (var description in context.Description.ParameterDescriptions
                     .Where(description => description.Source == BindingSource.Query))
        {
            if (IsRequired(description))
                MarkRequired(operation.Parameters, description.Name);
        }

        return Task.CompletedTask;
    }

    private static bool IsRequired(ApiParameterDescription description)
    {
        if (description.DefaultValue is not null)
            return false;

        var metadata = description.ModelMetadata;

        if (metadata.ContainerType is null || metadata.PropertyName is null)
            return description.ParameterDescriptor is ControllerParameterDescriptor descriptor &&
                   descriptor.ParameterInfo.HasDefaultValue is false &&
                   ContractNullability.IsRequired(descriptor.ParameterInfo);

        var property = FindProperty(metadata.ContainerType, metadata.PropertyName);

        return property is not null &&
               FlattenedQueryParameterDefaults.HasOmissionDefault(property) is false &&
               ContractNullability.IsRequired(property, forInput: true);
    }

    internal static OpenApiParameter? FindQueryParameter(
        IList<IOpenApiParameter>? parameters,
        string name)
    {
        if (parameters is null)
            return null;

        foreach (var candidate in parameters)
        {
            var parameter = ResolveParameter(candidate);
            if (parameter.In == ParameterLocation.Query &&
                string.Equals(parameter.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                return parameter;
            }
        }

        return null;
    }

    internal static bool MarkRequired(IList<IOpenApiParameter>? parameters, string name)
    {
        var parameter = FindQueryParameter(parameters, name);
        if (parameter is null)
            return false;

        parameter.Required = true;
        return true;
    }

    internal static OpenApiParameter ResolveParameter(IOpenApiParameter parameter)
    {
        var visited = new HashSet<IOpenApiParameter>(ReferenceEqualityComparer.Instance);
        while (visited.Add(parameter))
        {
            switch (parameter)
            {
                case OpenApiParameter concrete:
                    return concrete;
                case OpenApiParameterReference reference:
                    parameter = reference.Target ?? throw new InvalidOperationException(
                        "An OpenAPI parameter reference has no resolved target.");
                    break;
                default:
                    throw new InvalidOperationException(
                        $"Unsupported OpenAPI parameter implementation: {parameter.GetType().FullName}.");
            }
        }

        throw new InvalidOperationException("An OpenAPI parameter reference cycle was detected.");
    }

    internal static PropertyInfo? FindProperty(Type containerType, string propertyName) =>
        containerType.GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .FirstOrDefault(property => string.Equals(
                property.Name,
                propertyName,
                StringComparison.OrdinalIgnoreCase));
}
