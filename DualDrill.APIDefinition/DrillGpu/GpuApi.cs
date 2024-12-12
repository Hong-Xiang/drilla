using DualDrill.ApiGen.DrillLang.Declaration;
using DualDrill.ApiGen.DrillLang.Types;
using DualDrill.ApiGen.WebIDL;
using System.Diagnostics;

namespace DualDrill.ApiGen.DrillGpu;

public static class GPUApi
{
    public static void Validate(
        ModuleDeclaration drillGpuApi,
        ModuleDeclaration evergineApi
    )
    {
        Debug.Assert(evergineApi.Handles.Count == drillGpuApi.Handles.Count, "Handle count should be same for two apis");
        Debug.Assert(drillGpuApi.Handles.Count == 22, $"GPU API should provide 22 handles, got {drillGpuApi.Handles.Count}");

        foreach (var eDecl in drillGpuApi.Enums)
        {
            var evergineEnumName = EvergineWebGPUApi.GetEnumTypeName(eDecl.Name);
            var evergineEnumDecl = evergineApi.Enums.Single(e_ => string.Equals(e_.Name, evergineEnumName, StringComparison.OrdinalIgnoreCase));

            if (evergineEnumDecl.Values.Any(m => m.Name.Contains("Undefined")) && evergineEnumName != "WGPUDeviceLostReason")
            {
                Debug.Assert(eDecl.Values.Length == evergineEnumDecl.Values.Length - 2);
            }
            else
            {
                Debug.Assert(eDecl.Values.Length == evergineEnumDecl.Values.Length - 1);
            }
        }
    }

    public static WebIDLSpec WebGPUSpecAdHocFix(this WebIDLSpec spec)
    {
        // merge GPUDevice members
        var gpuDeviceDecls = spec.Declarations.OfType<InterfaceDecl>().Where(d => d.Name == "GPUDevice");
        WebIDL.IDeclaration mergedGPUDeviceDecl = new InterfaceDecl("GPUDevice", [.. gpuDeviceDecls.SelectMany(d => d.Members)]);
        var otherDecls = spec.Declarations.Where(decl => !(decl is InterfaceDecl d && d.Name == "GPUDevice"));
        return new WebIDLSpec([.. otherDecls, mergedGPUDeviceDecl]);
    }

    private static ModuleDeclaration AdHocFix(this ModuleDeclaration module, ModuleDeclaration evergineModule)
    {
        var result = module with
        {
            Enums = [..module.Enums.Select(e => {
                if(e.IsFlag){
                    return  e with {
                        Values =  [new EnumMemberDeclaration("NONE", new(0, true)), ..e.Values]
                    };
                }
                if(e.Name == "GPUVertexStepMode"){
                    return e with {
                        Values = [
                            ..e.Values,
                            new EnumMemberDeclaration("VertexBufferNotUsed", null)
                        ]
                    };
                }
                return e;
            })]
        };

        // attach evergine values
        result = result with
        {
            Enums = [..result.Enums.Select(e => {
                var evergineType = EvergineWebGPUApi.GetEnumType(e.Name);
                var values = e.Values.Select(v => v with {
                    Value = new ((int) Enum.Parse(evergineType, EvergineWebGPUApi.GetEnumMemberName( e.Name, v.Name, evergineModule )))
                });
                return e with {
                    Values = [..values] };
            })]
        };

        result = result with
        {
            Structs = [..result.Structs.Select(s => {
                if(s.Name.EndsWith("Descriptor") && !s.Properties.Any(p => p.Name == "label")){
                    return s with {
                        Properties = [new PropertyDeclaration("label", new StringTypeReference(), false), ..s.Properties]
                    };
                }else{
                    return s;
                }
            })]
        };

        return result;
    }

