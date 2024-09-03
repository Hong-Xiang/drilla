using DualDrill.ApiGen.Mini;
using DualDrill.Common;
using System.Collections.Immutable;

namespace DualDrill.ApiGen.CodeGen;

sealed record class GPUApiPreCodeGenVisitor() : IDeclarationVisitor<IDeclaration>
{
    public IDeclaration VisitEnumDeclaration(EnumDeclaration decl) => decl;

    public IDeclaration VisitEnumValueDeclaration(EnumValueDeclaration decl) => decl with
    {
        Name = decl.Name.Capitalize()
    };

    public IDeclaration VisitHandleDeclaration(HandleDeclaration decl)
        => decl with
        {
            Methods = decl.Methods.Select(m => (MethodDeclaration)m.AcceptVisitor(this)).ToImmutableArray(),
            Properties = decl.Properties.Select(m => (PropertyDeclaration)m.AcceptVisitor(this)).ToImmutableArray(),
        };

    public IDeclaration VisitMethodDeclaration(MethodDeclaration decl)
    {
        var ps = decl.Parameters.Select(p => p.AcceptVisitor(this)).OfType<ParameterDeclaration>();
        if (decl.IsAsync)
        {
            ps = ps.Concat([new ParameterDeclaration("cancellation", new PlainTypeRef("CancellationToken"), null)]);
        }
        var name = decl.Name.Capitalize();
        return decl with
        {
            Name = decl.IsAsync ? name + "Async" : name,
            Parameters = ps.ToImmutableArray(),
        };
    }

    public IDeclaration VisitParameterDeclaration(ParameterDeclaration decl) => decl;

    public IDeclaration VisitPropertyDeclaration(PropertyDeclaration decl) => decl with
    {
        Name = decl.Name.Capitalize()
    };

    public IDeclaration VisitStructDeclaration(StructDeclaration decl)
        => decl with
        {
            Properties = decl.Properties.Select(p => (PropertyDeclaration)p.AcceptVisitor(this)).ToImmutableArray()
        };

    public IDeclaration VisitTypeSystem(TypeSystem typeSystem)
        => typeSystem with
        {
            TypeDeclarations = typeSystem.TypeDeclarations
                                    .Select(d => KeyValuePair.Create(d.Key, (ITypeDeclaration)d.Value.AcceptVisitor(this)))
                                    .ToImmutableDictionary()
        };
}
