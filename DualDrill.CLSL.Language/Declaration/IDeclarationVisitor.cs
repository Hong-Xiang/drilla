using DualDrill.CLSL.Language.FunctionBody;

namespace DualDrill.CLSL.Language.Declaration;

public interface IDeclarationVisitor<T>
{
    T VisitValue(ValueDeclaration decl);
    T VisitVariable(VariableDeclaration decl);
    T VisitFunction(FunctionDeclaration decl);
    T VisitParameter(ParameterDeclaration decl);
    T VisitStructure(StructureDeclaration decl);
    T VisitMember(MemberDeclaration decl);
}

public interface IDeclarationVisitor<TBody, T> : IDeclarationVisitor<T>
    where TBody : IFunctionBody
{
    T VisitModule(ShaderModuleDeclaration<TBody> decl);
}

public interface IDeclarationSemantic<T>
{
    T VisitVariable(VariableDeclaration decl);
    T VisitFunction(FunctionDeclaration decl);
    T VisitParameter(ParameterDeclaration decl);
    T VisitStructure(StructureDeclaration decl);
    T VisitMember(MemberDeclaration decl);
    T VisitModule(ShaderModuleDeclaration<FunctionBody4> decl);
}

public interface IShaderModuleSimplePass
    : IDeclarationVisitor<IDeclaration?>
{
    FunctionBody4 VisitFunctionBody(FunctionBody4 body);
}

public static class DeclarationExtension
{
    public static T AcceptVisitor<T>(this IDeclaration decl, IDeclarationVisitor<T> visitor)
    {
        return decl switch
        {
            ValueDeclaration d => visitor.VisitValue(d),
            StructureDeclaration s => visitor.VisitStructure(s),
            MemberDeclaration m => visitor.VisitMember(m),
            FunctionDeclaration d => visitor.VisitFunction(d),
            ParameterDeclaration p => visitor.VisitParameter(p),
            VariableDeclaration d => visitor.VisitVariable(d),
            _ => throw new NotSupportedException($"{nameof(IDeclarationVisitor<T>)} does not support {decl}")
        };
    }
}