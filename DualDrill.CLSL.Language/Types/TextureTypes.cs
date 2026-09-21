using DualDrill.CLSL.Language.Symbol;
using DualDrill.Common;

namespace DualDrill.CLSL.Language.Types;

public sealed class SampledTexture2DF32Type
    : IShaderType<SampledTexture2DF32Type>,
      ISingleton<SampledTexture2DF32Type>
{
    private SampledTexture2DF32Type()
    {
    }

    public static SampledTexture2DF32Type Instance { get; } = new();

    public IShaderType SampleType => ShaderType.F32;
    public IShaderType ResultType => ShaderType.Vec4F32;
    public string Name => "Texture2D<vec4<f32>>";

    public IRefType GetRefType() => throw new NotSupportedException();

    public IPtrType GetPtrType(IAddressSpace addressSpace) =>
        IPtrType.CreateFromSingletonType<SampledTexture2DF32Type>(addressSpace);

    public T Evaluate<T>(IShaderTypeSemantic<T, T> semantic) =>
        throw new NotSupportedException("Sampled textures are not supported by this type semantic.");
}

public sealed class SamplerStateType
    : IShaderType<SamplerStateType>,
      ISingleton<SamplerStateType>
{
    private SamplerStateType()
    {
    }

    public static SamplerStateType Instance { get; } = new();

    public string Name => "SamplerState";

    public IRefType GetRefType() => throw new NotSupportedException();

    public IPtrType GetPtrType(IAddressSpace addressSpace) =>
        IPtrType.CreateFromSingletonType<SamplerStateType>(addressSpace);

    public T Evaluate<T>(IShaderTypeSemantic<T, T> semantic) =>
        throw new NotSupportedException("Samplers are not supported by this type semantic.");
}
