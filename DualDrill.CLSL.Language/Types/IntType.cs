using System.Diagnostics;
using DualDrill.Common.Nat;

namespace DualDrill.CLSL.Language.Types;

public interface IIntType : IIntegerType
{
    IUIntType SameWidthUIntType { get; }
}

public interface IIntType<TSelf> : IIntType, INumericType<TSelf>
    where TSelf : IIntType<TSelf>
{
}

[DebuggerDisplay("{Name}")]
public sealed class IntType<TBitWidth> : IIntType<IntType<TBitWidth>>
    where TBitWidth : IBitWidth
{
    private IntType()
    {
    }

    public static IntType<TBitWidth> Instance { get; } = new();

    public IBitWidth BitWidth => TBitWidth.BitWidth;

    public string Name => $"i{BitWidth.Value}";
    public int ByteSize => BitWidth.Value / 8;

    public T Accept<T, TVisitor>(TVisitor visitor) where TVisitor : IScalarType.IGenericVisitor<T> =>
        visitor.Visit(this);

    T IShaderType.Evaluate<T>(IShaderTypeSemantic<T, T> semantic) => semantic.IntType(this);

    public IUIntType SameWidthUIntType => UIntType<TBitWidth>.Instance;

    public ISignedness Signedness =>  Types. Signedness.S.Instance;
}

public static partial class ShaderType
{
    public static IIntType I8 => IntType<N8>.Instance;
    public static IIntType I16 => IntType<N16>.Instance;
    public static IIntType I32 => IntType<N32>.Instance;
    public static IIntType I64 => IntType<N64>.Instance;

    public static IIntType GetIntType(IBitWidth bitWidth)
    {
        return bitWidth switch
        {
            N8 => I8,
            N16 => I16,
            N32 => I32,
            N64 => I64,
            _ => throw new ArgumentException($"Unsupported bit width: {bitWidth.Value}")
        };
    }
}