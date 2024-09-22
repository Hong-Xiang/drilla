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
        var vertexBufferLayoutBuilder = _shaderModuleReflection.GetVertexBufferLayoutBuilder<VertexInput>();
        return vertexBufferLayoutBuilder.Build();
    }

    public GPUBindGroupLayoutDescriptor? GetBindGroupLayoutDescriptor(ILSL.IR.Module module)
    {
        return _shaderModuleReflection.GetBindGroupLayoutDescriptor(module);
    }
}

public struct QuadShader : IILSLDevelopShaderModule
{
    public string ILSLWGSLExpectedCode => """

    @location(0) position: vec2<f32>;

    struct Resolution
    {
        resX: u32,
        resY: u32,
    };

    @group(0) @binding(0) var<uniform> resolution: Resolution;

    @vertex
    fn vs(position: vec2<f32>) -> @builtin(position) vec4<f32>
    {
      return vec4<f32>(vert.position.x, vert.position.y, 0f, 1f);
    }


    @fragment
    fn fs(@builtin(position) vertex_in: vec4<f32>) -> @location(0) vec4<f32>
    {
      return vec4<f32>(vertex_in.x / f32(resolution.resX), vertex_in.y / f32(resolution.resY) , 0f, 1f);
    }
    """;

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
