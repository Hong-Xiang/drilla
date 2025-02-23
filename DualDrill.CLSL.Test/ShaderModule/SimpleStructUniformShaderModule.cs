using DualDrill.CLSL.Language.ShaderAttribute;
using DualDrill.Mathematics;
using System.Numerics;
using static DualDrill.Mathematics.DMath;

namespace DualDrill.CLSL.Test.ShaderModule;

// https://webgpufundamentals.org/webgpu/lessons/webgpu-uniforms.html
sealed class SimpleStructUniformShaderModule : ISharpShader
{
    public struct OurStruct
    {
        public vec4f32 color;
        public vec2f32 scale;
        public vec2f32 offset;
    }

    [Group(0)]
    [Binding(0)]
    [Uniform]
    static readonly OurStruct ourStruct;

    [Vertex]
    [return: Builtin(BuiltinBinding.position)]
    public static vec4f32 vs(
        [Builtin(BuiltinBinding.vertex_index)] uint vertexIndex
    )
    {
        var pos = vec2f32();
        // uint v0 = 0u;
        // uint v1 = 1u;
        // uint v2 = 2u;
        if (vertexIndex == 0)
        {
            pos = vec2(0.0f, 0.5f);
        }
        if (vertexIndex == 1)
        {
            pos = vec2(-0.5f, -0.5f);
        }
        if (vertexIndex == 2)
        {
            pos = vec2(0.5f, -0.5f);
        }
        return vec4(
            pos * ourStruct.scale + ourStruct.offset, 0.0f, 1.0f
        );
    }

    [Fragment]
    [return: Location(0)]
    public static vec4f32 fs()
    {
        return ourStruct.color;
    }
}
