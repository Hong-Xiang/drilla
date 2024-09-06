using DualDrill.ApiGen.DrillLang.Types;

namespace DualDrill.ApiGen.DrillLang;

public interface ITypeReferenceVisitor<TResult>
{
    TResult VisitOpaque(OpaqueTypeReference type);
    TResult VisitGeneric(GenericTypeReference type);
    TResult VisitUnknown(UnknownTypeReference type);
    TResult VisitNullable(NullableTypeReference type);
    TResult VisitFuture(FutureTypeReference type);
    TResult VisitSequence(SequenceTypeReference type);
    TResult VisitRecord(RecordTypeReference type);
    TResult VisitVoid(VoidTypeReference type);
    TResult VisitBool(BoolTypeReference type);
    TResult VisitString(StringTypeReference type);
    TResult VisitInteger(IntegerTypeReference type);
    TResult VisitFloat(FloatTypeReference type);
    TResult VisitVector(VectorTypeReference type);
    TResult VisitMatrix(MatrixTypeReference type);
}

public interface ITypeReferenceTransformVisitor : ITypeReferenceVisitor<ITypeReference>
{
    ITypeReference ITypeReferenceVisitor<ITypeReference>.VisitOpaque(OpaqueTypeReference type) => type;
    ITypeReference ITypeReferenceVisitor<ITypeReference>.VisitGeneric(GenericTypeReference type) => type;
    ITypeReference ITypeReferenceVisitor<ITypeReference>.VisitUnknown(UnknownTypeReference type) => type;
    ITypeReference ITypeReferenceVisitor<ITypeReference>.VisitNullable(NullableTypeReference type) => type;
    ITypeReference ITypeReferenceVisitor<ITypeReference>.VisitFuture(FutureTypeReference type) => type;
    ITypeReference ITypeReferenceVisitor<ITypeReference>.VisitSequence(SequenceTypeReference type) => type;
    ITypeReference ITypeReferenceVisitor<ITypeReference>.VisitVoid(VoidTypeReference type) => type;
    ITypeReference ITypeReferenceVisitor<ITypeReference>.VisitBool(BoolTypeReference type) => type;
    ITypeReference ITypeReferenceVisitor<ITypeReference>.VisitString(StringTypeReference type) => type;
    ITypeReference ITypeReferenceVisitor<ITypeReference>.VisitInteger(IntegerTypeReference type) => type;
    ITypeReference ITypeReferenceVisitor<ITypeReference>.VisitFloat(FloatTypeReference type) => type;
    ITypeReference ITypeReferenceVisitor<ITypeReference>.VisitVector(VectorTypeReference type) => type;
    ITypeReference ITypeReferenceVisitor<ITypeReference>.VisitMatrix(MatrixTypeReference type) => type;
}

public static partial class DrillLangExtension
{
    public static TResult AcceptVisitor<TResult>(this ITypeReference type, ITypeReferenceVisitor<TResult> visitor)
    {
        return type switch
        {
            OpaqueTypeReference t => visitor.VisitOpaque(t),
            GenericTypeReference t => visitor.VisitGeneric(t),
            UnknownTypeReference t => visitor.VisitUnknown(t),
            NullableTypeReference t => visitor.VisitNullable(t),
            FutureTypeReference t => visitor.VisitFuture(t),
            SequenceTypeReference t => visitor.VisitSequence(t),
            RecordTypeReference t => visitor.VisitRecord(t),
            VoidTypeReference t => visitor.VisitVoid(t),
            BoolTypeReference t => visitor.VisitBool(t),
            StringTypeReference t => visitor.VisitString(t),
            IntegerTypeReference t => visitor.VisitInteger(t),
            FloatTypeReference t => visitor.VisitFloat(t),
            VectorTypeReference t => visitor.VisitVector(t),
            MatrixTypeReference t => visitor.VisitMatrix(t),
            _ => throw new NotImplementedException($"Unsupported type reference {type}")
        };
    }


}



