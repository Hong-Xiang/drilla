using DualDrill.Mathematics;

namespace DualDrill.CLSL;

public readonly struct SamplerState;

public readonly struct Texture2D<T>
{
    public vec4f32 SampleLevel(in SamplerState sampler, vec2f32 uv, float lod) =>
        throw new NotSupportedException("Texture2D is a shader-only resource.");
}
