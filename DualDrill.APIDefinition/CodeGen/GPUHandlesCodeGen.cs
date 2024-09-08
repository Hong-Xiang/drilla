using DualDrill.ApiGen.DrillLang.Declaration;
using DualDrill.ApiGen.DrillLang.Types;
using System.Collections.Immutable;
using System.Text;

namespace DualDrill.ApiGen.CodeGen;

public sealed record class GPUHandlesCodeGen(ModuleDeclaration Module)
{
    INameTransform HandleRename = new BackendHandleNameTransform(Module);

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

    bool IsHandle(string name)
    {
        return Module.Handles.Any(h => h.Name == name);
    }

    void EmitTypeReference(StringBuilder sb, ITypeReference type)
    {
        sb.Append(type.GetCSharpTypeName());
        if (type is OpaqueTypeReference { Name: var name } && IsHandle(name))
        {
            sb.Append("<TBackend>");
        }
    }

    public void EmitMethodDefinitions(StringBuilder sb, HandleDeclaration decl)
    {
        foreach (var m in decl.Methods)
        {
            var isFirstArgument = true;
            sb.Append($"public ");
            sb.Append(m.ReturnType.GetCSharpTypeName(
                                option: CSharpTypeNameVisitor.Default.Option with
                                {
                                    Usage = CSharpTypeNameVisitor.VisitorOption.TypeUsage.ParameterType
                                },
                                transform: HandleRename
                            ));
            sb.Append(' ');
            sb.Append(m.Name);
            sb.AppendLine(" (");
            foreach (var p in m.Parameters)
            {
                sb.Append(isFirstArgument ? " " : ",");
                isFirstArgument = false;

                sb.Append(p.Type.GetCSharpTypeName(
                    option: CSharpTypeNameVisitor.Default.Option with
                    {
                        Usage = CSharpTypeNameVisitor.VisitorOption.TypeUsage.ParameterType
                    },
                    transform: HandleRename
                ));
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
