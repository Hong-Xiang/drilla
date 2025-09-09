namespace DualDrill.CLSL.Language.Statement;

public sealed record class CallStatement<TV, TE, TM, TF>(TV Result, TF F, IReadOnlyList<TV> Arguments)
    : IStatement<TV, TE, TM, TF>
{
    public TR Evaluate<TR>(IStatementSemantic<TV, TE, TM, TF, TR> semantic)
        => semantic.Call(Result, F, Arguments);

    public IStatement<TVR, TVE, TVM, TVF> Select<TVR, TVE, TVM, TVF>(Func<TV, TVR> fr, Func<TE, TVE> fe, Func<TM, TVM> fm, Func<TF, TVF> ff)
        => new CallStatement<TVR, TVE, TVM, TVF>(fr(Result), ff(F), [.. Arguments.Select(fr)]);
}

