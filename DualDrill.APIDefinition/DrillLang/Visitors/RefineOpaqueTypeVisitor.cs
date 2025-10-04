using DualDrill.ApiGen.DrillLang.Types;

namespace DualDrill.ApiGen.DrillLang.Visitors;

internal sealed class RefineOpaqueTypeVisitor(
    Func<string, ITypeReference> RefineMap
) : ITypeReferenceVisitor<ITypeReference>
{
    public ITypeReference VisitBool(BoolTypeReference type) => type;

    public ITypeReference VisitFloat(FloatTypeReference type) => type;

    public ITypeReference VisitFuture(FutureTypeReference type)
        => type with
        {
            Type = type.Type.AcceptVisitor(this)
        };

    public ITypeReference VisitGeneric(GenericTypeReference type)
    {
        throw new NotImplementedException();
    }

    public ITypeReference VisitInteger(IntegerTypeReference type) => type;

    public ITypeReference VisitMatrix(MatrixTypeReference type) => type;

    public ITypeReference VisitNullable(NullableTypeReference type)
        => type with
        {
            Type = type.Type.AcceptVisitor(this)
        };

    public ITypeReference VisitOpaque(OpaqueTypeReference type)
    {
        var result = RefineMap(type.Name);
        if (result is OpaqueTypeReference opaqueResult)
        {
            return opaqueResult;
        }
        else
        {
            return result.AcceptVisitor(this);
        }
    }

    public ITypeReference VisitRecord(RecordTypeReference type)
        => type with
        {
            KeyType = type.KeyType.AcceptVisitor(this),
            ValueType = type.KeyType.AcceptVisitor(this)
        };

    public ITypeReference VisitSequence(SequenceTypeReference type)
        => type with
        {
            Type = type.Type.AcceptVisitor(this)
        };

    public ITypeReference VisitString(StringTypeReference type) => type;

    public ITypeReference VisitUnknown(UnknownTypeReference type) => type;

    public ITypeReference VisitVector(VectorTypeReference type) => type;

    public ITypeReference VisitVoid(VoidTypeReference type) => type;
}

public static partial class TypeReferenceExtension
{
    public static ITypeReference RefineOpaqueType(ITypeReference type, Func<string, ITypeReference> refinement)
        => type.AcceptVisitor(new RefineOpaqueTypeVisitor(refinement));


}

