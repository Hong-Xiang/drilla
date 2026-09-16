using DualDrill.CLSL;
using DualDrill.CLSL.Language.ShaderAttribute;
using DualDrill.Mathematics;
using static DualDrill.Mathematics.DMath;

namespace DualDrill.Compiler.Server;

public sealed class MinimumTriangleShader : ISharpShader
{
    [Vertex]
    [return: Builtin(BuiltinBinding.position)]
    public static vec4f32 vs([Builtin(BuiltinBinding.vertex_index)] uint vertexIndex)
    {
        var index = (int)vertexIndex;
        var x = (1 - index) * (1 - (index & 1)) * 0.65f;
        var y = ((index & 1) * 2 - 1) * 0.65f;
        return vec4(x, y, 0.0f, 1.0f);
    }

    [Fragment]
    [return: Location(0)]
    public static vec4f32 fs() => vec4(1.0f, 0.32f, 0.12f, 1.0f);
}
