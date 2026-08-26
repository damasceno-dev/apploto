using System.Reflection;

namespace server.OpenApi;

internal static class RequestBodyPropertyDefaults
{
    internal static bool IsOptionalWhenOmitted(PropertyInfo property)
    {
        if (property.DeclaringType is null || ContractNullability.IsCommunicationRequest(property.DeclaringType) is false)
            return false;

        return ScalarOmissionDefaults.IsOptionalWhenOmitted(property.PropertyType);
    }
}
