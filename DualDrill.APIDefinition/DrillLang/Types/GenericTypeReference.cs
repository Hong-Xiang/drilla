using System.Collections.Immutable;

namespace DualDrill.ApiGen.DrillLang.Types;

public readonly record struct GenericTypeReference(string Name, ImmutableArray<ITypeReference> TypeArguments) : ITypeReference
{
}
