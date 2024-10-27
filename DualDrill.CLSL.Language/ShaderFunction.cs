using DualDrill.CLSL.Language.Types;
using DualDrill.CLSL.Language.IR.Declaration;
using System.Collections.Immutable;
using System.Security.Cryptography;
using System.ComponentModel;
using DualDrill.Common.Nat;

namespace DualDrill.CLSL.Language;

public enum NumericBuiltinFunctionName
{
    abs,
    acos,
    acosh,
    asin,
    asinh,
    atan,
    atanh,
    atan2,
    ceil,
    clamp,
    cos,
    cosh,
    countLeadingZeros,
    countOneBits,
    countTrailingZeros,
    cross,
    degrees,
    determinant,
    distance,
    dot,
    dot4U8Packed,
    dot4I8Packed,
    exp,
    exp2,
    extractBits,
    faceForward,
    firstLeadingBit,
    firstTrailingBit,
    floor,
    fma,
    fract,
    frexp,
    insertBits,
    inverseSqrt,
    ldexp,
    length,
    log,
    log2,
    max,
    min,
    mix,
    modf,
    normalize,
    pow,
    quantizeToF16,
    radians,
    reflect,
    refract,
    reverseBits,
    round,
    saturate,
    sign,
    sin,
    sinh,
    smoothstep,
    sqrt,
    step,
    tan,
    tanh,
    transpose,
    trunc,
}

public enum NumericBuiltinFunctionKind
{
    Unknown, // for functions that are not easily categorized, like cross, unpack related, determinant, etc
    UnaryFloatVector, // let S = Float Types, T = VecN<S>, T => T
    UnaryFloatScalarOrVector, // let S = Float Types, T = S | VecN<S>, T => T
    UnaryIntegerScalarOrVector, // let S = Int | UInt Types, T = S | VecN<S>, T => T
    UnaryScalarOrVector, // let S = Numeric Scalar Types, T = S | VecN<S>, T => T
    BinaryScalarOrVector, // let S = Numeric Scalar Types, T = S | VecN<S>, T * T => T
    BinaryFloatScalarOrVector, // let S = Float Types, T = S | VecN<S>, T * T => T
    BinaryFloatVector, // let S = Float Types, T = VecN<S>, T * T => T
    BinaryVectorVectorToScalar, // let S = Numeric Scalar Types, T = VecN<S>, T * T => S
    TernaryScalarOrVector, // let S = Float Types, T = S | VecN<S>, T * T * T => T,
    TernaryFloatVector // let S = Float Types, T = VecN<S>, T * T * T => T,
}

