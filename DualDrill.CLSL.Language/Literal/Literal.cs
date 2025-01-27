using DualDrill.CLSL.Language.Types;
using System.Text.Json.Serialization;

namespace DualDrill.CLSL.Language.Literal;

[JsonDerivedType(typeof(BoolLiteral), nameof(BoolLiteral))]
[JsonDerivedType(typeof(I32Literal), nameof(I32Literal))]
[JsonDerivedType(typeof(I64Literal), nameof(I64Literal))]
[JsonDerivedType(typeof(U32Literal), nameof(U32Literal))]
[JsonDerivedType(typeof(U64Literal), nameof(U64Literal))]
[JsonDerivedType(typeof(F32Literal), nameof(F32Literal))]
[JsonDerivedType(typeof(F64Literal), nameof(F64Literal))]
public interface ILiteral
{
    IShaderType Type { get; }
}

public interface INumericLiteral : ILiteral { }

public interface ILiteral<TShaderType> : ILiteral
    where TShaderType : class, IScalarType<TShaderType>
{
}

public static class Literal
{
    public static BoolLiteral Create(bool value) => new(value);
    public static I32Literal Create(int value) => new(value);
    public static I64Literal Create(long value) => new(value);
    public static U32Literal Create(uint value) => new(value);
    public static U64Literal Create(ulong value) => new(value);
    public static F32Literal Create(float value) => new(value);
    public static F64Literal Create(double value) => new(value);
}
