using DualDrill.CLSL.Language.ShaderAttribute;
using DualDrill.Mathematics;

namespace DualDrill.CLSL.Test.ShaderModule;

internal sealed class TextureSampleLevelShaderModule : ISharpShader
{
    [Group(0), Binding(2)]
    private static readonly Texture2D<float> Color;

    [Group(0), Binding(3)]
    private static readonly SamplerState Linear;

    [Fragment]
    [return: Location(0)]
    public static vec4f32 Shade([Location(0)] vec2f32 uv) =>
        Color.SampleLevel(Linear, uv, 0.0f);
}
