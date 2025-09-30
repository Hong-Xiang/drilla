using System.Text.Json.Serialization;
using DualDrill.CLSL.Language.Types;

namespace DualDrill.CLSL.Language.Literal;

[JsonDerivedType(typeof(BoolLiteral), nameof(BoolLiteral))]
[JsonDerivedType(typeof(I32Literal), nameof(I32Literal))]
[JsonDerivedType(typeof(I64Literal), nameof(I64Literal))]
[JsonDerivedType(typeof(U32Literal), nameof(U32Literal))]
[JsonDerivedType(typeof(U64Literal), nameof(U64Literal))]
[JsonDerivedType(typeof(F32Literal), nameof(F32Literal))]
[JsonDerivedType(typeof(F64Literal), nameof(F64Literal))]
public interface ILiteral : IPrintable
{
    IShaderType Type { get; }
    string Name { get; }

    T Evaluate<T>(ILiteralSemantic<T> semantic);
}

public interface ILiteralSemantic<T>
{
    T Bool(bool value);
    T I32(int value);
    T I64(long value);
    T U32(uint value);
    T U64(ulong value);
    T F32(float value);
    T F64(double value);
}

public interface ILiteral<TShaderType> : ILiteral
    where TShaderType : IShaderType<TShaderType>
{
}

public interface INumericLiteral : ILiteral
{
}

public interface ILiteral<TSelf, TCSharpType, TShaderType> : ILiteral<TShaderType>
    where TSelf : ILiteral<TSelf, TCSharpType, TShaderType>
    where TShaderType : IScalarType<TShaderType>
{
    TCSharpType Value { get; }
    string ILiteral.Name => $"{TShaderType.Instance.Name}({Value})";
    IShaderType ILiteral.Type => TShaderType.Instance;
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

    public static string TypeName<TLiteral>(this TLiteral literal) where TLiteral : ILiteral => literal.Type.Name;
}