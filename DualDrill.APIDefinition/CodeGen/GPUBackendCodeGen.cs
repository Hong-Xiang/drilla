﻿using DualDrill.ApiGen.Mini;
using DualDrill.Common;
using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;

namespace DualDrill.ApiGen.CodeGen;

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

    public void EmitIGPUHandleDisposer(StringBuilder sb)
    {
        foreach (var h in Spec.Handles)
        {
            sb.AppendLine($"    , IGPUHandleDisposer<TBackend, {h.Name}<TBackend>>");
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

    public void EmitHandleMethods(StringBuilder sb)
    {
        foreach (var h in Spec.Handles.OrderBy(h => h.Name))
        {
            if (!h.Methods.Any())
            {
                continue;
            }

            sb.AppendLine($"#region {h.Name} methods");
            sb.AppendLine();

            foreach (var m in h.Methods)
            {
                var isAsync = m.ReturnType is FutureTypeRef;
                sb.AppendLine($"    internal {RenameHandleType(m.ReturnType).GetCSharpName()} {m.Name}(");
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
            sb.AppendLine("#endregion");
            sb.AppendLine();
        }
    }

    public void EmitAll(StringBuilder sb)
    {
        sb.AppendLine("namespace DualDrill.Graphics;");
        sb.AppendLine("public partial interface IBackend<TBackend>");
        sb.AppendLine(" : IDisposable");
        EmitIGPUHandleDisposer(sb);
        sb.AppendLine("{");
        EmitHandleMethods(sb);
        sb.AppendLine("}");
    }
}
