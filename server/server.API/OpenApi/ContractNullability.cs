using System.Reflection;
using server.Communication.Requests;

namespace server.OpenApi;

internal static class ContractNullability
{
    private static readonly Assembly CommunicationAssembly = typeof(RequestDashboardJson).Assembly;

    [ThreadStatic]
    private static NullabilityInfoContext? _context;

    private static NullabilityInfoContext Context => _context ??= new NullabilityInfoContext();

    internal static bool IsCommunicationContract(Type type) => type.Assembly == CommunicationAssembly;

    internal static bool IsCommunicationRequest(Type type)
    {
        if (IsCommunicationContract(type) is false)
            return false;

        return type.Namespace == "server.Communication.Requests" ||
               type.Namespace?.StartsWith("server.Communication.Requests.", StringComparison.Ordinal) is true;
    }

    internal static bool IsRequired(MemberInfo member, bool forInput) => member switch
    {
        PropertyInfo property => IsRequired(property.PropertyType, State(Context.Create(property), forInput)),
        FieldInfo field => IsRequired(field.FieldType, State(Context.Create(field), forInput)),
        _ => false
    };

    internal static bool IsRequired(ParameterInfo parameter) =>
        IsRequired(parameter.ParameterType, Context.Create(parameter).WriteState);

    private static NullabilityState State(NullabilityInfo nullability, bool forInput) =>
        forInput ? nullability.WriteState : nullability.ReadState;

    private static bool IsRequired(Type type, NullabilityState nullability)
    {
        if (Nullable.GetUnderlyingType(type) is not null)
            return false;

        return type.IsValueType || nullability == NullabilityState.NotNull;
    }
}
