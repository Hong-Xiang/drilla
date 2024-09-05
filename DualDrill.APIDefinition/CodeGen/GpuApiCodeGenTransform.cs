using System.Collections.Immutable;

namespace DualDrill.ApiGen.CodeGen;

internal sealed class GpuApiCodeGenTransform : INameTransform
{
    static ImmutableHashSet<string> SupportMethodHandles = [
     "GPUAdapter",
    //"GPUBindGroup",
    //"GPUBindGroupLayout",
    "GPUBuffer",
    "GPUCommandBuffer",
    "GPUCommandEncoder",
    "GPUComputePassEncoder",
    //"GPUComputePipeline",
    "GPUDevice",
    "GPUInstance",
    //"GPUPipelineLayout",
    //"GPUQuerySet",
    //"GPUQueue",
    //"GPURenderBundle",
    //"GPURenderBundleEncoder",
    //"GPURenderPassEncoder",
    //"GPURenderPipeline",
    //"GPUSampler",
    //"GPUShaderModule",
    //"GPUSurface",
    //"GPUTexture",
    //"GPUTextureView"

    ];

    string? INameTransform.MethodName(string typeName, string methodName)
    {
        return (typeName, methodName) switch
        {
            (_, "destroy") => "Dispose",
            (_, _) when SupportMethodHandles.Contains(typeName) => methodName,
            _ => null
        };
    }
}
