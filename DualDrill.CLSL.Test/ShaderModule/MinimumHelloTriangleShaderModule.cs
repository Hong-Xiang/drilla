using DualDrill.CLSL.Language.ShaderAttribute;
using DualDrill.Mathematics;
using static DualDrill.Mathematics.DMath;

namespace DualDrill.CLSL.Test.ShaderModule;

/// <summary>
/// Minimum triangle shader for basic compile test
/// </summary>
sealed class MinimumHelloTriangleShaderModule : ISharpShader
{
    [Vertex]
    [return: Builtin(BuiltinBinding.position)]
    public static vec4f32 vs([Builtin(BuiltinBinding.vertex_index)] uint vertex_index)
    {
        // vertices [ (0.5, -0.5) (0.0, 0.5) (-0.5, -0.5) ]
        // use bit manipulation to avoid using any control flow and array index language features
        var vi = (int)vertex_index;
        var x = (1 - vi) * (1 - (vi & 1)) * 0.5f;
        var y = ((vi & 1) * 2 - 1) * 0.5f;
        return vec4(x, y, 0.0f, 1.0f);
    }

    [Fragment]
    [return: Location(0)]
    public static vec4f32 fs() => vec4(1.0f, 1.0f, 1.0f, 1.0f);
}
