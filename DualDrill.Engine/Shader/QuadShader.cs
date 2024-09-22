using DualDrill.Graphics;
using DualDrill.ILSL;
using DualDrill.ILSL.IR.Declaration;
using Silk.NET.Input;
using Silk.NET.SDL;
using System.Collections.Immutable;
using System.Numerics;
using static DualDrill.Engine.Shader.QuadShader;

namespace DualDrill.Engine.Shader;


public class QuadShaderReflection : IReflectable
{
    private IShaderModuleReflection _shaderModuleReflection;
    public QuadShaderReflection()
    {
        _shaderModuleReflection = new ShaderModuleReflection();
    }

    public ImmutableArray<GPUVertexBufferLayout>? GetVertexBufferLayout()
    {
        var vertexBufferLayoutBuilder = _shaderModuleReflection.GetVertexBufferLayoutBuilder<QuadShaderVertexInput>();
        return vertexBufferLayoutBuilder.Build();
    }

    public GPUBindGroupLayoutDescriptor? GetBindGroupLayoutDescriptor(ILSL.IR.Module module)
    {
        return _shaderModuleReflection.GetBindGroupLayoutDescriptor(module);
    }
}


public struct QuadShaderResolution
{
    public uint resX;
    public uint resY;
}

public struct QuadShaderVertexInput
{
    [Location(0)]
    public Vector2 position;
}

public struct QuadShader : IShaderModule
{
    [Group(0)]
    [Binding(0)]
    [Uniform]
    QuadShaderResolution resolution;

    [Vertex]
    [return: Builtin(BuiltinBinding.position)]
    Vector4 vs(QuadShaderVertexInput vert)
    {
        return new Vector4(vert.position.X, vert.position.Y, 0.0f, 1.0f);
    }

    [Fragment]
    [return: Location(0)]
    Vector4 fs([Builtin(BuiltinBinding.position)] Vector4 vertex_in)
    {
        return new Vector4(vertex_in.X / resolution.resX, vertex_in.Y / resolution.resY, 0.0f, 1.0f);
    }
}
