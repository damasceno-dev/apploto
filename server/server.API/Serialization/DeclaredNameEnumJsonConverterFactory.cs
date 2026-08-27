using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using server.Exceptions;

namespace server.Serialization;

internal sealed class DeclaredNameEnumJsonConverterFactory : JsonConverterFactory
{
    public override bool CanConvert(Type typeToConvert) => typeToConvert.IsEnum;

    public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options)
    {
        var converterType = typeof(DeclaredNameEnumJsonConverter<>).MakeGenericType(typeToConvert);
        return (JsonConverter)Activator.CreateInstance(converterType)!;
    }

    private sealed class DeclaredNameEnumJsonConverter<TEnum> : JsonConverter<TEnum>
        where TEnum : struct, Enum
    {
        private static readonly Type UnderlyingType = Enum.GetUnderlyingType(typeof(TEnum));
        private static readonly Dictionary<string, TEnum> ValuesByExactName =
            ContractEnumPolicy.GetDeclaredNames(typeof(TEnum)).ToDictionary(
                static name => name,
                static name => Enum.Parse<TEnum>(name),
                StringComparer.Ordinal);
        private static readonly ILookup<string, TEnum> ValuesByNameIgnoringCase =
            ValuesByExactName.ToLookup(
                static pair => pair.Key,
                static pair => pair.Value,
                StringComparer.OrdinalIgnoreCase);

        public override TEnum Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            switch (reader.TokenType)
            {
                case JsonTokenType.String:
                {
                    var name = reader.GetString();
                    if (name is not null)
                    {
                        if (ValuesByExactName.TryGetValue(name, out var exactValue))
                            return exactValue;

                        var caseInsensitiveMatches = ValuesByNameIgnoringCase[name].Take(2).ToList();
                        if (caseInsensitiveMatches.Count is 1)
                            return caseInsensitiveMatches[0];
                    }

                    throw new ContractJsonException(ResourcesErrorMessages.ENUM_NAME_INVALID);
                }
                case JsonTokenType.Number:
                {
                    var numericValue = JsonSerializer.Deserialize(ref reader, UnderlyingType, options)
                                       ?? throw new JsonException();

                    // During the transition window, carry any value representable by the enum's
                    // backing type into the DTO. The owning FluentValidation rule decides whether
                    // the value is a declared member and retains its feature-specific message.
                    return (TEnum)Enum.ToObject(typeof(TEnum), numericValue);
                }
                case JsonTokenType.None:
                case JsonTokenType.StartObject:
                case JsonTokenType.EndObject:
                case JsonTokenType.StartArray:
                case JsonTokenType.EndArray:
                case JsonTokenType.PropertyName:
                case JsonTokenType.Comment:
                case JsonTokenType.True:
                case JsonTokenType.False:
                case JsonTokenType.Null:
                default:
                    throw new JsonException();
            }
        }

        public override void Write(Utf8JsonWriter writer, TEnum value, JsonSerializerOptions options)
        {
            var name = ContractEnumPolicy.GetDeclaredName(value);
            if (name is not null)
            {
                writer.WriteStringValue(name);
                return;
            }

            // Legacy imports or out-of-band writes can surface an undefined persisted value.
            // Preserve a complete response instead of throwing after MVC may have flushed headers.
            var numericValue = Convert.ChangeType(value, UnderlyingType, CultureInfo.InvariantCulture);
            JsonSerializer.Serialize(writer, numericValue, UnderlyingType, options);
        }
    }
}
