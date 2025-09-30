using System.CodeDom.Compiler;
using DualDrill.CLSL.Language.FunctionBody;
using DualDrill.CLSL.Language.Symbol;
using DualDrill.CLSL.Language.Types;

namespace DualDrill.CLSL.Language.Literal;

public sealed record class LiteralValue(ILiteral Literal) : IShaderValue
{
    public IShaderType Type => Literal.Type;

    public void Dump(ILocalDeclarationContext context, IndentedTextWriter writer)
    {
        throw new NotImplementedException();
    }
}