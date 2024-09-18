using DualDrill.Graphics;
using System.Collections.Immutable;
using System.Linq.Expressions;

namespace DualDrill.ILSL;

public interface IShaderModuleReflection
{
    public GPUBindGroupLayoutDescriptor GetBindGroupLayoutDescriptor(
        IR.Module module
    );

    public GPUVertexBufferLayout GetVertexBufferLayout(
        IR.Module module
    );
}

sealed class HostBufferLayout<TBufferModel>(int Binding)
    where TBufferModel : unmanaged
{
}

sealed record VertexDataMapping<THostBufferModel, TShaderModel>(
    Expression<Func<THostBufferModel, TShaderModel>> Mapping)
{
}

