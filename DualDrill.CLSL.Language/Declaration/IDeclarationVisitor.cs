using DualDrill.CLSL.Language.AbstractSyntaxTree.Statement;
using DualDrill.CLSL.Language.FunctionBody;
using DualDrill.CLSL.Language.Types;
using DualDrill.CLSL.LinearInstruction;

namespace DualDrill.CLSL.Language.Declaration;

public interface IDeclarationVisitor<T>
{
    T VisitValue(ValueDeclaration decl);
    T VisitVariable(VariableDeclaration decl);
    T VisitFunction(FunctionDeclaration decl);
    T VisitParameter(ParameterDeclaration decl);
    T VisitStructure(StructureDeclaration decl);
    T VisitMember(MemberDeclaration decl);
    T VisitModule<TBody>(ShaderModuleDeclaration<TBody> decl) where TBody : IFunctionBody;
}

public interface ITypeReferenceVisitor<T>
{
    T VisitTypeReference(IShaderType type);
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
            ShaderModuleDeclaration<UnstructuredStackInstructionFunctionBody> d => visitor.VisitModule(d),
            ShaderModuleDeclaration<CompoundStatement> d => visitor.VisitModule(d),
            ShaderModuleDeclaration<StructuredStackInstructionFunctionBody> d => visitor.VisitModule(d),
            _ => throw new NotSupportedException($"{nameof(IDeclarationVisitor<T>)} does not support {decl}")
        };
    }
}
