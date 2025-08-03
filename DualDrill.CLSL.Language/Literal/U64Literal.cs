using DualDrill.CLSL.Language.Types;
using DualDrill.Common.Nat;
using System.CodeDom.Compiler;

namespace DualDrill.CLSL.Language.Literal;

public record struct U64Literal(ulong Value) : ILiteral<U64Literal, ulong, UIntType<N64>>
{
    public void PrettyPrint(IndentedTextWriter writer, PrettyPrintOption option)
    {
        writer.Write(Value);
        if (option.LiteralSuffix)
        {
            writer.Write("_u64");
        }
    }
}