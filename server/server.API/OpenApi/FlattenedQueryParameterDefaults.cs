using System.Collections.Concurrent;
using System.ComponentModel;
using System.Reflection;

namespace server.OpenApi;

internal static class FlattenedQueryParameterDefaults
{
    private static readonly ConcurrentDictionary<Type, Lazy<object?>> DefaultInstances = new();

    internal static bool HasOmissionDefault(PropertyInfo property)
    {
        if (property.GetCustomAttribute<DefaultValueAttribute>() is not null)
            return true;

        // MVC binds an omitted non-nullable Boolean or enum property to its CLR default.
        // A defined enum zero has the same accepted omission semantics as a request-body enum.
        if (ScalarOmissionDefaults.IsOptionalWhenOmitted(property.PropertyType))
            return true;

        if (property.DeclaringType is null || ContractNullability.IsCommunicationContract(property.DeclaringType) is false)
            return false;

        var instance = DefaultInstances.GetOrAdd(
            property.DeclaringType,
            static type => new Lazy<object?>(
                () => CreateDefaultInstance(type),
                LazyThreadSafetyMode.ExecutionAndPublication)).Value;

        if (instance is null)
            return false;

        var initializedValue = property.GetValue(instance);
        var clrDefault = property.PropertyType.IsValueType ? Activator.CreateInstance(property.PropertyType) : null;
        return Equals(initializedValue, clrDefault) is false;
    }

    private static object? CreateDefaultInstance(Type type)
    {
        if (type.IsAbstract || type.GetConstructor(Type.EmptyTypes) is null)
            return null;

        return Activator.CreateInstance(type);
    }
}
