using DualDrill.ApiGen.DrillLang.Declaration;
using DualDrill.Common;
using System.Collections.Immutable;

namespace DualDrill.ApiGen.CodeGen;

internal sealed record class GpuApiCodeGenTransform(ModuleDeclaration EvergineModule) : INameTransform
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

    string? INameTransform.EnumValueName(string enumName, string valueName)
    {
        return EvergineWebGPUApi.GetEnumMemberName(enumName, valueName, EvergineModule);
    }

    string? INameTransform.PropertyName(string typeName, string propertyName)
    {
        return propertyName.Capitalize();
    }

    string? INameTransform.MethodName(string typeName, string methodName)
    {
        return (typeName, methodName) switch
        {
            (_, "destroy") => null,
            (_, _) when SupportMethodHandles.Contains(typeName) => methodName.Capitalize(),
            _ => null
        };
    }
}
