using DualDrill.ApiGen.Mini;
using DualDrill.Common;
using System.Collections.Immutable;
using System.Text;

namespace DualDrill.ApiGen.CodeGen;

sealed record class GPUApiPreCodeGenVisitor() : IDeclarationVisitor<IDeclaration>
{
    public IDeclaration VisitEnumDeclaration(EnumDeclaration decl) => decl;

    public IDeclaration VisitEnumValueDeclaration(EnumValueDeclaration decl) => decl;

    public IDeclaration VisitHandleDeclaration(HandleDeclaration decl)
        => decl with
        {
            Methods = decl.Methods.Select(m => (MethodDeclaration)m.AcceptVisitor(this)).ToImmutableArray(),
            Properties = decl.Methods.Select(m => (PropertyDeclaration)m.AcceptVisitor(this)).ToImmutableArray(),
        };

    public IDeclaration VisitMethodDeclaration(MethodDeclaration decl)
    {
        var ps = decl.Parameters.Select(p => p.AcceptVisitor(this)).OfType<ParameterDeclaration>();
        if (decl.IsAsync)
        {
            ps = ps.Concat([new ParameterDeclaration("cancellation", new PlainTypeRef("CancellationToken"), null)]);
        }
        return decl with
        {
            Name = decl.IsAsync ? decl.Name + "Async" : decl.Name,
            Parameters = ps.ToImmutableArray(),
        };
    }

    public IDeclaration VisitParameterDeclaration(ParameterDeclaration decl) => decl;

    public IDeclaration VisitPropertyDeclaration(PropertyDeclaration decl) => decl;

    public IDeclaration VisitStructDeclaration(StructDeclaration decl)
        => decl with
        {
            Properties = decl.Properties.Select(p => (PropertyDeclaration)p.AcceptVisitor(this)).ToImmutableArray()
        };

    public IDeclaration VisitTypeSystem(TypeSystem typeSystem)
        => typeSystem with
        {
            TypeDeclarations = typeSystem.TypeDeclarations
                                    .Select(d => KeyValuePair.Create(d.Key, (ITypeDeclaration)d.Value.AcceptVisitor(this)))
                                    .ToImmutableDictionary()
        };
}

public sealed record class GPUBackendCodeGen(GPUApi Spec)
{

    private readonly ImmutableHashSet<string> HandleNames = Spec.Handles.Select(h => h.Name).ToImmutableHashSet();

    private string[] MemberSupportedHandles => [
    "GPUAdapter",
    //"GPUBindGroup",
    //"GPUBindGroupLayout",
    //"GPUBuffer",
    //"GPUCommandBuffer",
    //"GPUCommandEncoder",
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
    public void GPUHandleDisposer(StringBuilder sb)
    {
        foreach (var h in Spec.Handles)
        {
            sb.AppendLine($"    , IGPUHandleDisposer<TBackend, {h.Name}<TBackend>>");
        }
    }

    public void DisposeImpl(StringBuilder sb)
    {
        foreach (var h in Spec.Handles)
        {
            sb.AppendLine($"    void IGPUHandleDisposer<TBackend, {h.Name}<TBackend>>.DisposeHandle(GPUHandle<TBackend, {h.Name}<TBackend>> handle)");
            sb.AppendLine("    {");
            sb.AppendLine($"        TBackend.Instance.DisposeHandle(handle);");
            sb.AppendLine("    }");
        }
    }

    ITypeReference RenameHandleType(ITypeReference t)
    {
        return t switch
        {
            PlainTypeRef { Name: var n } p when HandleNames.Contains(n) => p with { Name = $"{n}<TBackend>" },
            FutureTypeRef f => f with { Type = RenameHandleType(f.Type) },
            NullableTypeRef n => n with { Type = RenameHandleType(n.Type) },
            _ => t
        };
    }

    bool References(ITypeReference t, string name)
    {
        return t switch
        {
            PlainTypeRef { Name: var n } => n == name,
            FutureTypeRef f => References(f.Type, name),
            NullableTypeRef n => References(n.Type, name),
            _ => false
        };
    }

    public void GenerateHandleMethods(StringBuilder sb)
    {
        foreach (var h in Spec.Handles)
        {
            if (!MemberSupportedHandles.Contains(h.Name))
            {
                continue;
            }
            foreach (var m in h.Methods)
            {
                if (!ShouldGenerateMethod(m))
                {
                    continue;
                }
                var isAsync = m.ReturnType is FutureTypeRef;
                var methodName = isAsync ? $"{m.Name.Capitalize()}Async" : m.Name.Capitalize();
                sb.AppendLine($"    internal {RenameHandleType(m.ReturnType).GetCSharpName()} {methodName}(");
                sb.Append($"        {h.Name}<TBackend> handle");

                for (var i = 0; i < m.Parameters.Length; i++)
                {
                    var p = m.Parameters[i];
                    sb.AppendLine(",");
                    sb.Append($"        {RenameHandleType(p.Type).GetCSharpName()} {p.Name}");
                }
                if (isAsync)
                {
                    sb.AppendLine(",");
                    sb.AppendLine("        CancellationToken cancellationToken);");
                }
                else
                {
                    sb.AppendLine(");");
                }
                sb.AppendLine();
            }
        }
    }

    bool ShouldGenerateMethod(MethodDeclaration m)
    {
        ImmutableHashSet<string> skipNames = [
            "destroy",
            "requestAdapterInfo",
            "pushErrorScope"
        ];

        ImmutableHashSet<string> skipTypes = [
            "GPUError",
            "GPUExternalTexture"
        ];

        if (skipNames.Contains(m.Name))
        {
            return false;
        }
        if (skipTypes.Any(s => References(m.ReturnType, s)))
        {
            return false;
        }
        return true;
    }

    public void GenerateAll(StringBuilder sb)
    {
        sb.AppendLine("namespace DualDrill.Graphics;");
        sb.AppendLine("public partial interface IBackend<TBackend>");
        sb.AppendLine(" : IDisposable");
        GPUHandleDisposer(sb);
        sb.AppendLine("{");
        GenerateHandleMethods(sb);
        sb.AppendLine("}");
    }
}
