using System.Collections.Immutable;

namespace DualDrill.ApiGen.DrillLang;

public readonly record struct GenericTypeRef(string Name, ImmutableArray<ITypeReference> TypeArguments) : ITypeReference
{
}
