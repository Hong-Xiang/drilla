using DualDrill.CLSL.Language.AbstractSyntaxTree.Expression;

namespace DualDrill.CLSL.Language.Types;

public sealed record class FixedSizedArrayType(IPlainType ElementType, int Size)
{
}

public sealed record class RuntimeSizedArrayType(IScalarType ElementType)
{
}
