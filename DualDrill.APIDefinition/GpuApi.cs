using DualDrill.ApiGen.CodeGen;
using DualDrill.ApiGen.DrillLang;
using DualDrill.ApiGen.WebIDL;
using DualDrill.Common;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Reflection;
using System.Text.Json;

namespace DualDrill.ApiGen;

internal sealed class WebGPUWebIDLSpecParser(
    WebIDLSpec Spec,
    IEnumerable<string> HandleNames
)
{
    public ModuleDeclaration Parse()
    {
        return new([
            .. Spec.Declarations.OfType<InterfaceDecl>().Select(ParseHandleDecl).OfType<HandleDeclaration>(),
            ..ParseEnums()
        ]);
    }

    string? GetHandleName(string name)
    {
        return name switch
        {
            "GPU" => "GPUInstance",
            "GPUCanvasContext" => "GPUSurface",
            "GPUError" => null,
            "GPUExternalTexture" => null,
            "GPUMapModeFlags" => "GPUMapMode",
            _ when HandleNames.Contains(name) => name,
            _ => null
        };
    }

    static string SpecName(string idlName)
    {
        return idlName switch
        {
            "GPU" => "GPUInstance",
            "GPUCanvasContext" => "GPUSurface",
            "GPUMapModeFlags" => "GPUMapMode",
            "GPUColorWrite" => "GPUColorWriteMask",
            _ => idlName
        };
    }

    bool IsSupportedEnum(string enumName)
    {
        return enumName switch
        {
            "GPUAutoLayoutMode" => false,
            "GPUPipelineErrorReason" => false,
            "GPUCanvasToneMappingMode" => false,
            "GPUCanvasAlphaMode" => false,
            _ => true
        };
    }

    bool IsSupportedEnumValue(string enumName, string valueName)
    {
        return (enumName, valueName) switch
        {
            ("GPUBlendFactor", _) when valueName.Contains("src1") => false,
            ("GPUVertexFormat", "unorm10-10-10-2") => false,
            ("GPUFeatureName", "texture-compression-bc-sliced-3d") => false,
            ("GPUFeatureName", "clip-distances") => false,
            ("GPUFeatureName", "dual-source-blending") => false,
            _ => true
        };
    }

    ImmutableHashSet<EnumDeclaration> ParseEnums()
    {
        var flagEnums = Spec.Declarations.OfType<WebIDL.NamespaceDecl>()
                                     .Where(n => IsSupportedEnum(n.Name))
                                     .Select(n =>
                                     {
                                         var values = n.Members.Where(m => IsSupportedEnumValue(n.Name, m.Name))
                                                               .Select(m => new EnumValueDeclaration(m.Name, new IntegerValue(0)));
                                         return new EnumDeclaration(SpecName(n.Name), [
                                             new EnumValueDeclaration("none", new IntegerValue(0)),
                                             .. values], true);
                                     });
        var enums = Spec.Declarations.OfType<WebIDL.EnumDecl>()
                                     .Where(n => IsSupportedEnum(n.Name))
                                     .Select(n =>
                                     {
                                         var values = n.Values
                                                       .Where(v => IsSupportedEnumValue(n.Name, v.Value))
                                                       .Select(m => new EnumValueDeclaration(m.Value, null));
                                         if (n.Name == "GPUVertexStepMode")
                                         {
                                             values = values.Append(new EnumValueDeclaration("VertexBufferNotUsed", null));
                                         }

                                         return new EnumDeclaration(SpecName(n.Name), [.. values], false);
                                     });

        return [.. flagEnums, .. enums];
    }

    public HandleDeclaration? ParseHandleDecl(InterfaceDecl decl)
    {
        var name = GetHandleName(decl.Name);
        if (name is null)
        {
            return null;
        }
        var members = Spec.GetAllMembers(decl).ToImmutableArray();
        var methods = members.OfType<OperationDecl>()
                             .Select(ParseOperationDecl)
                             .OfType<MethodDeclaration>()
                             .OrderBy(m => m.Name);
        var props = members.OfType<AttributeDecl>()
                             .Select(ParseAttribute)
                             .OfType<PropertyDeclaration>()
                             .OrderBy(m => m.Name);
        return new(name, [.. methods], [.. props]);
    }

    public MethodDeclaration? ParseOperationDecl(OperationDecl decl)
    {
        var parameters = decl.Arguments.Select(ParseParameter);
        return new MethodDeclaration(
            decl.Name,
            [.. parameters],
            ParseWebIDLType(decl.IdlType));
    }

    public ParameterDeclaration ParseParameter(ArgumentDecl decl)
    {
        return new ParameterDeclaration(decl.Name, ParseWebIDLType(decl.IdlType), null);
    }

    public PropertyDeclaration? ParseAttribute(AttributeDecl decl)
    {
        return new PropertyDeclaration(decl.Name, ParseWebIDLType(decl.IdlType), false);
    }

    ITypeReference ParseWebIDLType(JsonElement doc)
    {
        if (doc.ValueKind == JsonValueKind.String)
        {
            return ParseIdlTypeName(doc.GetString()!);
        }
        if (doc.ValueKind == JsonValueKind.Array && doc.GetArrayLength() == 1)
        {
            return ParseWebIDLType(doc[0]);
        }
        if (doc.ValueKind == JsonValueKind.Object)
        {
            var generic = doc.GetProperty("generic").Deserialize<string?>();
            var isNullable = doc.GetProperty("nullable").Deserialize<bool?>() ?? false;
            var isUnion = doc.GetProperty("union").Deserialize<bool?>() ?? false;
            var isGeneric = !string.IsNullOrEmpty(generic);
            var t = ParseWebIDLType(doc.GetProperty("idlType"));
            if (!isGeneric && !isNullable && !isUnion)
            {
                return t;
            }
            if (!isGeneric && isNullable && !isUnion)
            {
                return new NullableTypeRef(t);
            }
            if (isGeneric && !isNullable && !isUnion)
            {
                if (generic == "Promise")
                {
                    return new FutureTypeRef(t);
                }
                if (generic == "sequence")
                {
                    return new SequenceTypeRef(t);
                }
            }
        }

        return new UnknownTypeRef(doc);
    }
    ITypeReference ParseIdlTypeName(string name)
    {
        return name switch
        {
            "undefined" => new VoidTypeRef(),
            "boolean" => new BoolTypeReference(),
            "USVString" => new StringTypeReference(),
            "GPUSize64" => new IntegerTypeReference(BitWidth.N64, false),
            "GPUSize32" => new IntegerTypeReference(BitWidth.N32, false),
            "GPUIndex16" => new IntegerTypeReference(BitWidth.N16, true),
            "GPUIndex32" => new IntegerTypeReference(BitWidth.N32, true),
            "GPUIndex64" => new IntegerTypeReference(BitWidth.N64, true),
            "GPUBufferDynamicOffset" => new IntegerTypeReference(BitWidth.N32, false),
            "ArrayBuffer" => new SequenceTypeRef(new IntegerTypeReference(BitWidth.N8, false)),
            "Uint32Array" => new SequenceTypeRef(new IntegerTypeReference(BitWidth.N32, false)),
            _ => new PlainTypeRef(GetHandleName(name) is string value ? value : name),
        };
    }
}

