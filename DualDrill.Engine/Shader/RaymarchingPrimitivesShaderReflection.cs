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

    public ImmutableArray<ShaderUniformBinding> GetUniformBindings(IShaderModuleDeclaration module) =>
        _shaderModuleReflection.GetUniformBindings(module);

    public ImmutableArray<ShaderTextureBinding> GetTextureBindings(IShaderModuleDeclaration module) =>
        _shaderModuleReflection.GetTextureBindings(module);

    public ImmutableArray<ShaderSamplerBinding> GetSamplerBindings(IShaderModuleDeclaration module) =>
        _shaderModuleReflection.GetSamplerBindings(module);

    public GPUBindGroupLayoutDescriptor GetBindGroupLayoutDescriptor(
        IShaderModuleDeclaration module,
        int group) =>
        _shaderModuleReflection.GetBindGroupLayoutDescriptor(module, group);

    public GPUBindGroupLayoutDescriptorBuffer GetBindGroupLayoutDescriptorBuffer(
        IShaderModuleDeclaration module,
        int group) =>
        _shaderModuleReflection.GetBindGroupLayoutDescriptorBuffer(module, group);
}
