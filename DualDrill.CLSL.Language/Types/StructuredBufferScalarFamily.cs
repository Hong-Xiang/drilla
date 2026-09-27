using DualDrill.Common.Nat;

namespace DualDrill.CLSL.Language.Types;

public static class StructuredBufferScalarFamily
{
    public static void RequireElement<TElement>() where TElement : IScalarType<TElement>
    {
        if (typeof(TElement) != typeof(FloatType<N32>) &&
            typeof(TElement) != typeof(IntType<N32>) &&
            typeof(TElement) != typeof(UIntType<N32>))
            throw new NotSupportedException(
                $"StructuredBuffer<{typeof(TElement).Name}> supports only f32, i32, and u32 elements.");
    }
}
