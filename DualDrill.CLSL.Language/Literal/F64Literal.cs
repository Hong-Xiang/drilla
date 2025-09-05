using DualDrill.CLSL.Language.Types;
using DualDrill.Common.Nat;
using System.CodeDom.Compiler;

namespace DualDrill.CLSL.Language.Literal;

public record struct F64Literal(double Value) : ILiteral<F64Literal, double, FloatType<N64>>
{
    public T Evaluate<T>(ILiteralSemantic<T> semantic)
        => semantic.F64(Value);

    public void PrettyPrint(IndentedTextWriter writer, PrettyPrintOption option)
    {
        writer.Write(Value);
        if (option.LiteralSuffix)
        {
            writer.Write("_f64");
        }
    }
}