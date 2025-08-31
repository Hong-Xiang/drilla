using DualDrill.CLSL.Language.AbstractSyntaxTree.Expression;
using DualDrill.CLSL.Language.FunctionBody;
using DualDrill.CLSL.Language.ShaderAttribute;
using DualDrill.CLSL.Language.Symbol;
using DualDrill.CLSL.Language.Types;
using System.CodeDom.Compiler;
using System.Collections.Immutable;

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
    public IExpression? Initializer { get; set; } = null;

    public T Evaluate<T>(IDeclarationSemantic<T> semantic)
    {
        throw new NotImplementedException();
    }
}


