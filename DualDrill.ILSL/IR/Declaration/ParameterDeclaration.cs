using System.Collections.Immutable;

namespace DualDrill.ILSL.IR.Declaration;

public sealed record class ParameterDeclaration(
    string Name,
    IType Type,
    ImmutableHashSet<IAttribute> Attributes) : IDeclaration
{
}

