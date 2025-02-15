using DualDrill.CLSL.Language.ShaderAttribute;
using DualDrill.CLSL.Language.Types;
using System.Collections.Immutable;

namespace DualDrill.CLSL.Language.Declaration;

public sealed class StructureDeclaration : IDeclaration
{
    public required string Name { get; init; }
    public ImmutableArray<MemberDeclaration> Members { get; set; } = [];
    public ImmutableHashSet<IShaderAttribute> Attributes { get; set; } = [];

    public StructureDeclaration()
    {
    }
}