public sealed record class GPUApi(
    ModuleDeclaration Module
)
{
    public ImmutableArray<HandleDeclaration> Handles =>
        [.. Module.TypeDeclarations.OfType<HandleDeclaration>().OrderBy(h => h.Name)];

    public void Validate()
    {
        var handleNames = ParseEvergineGPUHandleTypeNames();
        foreach (var handle in Handles)
        {
            Debug.Assert(handleNames.Contains(handle.Name), $"Parsed handle {handle.Name} not existed in evergine");
        }
        Debug.Assert(Handles.Length == 22, $"GPU API should provide 22 handles, got {Handles.Length}");
    }

    public static ImmutableArray<string> ParseEvergineGPUHandleTypeNames()
    {
        var assembly = typeof(Evergine.Bindings.WebGPU.WebGPUNative).Assembly;
        var types = assembly.GetTypes();
        var handles = types.Where(t => t.IsValueType
                                         && !t.IsEnum
                                         && t.Name.StartsWith("WGPU")
                                         && HasHandleField(t))
                           .Select(t => t.Name[1..]);
        return [.. handles];

        static bool HasHandleField(Type t)
        {
            return t.GetMembers()
                    .OfType<FieldInfo>()
                    .Any(f => f.Name == "Handle" && f.FieldType == typeof(nint));
        }
    }

    public static ImmutableArray<string> ParseEverginGPUEnumTypeNames()
    {
        var assembly = typeof(Evergine.Bindings.WebGPU.WebGPUNative).Assembly;
        var types = assembly.GetTypes();
        var handles = types.Where(t => t.IsValueType
                                         && t.IsEnum)
                           .Select(t => t.Name[1..]);
        return [.. handles];
    }

    private static WebIDLSpec FixWebIDLSpec(WebIDLSpec spec)
    {
        // merge GPUDevice members
        var gpuDeviceDecls = spec.Declarations.OfType<InterfaceDecl>().Where(d => d.Name == "GPUDevice");
        WebIDL.IDeclaration mergedGPUDeviceDecl = new InterfaceDecl("GPUDevice", [.. gpuDeviceDecls.SelectMany(d => d.Members)]);
        var otherDecls = spec.Declarations.Where(decl => !(decl is InterfaceDecl d && d.Name == "GPUDevice"));
        return new WebIDLSpec([.. otherDecls, mergedGPUDeviceDecl]);
    }

    public static GPUApi ParseWebIDLSpec(WebIDL.WebIDLSpec idlSpec)
    {
        var fixedIdlSpec = FixWebIDLSpec(idlSpec);
        var handleNames = ParseEvergineGPUHandleTypeNames();
        var parser = new WebGPUWebIDLSpecParser(fixedIdlSpec, handleNames);
        var result = new GPUApi(parser.Parse());
        result.Validate();
        return result;
    }

    public GPUApi ProcessForCodeGen(bool useGenericBackend)
    {
        var visitor = new GPUApiPreCodeGenVisitor([
            ..Module.TypeDeclarations
                    .OfType<HandleDeclaration>()
                    .Select(h => h.Name)
        ], useGenericBackend);
        var result = this with
        {
            Module = (ModuleDeclaration)Module.AcceptVisitor(visitor)
        };
        return result;
    }
}

