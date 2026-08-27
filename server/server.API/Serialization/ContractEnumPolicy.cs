namespace server.Serialization;

internal static class ContractEnumPolicy
{
    internal static string[] GetDeclaredNames(Type enumType)
    {
        if (enumType.IsEnum is false)
            throw new ArgumentException($"{enumType.FullName} is not an enum type.", nameof(enumType));

        return Enum.GetNames(enumType);
    }

    internal static string? GetDeclaredName<TEnum>(TEnum value)
        where TEnum : struct, Enum => Enum.GetName(value);

    internal static string? GetDeclaredName(Type enumType, object value)
    {
        if (enumType.IsEnum is false)
            throw new ArgumentException($"{enumType.FullName} is not an enum type.", nameof(enumType));

        return Enum.GetName(enumType, value);
    }
}
