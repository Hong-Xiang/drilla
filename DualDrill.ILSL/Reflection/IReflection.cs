using System.Collections.Immutable;
using DualDrill.CLSL.Language.Declaration;
using DualDrill.Graphics;

namespace DualDrill.CLSL.Reflection;

public interface IReflection
{
    public ImmutableArray<GPUVertexBufferLayout>? GetVertexBufferLayout();
    public GPUBindGroupLayoutDescriptor? GetBindGroupLayoutDescriptor(IShaderModuleDeclaration module);
}