internal sealed class ParseWebIDLResultToGPUApiTypeVisitor : ITypeReferenceVisitor<ITypeReference>
{
    public ITypeReference VisitBool(BoolTypeReference type) => type;

    public ITypeReference VisitFloat(FloatTypeReference type) => type;

    public ITypeReference VisitFuture(FutureTypeRef type) => type with
    {
        Type = type.Type.AcceptVisitor(this)
    };


    public ITypeReference VisitGeneric(GenericTypeRef type) => type;


    public ITypeReference VisitInteger(IntegerTypeReference type) => type;


    public ITypeReference VisitMatrix(MatrixTypeReference type) => type;


    public ITypeReference VisitNullable(NullableTypeRef type) => type;


    public ITypeReference VisitPlain(PlainTypeRef type)
    {
        return type.Name switch
        {
            "undefined" => new VoidTypeRef(),
            "boolean" => new BoolTypeReference(),
            "USVString" => new StringTypeReference(),
            "GPUSize64" => new IntegerTypeReference(BitWidth.N64, false),
            "GPUSize32" => new IntegerTypeReference(BitWidth.N32, false),
            "GPUIndex16" => new IntegerTypeReference(BitWidth.N16, true),
            "GPUIndex32" => new IntegerTypeReference(BitWidth.N32, true),
            "GPUIndex64" => new IntegerTypeReference(BitWidth.N64, true),
            "GPUBufferDynamicOffset" => new IntegerTypeReference(BitWidth.N32, false),
            "ArrayBuffer" => new SequenceTypeRef(new IntegerTypeReference(BitWidth.N8, false)),
            "Uint32Array" => new SequenceTypeRef(new IntegerTypeReference(BitWidth.N32, false)),
            _ => type
        };
    }

    public ITypeReference VisitSequence(SequenceTypeRef type)
        => type with
        {
            Type = type.Type.AcceptVisitor(this)
        };

    public ITypeReference VisitString(StringTypeReference type)
        => type;

    public ITypeReference VisitUnknown(UnknownTypeRef type)
        => type;

