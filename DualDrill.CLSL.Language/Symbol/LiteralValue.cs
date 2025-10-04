using System.CodeDom.Compiler;
using DualDrill.CLSL.Language.FunctionBody;
using DualDrill.CLSL.Language.Literal;
using DualDrill.CLSL.Language.Types;

namespace DualDrill.CLSL.Language.Symbol;

public sealed class LiteralValue(ILiteral value) : ShaderValue
{
    public ILiteral Value => value;
    public override IShaderType Type => value.Type;

    public override void Dump(ILocalDeclarationContext context, IndentedTextWriter writer)
    {
        throw new NotImplementedException();
    }
}