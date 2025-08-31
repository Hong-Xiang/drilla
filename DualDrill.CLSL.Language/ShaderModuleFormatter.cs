using DualDrill.CLSL.Language.Declaration;
using DualDrill.CLSL.Language.FunctionBody;
using DualDrill.CLSL.Language.ShaderAttribute;
using DualDrill.CLSL.Language.Types;
using DualDrill.Common;
using DualDrill.Common.CodeTextWriter;
using System.CodeDom.Compiler;

namespace DualDrill.CLSL.Language;

public sealed class ShaderModuleFormatter
    : IDeclarationVisitor<FunctionBody4, Unit>
{
    StringWriter BaseWriter { get; }
    IndentedTextWriter Writer { get; }
    Stack<ShaderModuleDeclaration<FunctionBody4>> VisitingModule { get; } = [];

    ShaderModuleDeclaration<FunctionBody4> Module => VisitingModule.Peek();

    public ShaderModuleFormatter()
    {
        BaseWriter = new StringWriter();
        Writer = new IndentedTextWriter(BaseWriter);
    }

    Unit WriteAttribute(IShaderAttribute attr)
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
        return default;
    }


    Unit WriteAttributes(IEnumerable<IShaderAttribute> attributes, bool newLine = false)
    {
        foreach (var a in attributes)
        {
            WriteAttribute(a);
            if (newLine)
            {
                Writer.WriteLine();
            }
            else
            {
                Writer.Write(' ');
            }
        }
        return default;
    }

    void OnShaderType(IShaderType type)
    {
        Writer.Write(type.Name);
    }

    public Unit VisitFunction(FunctionDeclaration decl)
    {
        WriteAttributes(decl.Attributes);
        Writer.Write("fn ");
        Writer.Write(decl.Name);
        Writer.Write("(");
        foreach (var p in decl.Parameters)
        {
            p.AcceptVisitor(this);
        }

        Writer.Write(")");
        Writer.Write(" -> ");
        WriteAttributes(decl.Return.Attributes);
        if (decl.Return.Type is not null)
        {
            OnShaderType(decl.Return.Type);
        }

        Writer.WriteLine();
        using (Writer.IndentedScope())
        {
            if (Module.TryGetBody(decl, out var b))
            {
                OnBody(b);
            }
            else
            {
                Writer.WriteLine($"...{Module.GetType().CSharpFullName()}...");
            }
        }

        Writer.WriteLine();
        Writer.WriteLine();


        return default;
    }

    void OnBody(FunctionBody4 body)
    {
        new FunctionBodyFormatter(Writer, body).Dump();
    }

    public Unit VisitMember(MemberDeclaration decl)
    {
        throw new NotImplementedException();
    }

    public Unit VisitModule(ShaderModuleDeclaration<FunctionBody4> decl)
    {
        VisitingModule.Push(decl);
        foreach (var d in decl.Declarations)
        {
            d.AcceptVisitor(this);
        }
        VisitingModule.Pop();
        return default;
    }

    public Unit VisitParameter(ParameterDeclaration decl)
    {
        WriteAttributes(decl.Attributes);
        Writer.Write(decl.Name);
        Writer.Write(": ");
        OnShaderType(decl.Type);
        Writer.Write(", ");
        return default;
    }

    public Unit VisitStructure(StructureDeclaration decl)
    {
        throw new NotImplementedException();
    }

    public Unit VisitValue(ValueDeclaration decl)
    {
        throw new NotImplementedException();
    }

    public Unit VisitVariable(VariableDeclaration decl)
    {
        throw new NotImplementedException();
    }

    public string Dump()
    {
        return BaseWriter.ToString();
    }
}