    public ITypeReference VisitVector(VectorTypeReference type) => type;


    public ITypeReference VisitVoid(VoidTypeRef type) => type;

}

internal sealed class PostParseProcessGPUApiDeclarationsVisitor : IDeclarationVisitor<DrillLang.IDeclaration?>
{
    ParseWebIDLResultToGPUApiTypeVisitor TypePostProcessVisitor { get; } = new();

    private ImmutableHashSet<string> RemoveTypes = [
        "GPUError",
        "GPUExternalTexture"
    ];

    private ImmutableHashSet<string> RemoveMethods = [
        "destroy",
        "requestAdapterInfo",
        "pushErrorScope"
    ];

    private ImmutableHashSet<string> MemberSupportedHandles => [
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

    public DrillLang.IDeclaration? VisitEnum(EnumDeclaration decl)
        => decl with
        {
            Values = decl.Values.Select(v => v.AcceptVisitor(this)).OfType<EnumValueDeclaration>().ToImmutableArray()
        };

    public DrillLang.IDeclaration? VisitEnumValue(EnumValueDeclaration decl)
        => decl with
        {
            Name = decl.Name.Capitalize(),
        };

    public DrillLang.IDeclaration? VisitHandle(HandleDeclaration decl)
    {
        if (RemoveTypes.Contains(decl.Name))
        {
            return null;
        }
        if (!MemberSupportedHandles.Contains(decl.Name))
        {
            decl = decl with { Methods = [], Properties = [] };
        }
        return decl with
        {
            Name = decl.Name.Capitalize(),
            Methods = decl.Methods.Select(m => m.AcceptVisitor(this)).OfType<MethodDeclaration>().ToImmutableArray(),
            Properties = decl.Properties.Select(p => p.AcceptVisitor(this)).OfType<PropertyDeclaration>().ToImmutableArray()
        };
    }

    public DrillLang.IDeclaration? VisitMethod(MethodDeclaration decl)
    {
        ImmutableArray<ITypeReference> referencedTypes = [decl.ReturnType, .. decl.Parameters.Select(p => p.Type)];
        // TODO: recursive visitor
        var shouldRemove = referencedTypes.Any(t => t switch
        {
            PlainTypeRef { Name: var name } when RemoveTypes.Contains(name) => true,
            FutureTypeRef { Type: PlainTypeRef { Name: var name } } when RemoveTypes.Contains(name) => true,
            FutureTypeRef { Type: NullableTypeRef { Type: PlainTypeRef { Name: var name } } } when RemoveTypes.Contains(name) => true,
            NullableTypeRef { Type: PlainTypeRef { Name: var name } } when RemoveTypes.Contains(name) => true,
            SequenceTypeRef { Type: PlainTypeRef { Name: var name } } when RemoveTypes.Contains(name) => true,
            _ => false
        });
        if (shouldRemove)
        {
            return null;
        }
        if (RemoveMethods.Contains(decl.Name))
        {
            return null;
        }
        return decl with
        {
            ReturnType = decl.ReturnType.AcceptVisitor(TypePostProcessVisitor),
            Parameters = (decl.Parameters.Select(p => p.AcceptVisitor(this) as ParameterDeclaration))
                            .OfType<ParameterDeclaration>()
                            .ToImmutableArray()
        };
    }

    public DrillLang.IDeclaration? VisitParameter(ParameterDeclaration decl)
        => decl with
        {
            Type = decl.Type.AcceptVisitor(TypePostProcessVisitor)
        };

    public DrillLang.IDeclaration? VisitProperty(PropertyDeclaration decl)
        => decl with
        {
            Name = decl.Name.Capitalize(),
            Type = decl.Type.AcceptVisitor(TypePostProcessVisitor)
        };


    public DrillLang.IDeclaration? VisitStruct(StructDeclaration decl)
    {
        throw new NotImplementedException();
    }

    public DrillLang.IDeclaration? VisitModule(ModuleDeclaration module)
    {
        return module with
        {
            TypeDeclarations = [.. module.TypeDeclarations
                                        .Select(t => t.AcceptVisitor(this))
                                        .OfType<ITypeDeclaration>()]
        };
    }
}
