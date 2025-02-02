using DualDrill.CLSL.Language.ShaderAttribute;
using DualDrill.CLSL;
using DualDrill.Mathematics;

namespace DualDrill.Engine.Shader;

struct VertexOutput
{
    [Builtin(BuiltinBinding.position)]
    public vec4f32 position { get; set; }
}

[CLSLDevelopExpectedWGPUCode("""
        // Vertex shader

        struct VertexOutput {
            @builtin(position) clip_position: vec4<f32>,
        };

        @vertex
        fn vs_main(
            @builtin(vertex_index) in_vertex_index: u32,
        ) -> VertexOutput {
            var out: VertexOutput;
            let x = f32(1 - i32(in_vertex_index)) * 0.5;
            let y = f32(i32(in_vertex_index & 1u) * 2 - 1) * 0.5;
            out.clip_position = vec4<f32>(x, y, 0.0, 1.0);
            return out;
        }

        // Fragment shader

        @fragment
        fn fs_main(in: VertexOutput) -> @location(0) vec4<f32> {
            return vec4<f32>(0.3, 0.2, 0.1, 1.0);
        }
        """)]
public struct GradientColorTriangleShader : ISharpShader
{
    [Vertex]
    static VertexOutput vs(
        [Builtin(BuiltinBinding.vertex_index)]
        uint in_vertex_index
    )
    {
        var x = (float)(1 - (int)in_vertex_index) * 0.5f;
        var y = (float)((int)(in_vertex_index & 1u) * 2 - 1) * 0.5f;
        VertexOutput result = default;
        result.position = DMath.vec4(x, y, 0.0f, 1.0f);
        return result;
    }

    [Fragment]
    [return: Location(0)]
    static vec4f32 fs(VertexOutput vout)
    {
        return DMath.vec4(vout.position.xy * 0.001f, 1.0f, 1.0f);
    }
}
