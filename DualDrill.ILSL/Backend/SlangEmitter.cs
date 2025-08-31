using DualDrill.CLSL.Language.Declaration;
using DualDrill.CLSL.Language.FunctionBody;
using DualDrill.CLSL.Language.ShaderAttribute;
using DualDrill.CLSL.Language.Types;
using DualDrill.Common;
using DualDrill.Common.CodeTextWriter;
using System.CodeDom.Compiler;

namespace DualDrill.CLSL.Backend;

public class SlangEmitter
    : IDeclarationVisitor<FunctionBody4, Unit>
{
    private IndentedTextWriter Writer { get; }
    public ShaderModuleDeclaration<FunctionBody4> Module { get; }

    public SlangEmitter(
        ShaderModuleDeclaration<FunctionBody4> module
    )
    {
        Writer = new IndentedTextWriter(new StringWriter());
        Module = module;
    }

    Dictionary<FunctionDeclaration, int> functionIndicies = [];


    int GetFunctionIndex(FunctionDeclaration f)
    {
        if (functionIndicies.TryGetValue(f, out var index))
        {
            return index;
        }
        else
        {
            index = functionIndicies.Count;
            functionIndicies[f] = index;
            return index;
        }
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

    void VisitType(IShaderType type)
    {
        Writer.Write(type.Name);
    }

    string FunctionName(FunctionDeclaration decl)
    {
        return $"func{GetFunctionIndex(decl)}_{decl.Name}";
    }

    public Unit VisitFunction(FunctionDeclaration decl)
    {
        WriteAttributes(decl.Attributes);
        if (decl.Return.Type is not null)
        {
            VisitType(decl.Return.Type);
        }
        Writer.Write(FunctionName(decl));
        Writer.Write("(");
        foreach (var p in decl.Parameters)
        {
            p.AcceptVisitor(this);
        }

        Writer.Write(")");
        if (decl.Return.Attributes.Count > 0)
        {
            Writer.Write(":");
            // TODO: only semantic binding is supported here
            WriteAttributes(decl.Return.Attributes);
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

        return default;
    }

    void OnBody(FunctionBody4 body)
    {
    }

    public Unit VisitMember(MemberDeclaration decl)
    {
        throw new NotImplementedException();
    }

    public Unit VisitModule(ShaderModuleDeclaration<FunctionBody4> decl)
    {
        foreach (var d in decl.Declarations)
        {
            d.AcceptVisitor(this);
        }
        return default;
    }

    public Unit VisitParameter(ParameterDeclaration decl)
    {
        WriteAttributes(decl.Attributes);
        Writer.Write(decl.Name);
        Writer.Write(": ");
        VisitType(decl.Type);
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

    public string Emit()
    {
        Module.Accept(this);
        return Writer.InnerWriter.ToString();
    }
}
