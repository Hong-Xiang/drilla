using DualDrill.CLSL.Language.Types;
using System.Collections.Immutable;

namespace DualDrill.CLSL.Language.IR.Declaration;

public sealed record class TypeDeclaration(IShaderType Type, ImmutableHashSet<IAttribute> Attributes) : IDeclaration
{
    public string Name => Type.Name;
}
