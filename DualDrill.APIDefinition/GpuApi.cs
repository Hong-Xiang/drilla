using DualDrill.ApiGen.CodeGen;
using DualDrill.ApiGen.Mini;
using DualDrill.ApiGen.WebIDL;
using DualDrill.Common;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Reflection;
using System.Text.Json;

namespace DualDrill.ApiGen;

public sealed record class GPUApi(
    ImmutableArray<HandleDeclaration> Handles
)
{
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

    public static GPUApi ParseIDLSpec(JsonDocument doc)
    {
        var idlSpec = WebIDLSpec.Parse(doc);
        var handleNames = ParseEvergineGPUHandleTypeNames().ToImmutableHashSet();
        var postProcess = new PostParseProcessGPUApiDeclarationsVisitor();
        var handles = handleNames.Select(n => ParseHandle(n, idlSpec))
                                 .Select(h => h.AcceptVisitor(postProcess) as HandleDeclaration)
                                 .OfType<HandleDeclaration>()
                                 .OrderBy(t => t.Name);
        var result = new GPUApi([.. handles]);
        result.Validate();
        return result;

        static string SpecName(string idlName)
        {
            return idlName switch
            {
                "GPU" => "GPUInstance",
                "GPUCanvasContext" => "GPUSurface",
                _ => idlName
            };
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

    public GPUApi ProcessForCodeGen()
    {
        var visitor = new GPUApiPreCodeGenVisitor();
        return this with
        {
            Handles = Handles.Select(h => (HandleDeclaration)h.AcceptVisitor(visitor)).ToImmutableArray()
        };
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

internal sealed class PostParseProcessGPUApiDeclarationsVisitor : IDeclarationVisitor<Mini.IDeclaration?>
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
    //"GPUCommandBuffer",
    "GPUCommandEncoder",
    //"GPUComputePassEncoder",
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

    public Mini.IDeclaration? VisitEnumDeclaration(EnumDeclaration decl)
        => decl with
        {
            Values = decl.Values.Select(v => v.AcceptVisitor(this)).OfType<EnumValueDeclaration>().ToImmutableArray()
        };

    public Mini.IDeclaration? VisitEnumValueDeclaration(EnumValueDeclaration decl)
        => decl with
        {
            Name = decl.Name.Capitalize(),
        };

    public Mini.IDeclaration? VisitHandleDeclaration(HandleDeclaration decl)
    {
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

    public Mini.IDeclaration? VisitMethodDeclaration(MethodDeclaration decl)
    {
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

    public Mini.IDeclaration? VisitParameterDeclaration(ParameterDeclaration decl)
        => decl with
        {
            Type = decl.Type.AcceptVisitor(TypePostProcessVisitor)
        };

    public Mini.IDeclaration? VisitPropertyDeclaration(PropertyDeclaration decl)
        => decl with
        {
            Name = decl.Name.Capitalize(),
            Type = decl.Type.AcceptVisitor(TypePostProcessVisitor)
        };


    public Mini.IDeclaration? VisitStructDeclaration(StructDeclaration decl)
    {
        throw new NotImplementedException();
    }

    public Mini.IDeclaration? VisitTypeSystem(TypeSystem typeSystem)
    {
        var processed = typeSystem.TypeDeclarations
            .Select(kv => KeyValuePair.Create(kv.Key, kv.Value.AcceptVisitor(this)))
            .Where(kv => kv.Value is ITypeDeclaration)
            .Select(kv => KeyValuePair.Create(kv.Key, (ITypeDeclaration)kv.Value!));
        return typeSystem with
        {
            TypeDeclarations = processed.ToImmutableDictionary()
        };
    }
}
