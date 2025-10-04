﻿using System.CodeDom.Compiler;
using DualDrill.CLSL.Language.Declaration;
using DualDrill.CLSL.Language.FunctionBody;
using DualDrill.CLSL.Language.ShaderAttribute;
using DualDrill.CLSL.Language.ShaderAttribute.Metadata;
using DualDrill.CLSL.Language.Types;
using DualDrill.Common;
using DualDrill.Common.CodeTextWriter;

namespace DualDrill.CLSL.Language;

public sealed class ShaderModuleFormatter
    : IDeclarationVisitor<FunctionBody4, Unit>
{
    public ShaderModuleFormatter()
    {
        BaseWriter = new StringWriter();
        Writer = new IndentedTextWriter(BaseWriter);
    }

    private StringWriter BaseWriter { get; }
    private IndentedTextWriter Writer { get; }
    private Stack<ShaderModuleDeclaration<FunctionBody4>> VisitingModule { get; } = [];

    private ShaderModuleDeclaration<FunctionBody4> Module => VisitingModule.Peek();

    public Unit VisitFunction(FunctionDeclaration decl)
    {
        WriteAttributes(decl.Attributes);
        Writer.Write("fn ");
        Writer.Write(decl.Name);
        Writer.Write("(");
        foreach (var p in decl.Parameters) p.AcceptVisitor(this);

        Writer.Write(")");
        Writer.Write(" -> ");
        WriteAttributes(decl.Return.Attributes);
        if (decl.Return.Type is not null) OnShaderType(decl.Return.Type);

        Writer.WriteLine();
        using (Writer.IndentedScope())
        {
            if (Module.TryGetBody(decl, out var b))
                OnBody(b);
            else
                Writer.WriteLine($"...{Module.GetType().CSharpFullName()}...");
        }

        Writer.WriteLine();
        Writer.WriteLine();


        return default;
    }

    public Unit VisitMember(MemberDeclaration decl)
    {
        Writer.Write(decl.Name);
        Writer.Write(" : ");
        Writer.Write(decl.Type);
        Writer.Write("[");
        foreach (var a in decl.Attributes) WriteAttribute(a);
        Writer.Write("]");
        return default;
    }

    public Unit VisitModule(ShaderModuleDeclaration<FunctionBody4> decl)
    {
        VisitingModule.Push(decl);
        foreach (var d in decl.Declarations) d.AcceptVisitor(this);
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
        WriteAttributes(decl.Attributes, true);
        Writer.Write(decl.Name);
        using (Writer.IndentedScope())
        {
            foreach (var m in decl.Members) m.AcceptVisitor(this);
        }

        Writer.WriteLine();
        return default;
    }

    public Unit VisitValue(ValueDeclaration decl) => throw new NotImplementedException();

    public Unit VisitVariable(VariableDeclaration decl)
    {
        foreach (var s in decl.Attributes)
        {
            WriteAttribute(s);
            Writer.WriteLine();
        }

        Writer.Write("var ");
        Writer.Write(decl.Name);
        Writer.Write(": ");
        OnShaderType(decl.Type);
        Writer.WriteLine();
        Writer.WriteLine();
        return default;
    }

    private Unit WriteAttribute(IShaderAttribute attr)
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
                Writer.Write(attr);
                break;
        }

        return default;
    }


    private Unit WriteAttributes(IEnumerable<IShaderAttribute> attributes, bool newLine = false)
    {
        foreach (var a in attributes)
        {
            WriteAttribute(a);
            if (newLine)
                Writer.WriteLine();
            else
                Writer.Write(' ');
        }

        return default;
    }

    private void OnShaderType(IShaderType type)
    {
        Writer.Write(type.Name);
    }

    private void OnBody(FunctionBody4 body)
    {
        new FunctionBodyFormatter(Writer, body).Dump();
    }

    public string Dump() => BaseWriter.ToString();
}