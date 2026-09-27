using DualDrill.CLSL.Language.Operation;
using DualDrill.CLSL.Language.Symbol;
using DualDrill.Common;
using DualDrill.Common.Nat;

namespace DualDrill.CLSL.Language.Types;

public sealed class ReadOnlyStructuredBufferType<TElement>
    : IShaderType<ReadOnlyStructuredBufferType<TElement>>,
      ISingleton<ReadOnlyStructuredBufferType<TElement>>
    where TElement : IScalarType<TElement>
{
    private ReadOnlyStructuredBufferType()
    {
    }

    public static ReadOnlyStructuredBufferType<TElement> Instance
    {
        get
        {
            StructuredBufferScalarFamily.RequireElement<TElement>();
            return Holder.Instance;
        }
    }

    private static class Holder
    {
        internal static readonly ReadOnlyStructuredBufferType<TElement> Instance = new();
    }

    public IShaderType ElementType => TElement.Instance;
    public uint ElementStride => 4;
    public string Name => $"StructuredBuffer<{TElement.Instance.Name}>";

    public IRefType GetRefType() => throw new NotSupportedException();

    public IPtrType GetPtrType(IAddressSpace addressSpace) =>
        IPtrType.CreateFromSingletonType<ReadOnlyStructuredBufferType<TElement>>(addressSpace);

    public T Evaluate<T>(IShaderTypeSemantic<T, T> semantic) =>
        throw new NotSupportedException("Read-only structured buffers are not supported by this type semantic.");
}

public static class ReadOnlyStructuredBufferFamily
{
    public static bool IsCanonicalType(IShaderType type) =>
        ReferenceEquals(type, ReadOnlyStructuredBufferType<FloatType<N32>>.Instance) ||
        ReferenceEquals(type, ReadOnlyStructuredBufferType<IntType<N32>>.Instance) ||
        ReferenceEquals(type, ReadOnlyStructuredBufferType<UIntType<N32>>.Instance);

    public static bool IsCanonicalLength(IOperation operation) =>
        ReferenceEquals(operation, StructuredBufferLengthOperation<FloatType<N32>>.Instance) ||
        ReferenceEquals(operation, StructuredBufferLengthOperation<IntType<N32>>.Instance) ||
        ReferenceEquals(operation, StructuredBufferLengthOperation<UIntType<N32>>.Instance);

    public static bool IsCanonicalLoad(IOperation operation) =>
        ReferenceEquals(operation, StructuredBufferLoadOperation<FloatType<N32>>.Instance) ||
        ReferenceEquals(operation, StructuredBufferLoadOperation<IntType<N32>>.Instance) ||
        ReferenceEquals(operation, StructuredBufferLoadOperation<UIntType<N32>>.Instance);

    public static IEnumerable<IOperation> CanonicalOperations =>
    [
        StructuredBufferLengthOperation<FloatType<N32>>.Instance,
        StructuredBufferLengthOperation<IntType<N32>>.Instance,
        StructuredBufferLengthOperation<UIntType<N32>>.Instance,
        StructuredBufferLoadOperation<FloatType<N32>>.Instance,
        StructuredBufferLoadOperation<IntType<N32>>.Instance,
        StructuredBufferLoadOperation<UIntType<N32>>.Instance
    ];
}
