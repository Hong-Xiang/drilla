using System.CodeDom.Compiler;
using DualDrill.CLSL.Language.Declaration;
using DualDrill.CLSL.Language.FunctionBody;
using DualDrill.CLSL.Language.ShaderAttribute;
using DualDrill.CLSL.Language.ShaderAttribute.Metadata;
using DualDrill.CLSL.Language.Types;
using DualDrill.Common;
using DualDrill.Common.CodeTextWriter;

namespace DualDrill.CLSL.Backend;

public sealed class ModuleToCodeVisitor<TBody>(
    IndentedTextWriter Writer,
    IShaderModuleDeclaration Module,
    Func<TBody, ValueTask> OnBody)
    : IDeclarationVisitor<TBody, ValueTask>
    where TBody : IFunctionBody
{
    public async ValueTask VisitFunction(FunctionDeclaration decl)
    {
        await WriteAttributesAsync(decl.Attributes, true);
        Writer.Write("fn ");
        Writer.Write(decl.Name);
        Writer.Write("(");
        foreach (var p in decl.Parameters) await p.AcceptVisitor(this);

        Writer.Write(")");
        Writer.Write(" -> ");
        await WriteAttributesAsync(decl.Return.Attributes);
        if (decl.Return.Type is not null) await OnTypeReference(decl.Return.Type);

        Writer.WriteLine();
        using (Writer.IndentedScope())
        {
            if (Module is ShaderModuleDeclaration<TBody> module
                && module.TryGetBody(decl, out var b))
                await OnBody(b);
            else
                Writer.WriteLine($"...{Module.GetType().CSharpFullName()}...");
        }

        Writer.WriteLine();
        Writer.WriteLine();
    }

    public async ValueTask VisitParameter(ParameterDeclaration decl)
    {
        await WriteAttributesAsync(decl.Attributes);
        Writer.Write(decl.Name);
        Writer.Write(": ");
        await OnTypeReference(decl.Type);
        Writer.Write(", ");
    }

    public ValueTask VisitValue(ValueDeclaration decl) => throw new NotImplementedException();

    public async ValueTask VisitVariable(VariableDeclaration decl)
    {
        foreach (var a in decl.Attributes)
            switch (a)
            {
                case GroupAttribute g:
                    Writer.Write("@group(");
                    Writer.Write(g.Binding);
                    Writer.Write(") ");
                    break;
                case BindingAttribute b:
                    Writer.Write("@binding(");
                    Writer.Write(b.Binding);
                    Writer.Write(") ");
                    break;
                case UniformAttribute u:
                    Writer.Write("var<uniform> ");
                    break;
                default:
                    throw new NotSupportedException($"VisitVariableDeclaration attribute {a} not support ");
            }

        Writer.Write(decl.Name);
        Writer.Write(": ");
        await OnTypeReference(decl.Type);

        Writer.WriteLine(";");
        Writer.WriteLine();
    }

    public async ValueTask VisitMember(MemberDeclaration decl)
    {
        await WriteAttributesAsync(decl.Attributes, true);
        Writer.Write(decl.Name);
        Writer.Write(": ");
        await OnTypeReference(decl.Type);
        Writer.WriteLine(",");
    }

    public async ValueTask VisitStructure(StructureDeclaration decl)
    {
        Writer.Write("struct ");
        Writer.Write(decl.Name);
        Writer.WriteLine(" {");
        using (Writer.IndentedScope())
        {
            foreach (var m in decl.Members) await m.AcceptVisitor(this);
        }

        Writer.WriteLine("};");
        Writer.WriteLine();
    }


    public async ValueTask VisitModule(ShaderModuleDeclaration<TBody> decl)
    {
        foreach (var d in decl.Declarations) await d.AcceptVisitor(this);
    }

    private async ValueTask OnTypeReference(IShaderType type) => Writer.Write(type.Name);

    private async ValueTask WriteAttributeAsync(IShaderAttribute attr, CancellationToken cancellation = default)
    {
        switch (attr)
        {
            case ShaderMethodAttribute:
                break;
            case FragmentAttribute:
                Writer.Write("@");
                Writer.Write("fragment");
                break;
            case VertexAttribute:
                Writer.Write("@");
                Writer.Write("vertex");
                break;
            case BuiltinAttribute b:
                Writer.Write("@");
                Writer.Write("builtin(");
                Writer.Write(Enum.GetName(b.Slot));
                Writer.Write(")");
                break;
            case LocationAttribute a:
                Writer.Write("@");
                Writer.Write("location(");
                Writer.Write(a.Binding);
                Writer.Write(")");
                break;
            case IShaderMetadataAttribute:
                break;
            default:
                throw new NotSupportedException($"WriteAttribute not support {attr}");
        }
    }

    private async ValueTask WriteAttributesAsync(IEnumerable<IShaderAttribute> attributes, bool newLine = false)
    {
        foreach (var a in attributes)
        {
            await WriteAttributeAsync(a);
            if (newLine)
                Writer.WriteLine();
            else
                Writer.Write(' ');
        }
    }
}