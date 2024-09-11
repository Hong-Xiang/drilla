using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DualDrill.ILSL.IR;

public interface ILiteral { }

public interface INumericLiteral : ILiteral { }

public readonly record struct BoolLiteral(bool Value) : ILiteral
{
}

public enum FloatLiteralSuffix
{
    d,
    f,
    h
}

public readonly record struct FloatLiteral(float Value, FloatLiteralSuffix? Suffix = default) : INumericLiteral
{
}

public enum IntLiteralSuffix
{
    i,
    u
}

public readonly record struct IntLiteral(int Value, IntLiteralSuffix? Suffix = default) : INumericLiteral
{
}
