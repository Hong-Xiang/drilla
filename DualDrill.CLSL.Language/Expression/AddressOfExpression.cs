namespace DualDrill.CLSL.Language.Expression;

sealed record class AddressOfExpression<T>(T Target)
    : IExpression<T>
{
    public TR Evaluate<TX, TR>(IExpressionSemantic<TX, T, TR> semantic, TX ctx)
        => semantic.AddressOf(ctx, Target);

    public IExpression<TR> Select<TR>(Func<T, TR> f)
        => new AddressOfExpression<TR>(f(Target));
}

