using DualDrill.ApiGen.DrillLang;
using DualDrill.Common;
using System.Collections.Immutable;

namespace DualDrill.ApiGen.CodeGen;


sealed record class GPUApiPreCodeGenVisitor(
    ImmutableHashSet<string> HandleNames,
    bool UseGenericBackend) : IDeclarationVisitor<IDeclaration>
{
    TypeVisitor TypeTransform { get; } = new TypeVisitor(HandleNames);
    sealed class TypeVisitor(ImmutableHashSet<string> HandleNames) : ITypeReferenceTransformVisitor
    {
        public ITypeReference VisitFuture(FutureTypeRef type)
            => type with
            {
                Type = type.Type.AcceptVisitor(this)
            };

        public ITypeReference VisitGeneric(GenericTypeRef type)
        {
            throw new NotImplementedException();
        }

        public ITypeReference VisitNullable(NullableTypeRef type)
            => type with
            {
                Type = type.Type.AcceptVisitor(this)
            };

        public ITypeReference VisitPlain(PlainTypeRef type)
        {
            if (HandleNames.Contains(type.Name))
            {
                return type with
                {
                    Name = type.Name + "<TBackend>"
                };
            }
            else
            {
                return type;
            }
        }

        public ITypeReference VisitSequence(SequenceTypeRef type)
            => type with
            {
                Type = type.Type.AcceptVisitor(this)
            };
    }
    public IDeclaration VisitEnum(EnumDeclaration decl) => decl;

    public IDeclaration VisitEnumValue(EnumValueDeclaration decl) => decl with
    {
        Name = decl.Name.Capitalize()
    };

    public IDeclaration VisitHandle(HandleDeclaration decl)
    {
        ImmutableArray<MethodDeclaration> methods = [.. decl.Methods.Select(m => (MethodDeclaration)m.AcceptVisitor(this))];
        var result = decl with
        {
            Methods = methods,
            Properties = [.. decl.Properties.Select(m => (PropertyDeclaration)m.AcceptVisitor(this))],
        };
        return result;
    }

    public IDeclaration VisitMethod(MethodDeclaration decl)
    {
        var ps = decl.Parameters.Select(p => p.AcceptVisitor(this)).OfType<ParameterDeclaration>();
        if (decl.IsAsync)
        {
            ps = ps.Concat([new ParameterDeclaration("cancellation", new PlainTypeRef("CancellationToken"), null)]);
        }
        var name = decl.Name.Capitalize();
        var result = decl with
        {
            Name = decl.IsAsync ? name + "Async" : name,
            Parameters = ps.ToImmutableArray(),
            ReturnType = decl.ReturnType.AcceptVisitor(TypeTransform)
        };
        return result;
    }

    public IDeclaration VisitParameter(ParameterDeclaration decl) => decl with
    {
        Type = decl.Type.AcceptVisitor(TypeTransform)
    };

    public IDeclaration VisitProperty(PropertyDeclaration decl) => decl with
    {
        Name = decl.Name.Capitalize(),
        Type = decl.Type.AcceptVisitor(TypeTransform)
    };

    public IDeclaration VisitStruct(StructDeclaration decl)
        => decl with
        {
            Properties = [.. decl.Properties.Select(p => (PropertyDeclaration)p.AcceptVisitor(this))]
        };

    public IDeclaration VisitModule(ModuleDeclaration module)
    {
        var result = module with
        {
            TypeDeclarations = [.. module.TypeDeclarations.Select(d => d.AcceptVisitor(this)).OfType<ITypeDeclaration>()]
        };
        return result;
    }
}
