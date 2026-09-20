using DualDrill.CLSL.Language.Symbol;
using DualDrill.Common;
using DualDrill.Common.Nat;

namespace DualDrill.CLSL.Language.Types;

public sealed class ReadWriteStructuredBufferType
    : IShaderType<ReadWriteStructuredBufferType>,
      ISingleton<ReadWriteStructuredBufferType>
{
    private ReadWriteStructuredBufferType()
    {
    }

    public static ReadWriteStructuredBufferType Instance { get; } = new();

    public IShaderType ElementType => FloatType<N32>.Instance;
    public uint ElementStride => 4;
    public string Name => "RWStructuredBuffer<f32>";

    public IRefType GetRefType() => throw new NotSupportedException();

    public IPtrType GetPtrType(IAddressSpace addressSpace) =>
        IPtrType.CreateFromSingletonType<ReadWriteStructuredBufferType>(addressSpace);

    public T Evaluate<T>(IShaderTypeSemantic<T, T> semantic) =>
        throw new NotSupportedException("Read-write structured buffers are not supported by this type semantic.");
}
