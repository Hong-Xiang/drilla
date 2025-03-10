using DualDrill.CLSL.Language.AbstractSyntaxTree.Statement;
using DualDrill.CLSL.Language.Declaration;
using DualDrill.CLSL.Language.FunctionBody;
using DualDrill.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DualDrill.CLSL.Backend;

public sealed class LLVMVisitor
    : IDeclarationVisitor<ControlFlowGraphFunctionBody<IStackStatement>, Unit>
{
    private Stack<ControlFlowGraphFunctionBody<IStackStatement>> FunctionBody { get; }

    public Unit VisitFunction(FunctionDeclaration decl)
    {
        var body = FunctionBody.Pop();
        return default;
    }

    public Unit VisitMember(MemberDeclaration decl)
    {
        throw new NotImplementedException();
    }

    public Unit VisitModule(ShaderModuleDeclaration<ControlFlowGraphFunctionBody<IStackStatement>> decl)
    {
        foreach (var f in decl.FunctionDefinitions)
        {
            FunctionBody.Push(f.Value);
            f.Key.AcceptVisitor(this);
        }
        return default;
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
