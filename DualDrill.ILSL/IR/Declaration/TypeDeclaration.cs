using DualDrill.ILSL.Types;
using System.Collections.Immutable;

namespace DualDrill.ILSL.IR.Declaration;

public sealed record class TypeDeclaration(IType Type, ImmutableHashSet<IAttribute> Attributes) : IDeclaration
{
    public string Name => Type.Name;
}
