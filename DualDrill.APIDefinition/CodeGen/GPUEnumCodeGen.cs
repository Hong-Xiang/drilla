using DualDrill.ApiGen.DrillLang.Declaration;
using System.Text;

namespace DualDrill.ApiGen.CodeGen;

public sealed class GPUEnumCodeGen
{
    public void EmitEnumDecl(StringBuilder sb, EnumDeclaration decl)
    {
        if (decl.IsFlag)
        {
            sb.AppendLine("[Flags]");
        }
        sb.AppendLine($"public enum {decl.Name} : int");
        sb.AppendLine("{");

        foreach (var m in decl.Values.OrderBy(v => v.Value?.Value))
        {
            sb.Append(m.Name);
            if (m.Value.HasValue)
            {
                sb.Append(" = ");
                if (decl.IsFlag)
                {
                    sb.Append($"0x{m.Value.Value.Value:X}");
                }
                else
                {
                    sb.Append(m.Value.Value.Value);
                }
            }

            sb.AppendLine(",");
        }
        sb.AppendLine("}");
        sb.AppendLine();
    }
}
