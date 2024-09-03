using DualDrill.ApiGen.Mini;
using System.Text;

namespace DualDrill.ApiGen.CodeGen;

public sealed record class GPUHandlesCodeGen(GPUApi Spec)
{
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
        sb.AppendLine("    public void Dispose()");
        sb.AppendLine("    {");
        sb.AppendLine("        TBackend.Instance.DisposeHandle(Handle);");
        sb.AppendLine("    }");
        sb.AppendLine("}");
    }
}
