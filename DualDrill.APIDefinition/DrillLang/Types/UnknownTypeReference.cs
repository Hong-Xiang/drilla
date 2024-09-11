using System.Text.Json;

namespace DualDrill.ApiGen.DrillLang.Types;

public readonly record struct UnknownTypeReference(JsonElement Doc) : ITypeReference
{
}
