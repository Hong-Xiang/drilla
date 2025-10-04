using System.CodeDom.Compiler;
using DualDrill.CLSL.Language.Types;
using DualDrill.Common.Nat;

namespace DualDrill.CLSL.Language.Literal;

public record struct I64Literal(long Value) : ILiteral<I64Literal, long, IntType<N64>>
{
    public T Evaluate<T>(ILiteralSemantic<T> semantic) => semantic.I64(Value);

    public void PrettyPrint(IndentedTextWriter writer, PrettyPrintOption option)
    {
        writer.Write(Value);
        if (option.LiteralSuffix) writer.Write("_i64");
    }
}