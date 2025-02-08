using DualDrill.CLSL.Language.ShaderAttribute;
using System.Numerics;

namespace DualDrill.CLSL.Test.ShaderModule;

// https://webgpufundamentals.org/webgpu/lessons/webgpu-uniforms.html
sealed class SimpleStructUniformShaderModule : ISharpShader
{
    public struct OurStruct
    {
        public Vector4 color;
        public Vector2 scale;
        public Vector2 offset;
    }

    [Group(0)]
    [Binding(0)]
    [Uniform]
    static readonly OurStruct ourStruct;

    [Vertex]
    [return: Builtin(BuiltinBinding.position)]
    public static Vector4 vs(
        [Builtin(BuiltinBinding.vertex_index)] uint vertexIndex
    )
    {
        Vector2 pos = new();
        uint v0 = 0u;
        uint v1 = 1u;
        uint v2 = 2u;
        if (vertexIndex == v0)
        {
            pos = new Vector2(0.0f, 0.5f);
        }
        if (vertexIndex == v1)
        {
            pos = new Vector2(-0.5f, -0.5f);
        }
        if (vertexIndex == v2)
        {
            pos = new Vector2(0.5f, -0.5f);
        }
        return new Vector4(
            pos * ourStruct.scale + ourStruct.offset, 0.0f, 1.0f
        );
    }

    [Fragment]
    [return: Location(0)]
    public static Vector4 fs()
    {
        return ourStruct.color;
    }
}