public static class ShaderFunction
{
    static NumericBuiltinFunctionKind FunctionKind(NumericBuiltinFunctionName name)
    {
        return name switch
        {
            NumericBuiltinFunctionName.abs => NumericBuiltinFunctionKind.UnaryScalarOrVector,
            NumericBuiltinFunctionName.acos => NumericBuiltinFunctionKind.UnaryFloatScalarOrVector,
            NumericBuiltinFunctionName.acosh => NumericBuiltinFunctionKind.UnaryFloatScalarOrVector,
            NumericBuiltinFunctionName.asin => NumericBuiltinFunctionKind.UnaryFloatScalarOrVector,
            NumericBuiltinFunctionName.asinh => NumericBuiltinFunctionKind.UnaryFloatScalarOrVector,
            NumericBuiltinFunctionName.atan => NumericBuiltinFunctionKind.UnaryFloatScalarOrVector,
            NumericBuiltinFunctionName.atanh => NumericBuiltinFunctionKind.UnaryFloatScalarOrVector,
            NumericBuiltinFunctionName.atan2 => NumericBuiltinFunctionKind.BinaryFloatScalarOrVector,
            NumericBuiltinFunctionName.ceil => NumericBuiltinFunctionKind.UnaryFloatScalarOrVector,
            NumericBuiltinFunctionName.clamp => NumericBuiltinFunctionKind.TernaryScalarOrVector,
            NumericBuiltinFunctionName.cos => NumericBuiltinFunctionKind.UnaryFloatScalarOrVector,
            NumericBuiltinFunctionName.cosh => NumericBuiltinFunctionKind.UnaryFloatScalarOrVector,
            NumericBuiltinFunctionName.countLeadingZeros => NumericBuiltinFunctionKind.UnaryIntegerScalarOrVector,
            NumericBuiltinFunctionName.countOneBits => NumericBuiltinFunctionKind.UnaryIntegerScalarOrVector,
            NumericBuiltinFunctionName.countTrailingZeros => NumericBuiltinFunctionKind.UnaryIntegerScalarOrVector,
            NumericBuiltinFunctionName.cross => NumericBuiltinFunctionKind.Unknown,
            NumericBuiltinFunctionName.degrees => NumericBuiltinFunctionKind.UnaryFloatScalarOrVector,
            NumericBuiltinFunctionName.determinant => NumericBuiltinFunctionKind.Unknown,
            NumericBuiltinFunctionName.distance => NumericBuiltinFunctionKind.BinaryFloatScalarOrVector,
            NumericBuiltinFunctionName.dot => NumericBuiltinFunctionKind.BinaryVectorVectorToScalar,
            NumericBuiltinFunctionName.dot4U8Packed => NumericBuiltinFunctionKind.Unknown,
            NumericBuiltinFunctionName.dot4I8Packed => NumericBuiltinFunctionKind.Unknown,
            NumericBuiltinFunctionName.exp => NumericBuiltinFunctionKind.UnaryFloatScalarOrVector,
            NumericBuiltinFunctionName.exp2 => NumericBuiltinFunctionKind.UnaryFloatScalarOrVector,
            NumericBuiltinFunctionName.extractBits => NumericBuiltinFunctionKind.Unknown,
            NumericBuiltinFunctionName.faceForward => NumericBuiltinFunctionKind.TernaryFloatVector,
            NumericBuiltinFunctionName.firstLeadingBit => NumericBuiltinFunctionKind.UnaryIntegerScalarOrVector,
            NumericBuiltinFunctionName.firstTrailingBit => NumericBuiltinFunctionKind.UnaryIntegerScalarOrVector,
            NumericBuiltinFunctionName.floor => NumericBuiltinFunctionKind.UnaryFloatScalarOrVector,
            NumericBuiltinFunctionName.fma => NumericBuiltinFunctionKind.TernaryScalarOrVector,
            NumericBuiltinFunctionName.fract => NumericBuiltinFunctionKind.UnaryFloatScalarOrVector,
            NumericBuiltinFunctionName.frexp => NumericBuiltinFunctionKind.Unknown,
            NumericBuiltinFunctionName.insertBits => NumericBuiltinFunctionKind.Unknown,
            NumericBuiltinFunctionName.inverseSqrt => NumericBuiltinFunctionKind.UnaryFloatScalarOrVector,
            NumericBuiltinFunctionName.ldexp => NumericBuiltinFunctionKind.Unknown,
            NumericBuiltinFunctionName.length => NumericBuiltinFunctionKind.UnaryFloatScalarOrVector,
            NumericBuiltinFunctionName.log => NumericBuiltinFunctionKind.UnaryFloatScalarOrVector,
            NumericBuiltinFunctionName.log2 => NumericBuiltinFunctionKind.UnaryFloatScalarOrVector,
            NumericBuiltinFunctionName.max => NumericBuiltinFunctionKind.BinaryScalarOrVector,
            NumericBuiltinFunctionName.min => NumericBuiltinFunctionKind.BinaryScalarOrVector,
            NumericBuiltinFunctionName.mix => NumericBuiltinFunctionKind.TernaryScalarOrVector,
            NumericBuiltinFunctionName.modf => NumericBuiltinFunctionKind.Unknown,
            NumericBuiltinFunctionName.normalize => NumericBuiltinFunctionKind.UnaryFloatVector,
            NumericBuiltinFunctionName.pow => NumericBuiltinFunctionKind.BinaryFloatScalarOrVector,
            NumericBuiltinFunctionName.quantizeToF16 => NumericBuiltinFunctionKind.Unknown,
            NumericBuiltinFunctionName.radians => NumericBuiltinFunctionKind.UnaryFloatScalarOrVector,
            NumericBuiltinFunctionName.reflect => NumericBuiltinFunctionKind.BinaryFloatVector,
            NumericBuiltinFunctionName.refract => NumericBuiltinFunctionKind.TernaryScalarOrVector,
            NumericBuiltinFunctionName.reverseBits => NumericBuiltinFunctionKind.UnaryIntegerScalarOrVector,
            NumericBuiltinFunctionName.round => NumericBuiltinFunctionKind.UnaryFloatScalarOrVector,
            NumericBuiltinFunctionName.saturate => NumericBuiltinFunctionKind.UnaryFloatScalarOrVector,
            NumericBuiltinFunctionName.sign => NumericBuiltinFunctionKind.UnaryScalarOrVector,
            NumericBuiltinFunctionName.sin => NumericBuiltinFunctionKind.UnaryFloatScalarOrVector,
            NumericBuiltinFunctionName.sinh => NumericBuiltinFunctionKind.UnaryFloatScalarOrVector,
            NumericBuiltinFunctionName.smoothstep => NumericBuiltinFunctionKind.TernaryScalarOrVector,
            NumericBuiltinFunctionName.sqrt => NumericBuiltinFunctionKind.UnaryFloatScalarOrVector,
            NumericBuiltinFunctionName.step => NumericBuiltinFunctionKind.BinaryFloatScalarOrVector,
            NumericBuiltinFunctionName.tan => NumericBuiltinFunctionKind.UnaryFloatScalarOrVector,
            NumericBuiltinFunctionName.tanh => NumericBuiltinFunctionKind.UnaryFloatScalarOrVector,
            NumericBuiltinFunctionName.transpose => NumericBuiltinFunctionKind.Unknown,
            NumericBuiltinFunctionName.trunc => NumericBuiltinFunctionKind.UnaryFloatScalarOrVector,
            _ => throw new InvalidEnumArgumentException(
             nameof(name),
             (int)name,
             typeof(NumericBuiltinFunctionName))
        };
    }

