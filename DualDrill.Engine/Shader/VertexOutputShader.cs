using System.Collections.Immutable;
using System.Numerics;
using DualDrill.Graphics;
using DualDrill.ILSL;

namespace DualDrill.Engine.Shader;

struct VertexOutput
{
    [Builtin(BuiltinBinding.position)]
    public Vector4 clip_position { get; set; }
}

public struct VertexOutputShader : IILSLDevelopShaderModule
{
    public string ILSLWGSLExpectedCode => """
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
        """;

    [Vertex]
    static VertexOutput vs_main(
        [Builtin(BuiltinBinding.position)]
        uint in_vertex_index
    )
    {
        VertexOutput result = default;
        var x = (float)(1 - (int)in_vertex_index) * 0.5f;
        var y = (float)((int)(in_vertex_index & 1u) * 2 - 1) * 0.5f;
        result.clip_position = new Vector4(x, y, 0.0f, 1.0f);
        return result;
    }

    [Fragment]
    [return: Location(0)]
    static Vector4 fs_main(VertexOutput vout)
    {
        return new Vector4(0.3f, 0.2f, 0.1f, 1.0f);
    }
}
