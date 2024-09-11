using DualDrill.ApiGen.DrillLang.Declaration;
using DualDrill.ApiGen.DrillLang.Types;
using System.Collections.Immutable;
using System.Text;

namespace DualDrill.ApiGen.CodeGen;

public sealed record class GPUHandlesCodeGen(ModuleDeclaration Module)
{
    INameTransform HandleRename = new InterfaceHandleNameTransform(Module);

    ImmutableHashSet<string> HandleNames = [.. Module.Handles.Select(h => h.Name)];

    public void Emit(StringBuilder sb, HandleDeclaration decl)
    {
        EmitHandleInterfaceDeclaration(sb, decl);
        EmitHandleDedinition(sb, decl);
    }

    public void EmitHandleInterfaceDeclaration(StringBuilder sb, HandleDeclaration decl)
    {
        sb.AppendLine($"public partial interface I{decl.Name} : IDisposable");
        sb.AppendLine("{");
        foreach (var m in decl.Methods)
        {
            EmitMethodDeclaration(sb, decl, m);
            sb.AppendLine(";");
        }
        sb.AppendLine("}");
        sb.AppendLine();
    }

    void EmitMethodDeclaration(StringBuilder sb, HandleDeclaration handle, MethodDeclaration method)
    {

        var isFirstArgument = true;
        sb.Append($"public ");
        sb.Append(method.ReturnType.GetCSharpTypeName(
                            option: CSharpTypeNameVisitorOption.ParameterType,
                            transform: HandleRename
                        ));
        sb.Append(' ');
        sb.Append(method.Name);
        sb.AppendLine(" (");
        foreach (var p in method.Parameters)
        {
            sb.Append(isFirstArgument ? " " : ",");
            isFirstArgument = false;

            sb.Append(p.Type.GetCSharpTypeName(
                option: CSharpTypeNameVisitorOption.ParameterType,
                transform: HandleRename
            ));
            sb.Append(' ');
            sb.AppendLine(p.Name);
        }
        sb.Append(")");
    }

    public void EmitHandleDedinition(StringBuilder sb, HandleDeclaration decl)
    {
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
            if (m.ReturnType is FutureTypeReference)
            {
                continue;
            }
            EmitMethodDeclaration(sb, decl, m);
            sb.AppendLine();
            sb.AppendLine("{");
            if (!(m.ReturnType is VoidTypeReference))
            {
                sb.Append("  return ");
            }
            sb.Append($"TBackend.Instance.{m.Name}(this");
            foreach (var p in m.Parameters)
            {
                sb.Append(", ");
                sb.Append(p.Name);
            }
            sb.AppendLine(");");
            sb.AppendLine("}");
            sb.AppendLine();
        }
    }
}
