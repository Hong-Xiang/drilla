using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace DualDrill.Common
{

    [AttributeUsage(AttributeTargets.Field)]
    public class KebabCaseLowerAttribute : Attribute
    {
    }

    public class KebabCaseLowerEnumConverter<T> : JsonConverter<T> where T : struct, Enum
    {
        public override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.String)
                throw new JsonException($"Expected a string for {typeof(T).Name}");

            var enumString = reader.GetString() ?? throw new JsonException($"Expected a string for {typeof(T).Name}");
            if (Enum.TryParse<T>(enumString, true, out var value))
                return value;

            foreach (var candidate in Enum.GetValues<T>())
            {
                if (string.Equals(ConvertToKebabCase(candidate.ToString()), enumString, StringComparison.OrdinalIgnoreCase))
                    return candidate;
            }

            throw new JsonException($"Invalid {typeof(T).Name} value: {enumString}");
        }

        public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
        {
            // Convert the enum name to kebab case
            var enumName = value.ToString();
            var kebabCaseName = ConvertToKebabCase(enumName);

            // Write the kebab case string to JSON
            writer.WriteStringValue(kebabCaseName);
        }

        private string ConvertToKebabCase(string name)
        {
            // Convert the enum name to kebab case
            var words = Regex.Split(name, @"(?<!^)(?=[A-Z])");
            return string.Join("-", Array.ConvertAll(words, word => word.ToLower()));
        }
    }
}
