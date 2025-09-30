﻿using System.CodeDom.Compiler;
using DualDrill.CLSL.Language.Types;
using DualDrill.Common.Nat;

namespace DualDrill.CLSL.Language.Literal;

public record struct U32Literal(uint Value) : ILiteral<U32Literal, uint, UIntType<N32>>
{
    public T Evaluate<T>(ILiteralSemantic<T> semantic) => semantic.U32(Value);

    public void PrettyPrint(IndentedTextWriter writer, PrettyPrintOption option)
    {
        writer.Write(Value);
        if (option.LiteralSuffix) writer.Write("_u32");
    }
}