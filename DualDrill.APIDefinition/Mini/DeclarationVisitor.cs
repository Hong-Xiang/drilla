namespace DualDrill.ApiGen.Mini;

public interface IDeclarationVisitor<TResult>
{
    public TResult VisitTypeSystem(TypeSystem typeSystem);
    public TResult VisitHandleDeclaration(HandleDeclaration decl);
    public TResult VisitStructDeclaration(StructDeclaration decl);
    public TResult VisitEnumDeclaration(EnumDeclaration decl);
    public TResult VisitEnumValueDeclaration(EnumValueDeclaration decl);
    public TResult VisitMethodDeclaration(MethodDeclaration decl);
    public TResult VisitParameterDeclaration(ParameterDeclaration decl);
    public TResult VisitPropertyDeclaration(PropertyDeclaration decl);
}

public static partial class TypeSystemExtension
{
    public static TResult AcceptVisitor<TResult>(this IDeclaration decl, IDeclarationVisitor<TResult> visitor)
    {
        return decl switch
        {
            TypeSystem d => visitor.VisitTypeSystem(d),
            HandleDeclaration d => visitor.VisitHandleDeclaration(d),
            StructDeclaration d => visitor.VisitStructDeclaration(d),
            EnumDeclaration d => visitor.VisitEnumDeclaration(d),
            EnumValueDeclaration d => visitor.VisitEnumValueDeclaration(d),
            MethodDeclaration d => visitor.VisitMethodDeclaration(d),
            ParameterDeclaration d => visitor.VisitParameterDeclaration(d),
            PropertyDeclaration d => visitor.VisitPropertyDeclaration(d),
            _ => throw new NotImplementedException($"Unsupported declaration {decl}")
        };
    }
}


