namespace server.OpenApi;

internal static class ScalarOmissionDefaults
{
    internal static bool IsOptionalWhenOmitted(Type type)
    {
        if (type == typeof(bool))
            return true;

        if (type.IsEnum is false)
            return false;

        var clrDefault = Activator.CreateInstance(type)!;
        return Enum.IsDefined(type, clrDefault);
    }
}
