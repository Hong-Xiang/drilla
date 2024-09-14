namespace DualDrill.ILSL.IR.Declaration;

public interface IDeclarationVisitor<T>
{
    T VisitValue(ValueDeclaration decl);
    T VisitVariable(VariableDeclaration decl);
    T VisitFunction(FunctionDeclaration decl);
    T VisitParameter(ParameterDeclaration decl);
    T VisitType(IType type);
}

public static class DeclarationExtension
{
    public static T AcceptVisitor<T>(this IDeclaration decl, IDeclarationVisitor<T> visitor)
    {
        return decl switch
        {
            ValueDeclaration d => visitor.VisitValue(d),
            IType t => visitor.VisitType(t),
            FunctionDeclaration d => visitor.VisitFunction(d),
            ParameterDeclaration p => visitor.VisitParameter(p),
            VariableDeclaration d => visitor.VisitVariable(d),
            _ => throw new NotSupportedException($"{nameof(IDeclarationVisitor<T>)} does not support {decl}")
        };
    }
}
