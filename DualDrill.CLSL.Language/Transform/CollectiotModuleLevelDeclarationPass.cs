using DualDrill.CLSL.Language.Declaration;
using DualDrill.CLSL.Language.FunctionBody;

namespace DualDrill.CLSL.Language.Transform;

public sealed class CollectiotModuleLevelDeclarationPass : IShaderModuleSimplePass
{
    public IDeclaration? VisitFunction(FunctionDeclaration decl)
    {
        throw new NotImplementedException();
    }

    public FunctionBody4 VisitFunctionBody(FunctionBody4 body)
    {
        throw new NotImplementedException();
    }

    public IDeclaration? VisitMember(MemberDeclaration decl)
    {
        throw new NotImplementedException();
    }

    public IDeclaration? VisitParameter(ParameterDeclaration decl)
    {
        throw new NotImplementedException();
    }

    public IDeclaration? VisitStructure(StructureDeclaration decl)
    {
        throw new NotImplementedException();
    }

    public IDeclaration? VisitValue(ValueDeclaration decl)
    {
        throw new NotImplementedException();
    }

    public IDeclaration? VisitVariable(VariableDeclaration decl)
    {
        throw new NotImplementedException();
    }
}
