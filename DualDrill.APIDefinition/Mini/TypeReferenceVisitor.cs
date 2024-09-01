namespace DualDrill.ApiGen.Mini;

public interface ITypeReferenceVisitor<TResult>
{
    TResult VisitPlain(PlainTypeRef type);
    TResult VisitGeneric(GenericTypeRef type);
    TResult VisitUnknown(UnknownTypeRef type);
    TResult VisitNullable(NullableTypeRef type);
    TResult VisitFuture(FutureTypeRef type);
    TResult VisitSequence(SequenceTypeRef type);
    TResult VisitVoid(VoidTypeRef type);
    TResult VisitBool(BoolTypeReference type);
    TResult VisitString(StringTypeReference type);
    TResult VisitInteger(IntegerTypeReference type);
    TResult VisitFloat(FloatTypeReference type);
    TResult VisitVector(VectorTypeReference type);
    TResult VisitMatrix(MatrixTypeReference type);
}

public static partial class TypeSystemExtension
{
    public static TResult AcceptVisitor<TResult>(this ITypeReference type, ITypeReferenceVisitor<TResult> visitor)
    {
        return type switch
        {
            PlainTypeRef t => visitor.VisitPlain(t),
            GenericTypeRef t => visitor.VisitGeneric(t),
            UnknownTypeRef t => visitor.VisitUnknown(t),
            NullableTypeRef t => visitor.VisitNullable(t),
            FutureTypeRef t => visitor.VisitFuture(t),
            SequenceTypeRef t => visitor.VisitSequence(t),
            VoidTypeRef t => visitor.VisitVoid(t),
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



