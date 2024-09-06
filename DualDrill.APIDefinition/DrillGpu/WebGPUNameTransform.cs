using DualDrill.ApiGen.DrillLang.Types;
using System.Collections.Immutable;

namespace DualDrill.ApiGen.DrillGpu;

internal sealed record class WebGPUNameTransform(
    ImmutableHashSet<string> HandleNames
) : INameTransform
{
    string? INameTransform.HandleName(string name)
    {
        return name switch
        {
            _ when HandleNames.Contains("W" + name) => name,
            "GPU" => "GPUInstance",
            "GPUCanvasContext" => "GPUSurface",

            //"GPUError" => null,
            //"GPUExternalTexture" => null,
            _ => null
        };
    }

    string? INameTransform.MethodName(string typeName, string methodName)
    {
        return (typeName, methodName) switch
        {
            (_, "pushErrorScope") => null,
            (_, "copyExternalImageToTexture") => null,
            _ => methodName,
        };
    }

    string? INameTransform.EnumName(string name)
    {
        return name switch
        {
            "GPUAutoLayoutMode" => null,
            "GPUPipelineErrorReason" => null,
            "GPUCanvasToneMappingMode" => null,
            "GPUCanvasAlphaMode" => null,
            "GPUColorWrite" => "GPUColorWriteMask",
            _ => name
        };
    }
    string? INameTransform.EnumValueName(string enumName, string valueName)
    {
        return (enumName, valueName) switch
        {
            ("GPUBlendFactor", _) when valueName.Contains("src1") => null,
            ("GPUVertexFormat", "unorm10-10-10-2") => null,
            ("GPUFeatureName", "texture-compression-bc-sliced-3d") => null,
            ("GPUFeatureName", "clip-distances") => null,
            ("GPUFeatureName", "dual-source-blending") => null,
            _ => valueName
        };
    }

    string? INameTransform.TypeReferenceName(string name)
    {
        return name switch
        {
            //"undefined" => new VoidTypeReference(),
            //"boolean" => new BoolTypeReference(),
            //"USVString" => new StringTypeReference(),
            //"GPUSize64" => new IntegerTypeReference(BitWidth.N64, false),
            //"GPUSize32" => new IntegerTypeReference(BitWidth.N32, false),
            //"GPUIndex16" => new IntegerTypeReference(BitWidth.N16, true),
            //"GPUIndex32" => new IntegerTypeReference(BitWidth.N32, true),
            //"GPUIndex64" => new IntegerTypeReference(BitWidth.N64, true),
            //"GPUBufferDynamicOffset" => new IntegerTypeReference(BitWidth.N32, false),
            //"ArrayBuffer" => new SequenceTypeReference(new IntegerTypeReference(BitWidth.N8, false)),
            //"Uint32Array" => new SequenceTypeReference(new IntegerTypeReference(BitWidth.N32, false)),

            // no support for GPUError and GPUExternalTexture
            "GPUError" => null,
            "GPUExternalTexture" => null,

            "GPUMapModeFlags" => "GPUMapMode",
            "GPUColorWrite" => "GPUColorWriteMask",
            _ => name
        };
    }
}

