using DualDrill.Common.Nat;

namespace DualDrill.ILSL.Types;

public readonly record struct UIntType<TBitWidth>() : IIntegerType, IBasicPrimitiveType<UIntType<TBitWidth>>
    where TBitWidth : IBitWidth
{
    public static UIntType<TBitWidth> Instance { get; } = new();

    public string Name => $"u{Nat.GetInstance<TBitWidth>().Value}";

    public int ByteSize => Nat.GetInstance<TBitWidth>().Value / 8;
}
