using DualDrill.CLSL.Language.Symbol;
using DualDrill.Common;
using DualDrill.Common.Nat;

namespace DualDrill.CLSL.Language.Types;

public sealed class ReadOnlyStructuredBufferType
    : IShaderType<ReadOnlyStructuredBufferType>,
      ISingleton<ReadOnlyStructuredBufferType>
{
    private ReadOnlyStructuredBufferType()
    {
    }

    public static ReadOnlyStructuredBufferType Instance { get; } = new();

    public IShaderType ElementType => FloatType<N32>.Instance;
    public uint ElementStride => 4;
    public string Name => "StructuredBuffer<f32>";

    public IRefType GetRefType() => throw new NotSupportedException();

    public IPtrType GetPtrType(IAddressSpace addressSpace) =>
        IPtrType.CreateFromSingletonType<ReadOnlyStructuredBufferType>(addressSpace);

    public T Evaluate<T>(IShaderTypeSemantic<T, T> semantic) =>
        throw new NotSupportedException("Read-only structured buffers are not supported by this type semantic.");
}
