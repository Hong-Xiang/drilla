using DualDrill.CLSL.Language.Types;
using DualDrill.Common.Nat;
using System.CodeDom.Compiler;

namespace DualDrill.CLSL.Language.Literal;

public record struct I32Literal(int Value) : ILiteral<I32Literal, int, IntType<N32>>
{
    public T Evaluate<T>(ILiteralSemantic<T> semantic)
        => semantic.I32(Value);

    public void PrettyPrint(IndentedTextWriter writer, PrettyPrintOption option)
    {
        writer.Write(Value);
        if (option.LiteralSuffix)
        {
            writer.Write("_i32");
        }
    }
}