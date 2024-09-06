using DualDrill.ApiGen.DrillLang.Declaration;
using DualDrill.ApiGen.DrillLang.Types;
using System.Collections.Immutable;
using System.Text;

namespace DualDrill.ApiGen.CodeGen;

public sealed record class GPUHandlesCodeGen(ModuleDeclaration Module)
{
    ImmutableHashSet<string> HandleNames = [.. Module.Handles.Select(h => h.Name)];
    public void EmitHandleDeclaration(StringBuilder sb, HandleDeclaration decl)
    {
        sb.AppendLine($"public partial interface I{decl.Name}");
        sb.AppendLine("{");
        sb.AppendLine("}");
        sb.AppendLine();
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

    public void EmitMethodDefinitions(StringBuilder sb, HandleDeclaration decl)
    {
        foreach (var m in decl.Methods)
        {
            var isFirstArgument = true;
            sb.AppendLine($"public {m.ReturnType.GetCSharpTypeName()} {m.Name} (");
            foreach (var p in m.Parameters)
            {
                sb.Append(isFirstArgument ? " " : ",");
                isFirstArgument = false;

                sb.Append(p.Type.GetCSharpTypeName());
                sb.Append(' ');
                sb.AppendLine(p.Name);
            }
            sb.AppendLine(")");
            sb.AppendLine("{");
            if (!(m.ReturnType is VoidTypeReference))
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
