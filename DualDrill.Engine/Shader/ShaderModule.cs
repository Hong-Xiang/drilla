using System.Numerics;
using DualDrill.ILSL;

namespace DualDrill.Engine.Shader;

struct VertexOutput
{
    [Builtin(BuiltinBinding.Position)]
    public Vector4 clip_position { get; set; }
}


public static class ExpectedResult
{
    public static readonly string Code = """
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
}


public struct ShaderModule : IShaderModule
{
    [Vertex]
    static VertexOutput vs_main(
        [Builtin(BuiltinBinding.Position)]
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

    public TResult Match<TResult>(IShaderModule.IVisitor<TResult> matcher)
        => matcher.Match(this);
}

public interface IDevelopILSLExpectedCode
{
    public interface IMatcher<TResult>
    {
        TResult Match<T>(T value) where T : IDevelopILSLExpectedCode;
    }

    internal abstract static string __ILSLWGSLCode { get; }

    public static string GetCode<T>()
        where T : IDevelopILSLExpectedCode => T.__ILSLWGSLCode;

    TResult Match<TResult>(IMatcher<TResult> matcher);
}




public struct MinimumTriangle : IShaderModule, IDevelopILSLExpectedCode
{
    static string IDevelopILSLExpectedCode.__ILSLWGSLCode => """
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

    public TResult Match<TResult>(IDevelopILSLExpectedCode.IMatcher<TResult> matcher)
        => matcher.Match(this);
}
