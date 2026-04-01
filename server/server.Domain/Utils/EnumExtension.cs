using System;
using System.ComponentModel;
using System.Reflection;

namespace server.Utils;

public static class EnumExtensions
{
    public static string GetDescription(this Enum value)
    {
        FieldInfo field = value.GetType().GetField(value.ToString());
        DescriptionAttribute attribute = field.GetCustomAttribute<DescriptionAttribute>();

        return attribute == null ? value.ToString() : attribute.Description;
    }
}
public static class StringExtensions
{
    public static string AddFernando(this string value)
    {
        return value + " - Fernando";
    }
}
