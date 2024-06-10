using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace JsonApiProcess;


[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(WebIdlInterface), typeDiscriminator: "interface")]
[JsonDerivedType(typeof(WebIdlDictionary), typeDiscriminator: "dictionary")]
interface IWebIdlElement
{
}

record class WebIdlInterface(
    string Name,
    bool Partial
) : IWebIdlElement
{
}

record class WebIdlDictionary(
) : IWebIdlElement
{

}


readonly record struct JsonElementPropertyIndexer(JsonElement Element)
{
    public JsonElement this[string name] => Element.GetProperty(name);
}

readonly record struct JsonElementNullablePropertyIndexer(JsonElement Element)
{
    public JsonElement? this[string name]
    {
        get
        {
            if (Element.TryGetProperty(name, out var property))
            {
                return property;
            }
            return null;
        }
    }
}

public static class JQ
{

    public static JsonElement? GetNullableProperty(this JsonElement Element, ReadOnlySpan<char> name)
    {
        if (Element.TryGetProperty(name, out var property))
        {
            return property;
        }
        return null;
    }
}

readonly record struct JE(JsonElement Element)
{
    public JE this[int index] => new(Element[index]);
    public JE this[ReadOnlySpan<char> name] => new(Element.GetProperty(name));
}

record class A() { }
record class B() : A { }

readonly record struct SelfElement(
    string name,
    string? @unsafe)
{
}

readonly record struct FieldElement()
{
    [JsonPropertyName("$")]
    public SelfElement Self { get; init; }

}

readonly record struct StructElement()
{
    [JsonPropertyName("$")]
    public SelfElement Self { get; init; }
    public FieldElement[]? field { get; init; }
}


internal class Program
{

    static IEnumerable<JsonNode> Query(JsonElement doc)
    {
        var ns = doc
                  [0]
                  .GetProperty("bindings")
                  .GetProperty("namespace")
                  [0];

        var structs = ns.GetProperty("struct");
        var structElements = structs.Deserialize<StructElement[]>();
        return from s in structElements
               select new JsonObject(
                   [
                       new("name", JsonValue.Create(s.Self.name)),
                       new("unsafe", JsonValue.Create(s.Self.@unsafe is "true")),
                   ]
               );
    }

    static void Main(string[] args)
    {
        var data = File.ReadAllText(args[0]);
        using var json = JsonDocument.Parse(data);
        File.WriteAllLines(args[1], Query(json.RootElement).Select(n => n.ToJsonString()));
    }
}
