using System.Collections.Immutable;
using DualDrill.CLSL.Language.Declaration;
using DualDrill.Graphics;

namespace DualDrill.CLSL.Reflection;

public interface IReflection
{
    public ImmutableArray<GPUVertexBufferLayout>? GetVertexBufferLayout();
    public ImmutableArray<ShaderUniformBinding> GetUniformBindings(IShaderModuleDeclaration module);
    public GPUBindGroupLayoutDescriptor GetBindGroupLayoutDescriptor(
        IShaderModuleDeclaration module,
        int group);
    public GPUBindGroupLayoutDescriptorBuffer GetBindGroupLayoutDescriptorBuffer(
        IShaderModuleDeclaration module,
        int group);
}