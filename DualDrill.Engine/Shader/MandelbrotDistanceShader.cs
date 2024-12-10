using DualDrill.CLSL.Language.IR.ShaderAttribute;
using DualDrill.Graphics;
using DualDrill.Mathematics;
using System.Collections.Immutable;
using static DualDrill.Mathematics.DMath;

namespace DualDrill.Engine.Shader;

public class SampleFragmentShaderReflection : ILSL.IReflection
{
    private ILSL.IShaderModuleReflection _shaderModuleReflection;
    public SampleFragmentShaderReflection()
    {
        _shaderModuleReflection = new ILSL.ShaderModuleReflection();
    }

    public ImmutableArray<GPUVertexBufferLayout>? GetVertexBufferLayout()
    {
        var vertexBufferLayoutBuilder = _shaderModuleReflection.GetVertexBufferLayoutBuilder<MandelbrotDistanceShader.VertexInput>();
        return vertexBufferLayoutBuilder.Build();
    }

    public GPUBindGroupLayoutDescriptor? GetBindGroupLayoutDescriptor(CLSL.Language.IR.ShaderModule module)
    {
        return _shaderModuleReflection.GetBindGroupLayoutDescriptor(module);
    }

    public GPUBindGroupLayoutDescriptorBuffer? GetBindGroupLayoutDescriptorBuffer(CLSL.Language.IR.ShaderModule module)
    {
        return _shaderModuleReflection.GetBindGroupLayoutDescriptorBuffer(module);
    }
}


public struct BasicConditionShader : ILSL.ISharpShader
{
    static bool Pred(int a, int b)
    {
        return a >= b;
    }


    [Vertex]
    public static int vs(int a, int b)
    {
        if (Pred(a, b))
        {
            return a;
        }
        else
        {
            return b;
        }
    }
}

public struct MandelbrotDistanceShader : ILSL.ISharpShader
{
    public struct VertexInput
    {
        [Location(0)] public vec2f32 position;
    }

    [Uniform]
    [Group(0)]
    [Binding(0)]
    float iTime;

    [Vertex]
    [return: Builtin(BuiltinBinding.position)]
    public static vec4f32 vs(VertexInput vertex)
    {
        return vec4(vertex.position.xy, 0.0f, 1.0f);
    }

    [Fragment]
    [return: Location(0)]
    public vec4f32 fs([Builtin(BuiltinBinding.position)] vec4f32 fragCoord)
    {
        // Courtesy https://www.shadertoy.com/view/lsX3W4
        var iResolution = vec2(800.0f, 600.0f);
        var p = (2.0f * fragCoord.xy - iResolution) / iResolution;

        // animation
        float tz = 0.5f - 0.5f * cos(0.225f * iTime);
        float zoo = pow(0.5f, 13.0f * tz);
        var c = vec2(-0.05f, 0.6805f) + p * zoo;
        // distance to Mandelbrot
        var di = 1.0f;
        var z = vec2(0.0f, 0.0f);
        var m2 = 0.0f;
        var dz = vec2(0.0f);
        for (int i = 0; i < 300; i++)
        {
            if (m2 > 1024.0f)
            {
                di = 0.0f;
                break;
            }
            // Z' -> 2·Z·Z' + 1
            dz = 2.0f * vec2(z.x * dz.x - z.y * dz.y, z.x * dz.y + z.y * dz.x) + vec2(1.0f, 0.0f);
            // Z -> Z² + c
            z = vec2(z.x * z.x - z.y * z.y, 2.0f * z.x * z.y) + c;
            m2 = dot(z, z);
        }
        // distance
        // d(c) = |Z|·log|Z|/|Z'|
        var d = 0.5f * sqrt(dot(z, z) / dot(dz, dz)) * log(dot(z, z));
        if (di > 0.5f)
        {
            d = 0.0f;
        }
        // do some soft coloring based on distance
        var d_clamped = clamp(pow(4.0f * d / zoo, 0.2f), 0.0f, 1.0f);
        var col = vec3(d_clamped);
        return vec4(col, 1.0f);
    }
}
