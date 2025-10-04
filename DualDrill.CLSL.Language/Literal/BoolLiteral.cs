using System.CodeDom.Compiler;
using DualDrill.CLSL.Language.Types;

namespace DualDrill.CLSL.Language.Literal;

public readonly record struct BoolLiteral(bool Value)
    : ILiteral<BoolLiteral, bool, BoolType>
{
    public T Evaluate<T>(ILiteralSemantic<T> semantic) => semantic.Bool(Value);

    public void PrettyPrint(IndentedTextWriter writer, PrettyPrintOption option)
    {
        writer.Write(Value);
        if (option.LiteralSuffix) writer.Write("_b");
    }
}