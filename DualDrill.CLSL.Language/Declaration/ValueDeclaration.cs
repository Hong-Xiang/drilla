using System.Collections.Immutable;
using DualDrill.CLSL.Language.ShaderAttribute;
using DualDrill.CLSL.Language.Types;

namespace DualDrill.CLSL.Language.Declaration;

[Obsolete]
public sealed record class ValueDeclaration(
    DeclarationScope DeclarationScope,
    string Name,
    IShaderType Type,
    ValueDeclarationKind Kind,
    ImmutableHashSet<IShaderAttribute> Attributes
) : IDeclaration
{
    public T Evaluate<T>(IDeclarationSemantic<T> semantic) => throw new NotImplementedException();
}