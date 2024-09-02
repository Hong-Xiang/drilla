using System.Text;

namespace DualDrill.ApiGen.CodeGen;

public sealed record class GPUHandlesCodeGen(GPUApi Spec)
{
    public void HandleDecl(StringBuilder sb)
    {
        foreach (var h in Spec.Handles)
        {
            if (h.Name != "GPUSurface")
            {
                sb.AppendLine($"public interface I{h.Name}");
                sb.AppendLine("{");
                sb.AppendLine("}");
                sb.AppendLine();
            }
            sb.AppendLine($"public sealed partial record class {h.Name}<TBackend>(GPUHandle<TBackend, {h.Name}<TBackend>> Handle)");
            if (h.Name == "GPUSurface")
            {
                sb.AppendLine($"    : IDisposable");
            }
            else
            {
                sb.AppendLine($"    : IDisposable, I{h.Name}");
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

}
