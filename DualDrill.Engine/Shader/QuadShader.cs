using DualDrill.Graphics;
using DualDrill.ILSL;
using Silk.NET.SDL;
using System.Collections.Immutable;
using System.Numerics;


namespace DualDrill.Engine.Shader;


public class QuadShaderReflection : IReflection
{
    private IShaderModuleReflection _shaderModuleReflection;
    public QuadShaderReflection()
    {
        _shaderModuleReflection = new ShaderModuleReflection();
    }

    public ImmutableArray<GPUVertexBufferLayout>? GetVertexBufferLayout()
    {
        var vertexBufferLayoutBuilder = _shaderModuleReflection.GetVertexBufferLayoutBuilder<QuadShader.VertexInput>();
        return vertexBufferLayoutBuilder.Build();
    }

    public GPUBindGroupLayoutDescriptor? GetBindGroupLayoutDescriptor(ILSL.IR.Module module)
    {
        return _shaderModuleReflection.GetBindGroupLayoutDescriptor(module);
    }

    public GPUBindGroupLayoutDescriptorBuffer? GetBindGroupLayoutDescriptorBuffer(ILSL.IR.Module module)
    {
        return _shaderModuleReflection.GetBindGroupLayoutDescriptorBuffer(module);
    }
}

public struct QuadShader : IShaderModule
{
    public struct Resolution
    {
        public uint resX;
        public uint resY;
    }

    public struct VertexInput
    {
        [Location(0)]
        public Vector2 position;
    }

    [Group(0)]
    [Binding(0)]
    [Uniform]
    Resolution resolution;

    [Vertex]
    [return: Builtin(BuiltinBinding.position)]
    Vector4 vs(VertexInput vert)
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
