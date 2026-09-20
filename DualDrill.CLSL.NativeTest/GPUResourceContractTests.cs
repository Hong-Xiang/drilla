using DualDrill.Graphics;

namespace DualDrill.CLSL.NativeTest;

public sealed class GPUResourceContractTests
{
    [Theory]
    [InlineData(typeof(IGPURenderBundle))]
    [InlineData(typeof(IGPUComputePipeline))]
    [InlineData(typeof(IGPUQuerySet))]
    [InlineData(typeof(IGPUSampler))]
    [InlineData(typeof(IGPUBindGroup))]
    [InlineData(typeof(IGPUBindGroupLayout))]
    public void Owned_resource_interfaces_expose_disposal(Type contract)
    {
        Assert.True(contract.IsAssignableTo(typeof(IDisposable)));
    }
}
