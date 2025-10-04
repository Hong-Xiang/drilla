using System.Collections.Immutable;
using DualDrill.CLSL.Language.AbstractSyntaxTree.Expression;
using DualDrill.CLSL.Language.ShaderAttribute;
using DualDrill.CLSL.Language.Symbol;
using DualDrill.CLSL.Language.Types;

namespace DualDrill.CLSL.Language.Declaration;

public sealed class MemberDeclaration(
    string Name,
    IShaderType Type,
    ImmutableHashSet<IShaderAttribute> Attributes)
    : IDeclaration
{
    public string Name { get; } = Name;
    public ImmutableHashSet<IShaderAttribute> Attributes { get; } = Attributes;

    public T Evaluate<T>(IDeclarationSemantic<T> semantic) => semantic.VisitMember(this);

    public IShaderType Type { get; } = Type;

    public override string ToString() => $"member {Name} : {Type.Name}";
}