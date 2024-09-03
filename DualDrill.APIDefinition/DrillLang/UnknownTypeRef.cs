using System.Text.Json;

namespace DualDrill.ApiGen.DrillLang;

public readonly record struct UnknownTypeRef(JsonElement Doc) : ITypeReference
{
}
