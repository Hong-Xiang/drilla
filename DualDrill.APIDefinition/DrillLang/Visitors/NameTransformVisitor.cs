using DotNext.Reflection;
using DualDrill.ApiGen.DrillLang.Declaration;
using DualDrill.ApiGen.DrillLang.Types;
using System.Diagnostics;

namespace DualDrill.ApiGen.DrillLang.Visitors;

internal sealed class NameTransformVisitor(
    INameTransform Transform
) : IDeclarationVisitor<IDeclaration?>
{
    TypeReferenceNameVisitor TypeReferenceVisitor { get; } = new(Transform);
    Stack<string> ContextNames { get; } = [];

    sealed class TypeReferenceNameVisitor(INameTransform Transform) : ITypeReferenceVisitor<ITypeReference?>
    {
        public ITypeReference? VisitBool(BoolTypeReference type) => type;


        public ITypeReference? VisitFloat(FloatTypeReference type) => type;


        public ITypeReference? VisitFuture(FutureTypeReference type)
        {
            var r = type.Type.AcceptVisitor(this);
            return r is null ? null : type with
            {
                Type = r
            };
        }

        public ITypeReference? VisitGeneric(GenericTypeReference type)
        {
            throw new NotImplementedException();
        }

        public ITypeReference? VisitInteger(IntegerTypeReference type) => type;


        public ITypeReference? VisitMatrix(MatrixTypeReference type) => type;

        public ITypeReference? VisitNullable(NullableTypeReference type)
        {
            var r = type.Type.AcceptVisitor(this);
            return r is null ? null : type with
            {
                Type = r
            };
        }

        public ITypeReference? VisitOpaque(OpaqueTypeReference type)
        {
            var name = Transform.TypeReferenceName(type.Name);
            if (name is null)
            {
                return null;
            }
            return new OpaqueTypeReference(name);
        }

        public ITypeReference? VisitRecord(RecordTypeReference type)
        {
            var kt = type.KeyType.AcceptVisitor(this);
            var vt = type.ValueType.AcceptVisitor(this);
            if (kt is null || vt is null)
            {
                return null;
            }
            return type with
            {
                KeyType = kt,
                ValueType = vt
            };
        }

        public ITypeReference? VisitSequence(SequenceTypeReference type)
        {
            var r = type.Type.AcceptVisitor(this);
            return r is null ? null : type with
            {
                Type = r
            };
        }

        public ITypeReference? VisitString(StringTypeReference type) => type;

        public ITypeReference? VisitUnknown(UnknownTypeReference type) => type;

        public ITypeReference? VisitVector(VectorTypeReference type) => type;


        public ITypeReference? VisitVoid(VoidTypeReference type) => type;
    }

    public IDeclaration? VisitEnum(EnumDeclaration decl)
    {
        var r = Transform.EnumName(decl.Name);
        if (r is null)
        {
            return null;
        }
        List<EnumMemberDeclaration> valueResults = [];
        ContextNames.Push(r);
        foreach (var v in decl.Values)
        {
            var tv = v.AcceptVisitor(this);
            if (tv is EnumMemberDeclaration d)
            {
                valueResults.Add(d);
            }
        }
        var p = ContextNames.Pop();
        Debug.Assert(r == p);
        return decl with
        {
            Name = r,
            Values = [.. valueResults]
        };
    }

    public IDeclaration? VisitEnumValue(EnumMemberDeclaration decl)
    {
        var r = Transform.EnumValueName(ContextNames.Peek(), decl.Name);
        if (r is null)
        {
            return null;
        }
        return decl with
        {
            Name = r
        };
    }

    public IDeclaration? VisitHandle(HandleDeclaration decl)
    {
        var r = Transform.HandleName(decl.Name);
        if (r is null)
        {
            return null;
        }
        List<MethodDeclaration> methods = [];
        List<PropertyDeclaration> props = [];
        ContextNames.Push(r);
        foreach (var v in decl.Methods)
        {
            var tv = v.AcceptVisitor(this);
            if (tv is MethodDeclaration d)
            {
                methods.Add(d);
            }
        }
        foreach (var v in decl.Properties)
        {
            var tv = v.AcceptVisitor(this);
            if (tv is PropertyDeclaration d)
            {
                props.Add(d);
            }
        }
        var p = ContextNames.Pop();
        Debug.Assert(r == p);
        return decl with
        {
            Name = r,
            Methods = [.. methods],
            Properties = [.. props]
        };
    }

    public IDeclaration? VisitMethod(MethodDeclaration decl)
    {
        var r = Transform.MethodName(ContextNames.Peek(), decl.Name);
        if (r is null)
        {
            return null;
        }
        var returnType = decl.ReturnType.AcceptVisitor(TypeReferenceVisitor);
        if (returnType is null)
        {
            return null;
        }

        List<ParameterDeclaration> paramters = [];
        foreach (var p in decl.Parameters)
        {
            var rpt = p.Type.AcceptVisitor(TypeReferenceVisitor);
            if (rpt is null)
            {
                return null;
            }
            paramters.Add(p with
            {
                Type = rpt
            });
        }


        return decl with
        {
            Name = r,
            ReturnType = returnType,
            Parameters = [.. paramters]
        };
    }

    public IDeclaration? VisitModule(ModuleDeclaration module)
    {
        return module with
        {
            Handles = [.. module.Handles.Select(d => d.AcceptVisitor(this)).OfType<HandleDeclaration>()],
            Structs = [.. module.Structs.Select(d => d.AcceptVisitor(this)).OfType<StructDeclaration>()],
            Enums = [.. module.Enums.Select(d => d.AcceptVisitor(this)).OfType<EnumDeclaration>()],
        };
    }

    public IDeclaration? VisitParameter(ParameterDeclaration decl)
    {
        var t = decl.Type.AcceptVisitor(TypeReferenceVisitor);
        return t is null
            ? null
            : decl with { Type = t };
    }

    public IDeclaration? VisitProperty(PropertyDeclaration decl)
    {
        var r = Transform.PropertyName(ContextNames.Peek(), decl.Name);
        if (r is null)
        {
            return null;
        }
        var t = decl.Type.AcceptVisitor(TypeReferenceVisitor);
        if (t is null)
        {
            return null;
        }
        return decl with
        {
            Name = r,
            Type = t
        };
    }

    public IDeclaration? VisitStruct(StructDeclaration decl)
    {
        var r = Transform.StructName(decl.Name);
        if (r is null)
        {
            return null;
        }
        List<PropertyDeclaration> props = [];
        ContextNames.Push(r);
        foreach (var v in decl.Properties)
        {
            var tv = v.AcceptVisitor(this);
            if (tv is PropertyDeclaration d)
            {
                props.Add(d);
            }
        }
        var p = ContextNames.Pop();
        Debug.Assert(r == p);
        return decl with
        {
            Name = r,
            Properties = [.. props]
        };
    }
}