    static ITypeReference WebGPUWebIDLOpaqueTypeRefinement(string name)
    {
        return name switch
        {
            "undefined" => new VoidTypeReference(),
            "boolean" => new BoolTypeReference(),
            "USVString" => new StringTypeReference(),
            "DOMString" => new StringTypeReference(),
            "GPUSize64" => new IntegerTypeReference(BitWidth._64, false),
            "GPUSize32" => new IntegerTypeReference(BitWidth._32, false),
            "GPUSize32Out" => new IntegerTypeReference(BitWidth._32, false),
            "GPUIndex16" => new IntegerTypeReference(BitWidth._16, true),
            "GPUIndex32" => new IntegerTypeReference(BitWidth._32, true),
            "GPUIndex64" => new IntegerTypeReference(BitWidth._64, true),
            "double" => new FloatTypeReference(BitWidth._64),
            "GPUBufferDynamicOffset" => new IntegerTypeReference(BitWidth._32, false),
            "ArrayBuffer" => new SequenceTypeReference(new IntegerTypeReference(BitWidth._8, false)),
            "Uint32Array" => new SequenceTypeReference(new IntegerTypeReference(BitWidth._32, false)),
            "unsigned long" => new IntegerTypeReference(BitWidth._32, false),
            "unsigned short" => new IntegerTypeReference(BitWidth._16, false),
            "GPUStencilValue" => new IntegerTypeReference(BitWidth._32, false),
            "GPUIntegerCoordinate" => new IntegerTypeReference(BitWidth._32, false),
            "GPUMapModeFlags" => new OpaqueTypeReference("GPUMapMode"),
            "GPUSignedOffset32" => new IntegerTypeReference(BitWidth._32, true),
            "AllowSharedBufferSource" => new OpaqueTypeReference("nint"),
            "GPUImageDataLayout" => new OpaqueTypeReference("GPUTextureDataLayout"),
            "GPUCanvasConfiguration" => new OpaqueTypeReference("GPUSurfaceConfiguration"),
            "GPUSampleMask" => new IntegerTypeReference(BitWidth._32, false),
            "GPUDepthBias" => new IntegerTypeReference(BitWidth._32, true),
            _ => new OpaqueTypeReference(name)
        };

    }

    public static ModuleDeclaration ParseWebGPUWebIDLSpecToModuleDeclaration(
        WebIDLSpec idlSpec,
        ModuleDeclaration evergineModule)
    {
        // WebGPU specific transform,
        // used to map WebGPU's WebIDL types to DrillLang types
        //         map type names, enum names, etc.
        var transform = new WebGPUIdlToDrillGpuNameTransform([.. evergineModule.Handles.Select(h => h.Name)]);

        //idlSpec = idlSpec.WebGPUSpecAdHocFix();
        var module = idlSpec.ToModuleDeclaration();
        module = module.Transform(transform);
        module = module.RefineOpaqueType(WebGPUWebIDLOpaqueTypeRefinement);
        module = module.AdHocFix(evergineModule);
        Validate(module, evergineModule);
        return module;
    }

}

//internal sealed class ParseWebIDLResultToGPUApiTypeVisitor : ITypeReferenceVisitor<ITypeReference>
//{
//    public ITypeReference VisitBool(BoolTypeReference type) => type;

//    public ITypeReference VisitFloat(FloatTypeReference type) => type;

//    public ITypeReference VisitFuture(FutureTypeReference type) => type with
//    {
//        Type = type.Type.AcceptVisitor(this)
//    };


//    public ITypeReference VisitGeneric(GenericTypeReference type) => type;


//    public ITypeReference VisitInteger(IntegerTypeReference type) => type;


//    public ITypeReference VisitMatrix(MatrixTypeReference type) => type;


//    public ITypeReference VisitNullable(NullableTypeReference type) => type;


//    public ITypeReference VisitOpaque(OpaqueTypeReference type)
//    {
//        return type.Name switch
//        {
//            "undefined" => new VoidTypeReference(),
//            "boolean" => new BoolTypeReference(),
//            "USVString" => new StringTypeReference(),
//            "GPUSize64" => new IntegerTypeReference(BitWidth.N64, false),
//            "GPUSize32" => new IntegerTypeReference(BitWidth.N32, false),
//            "GPUIndex16" => new IntegerTypeReference(BitWidth.N16, true),
//            "GPUIndex32" => new IntegerTypeReference(BitWidth.N32, true),
//            "GPUIndex64" => new IntegerTypeReference(BitWidth.N64, true),
//            "GPUBufferDynamicOffset" => new IntegerTypeReference(BitWidth.N32, false),
//            "ArrayBuffer" => new SequenceTypeReference(new IntegerTypeReference(BitWidth.N8, false)),
//            "Uint32Array" => new SequenceTypeReference(new IntegerTypeReference(BitWidth.N32, false)),
//            _ => type
//        };
//    }

//    public ITypeReference VisitSequence(SequenceTypeReference type)
//        => type with
//        {
//            Type = type.Type.AcceptVisitor(this)
//        };

//    public ITypeReference VisitString(StringTypeReference type)
//        => type;

//    public ITypeReference VisitUnknown(UnknownTypeReference type)
//        => type;

//    public ITypeReference VisitVector(VectorTypeReference type) => type;


//    public ITypeReference VisitVoid(VoidTypeReference type) => type;

//}

//[Obsolete("Use module.CodeGenAdHocTransform() instead")]
//internal sealed class PostParseProcessGPUApiDeclarationsVisitor : IDeclarationVisitor<DrillLang.Declaration.IDeclaration?>
//{
//    ParseWebIDLResultToGPUApiTypeVisitor TypePostProcessVisitor { get; } = new();

//    private ImmutableHashSet<string> RemoveTypes = [
//        "GPUError",
//        "GPUExternalTexture"
//    ];

