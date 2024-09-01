using System.Text.Json;

namespace DualDrill.ApiGen.Mini;

public readonly record struct UnknownTypeRef(JsonElement Doc) : ITypeRef
{
}
