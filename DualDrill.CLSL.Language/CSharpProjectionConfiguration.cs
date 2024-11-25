using DualDrill.CLSL.Language.IR.Expression;
using DualDrill.CLSL.Language.Types;
using DualDrill.Common.Nat;
using System.Collections.Immutable;

namespace DualDrill.CLSL.Language;

public sealed class CSharpProjectionConfiguration
{
    public static readonly CSharpProjectionConfiguration Instance = new();

    public string NameSpace { get; } = "DualDrill.Mathematics";
    public string StaticMathTypeName { get; } = "DMath";

    ImmutableDictionary<IShaderType, string> CSharpTypeNameMap { get; }
    CSharpProjectionConfiguration()
    {
        var typeNameMap = new Dictionary<IShaderType, string>();
        foreach (var t in ShaderType.ScalarTypes)
        {
            typeNameMap.Add(t, DefineCSharpTypeName(t));
            foreach (var v in ShaderType.GetVecTypes(t))
            {
                typeNameMap.Add(v, DefineCSharpTypeName(v));
            }
        }
        CSharpTypeNameMap = typeNameMap.ToImmutableDictionary();
    }

    public string OpName(UnaryArithmeticOp op)
    {
        return op switch
        {
            UnaryArithmeticOp.Minus => "-",
            _ => throw new NotSupportedException($"Unary arithmetic operator {op} is not supported")
        };
    }
    public string OpName(BinaryArithmeticOp op)
    {
        return op switch
        {
            BinaryArithmeticOp.Addition => "+",
            BinaryArithmeticOp.Subtraction => "-",
            BinaryArithmeticOp.Multiplication => "*",
            BinaryArithmeticOp.Division => "/",
            BinaryArithmeticOp.Remainder => "%",
            _ => throw new NotSupportedException($"Binary arithmetic operator {op} is not supported")
        };
    }
    static string DefineCSharpTypeName(IShaderType type)
    {
        return (type switch
        {
            BoolType _ => typeof(bool).FullName,
            IntType { BitWidth: N8 } => typeof(sbyte).FullName,
            IntType { BitWidth: N16 } => typeof(short).FullName,
            IntType { BitWidth: N32 } => typeof(int).FullName,
            IntType { BitWidth: N64 } => typeof(long).FullName,

            UIntType { BitWidth: N8 } => typeof(byte).FullName,
            UIntType { BitWidth: N16 } => typeof(ushort).FullName,
            UIntType { BitWidth: N32 } => typeof(uint).FullName,
            UIntType { BitWidth: N64 } => typeof(ulong).FullName,

            FloatType { BitWidth: N16 } => typeof(Half).FullName,
            FloatType { BitWidth: N32 } => typeof(float).FullName,
            FloatType { BitWidth: N64 } => typeof(double).FullName,

            IVecType { Size: var size, ElementType: BoolType b } => $"vec{size.Value}{ScalarShaderName(b)}",
            IVecType { Size: var size, ElementType: var e } => $"vec{size.Value}{ScalarShaderName(e)}",
            MatType { Row: var r, Column: var c, ElementType: var e } => $"mat{r.Value}x{c.Value}{ScalarShaderName(e)}",
            _ => throw new NotSupportedException($"C# type map for {type} is undefined")
        })!;
    }

    public static string ScalarShaderName(IScalarType type)
    {
        return type switch
        {
            BoolType => "b",
            FloatType => $"f{type.BitWidth.Value}",
            IntType => $"i{type.BitWidth.Value}",
            UIntType => $"u{type.BitWidth.Value}",
            _ => throw new NotSupportedException($"{nameof(ScalarShaderName)} does not support {type}")
        };
    }

    public string GetCSharpTypeName(IShaderType type) => CSharpTypeNameMap[type];

    public bool IsSimdDataSupported(IShaderType type)
    {
        // for numeric vectors with data larger than 64 bits (except System.Half, which is not supported in VectorXX<Half>),
        // we use .NET builtin SIMD optimization
        // for vec3, we use vec4's data for optimizing memory access and SIMD optimization
        return type switch
        {
            IVecType { ElementType: BoolType } => false,
            IVecType { ElementType: FloatType { BitWidth: N16 } } => false,
            IVecType { ElementType: var e, Size: N3 } when e.BitWidth.Value * 4 >= 64 => true,
            IVecType { ElementType: var e, Size: var s } when e.BitWidth.Value * s.Value >= 64 => true,
            _ => false
        };
    }
}
