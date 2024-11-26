using DualDrill.CLSL.Language.IR.Expression;
using DualDrill.CLSL.Language.Types;
using DualDrill.Common.Nat;
using System.Collections.Immutable;

namespace DualDrill.CLSL.Language;

public interface ILanguageProjectionConfiguration
{
    string NameSpace { get; }
    string StaticMathTypeName { get; }
    string OpName(UnaryArithmeticOp op);
    string OpName(BinaryArithmeticOp op);
    string ProjectedTypeName(IShaderType type);
    string ProjectedFullTypeName(IShaderType type);
    bool IsSimdDataSupported(IShaderType type);
}

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

            IVecType t => t.Name,
            MatType m => m.Name,
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
}
