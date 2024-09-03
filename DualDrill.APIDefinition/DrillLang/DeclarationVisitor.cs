namespace DualDrill.ApiGen.DrillLang;

public interface IDeclarationVisitor<TResult>
{
    public TResult VisitModule(ModuleDeclaration typeSystem);
    public TResult VisitHandle(HandleDeclaration decl);
    public TResult VisitStruct(StructDeclaration decl);
    public TResult VisitEnum(EnumDeclaration decl);
    public TResult VisitEnumValue(EnumValueDeclaration decl);
    public TResult VisitMethod(MethodDeclaration decl);
    public TResult VisitParameter(ParameterDeclaration decl);
    public TResult VisitProperty(PropertyDeclaration decl);
}

public static partial class ModuleExtension
{
    public static TResult AcceptVisitor<TResult>(this IDeclaration decl, IDeclarationVisitor<TResult> visitor)
    {
        return decl switch
        {
            ModuleDeclaration d => visitor.VisitModule(d),
            HandleDeclaration d => visitor.VisitHandle(d),
            StructDeclaration d => visitor.VisitStruct(d),
            EnumDeclaration d => visitor.VisitEnum(d),
            EnumValueDeclaration d => visitor.VisitEnumValue(d),
            MethodDeclaration d => visitor.VisitMethod(d),
            ParameterDeclaration d => visitor.VisitParameter(d),
            PropertyDeclaration d => visitor.VisitProperty(d),
            _ => throw new NotImplementedException($"Unsupported declaration {decl}")
        };
    }
}