    static IEnumerable<FunctionDeclaration> CreateFunctionOverloads(NumericBuiltinFunctionName name)
    {
        var kind = FunctionKind(name);
        var fn = Enum.GetName(name) ?? throw new InvalidEnumArgumentException(nameof(name), (int)name, typeof(NumericBuiltinFunctionName));
        return (kind, name) switch
        {
            (NumericBuiltinFunctionKind.UnaryFloatVector, _) =>
                            from s in ShaderType.FloatTypes
                            from t in ShaderType.GetVecTypes(s)
                            select new FunctionDeclaration(fn, [new ParameterDeclaration("e", t, [])], new FunctionReturn(t, []), []),
            (NumericBuiltinFunctionKind.UnaryFloatScalarOrVector, _) =>
                from s in ShaderType.FloatTypes
                from t in ShaderType.GetScalarOrVectorTypes(s)
                select new FunctionDeclaration(fn, [new ParameterDeclaration("e", t, [])], new FunctionReturn(t, []), []),
            (NumericBuiltinFunctionKind.UnaryIntegerScalarOrVector, _) =>
                            from s in ShaderType.IntTypes
                            from t in ShaderType.GetVecTypes(s)
                            select new FunctionDeclaration(fn, [new ParameterDeclaration("e", t, [])], new FunctionReturn(t, []), []),
            (NumericBuiltinFunctionKind.BinaryFloatVector, _) =>
                from s in ShaderType.FloatTypes
                from t in ShaderType.GetVecTypes(s)
                select new FunctionDeclaration(fn, [new ParameterDeclaration("e1", t, []), new ParameterDeclaration("e2", t, [])], new FunctionReturn(t, []), []),
            (NumericBuiltinFunctionKind.BinaryFloatScalarOrVector, _) =>
                from s in ShaderType.FloatTypes
                from t in ShaderType.GetScalarOrVectorTypes(s)
                select new FunctionDeclaration(fn, [new ParameterDeclaration("e1", t, []), new ParameterDeclaration("e2", t, [])], new FunctionReturn(t, []), []),
            (NumericBuiltinFunctionKind.BinaryScalarOrVector, _) =>
                from s in ShaderType.NumericScalarTypes
                from t in ShaderType.GetScalarOrVectorTypes(s)
                select new FunctionDeclaration(fn, [new ParameterDeclaration("e1", t, []), new ParameterDeclaration("e2", t, [])], new FunctionReturn(t, []), []),
            (NumericBuiltinFunctionKind.BinaryVectorVectorToScalar, _) =>
                from s in ShaderType.NumericScalarTypes
                from t in ShaderType.GetVecTypes(s)
                select new FunctionDeclaration(fn, [new ParameterDeclaration("e1", t, []), new ParameterDeclaration("e2", t, [])], new FunctionReturn(s, []), []),
            (NumericBuiltinFunctionKind.TernaryFloatVector, _) =>
                from s in ShaderType.FloatTypes
                from t in ShaderType.GetVecTypes(s)
                select new FunctionDeclaration(fn, [new ParameterDeclaration("e1", t, []), new ParameterDeclaration("e2", t, []), new ParameterDeclaration("e3", t, [])], new FunctionReturn(t, []), []),
            (NumericBuiltinFunctionKind.TernaryScalarOrVector, _) =>
                from s in ShaderType.FloatTypes
                from t in ShaderType.GetScalarOrVectorTypes(s)
                select new FunctionDeclaration(fn, [new ParameterDeclaration("e1", t, []), new ParameterDeclaration("e2", t, []), new ParameterDeclaration("e3", t, [])], new FunctionReturn(t, []), []),
            (_, NumericBuiltinFunctionName.cross) =>
                from s in ShaderType.FloatTypes
                let v = ShaderType.GetVecType(N3.Instance, s)
                select new FunctionDeclaration(fn, [new ParameterDeclaration("e1", v, []), new ParameterDeclaration("e2", v, [])], new FunctionReturn(v, []), []),
            _ => throw new NotSupportedException($"Function {Enum.GetName(name)} is not supported yet")
        };
    }

