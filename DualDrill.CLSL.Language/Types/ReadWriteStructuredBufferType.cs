using DualDrill.CLSL.Language.Operation;
using DualDrill.CLSL.Language.Symbol;
using DualDrill.Common;
using DualDrill.Common.Nat;

namespace DualDrill.CLSL.Language.Types;

public sealed class ReadWriteStructuredBufferType<TElement>
    : IShaderType<ReadWriteStructuredBufferType<TElement>>,
      ISingleton<ReadWriteStructuredBufferType<TElement>>
    where TElement : IScalarType<TElement>
{
    private ReadWriteStructuredBufferType()
    {
    }

    public static ReadWriteStructuredBufferType<TElement> Instance
    {
        get
        {
            StructuredBufferScalarFamily.RequireElement<TElement>();
            return Holder.Instance;
        }
    }

    private static class Holder
    {
        internal static readonly ReadWriteStructuredBufferType<TElement> Instance = new();
    }

    public IShaderType ElementType => TElement.Instance;
    public uint ElementStride => 4;
    public string Name => $"RWStructuredBuffer<{TElement.Instance.Name}>";

    public IRefType GetRefType() => throw new NotSupportedException();

    public IPtrType GetPtrType(IAddressSpace addressSpace) =>
        IPtrType.CreateFromSingletonType<ReadWriteStructuredBufferType<TElement>>(addressSpace);

    public T Evaluate<T>(IShaderTypeSemantic<T, T> semantic) =>
        throw new NotSupportedException("Read-write structured buffers are not supported by this type semantic.");
}

public static class ReadWriteStructuredBufferFamily
{
    public static bool IsCanonicalType(IShaderType type) =>
        ReferenceEquals(type, ReadWriteStructuredBufferType<FloatType<N32>>.Instance) ||
        ReferenceEquals(type, ReadWriteStructuredBufferType<IntType<N32>>.Instance) ||
        ReferenceEquals(type, ReadWriteStructuredBufferType<UIntType<N32>>.Instance);

    public static bool IsCanonicalLength(IOperation operation) =>
        ReferenceEquals(operation, ReadWriteStructuredBufferLengthOperation<FloatType<N32>>.Instance) ||
        ReferenceEquals(operation, ReadWriteStructuredBufferLengthOperation<IntType<N32>>.Instance) ||
        ReferenceEquals(operation, ReadWriteStructuredBufferLengthOperation<UIntType<N32>>.Instance);

    public static bool IsCanonicalLoad(IOperation operation) =>
        ReferenceEquals(operation, ReadWriteStructuredBufferLoadOperation<FloatType<N32>>.Instance) ||
        ReferenceEquals(operation, ReadWriteStructuredBufferLoadOperation<IntType<N32>>.Instance) ||
        ReferenceEquals(operation, ReadWriteStructuredBufferLoadOperation<UIntType<N32>>.Instance);

    public static bool IsCanonicalStore(IOperation operation) =>
        ReferenceEquals(operation, ReadWriteStructuredBufferStoreOperation<FloatType<N32>>.Instance) ||
        ReferenceEquals(operation, ReadWriteStructuredBufferStoreOperation<IntType<N32>>.Instance) ||
        ReferenceEquals(operation, ReadWriteStructuredBufferStoreOperation<UIntType<N32>>.Instance);

    public static IEnumerable<IOperation> CanonicalOperations =>
    [
        ReadWriteStructuredBufferLengthOperation<FloatType<N32>>.Instance,
        ReadWriteStructuredBufferLengthOperation<IntType<N32>>.Instance,
        ReadWriteStructuredBufferLengthOperation<UIntType<N32>>.Instance,
        ReadWriteStructuredBufferLoadOperation<FloatType<N32>>.Instance,
        ReadWriteStructuredBufferLoadOperation<IntType<N32>>.Instance,
        ReadWriteStructuredBufferLoadOperation<UIntType<N32>>.Instance,
        ReadWriteStructuredBufferStoreOperation<FloatType<N32>>.Instance,
        ReadWriteStructuredBufferStoreOperation<IntType<N32>>.Instance,
        ReadWriteStructuredBufferStoreOperation<UIntType<N32>>.Instance
    ];
}
