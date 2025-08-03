using DualDrill.CLSL.Language.Types;
using System.CodeDom.Compiler;

namespace DualDrill.CLSL.Language.Literal;

public readonly record struct BoolLiteral(bool Value)
    : ILiteral<BoolLiteral, bool, BoolType>
{
    public void PrettyPrint(IndentedTextWriter writer, PrettyPrintOption option)
    {
        writer.Write(Value);
        if (option.LiteralSuffix)
        {
            writer.Write("_b");
        }
    }
}