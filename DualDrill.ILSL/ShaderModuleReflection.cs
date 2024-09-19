using DualDrill.Graphics;
using System.Collections.Immutable;
using System.Linq.Expressions;

namespace DualDrill.ILSL;

public interface IShaderModuleReflection
{
    public GPUBindGroupLayoutDescriptor GetBindGroupLayoutDescriptor(
        IR.Module module
    );

    public IVertexBufferMappingBuilder<TGPULayout, THostLayout> GetVertexBufferLayoutBuilder<TGPULayout, THostLayout>();
}

public interface IVertexBufferMappingBuilder<TGPULayout, THostLayout>
{
    IVertexBufferMappingBuilder<TGPULayout, THostLayout> AddMapping<TElement>(
           Expression<Func<TGPULayout, TElement>> targetBinding,
           Expression<Func<THostLayout, TElement>> sourceBuffer);

    ImmutableArray<GPUVertexBufferLayout> Build();
}

sealed class HostBufferLayout<TBufferModel>(int Binding)
    where TBufferModel : unmanaged
{
}

sealed record VertexDataMapping<THostBufferModel, TShaderModel>(
    Expression<Func<THostBufferModel, TShaderModel>> Mapping)
{
}

