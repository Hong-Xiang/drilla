using DualDrill.CLSL.Language.Declaration;
using DualDrill.CLSL.Language.FunctionBody;
using DualDrill.Common;
using System.CodeDom.Compiler;

namespace DualDrill.CLSL.Backend;

public sealed class SPIRVEmitter
    : IDeclarationVisitor<FunctionBody4, Unit>
{
    IndentedTextWriter Writer = new IndentedTextWriter(new StringWriter());

    public string Emit(ShaderModuleDeclaration<FunctionBody4> module)
    {
        return ((StringWriter)Writer.InnerWriter).ToString();
    }

    public Unit VisitFunction(FunctionDeclaration decl)
    {
        throw new NotImplementedException();
    }

    public Unit VisitMember(MemberDeclaration decl)
    {
        throw new NotImplementedException();
    }

    public Unit VisitModule(ShaderModuleDeclaration<FunctionBody4> decl)
    {
        throw new NotImplementedException();
    }

    public Unit VisitParameter(ParameterDeclaration decl)
    {
        throw new NotImplementedException();
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
}
