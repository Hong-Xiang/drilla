using DualDrill.CLSL.Language.AbstractSyntaxTree.Expression;
using DualDrill.CLSL.Language.ShaderAttribute;
using DualDrill.CLSL.Language.Types;
using System.Collections.Immutable;

namespace DualDrill.CLSL.Language.Declaration;

public sealed class MemberDeclaration(
    string Name,
    IShaderType Type,
    ImmutableHashSet<IShaderAttribute> Attributes)
    : IDeclaration, ILoadStoreTargetSymbol
{
    public string Name { get; } = Name;
    public ImmutableHashSet<IShaderAttribute> Attributes { get; } = Attributes;
    public IShaderType Type { get; } = Type;
}
