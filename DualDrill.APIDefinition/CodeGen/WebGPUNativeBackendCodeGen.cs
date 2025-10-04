using DualDrill.ApiGen.DrillLang.Declaration;
using DualDrill.ApiGen.DrillLang.Types;
using System.Text;

namespace DualDrill.ApiGen.CodeGen;

public sealed record class WebGPUNativeBackendCodeGen(
    ModuleDeclaration Module
)
{
    static readonly Type NativeMethodType = typeof(Evergine.Bindings.WebGPU.WebGPUNative);

    public void EmitHandleToNative(StringBuilder sb, HandleDeclaration handle)
    {
        sb.AppendLine($"    W{handle.Name} ToNative(GPUHandle<Backend, {handle.Name}<Backend>> instance)");
        sb.AppendLine($"        => new(instance.Pointer);");
        sb.AppendLine();
    }

    public void EmitEnumToNative(StringBuilder sb, EnumDeclaration handle)
    {
        sb.AppendLine($"    W{handle.Name} ToNative({handle.Name} value)");
        sb.AppendLine($"        => (W{handle.Name})(value);");
        sb.AppendLine();
    }

    public void EmitDisposeHandleImpl(StringBuilder sb, HandleDeclaration handle)
    {
        sb.AppendLine($"    void IGPUHandleDisposer<Backend, {handle.Name}<Backend>>.DisposeHandle(GPUHandle<Backend, {handle.Name}<Backend>> handle)");
        sb.AppendLine("    {");
        sb.AppendLine($"        wgpu{handle.Name[3..]}Release(ToNative(handle));");
        sb.AppendLine("    }");
        sb.AppendLine();
    }
    bool IsHandle(string name)
    {
        return Module.Handles.Any(h => h.Name == name);
    }

    bool ParameterTypeMatch(Type tn, ITypeReference tm)
    {
        if (tm is OpaqueTypeReference { Name: var n } && IsHandle(n))
        {
            return tn.Name == "W" + n;
        }
        return tm switch
        {
            IntegerTypeReference { BitWidth: BitWidth._32, Signed: false } => tn == typeof(uint),
            IntegerTypeReference { BitWidth: BitWidth._32, Signed: true } => tn == typeof(int),
            IntegerTypeReference { BitWidth: BitWidth._64, Signed: false } => tn == typeof(ulong),
            IntegerTypeReference { BitWidth: BitWidth._64, Signed: true } => tn == typeof(long),
            StringTypeReference => tn == typeof(char*),
            _ => false
        };
    }

    public void EmitMethod(StringBuilder sb, HandleDeclaration handle, MethodDeclaration method)
    {
        var m = NativeMethodType.GetMethod($"wgpu{handle.Name[3..]}{method.Name}", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
        var matched = true;
        if (m is not null)
        {
            var ps = m.GetParameters();
            matched = matched
                       && ps.Length == (method.Parameters.Length + 1)
                       && ps[0].ParameterType.Name == "W" + handle.Name;


            if (matched)
            {
                for (var i = 0; i < method.Parameters.Length; i++)
                {
                    matched = matched && ParameterTypeMatch(ps[i + 1].ParameterType, method.Parameters[i].Type);
                }
            }
        }
        if (!matched)
        {
            return;
        }

        var defaultDumpTypeOption = CSharpTypeNameVisitor.Default.Option;
        sb.Append("unsafe ");
        sb.Append(method.ReturnType.GetCSharpTypeName(defaultDumpTypeOption with
        {
            Usage = CSharpTypeNameVisitorOption.TypeUsage.ReturnType
        }));
        sb.Append(" IBackend<Backend>.");
        sb.Append(method.Name);
        sb.Append('(');
        var paramterOption = defaultDumpTypeOption with
        {
            Usage = CSharpTypeNameVisitorOption.TypeUsage.ParameterType
        };
        sb.Append(handle.Name);
        sb.Append("<Backend> handle");
        foreach (var p in method.Parameters)
        {
            sb.Append(", ");
            sb.Append(p.Type.GetCSharpTypeName(paramterOption));
            if (p.Type is OpaqueTypeReference { Name: var pname } && Module.Handles.Any(h => h.Name == pname))
            {
                sb.Append("<Backend>");
            }
            sb.Append(' ');
            sb.Append(p.Name);
        }

        sb.AppendLine(")");
        sb.AppendLine("{");

        var interopStringSuffix = "_native_string";
        var pinNativeStringSuffix = "_native_pined_string";
        foreach (var p in method.Parameters)
        {
            if (p.Type is StringTypeReference)
            {
                sb.AppendLine($"var {p.Name + interopStringSuffix} = InteropUtf8String.Create({p.Name});");
                sb.AppendLine($"using var {p.Name + pinNativeStringSuffix} = {p.Name + interopStringSuffix}.Pin();");

            }
        }

        if (m is not null && matched)
        {
            sb.Append(m.Name);
            sb.Append('(');
            sb.Append("ToNative(handle.Handle)");
            foreach (var p in method.Parameters)
            {
                sb.Append(", ");
                if (p.Type is StringTypeReference)
                {
                    sb.Append("(char*)");
                    sb.Append(p.Name + pinNativeStringSuffix);
                    sb.Append(".Pointer");
                }
                else if (p.Type is OpaqueTypeReference { Name: var n } && IsHandle(n))
                {
                    sb.Append($"ToNative({p.Name}.Handle)");
                }
                else
                {
                    sb.Append(p.Name);
                }
            }
            sb.AppendLine(");");
        }
        //else
        //{
        //    sb.AppendLine(m?.ToString());
        //    sb.AppendLine(matched.ToString());
        //}
        sb.AppendLine("}");
        sb.AppendLine();

    }

    static readonly HashSet<string> SkipImplHandles = [
        "GPUInstance",
        "GPUDevice",
    ];

    public void EmitAll(StringBuilder sb)
    {
        sb.AppendLine("public sealed partial class WebGPUNETBackend");
        sb.AppendLine("{");

        foreach (var h in Module.Handles.OrderBy(h => h.Name))
        {
            EmitDisposeHandleImpl(sb, h);
            EmitHandleToNative(sb, h);
            if (!SkipImplHandles.Contains(h.Name))
            {
                foreach (var m in h.Methods)
                {
                    if (m.ReturnType is FutureTypeReference)
                    {
                        continue;
                    }
                    if (m.ReturnType is SequenceTypeReference)
                    {
                        continue;
                    }
                    EmitMethod(sb, h, m);
                }
            }

        }
        foreach (var e in Module.Enums.OrderBy(e => e.Name))
        {
            EmitEnumToNative(sb, e);
        }
        sb.AppendLine("}");
    }
}
