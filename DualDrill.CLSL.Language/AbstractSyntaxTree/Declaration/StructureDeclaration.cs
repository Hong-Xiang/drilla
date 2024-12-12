using DualDrill.CLSL.Language.AbstractSyntaxTree.ShaderAttribute;
using DualDrill.CLSL.Language.Types;
using System.Collections.Immutable;

namespace DualDrill.CLSL.Language.AbstractSyntaxTree.Declaration;

public sealed record class StructureDeclaration(
    string Name,
    ImmutableArray<MemberDeclaration> Members,
    ImmutableHashSet<IShaderAttribute> Attributes
) : IShaderType, IDeclaration
{
}
