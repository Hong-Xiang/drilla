using DualDrill.CLSL.Language.Operation;
using System.Collections.Immutable;

namespace DualDrill.CLSL.Language.Expression;

public sealed class VectorCompositeConstructExpression<T>(
    VectorCompositeConstructionOperation Operation,
    ImmutableArray<T> Arguments
) : IExpression<T>
{
    public TR Evaluate<TR>(IExpressionSemantic<T, TR> semantic)
        => semantic.VectorCompositeConstruction(Operation, Arguments);

    public IExpression<TR> Select<TR>(Func<T, TR> f)
        => new VectorCompositeConstructExpression<TR>(Operation, [.. Arguments.Select(f)]);
}
