using DualDrill.ApiGen.DrillLang.Declaration;
using DualDrill.ApiGen.DrillLang.Types;

namespace DualDrill.ApiGen.DrillLang.Visitors;

internal sealed class ModuleTypeReferenceVisitor : IDeclarationVisitor<IDeclaration>
{
    private readonly ITypeReferenceVisitor<ITypeReference> TypeTransform;

    public ModuleTypeReferenceVisitor(ITypeReferenceVisitor<ITypeReference> typeTransform)
    {
        TypeTransform = typeTransform;
    }

    public IDeclaration VisitEnum(EnumDeclaration decl)
    {
        return decl with
        {
            Values = [.. decl.Values.Select(v => v.AcceptVisitor(this)).OfType<EnumMemberDeclaration>()]
        };
    }

    public IDeclaration VisitEnumValue(EnumMemberDeclaration decl)
    {
        return decl;
    }

    public IDeclaration VisitHandle(HandleDeclaration decl)
    {
        return decl with
        {
            Methods = [.. decl.Methods.Select(m => m.AcceptVisitor(this)).OfType<MethodDeclaration>()],
            Properties = [.. decl.Properties.Select(m => m.AcceptVisitor(this)).OfType<PropertyDeclaration>()],
        };
    }

    public IDeclaration VisitMethod(MethodDeclaration decl)
    {
        return decl with
        {
            ReturnType = decl.ReturnType.AcceptVisitor(TypeTransform),
            Parameters = [.. decl.Parameters.Select(p => p.AcceptVisitor(this)).OfType<ParameterDeclaration>()],
        };
    }

    public IDeclaration VisitModule(ModuleDeclaration decl)
    {
        return decl with
        {
            Handles = [.. decl.Handles.Select(h => h.AcceptVisitor(this)).OfType<HandleDeclaration>()],
            Structs = [.. decl.Structs.Select(h => h.AcceptVisitor(this)).OfType<StructDeclaration>()],
            Enums = [.. decl.Enums.Select(h => h.AcceptVisitor(this)).OfType<EnumDeclaration>()],
        };
    }

    public IDeclaration VisitParameter(ParameterDeclaration decl)
    {
        return decl with
        {
            Type = decl.Type.AcceptVisitor(TypeTransform),
        };
    }

    public IDeclaration VisitProperty(PropertyDeclaration decl)
    {
        return decl with
        {
            Type = decl.Type.AcceptVisitor(TypeTransform),
        };
    }

    public IDeclaration VisitStruct(StructDeclaration decl)
    {
        return decl with
        {
            Properties = [.. decl.Properties.Select(f => f.AcceptVisitor(this)).OfType<PropertyDeclaration>()],
        };
    }
}
