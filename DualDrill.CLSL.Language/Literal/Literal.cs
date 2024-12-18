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
