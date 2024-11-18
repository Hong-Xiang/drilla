using DualDrill.ILSL.Types;
using System.Collections.Immutable;

namespace DualDrill.ILSL.IR.Declaration;

public sealed record class TypeDeclaration(IType Type, ImmutableHashSet<IShaderAttribute> Attributes) : IDeclaration
{
    public string Name => Type.Name;
}
