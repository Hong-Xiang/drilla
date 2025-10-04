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
            // Deserialize the enum value from the string
            var enumString = reader.GetString();
            return (T)Enum.Parse(typeof(T), enumString, true);
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
