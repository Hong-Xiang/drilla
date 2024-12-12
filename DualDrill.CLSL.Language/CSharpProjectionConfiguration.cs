using DualDrill.CLSL.Language.AbstractSyntaxTree.Expression;
using DualDrill.CLSL.Language.Types;
using DualDrill.Common.Nat;
using System.Collections.Immutable;

namespace DualDrill.CLSL.Language;

public sealed class CSharpProjectionConfiguration
{
    public static readonly CSharpProjectionConfiguration Instance = new();

    public string MathLibNameSpaceName { get; } = "DualDrill.Mathematics";
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
            BoolType _ => typeof(bool).Name,
            IIntType { BitWidth: N8 } => typeof(sbyte).Name,
            IIntType { BitWidth: N16 } => typeof(short).Name,
            IIntType { BitWidth: N32 } => typeof(int).Name,
            IIntType { BitWidth: N64 } => typeof(long).Name,

            UIntType { BitWidth: N8 } => typeof(byte).Name,
            UIntType { BitWidth: N16 } => typeof(ushort).Name,
            UIntType { BitWidth: N32 } => typeof(uint).Name,
            UIntType { BitWidth: N64 } => typeof(ulong).Name,

            FloatType { BitWidth: N16 } => typeof(Half).Name,
            FloatType { BitWidth: N32 } => typeof(float).Name,
            FloatType { BitWidth: N64 } => typeof(double).Name,

            IVecType t => t.Name,
            MatType m => m.Name,
            _ => throw new NotSupportedException($"C# type map for {type} is undefined")
        })!;
    }

    public string GetCSharpTypeName(IShaderType type) => CSharpTypeNameMap[type];
}
