using DualDrill.ApiGen.DrillLang;

namespace DualDrill.ApiGen.DrillLang;

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

public interface ITypeReferenceTransformVisitor : ITypeReferenceVisitor<ITypeReference>
{
    ITypeReference ITypeReferenceVisitor<ITypeReference>.VisitPlain(PlainTypeRef type) => type;
    ITypeReference ITypeReferenceVisitor<ITypeReference>.VisitGeneric(GenericTypeRef type) => type;
    ITypeReference ITypeReferenceVisitor<ITypeReference>.VisitUnknown(UnknownTypeRef type) => type;
    ITypeReference ITypeReferenceVisitor<ITypeReference>.VisitNullable(NullableTypeRef type) => type;
    ITypeReference ITypeReferenceVisitor<ITypeReference>.VisitFuture(FutureTypeRef type) => type;
    ITypeReference ITypeReferenceVisitor<ITypeReference>.VisitSequence(SequenceTypeRef type) => type;
    ITypeReference ITypeReferenceVisitor<ITypeReference>.VisitVoid(VoidTypeRef type) => type;
    ITypeReference ITypeReferenceVisitor<ITypeReference>.VisitBool(BoolTypeReference type) => type;
    ITypeReference ITypeReferenceVisitor<ITypeReference>.VisitString(StringTypeReference type) => type;
    ITypeReference ITypeReferenceVisitor<ITypeReference>.VisitInteger(IntegerTypeReference type) => type;
    ITypeReference ITypeReferenceVisitor<ITypeReference>.VisitFloat(FloatTypeReference type) => type;
    ITypeReference ITypeReferenceVisitor<ITypeReference>.VisitVector(VectorTypeReference type) => type;
    ITypeReference ITypeReferenceVisitor<ITypeReference>.VisitMatrix(MatrixTypeReference type) => type;
}

public static partial class ModuleExtension
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



