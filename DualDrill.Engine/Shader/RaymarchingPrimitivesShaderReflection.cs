using System.Collections.Immutable;
using DualDrill.CLSL.Language.Declaration;
using DualDrill.CLSL.Language.ShaderAttribute;
using DualDrill.CLSL.Reflection;
using DualDrill.Graphics;
using DualDrill.Mathematics;

namespace DualDrill.Engine.Shader;

public struct RaymarchingPrimitiveVertexInput
{
    [Location(0)] public vec2f32 position;
}

public sealed class RaymarchingPrimitivesShaderReflection : IReflection
{
    private readonly IShaderModuleReflection _shaderModuleReflection = new ShaderModuleReflection();

    public ImmutableArray<GPUVertexBufferLayout>? GetVertexBufferLayout()
    {
        var builder =
            _shaderModuleReflection.GetVertexBufferLayoutBuilder<RaymarchingPrimitiveVertexInput>();
        return builder.Build();
    }

    public GPUBindGroupLayoutDescriptor? GetBindGroupLayoutDescriptor(IShaderModuleDeclaration module)
    {
        return _shaderModuleReflection.GetBindGroupLayoutDescriptor(module);
    }

    public GPUBindGroupLayoutDescriptorBuffer? GetBindGroupLayoutDescriptorBuffer(IShaderModuleDeclaration module)
    {
        return _shaderModuleReflection.GetBindGroupLayoutDescriptorBuffer(module);
    }
}
