using DotNext.Patterns;
using DualDrill.CLSL.Language.Declaration;
using DualDrill.CLSL.Language.ShaderAttribute;
using DualDrill.CLSL.Language.Types;
using DualDrill.Common.Nat;
using System.Collections.Immutable;
using System.ComponentModel;

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
    dpdx,
    dpdxCoarse,
    dpdxFine,
    dpdy,
    dpdyCoarse,
    dpdyFine,
    fwidth,
    fwidthCoarse,
    fwidthFine
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

public class ShaderFunction : ISingleton<ShaderFunction>
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
            NumericBuiltinFunctionName.length => NumericBuiltinFunctionKind.Unknown,
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
            NumericBuiltinFunctionName.dpdx => NumericBuiltinFunctionKind.UnaryFloatScalarOrVector,
            NumericBuiltinFunctionName.dpdxCoarse => NumericBuiltinFunctionKind.UnaryFloatScalarOrVector,
            NumericBuiltinFunctionName.dpdxFine => NumericBuiltinFunctionKind.UnaryFloatScalarOrVector,
            NumericBuiltinFunctionName.dpdy => NumericBuiltinFunctionKind.UnaryFloatScalarOrVector,
            NumericBuiltinFunctionName.dpdyCoarse => NumericBuiltinFunctionKind.UnaryFloatScalarOrVector,
            NumericBuiltinFunctionName.dpdyFine => NumericBuiltinFunctionKind.UnaryFloatScalarOrVector,
            NumericBuiltinFunctionName.fwidth => NumericBuiltinFunctionKind.UnaryFloatScalarOrVector,
            NumericBuiltinFunctionName.fwidthCoarse => NumericBuiltinFunctionKind.UnaryFloatScalarOrVector,
            NumericBuiltinFunctionName.fwidthFine => NumericBuiltinFunctionKind.UnaryFloatScalarOrVector,
            _ => throw new InvalidEnumArgumentException(
             nameof(name),
             (int)name,
             typeof(NumericBuiltinFunctionName))
        };
    }

    static IEnumerable<FunctionDeclaration> CreateKnownNumericFunctionOverloads(NumericBuiltinFunctionName name)
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
            (NumericBuiltinFunctionKind.UnaryScalarOrVector, _) =>
                from s in ShaderType.NumericScalarTypes
                from t in ShaderType.GetScalarOrVectorTypes(s)
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
            (_, NumericBuiltinFunctionName.length) =>
                from s in ShaderType.FloatTypes
                from t in ShaderType.GetScalarOrVectorTypes(s)
                select new FunctionDeclaration(fn, [new ParameterDeclaration("e", t, [])], new FunctionReturn(s, []), []),
            (_, NumericBuiltinFunctionName.determinant) => [], // TODO: add mat types and fix this
            (_, NumericBuiltinFunctionName.dot4U8Packed) => [
                new FunctionDeclaration(
                    fn,
                    [new ParameterDeclaration("e1", ShaderType.U32, []),
                     new ParameterDeclaration("e2", ShaderType.U32, [])],
                    new FunctionReturn(ShaderType.U32, []), [])],
            (_, NumericBuiltinFunctionName.dot4I8Packed) => [
                new FunctionDeclaration(
                    fn,
                    [new ParameterDeclaration("e1", ShaderType.U32, []),
                     new ParameterDeclaration("e2", ShaderType.U32, [])],
                    new FunctionReturn(ShaderType.I32, []), [])],
            (_, NumericBuiltinFunctionName.extractBits) =>
                from t in (IEnumerable<IShaderType>)[.. ShaderType.GetScalarOrVectorTypes(ShaderType.I32),
                                                     .. ShaderType.GetScalarOrVectorTypes(ShaderType.U32)]
                select new FunctionDeclaration(
                    fn,
                    [new ParameterDeclaration("e", t, []),
                     new ParameterDeclaration("offset", ShaderType.U32, []),
                     new ParameterDeclaration("count", ShaderType.U32, [])],
                    new FunctionReturn(t, []), []),
            (_, NumericBuiltinFunctionName.frexp) => [], // TODO: add frexp result type and fix this
            (_, NumericBuiltinFunctionName.insertBits) =>
                from t in (IEnumerable<IShaderType>)[.. ShaderType.GetScalarOrVectorTypes(ShaderType.I32),
                                                     .. ShaderType.GetScalarOrVectorTypes(ShaderType.U32)]
                select new FunctionDeclaration(
                    fn,
                    [new ParameterDeclaration("e", t, []),
                     new ParameterDeclaration("newBits", t, []),
                     new ParameterDeclaration("offset", ShaderType.U32, []),
                     new ParameterDeclaration("count", ShaderType.U32, [])],
                    new FunctionReturn(t, []), []),
            (_, NumericBuiltinFunctionName.ldexp) =>
                from s in ShaderType.FloatTypes
                from t in ShaderType.GetScalarOrVectorTypes(s)
                let i = t is IScalarType ? (IShaderType)ShaderType.I32 : ShaderType.GetVecType(((IVecType)t).Size, ShaderType.I32)
                select new FunctionDeclaration(
                    fn,
                    [new ParameterDeclaration("e1", t, []),
                     new ParameterDeclaration("e2", i, [])],
                    new FunctionReturn(t, []), []),
            (_, NumericBuiltinFunctionName.modf) => [], // TODO: add modf result type and fix this
            (_, NumericBuiltinFunctionName.quantizeToF16) =>
                 from t in ShaderType.GetScalarOrVectorTypes(ShaderType.F32)
                 select new FunctionDeclaration(
                     fn,
                     [new ParameterDeclaration("e", t, [])],
                     new FunctionReturn(t, []), []),
            (_, NumericBuiltinFunctionName.transpose) => [], // TODO: add mat types and fix this
            _ => throw new NotSupportedException($"Function {Enum.GetName(name)} is not supported yet")
        };
    }

    public ImmutableArray<FunctionDeclaration> Functions;

    static IEnumerable<FunctionDeclaration> BuiltinScalarConstructors()
    {
        return from s in ShaderType.NumericScalarTypes
               from t in ShaderType.NumericScalarTypes
               select new FunctionDeclaration(
                   s.Name,
                   [new ParameterDeclaration("e", t, [])],
                    new FunctionReturn(s, []),
                   [new ShaderRuntimeMethodAttribute(), new ConversionMethodAttribute()]);
    }


    static IEnumerable<FunctionDeclaration> VecConstructors()
    {
        static IEnumerable<int[]> ValueConstructorParametersPattern(IRank rank)
        {
            return rank switch
            {
                N3 => [[1, 2], [2, 1]],
                N4 => [[1, 3], [3, 1], [1, 1, 2], [1, 2, 1], [2, 1, 1], [2, 2]],
                _ => []
            };
        }
        static ParameterDeclaration PatternToParameter(int pattern, IScalarType s, int i)
        {
            var t = pattern switch
            {
                1 => new ParameterDeclaration($"e{i}", s, []),
                2 => new ParameterDeclaration($"e{i}", ShaderType.GetVecType(N2.Instance, s), []),
                3 => new ParameterDeclaration($"e{i}", ShaderType.GetVecType(N3.Instance, s), []),
                4 => new ParameterDeclaration($"e{i}", ShaderType.GetVecType(N4.Instance, s), []),
                _ => throw new NotSupportedException()
            };
            return t;
        }

        return from s in ShaderType.ScalarTypes
               from r in ShaderType.Ranks
               let v = ShaderType.GetVecType(r, s)
               let name = $"vec{r.Value}"
               let tname = CSharpProjectionConfiguration.Instance.GetCSharpTypeName(v)
               let bc = new FunctionDeclaration(
                            name,
                            [new ParameterDeclaration("e", s, [])],
                            new FunctionReturn(v, []),
                            [new ShaderRuntimeMethodAttribute()])
               let zc = new FunctionDeclaration(
                            //v.Name,
                            tname,
                            [],
                            new FunctionReturn(v, []),
                            [new ShaderRuntimeMethodAttribute(), new ZeroConstructorMethodAttribute(), new VecMethodRenamedForOverloadAttribute()])
               let sc = new FunctionDeclaration(
                            name,
                            [.. Enumerable.Range(0, r.Value).Select(i => new ParameterDeclaration($"e{i}", s, []))],
                            new FunctionReturn(v, []),
                            [new ShaderRuntimeMethodAttribute()])
               let vc = from p in ValueConstructorParametersPattern(r)
                        select new FunctionDeclaration(
                            name,
                            [.. p.Select((pr, i) => PatternToParameter(pr, s, i))],
                            new FunctionReturn(v, []),
                            [new ShaderRuntimeMethodAttribute()])
               let cc = from ss in ShaderType.ScalarTypes
                        select new FunctionDeclaration(
                            tname,
                            [new ParameterDeclaration("v", ShaderType.GetVecType(r, ss), [])],
                            new FunctionReturn(v, []),
                            [new ShaderRuntimeMethodAttribute(), new ConversionMethodAttribute(), new VecMethodRenamedForOverloadAttribute()])
               from f in (IEnumerable<FunctionDeclaration>)[bc, zc, sc, .. vc, .. cc]
               select f;
    }

    ShaderFunction()
    {
        FunctionDeclaration[] fs = [.. VecConstructors()];
        var fsvec2 = fs.Where(f => f.Name == "vec2" && f.Parameters.Length == 1 && f.Return.Type is VecType<N2, BoolType>).ToArray();

        Functions = [.. from n in Enum.GetValues<NumericBuiltinFunctionName>()
                        from f in CreateKnownNumericFunctionOverloads(n)
                        select f,
                     .. BuiltinScalarConstructors(),
                     .. VecConstructors()];

        Func0Lookup = (from f in Functions
                       where f.Parameters.Length == 0
                       select KeyValuePair.Create((f.Name, f.Return.Type), f)).ToDictionary();
        Func1Lookup = (from f in Functions
                       where f.Parameters.Length == 1
                       select KeyValuePair.Create((f.Name, f.Return.Type, ResolvedType: f.Parameters[0].Type), f)).ToDictionary();
        Func2Lookup = (from f in Functions
                       where f.Parameters.Length == 2
                       select KeyValuePair.Create((f.Name, Type: f.Return.Type, f.Parameters[0].Type, f.Parameters[1].Type), f)).ToDictionary();
        Func3Lookup = (from f in Functions
                       where f.Parameters.Length == 3
                       select KeyValuePair.Create((f.Name, Type: f.Return.Type, f.Parameters[0].Type, f.Parameters[1].Type, f.Parameters[2].Type), f)).ToDictionary();
        Func4Lookup = (from f in Functions
                       where f.Parameters.Length == 4
                       select KeyValuePair.Create((f.Name, Type: f.Return.Type, f.Parameters[0].Type, f.Parameters[1].Type, f.Parameters[2].Type, f.Parameters[3].Type), f)).ToDictionary();
    }

    IReadOnlyDictionary<(string, IShaderType), FunctionDeclaration> Func0Lookup { get; }
    IReadOnlyDictionary<(string, IShaderType, IShaderType), FunctionDeclaration> Func1Lookup { get; }
    IReadOnlyDictionary<(string, IShaderType, IShaderType, IShaderType), FunctionDeclaration> Func2Lookup { get; }
    IReadOnlyDictionary<(string, IShaderType, IShaderType, IShaderType, IShaderType), FunctionDeclaration> Func3Lookup { get; }
    IReadOnlyDictionary<(string, IShaderType, IShaderType, IShaderType, IShaderType, IShaderType), FunctionDeclaration> Func4Lookup { get; }

    public static ShaderFunction Instance { get; } = new();

    public FunctionDeclaration GetFunction(string name, IShaderType returnType, params IShaderType[] types)
    {
        return types.Length switch
        {
            0 => Func0Lookup[(name, returnType)],
            1 => Func1Lookup[(name, returnType, types[0])],
            2 => Func2Lookup[(name, returnType, types[0], types[1])],
            3 => Func3Lookup[(name, returnType, types[0], types[1], types[2])],
            4 => Func4Lookup[(name, returnType, types[0], types[1], types[2], types[3])],
            _ => Functions.Single(f =>
            {
                if (f.Name != name || f.Parameters.Length != types.Length || (!f.Return.Type.Equals(returnType)))
                {
                    return false;
                }
                for (var i = 0; i < types.Length; i++)
                {
                    if (f.Parameters[i].Type != types[i])
                        return false;
                }
                return true;
            })
        };
    }
}
