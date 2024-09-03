using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DualDrill.ApiGen.CodeGen;

public sealed class GPUBackendImplCodeGen(GPUApi Spec)
{
    public void EmitDisposeHandle(StringBuilder sb)
    {
        foreach (var h in Spec.Handles)
        {
            sb.AppendLine($"    void IGPUHandleDisposer<TBackend, {h.Name}<TBackend>>.DisposeHandle(GPUHandle<TBackend, {h.Name}<TBackend>> handle)");
            sb.AppendLine("    {");
            sb.AppendLine($"        TBackend.Instance.DisposeHandle(handle);");
            sb.AppendLine("    }");
        }
    }
}
