using DualDrill.CLSL.Language.Declaration;
using DualDrill.Graphics;
using System.Collections.Immutable;

namespace DualDrill.CLSL.Reflection;

public interface IReflection
{
    public ImmutableArray<GPUVertexBufferLayout>? GetVertexBufferLayout();
    public GPUBindGroupLayoutDescriptor? GetBindGroupLayoutDescriptor(IShaderModuleDeclaration module);
}


