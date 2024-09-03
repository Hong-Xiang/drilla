using DualDrill.ApiGen.DrillLang;
using System.Collections.Immutable;
using System.Text;

namespace DualDrill.ApiGen.CodeGen;

public sealed record class GPUHandlesCodeGen(GPUApi Spec)
{
    ImmutableHashSet<string> HandleNames = [.. Spec.Handles.Select(h => h.Name)];
    public void EmitHandleDeclaration(StringBuilder sb, HandleDeclaration decl)
    {
        if (decl.Name != "GPUSurface")
        {
            sb.AppendLine($"public interface I{decl.Name}");
            sb.AppendLine("{");
            sb.AppendLine("}");
            sb.AppendLine();
        }
        sb.AppendLine($"public sealed partial record class {decl.Name}<TBackend>(GPUHandle<TBackend, {decl.Name}<TBackend>> Handle)");
        if (decl.Name == "GPUSurface")
        {
            sb.AppendLine($"    : IDisposable");
        }
        else
        {
            sb.AppendLine($"    : IDisposable, I{decl.Name}");
        }
        sb.AppendLine("    where TBackend : IBackend<TBackend>");
        sb.AppendLine("{");
        sb.AppendLine();
        EmitMethodDefinitions(sb, decl);
        sb.AppendLine("    public void Dispose()");
        sb.AppendLine("    {");
        sb.AppendLine("        TBackend.Instance.DisposeHandle(Handle);");
        sb.AppendLine("    }");
        sb.AppendLine("}");
    }

    readonly string Indent = new string(' ', 4);

    public void EmitMethodDefinitions(StringBuilder sb, HandleDeclaration decl)
    {
        foreach (var m in decl.Methods)
        {
            var isFirstArgument = true;
            sb.AppendLine($"public {m.ReturnType.GetCSharpName()} {m.Name} (");
            foreach (var p in m.Parameters)
            {
                sb.Append(isFirstArgument ? " " : ",");
                isFirstArgument = false;

                sb.Append(p.Type.GetCSharpName());
                sb.Append(" ");
                sb.AppendLine(p.Name);
            }
            sb.AppendLine(")");
            sb.AppendLine("{");
            if (!(m.ReturnType is VoidTypeRef))
            {
                sb.Append("  return ");
            }
            var thisValue = "this";
            sb.AppendLine($"TBackend.Instance.{m.Name}({string.Join(',', [thisValue, .. m.Parameters.Select(p => p.Name)])});");
            sb.AppendLine("}");
            sb.AppendLine();
        }
    }
}
