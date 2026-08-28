using System.Reflection;
using server.Communication.Requests;
using server.Communication.Responses;

namespace WebApi.Test.OpenApi;

/// <summary>
/// Reflection helpers over the <c>server.Communication</c> contract boundary. Contract tests derive
/// their expectations from the real DTO graph instead of maintaining a parallel hand-written list,
/// which is the same rule the OpenAPI enum transformer follows.
/// </summary>
internal static class CommunicationContractTypes
{
    private static readonly Assembly CommunicationAssembly = typeof(RequestUpsertTimeEntryJson).Assembly;
    private static readonly string ResponsesNamespace = typeof(ResponseErrorJson).Namespace!;

    /// <summary>Every enum reachable from any request or response contract property.</summary>
    internal static IReadOnlyList<Type> EnumTypes() => EnumTypesIn(null);

    /// <summary>Every enum reachable from a response contract property.</summary>
    internal static IReadOnlyList<Type> ResponseEnumTypes() => EnumTypesIn(ResponsesNamespace);

    private static IReadOnlyList<Type> EnumTypesIn(string? contractNamespace) =>
        CommunicationAssembly.GetTypes()
            .Where(type => contractNamespace is null || type.Namespace == contractNamespace)
            .SelectMany(type => type.GetProperties())
            .SelectMany(property => EnumTypesWithin(property.PropertyType))
            .Distinct()
            .OrderBy(type => type.FullName, StringComparer.Ordinal)
            .ToList();

    private static IEnumerable<Type> EnumTypesWithin(Type type)
    {
        type = Nullable.GetUnderlyingType(type) ?? type;
        if (type.IsEnum)
        {
            yield return type;
            yield break;
        }

        if (type.IsArray)
        {
            foreach (var enumType in EnumTypesWithin(type.GetElementType()!))
                yield return enumType;
        }

        foreach (var argument in type.GetGenericArguments())
        {
            foreach (var enumType in EnumTypesWithin(argument))
                yield return enumType;
        }
    }
}
