using System.Collections.Immutable;

namespace DualDrill.ILSL.IR.Declaration;

public sealed record class TypeDeclaration(IType Type) : IDeclaration
{
    public string Name => Type.Name;

    public ImmutableHashSet<IAttribute> Attributes => Type.Attributes;
}