//    private ImmutableHashSet<string> RemoveMethods = [
//        "destroy",
//        "requestAdapterInfo",
//        "pushErrorScope"
//    ];

//    private ImmutableHashSet<string> MemberSupportedHandles => [
//       "GPUAdapter",
//    //"GPUBindGroup",
//    //"GPUBindGroupLayout",
//    "GPUBuffer",
//    "GPUCommandBuffer",
//    "GPUCommandEncoder",
//    "GPUComputePassEncoder",
//    //"GPUComputePipeline",
//    "GPUDevice",
//    "GPUInstance",
//    //"GPUPipelineLayout",
//    //"GPUQuerySet",
//    //"GPUQueue",
//    //"GPURenderBundle",
//    //"GPURenderBundleEncoder",
//    //"GPURenderPassEncoder",
//    //"GPURenderPipeline",
//    //"GPUSampler",
//    //"GPUShaderModule",
//    //"GPUSurface",
//    //"GPUTexture",
//    //"GPUTextureView"
//    ];

//    public DrillLang.Declaration.IDeclaration? VisitEnum(EnumDeclaration decl)
//        => decl with
//        {
//            Values = decl.Values.Select(v => v.AcceptVisitor(this)).OfType<EnumMemberDeclaration>().ToImmutableArray()
//        };

//    public DrillLang.Declaration.IDeclaration? VisitEnumValue(EnumMemberDeclaration decl)
//        => decl with
//        {
//            Name = decl.Name.Capitalize(),
//        };

//    public DrillLang.Declaration.IDeclaration? VisitHandle(HandleDeclaration decl)
//    {
//        if (RemoveTypes.Contains(decl.Name))
//        {
//            return null;
//        }
//        if (!MemberSupportedHandles.Contains(decl.Name))
//        {
//            decl = decl with { Methods = [], Properties = [] };
//        }
//        return decl with
//        {
//            Name = decl.Name.Capitalize(),
//            Methods = decl.Methods.Select(m => m.AcceptVisitor(this)).OfType<MethodDeclaration>().ToImmutableArray(),
//            Properties = decl.Properties.Select(p => p.AcceptVisitor(this)).OfType<PropertyDeclaration>().ToImmutableArray()
//        };
//    }

//    public DrillLang.Declaration.IDeclaration? VisitMethod(MethodDeclaration decl)
//    {
//        ImmutableArray<ITypeReference> referencedTypes = [decl.ReturnType, .. decl.Parameters.Select(p => p.Type)];
//        // TODO: recursive visitor
//        var shouldRemove = referencedTypes.Any(t => t switch
//        {
//            OpaqueTypeReference { Name: var name } when RemoveTypes.Contains(name) => true,
//            FutureTypeReference { Type: OpaqueTypeReference { Name: var name } } when RemoveTypes.Contains(name) => true,
//            FutureTypeReference { Type: NullableTypeReference { Type: OpaqueTypeReference { Name: var name } } } when RemoveTypes.Contains(name) => true,
//            NullableTypeReference { Type: OpaqueTypeReference { Name: var name } } when RemoveTypes.Contains(name) => true,
//            SequenceTypeReference { Type: OpaqueTypeReference { Name: var name } } when RemoveTypes.Contains(name) => true,
//            _ => false
//        });
//        if (shouldRemove)
//        {
//            return null;
//        }
//        if (RemoveMethods.Contains(decl.Name))
//        {
//            return null;
//        }
//        return decl with
//        {
//            ReturnType = decl.ReturnType.AcceptVisitor(TypePostProcessVisitor),
//            Parameters = decl.Parameters.Select(p => p.AcceptVisitor(this) as ParameterDeclaration)
//                            .OfType<ParameterDeclaration>()
//                            .ToImmutableArray()
//        };
//    }

//    public DrillLang.Declaration.IDeclaration? VisitParameter(ParameterDeclaration decl)
//        => decl with
//        {
//            Type = decl.Type.AcceptVisitor(TypePostProcessVisitor)
//        };

//    public DrillLang.Declaration.IDeclaration? VisitProperty(PropertyDeclaration decl)
//        => decl with
//        {
//            Name = decl.Name.Capitalize(),
//            Type = decl.Type.AcceptVisitor(TypePostProcessVisitor)
//        };


//    public DrillLang.Declaration.IDeclaration? VisitStruct(StructDeclaration decl)
//    {
//        throw new NotImplementedException();
//    }

//    public DrillLang.Declaration.IDeclaration? VisitModule(ModuleDeclaration module)
//    {
//        return ModuleDeclaration.Create(module.Name,
//              [.. module.AllTypeDeclarations
//                         .Select(t => t.AcceptVisitor(this))
//                         .OfType<ITypeDeclaration>()]
//        );
//    }
//}
