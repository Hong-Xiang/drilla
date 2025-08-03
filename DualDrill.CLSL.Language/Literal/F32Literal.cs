using DualDrill.CLSL.Language.Types;
using DualDrill.Common.Nat;
using System.CodeDom.Compiler;

namespace DualDrill.CLSL.Language.Literal;

public record struct F32Literal(float Value) : ILiteral<F32Literal, float, FloatType<N32>>
{
    public void PrettyPrint(IndentedTextWriter writer, PrettyPrintOption option)
    {
        writer.Write(Value);
        if (option.LiteralSuffix)
        {
            writer.Write("_f32");
        }
    }
}