using DualDrill.ApiGen.CodeGen;
using DualDrill.ApiGen.DrillLang;
using DualDrill.ApiGen.WebIDL;
using DualDrill.Common;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Reflection;
using System.Text.Json;

namespace DualDrill.ApiGen;

public sealed record class GPUApi(
    ModuleDeclaration Module
)
{
    public ImmutableArray<HandleDeclaration> Handles =>
        [.. Module.TypeDeclarations.OfType<HandleDeclaration>().OrderBy(h => h.Name)];

    public void Validate()
    {
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
        return handles.ToImmutableArray();

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


    public static GPUApi ParseIDLSpec(JsonDocument doc)
    {
        var idlSpec = WebIDLSpec.Parse(doc);
        var handleNames = ParseEvergineGPUHandleTypeNames().ToImmutableHashSet();
        var postProcess = new PostParseProcessGPUApiDeclarationsVisitor();
        var handles = handleNames.Select(n => ParseHandle(n, idlSpec))
                                 .Select(h => h.AcceptVisitor(postProcess) as HandleDeclaration)
                                 .OfType<HandleDeclaration>()
                                 .OrderBy(t => t.Name);
        var enums = ParseEnums(idlSpec);
        var result = new GPUApi(new ModuleDeclaration([.. handles, .. enums]));
        result.Validate();
        return result;

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

        static ImmutableHashSet<EnumDeclaration> ParseEnums(WebIDLSpec spec)
        {
            var flagEnums = spec.Declarations.OfType<WebIDL.Namespace>()
                                         .Select(n => new EnumDeclaration(SpecName(n.Name), [], true));
            var enums = spec.Declarations.OfType<WebIDL.WebIDLEnum>()
                                         .Select(n => new EnumDeclaration(SpecName(n.Name), [], false));

            return [.. flagEnums, .. enums];
        }

        static HandleDeclaration ParseHandle(string name, WebIDLSpec spec)
        {
            var interfaces = spec.Declarations
                           .OfType<Interface>();
            var idlName = interfaces.First(t => SpecName(t.Name) == name).Name;

            var includedNames = GetAllIncludes(idlName, [.. spec.Declarations.OfType<IncludeDecl>()]).ToImmutableHashSet();

            var members = from t in spec.Declarations
                          from m in t switch
                          {
                              Interface t_ when includedNames.Contains(t_.Name) => t_.Members,
                              InterfaceMixin m when includedNames.Contains(m.Name) => m.Members,
                              _ => []
                          }
                          select m;
            var methods = members.OfType<Operation>()
                                 .Select(m =>
                                 {
                                     var parameters = m.Arguments.Select(p =>
                                            new ParameterDeclaration(p.Name, WebIDLSpec.ParseWebIDLTypeRef(p.IdlType), null));
                                     return new MethodDeclaration(
                                         m.Name,
                                         [.. parameters],
                                         WebIDLSpec.ParseWebIDLTypeRef(m.IdlType));
                                 })
                                 .ToImmutableArray();
            var properties = members.OfType<WebIDLAttribute>()
                                    .Select(p =>
                                        new PropertyDeclaration(p.Name,
                                                              WebIDLSpec.ParseWebIDLTypeRef(p.IdlType)))
                                    .ToImmutableArray();
            return new HandleDeclaration(name, methods, properties);

            static IEnumerable<string> GetAllIncludes(string name, ImmutableArray<IncludeDecl> includes)
            {
                var targets = includes.Where(t => t.Target == name);
                return targets.Select(t => t.Includes)
                              .Concat(targets.SelectMany(t => GetAllIncludes(t.Includes, includes)))
                              .Concat([name]);
            }
        }
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
