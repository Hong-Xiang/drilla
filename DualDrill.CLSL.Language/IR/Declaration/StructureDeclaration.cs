using DualDrill.CLSL.Language.IR.ShaderAttribute;
using DualDrill.CLSL.Language.Types;
using System.Collections.Immutable;

namespace DualDrill.CLSL.Language.IR.Declaration;

public sealed record class StructureDeclaration(
    string Name,
    ImmutableArray<MemberDeclaration> Members,
    ImmutableHashSet<IShaderAttribute> Attributes
) : IShaderType, IDeclaration
{
}
