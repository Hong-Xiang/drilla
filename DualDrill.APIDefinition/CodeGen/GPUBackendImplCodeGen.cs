using DualDrill.ApiGen.DrillLang.Declaration;
using System.Text;

namespace DualDrill.ApiGen.CodeGen;

public sealed class GPUBackendImplCodeGen(ModuleDeclaration Module)
{
    public void EmitDisposeHandle(StringBuilder sb)
    {
        foreach (var h in Module.Handles)
        {
            sb.AppendLine($"    void IGPUHandleDisposer<TBackend, {h.Name}<TBackend>>.DisposeHandle(GPUHandle<TBackend, {h.Name}<TBackend>> handle)");
            sb.AppendLine("    {");
            sb.AppendLine($"        TBackend.Instance.DisposeHandle(handle);");
            sb.AppendLine("    }");
        }
    }
}
