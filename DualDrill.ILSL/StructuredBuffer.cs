namespace DualDrill.CLSL;

public readonly struct StructuredBuffer<T>
{
    public uint Length => throw new NotSupportedException("StructuredBuffer is a shader-only resource.");

    public T this[uint index] => throw new NotSupportedException("StructuredBuffer is a shader-only resource.");
}

public readonly struct RWStructuredBuffer<T>
{
    public uint Length => throw new NotSupportedException("RWStructuredBuffer is a shader-only resource.");

    public T this[uint index]
    {
        get => throw new NotSupportedException("RWStructuredBuffer is a shader-only resource.");
        set => throw new NotSupportedException("RWStructuredBuffer is a shader-only resource.");
    }
}
