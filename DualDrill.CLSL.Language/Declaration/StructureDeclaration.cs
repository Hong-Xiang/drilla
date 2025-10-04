using System.Collections.Immutable;
using DualDrill.CLSL.Language.ShaderAttribute;

namespace DualDrill.CLSL.Language.Declaration;

public sealed class StructureDeclaration : IDeclaration
{
    public ImmutableArray<MemberDeclaration> Members { get; set; } = [];
    public required string Name { get; init; }
    public ImmutableHashSet<IShaderAttribute> Attributes { get; set; } = [];

    public T Evaluate<T>(IDeclarationSemantic<T> semantic) => semantic.VisitStructure(this);
}