    static ImmutableArray<FunctionDeclaration> Functions;

    static ShaderFunction()
    {
        Functions = [.. from n in Enum.GetValues<NumericBuiltinFunctionName>()
                        from f in CreateFunctionOverloads(n)
                        select f];

        Func1Lookup = (from f in Functions
                       where f.Parameters.Length == 1
                       select KeyValuePair.Create((f.Name, f.Parameters[0].Type), f)).ToDictionary();
        Func2Lookup = (from f in Functions
                       where f.Parameters.Length == 2
                       select KeyValuePair.Create((f.Name, f.Parameters[0].Type, f.Parameters[1].Type), f)).ToDictionary();
        Func3Lookup = (from f in Functions
                       where f.Parameters.Length == 3
                       select KeyValuePair.Create((f.Name, f.Parameters[0].Type, f.Parameters[1].Type, f.Parameters[2].Type), f)).ToDictionary();
    }

    static IReadOnlyDictionary<(string, IShaderType), FunctionDeclaration> Func1Lookup { get; }
    static IReadOnlyDictionary<(string, IShaderType, IShaderType), FunctionDeclaration> Func2Lookup { get; }
    static IReadOnlyDictionary<(string, IShaderType, IShaderType, IShaderType), FunctionDeclaration> Func3Lookup { get; }

    public static FunctionDeclaration GetFunction(string name, IShaderType type)
    {
        return Func1Lookup[(name, type)];
    }

    public static FunctionDeclaration GetFunction(string name, IShaderType t0, IShaderType t1)
    {
        return Func2Lookup[(name, t0, t1)];
    }

    public static FunctionDeclaration GetFunction(string name, IShaderType t0, IShaderType t1, IShaderType t2)
    {
        return Func3Lookup[(name, t0, t1, t2)];
    }
}
