using System.Numerics;
using DualDrill.ILSL;

namespace DualDrill.Engine.Shader;

public struct MinimumTriangle : IShaderModule, IILSLDevelopShaderModule
{
    string IILSLDevelopShaderModule.ILSLWGSLExpectedCode => """
      @vertex fn vs(@builtin(vertex_index) vertex_index : u32) 
        -> @builtin(position) vec4f 
      {
        let x = f32(1 - i32(vertex_index)) * 0.5;
        let y = f32(i32(vertex_index & 1u) * 2 - 1) * 0.5;
        return vec4f(x, y, 0.0, 1.0);
      }
 
      @fragment fn fs() -> @location(0) vec4f {
        return vec4f(0.5, 1.0, 0.5, 1.0);
      }
      """;

    [Vertex]
    [return: Builtin(BuiltinBinding.Position)]
    static Vector4 vs(
        [Builtin(BuiltinBinding.VertexIndex)] uint vertexIndex
    )
    {
        var x = (float)(1 - (int)vertexIndex) * 0.5f;
        var y = (float)((int)(vertexIndex & 1u) * 2 - 1) * 0.5f;
        return new Vector4(x, y, 0.0f, 1.0f);
    }

    [Fragment]
    [return: Location(0)]
    static Vector4 fs()
    {
        return new Vector4(0.5f, 1.0f, 0.5f, 1.0f);
    }

}
