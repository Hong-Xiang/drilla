﻿using System.Diagnostics;
using DualDrill.Common.Nat;

namespace DualDrill.CLSL.Language.Types;

public interface IFloatType : IScalarType
{
}

public interface IFloatType<TSelf> : IFloatType, INumericType<TSelf>
    where TSelf : IFloatType<TSelf>
{
}

[DebuggerDisplay("{Name}")]
public sealed record class FloatType<TBitWidth> : IFloatType<FloatType<TBitWidth>>
    where TBitWidth : IBitWidth
{
    public static FloatType<TBitWidth> Instance { get; } = new();

    public string Name => $"f{BitWidth.Value}";
    public IBitWidth BitWidth => TBitWidth.BitWidth;
    public int ByteSize => BitWidth.Value / 8;

    public T Accept<T, TVisitor>(TVisitor visitor) where TVisitor : IScalarType.IGenericVisitor<T> =>
        visitor.Visit(this);

    public T Evaluate<T>(IShaderTypeSemantic<T, T> semantic) => semantic.FloatType(this);
}

public static partial class ShaderType
{
    public static IFloatType F16 { get; } = FloatType<N16>.Instance;
    public static IFloatType F32 { get; } = FloatType<N32>.Instance;
    public static IFloatType F64 { get; } = FloatType<N64>.Instance;

    public static IFloatType GetFloatType(IBitWidth bitWidth)
    {
        return bitWidth switch
        {
            N16 => F16,
            N32 => F32,
            N64 => F64,
            _ => throw new ArgumentException($"Unsupported bit width: {bitWidth.Value}")
        };
    }
}