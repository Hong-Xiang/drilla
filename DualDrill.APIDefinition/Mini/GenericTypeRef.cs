using System.Collections.Immutable;

namespace DualDrill.ApiGen.Mini;

public readonly record struct GenericTypeRef(string Name, ImmutableArray<ITypeReference> TypeArguments) : ITypeReference
{
}
