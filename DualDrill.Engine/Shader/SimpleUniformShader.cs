using DualDrill.ILSL;
using System.Numerics;

namespace DualDrill.Engine.Shader;

public struct SimpleUniformShader : IShaderModule
{
    //public string ILSLWGSLExpectedCode => """
    //            struct OurStruct {
    //      color: vec4f,
    //      scale: vec2f,
    //      offset: vec2f,
    //    };

    //    @group(0) @binding(0) var<uniform> ourStruct: OurStruct;

    //    @vertex fn vs(
    //      @builtin(vertex_index) vertexIndex : u32
    //    ) -> @builtin(position) vec4f {
    //      let pos = array(
    //        vec2f( 0.0,  0.5),  // top center
    //        vec2f(-0.5, -0.5),  // bottom left
    //        vec2f( 0.5, -0.5)   // bottom right
    //      );

    //      return vec4f(
    //        pos[vertexIndex] * ourStruct.scale + ourStruct.offset, 0.0, 1.0);
    //    }

    //    @fragment fn fs() -> @location(0) vec4f {
    //      return ourStruct.color;
    //    }
    //    """;

    public struct VertexOutput
    {
        public Vector4 color;
        public Vector2 scale;
        public Vector2 offset;
    }

    [Group(0)]
    [Binding(0)]
    [Uniform]
    VertexOutput uniformData;


    [Vertex]
    [return: Builtin(BuiltinBinding.position)]
    Vector4 vs([Builtin(BuiltinBinding.vertex_index)] uint vertexIndex)
    {
        Vector2 pos = new(0.0f, 0.0f);
        var u0 = 0u;
        var u1 = 1u;
        var u2 = 2u;
        if (vertexIndex == u0)
        {
            pos = new Vector2(0.0f, 0.5f);
        }
        if (vertexIndex == u1)
        {
            pos = new Vector2(-0.5f, -0.5f);
        }
        if (vertexIndex == u2)
        {
            pos = new Vector2(0.5f, -0.5f);
        }
        return new Vector4(pos * uniformData.scale + uniformData.offset, 0.0f, 1.0f);
    }

    [Fragment]
    [return: Location(0)]
    Vector4 fs()
    {
        return uniformData.color;
    }
}
