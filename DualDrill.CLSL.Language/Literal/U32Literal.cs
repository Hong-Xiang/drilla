using DualDrill.CLSL.Language.Types;
using DualDrill.Common.Nat;
using System.CodeDom.Compiler;

namespace DualDrill.CLSL.Language.Literal;

public record struct U32Literal(uint Value) : ILiteral<U32Literal, uint, UIntType<N32>>
{
    public void PrettyPrint(IndentedTextWriter writer, PrettyPrintOption option)
    {
        writer.Write(Value);
        if (option.LiteralSuffix)
        {
            writer.Write("_u32");
        }
    }
}