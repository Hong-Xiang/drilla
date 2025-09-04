namespace DualDrill.CLSL.Language.Statement;

public sealed record class LetStatement<TV, TE, TM, TF>(TV Result, TE Expr) : IStatement<TV, TE, TM, TF>
{
    public TR Evaluate<TR>(IStatementSemantic<TV, TE, TM, TF, TR> semantic)
        => semantic.Let(Result, Expr);

    public IStatement<TVR, TVE, TVM, TVF> Select<TVR, TVE, TVM, TVF>(Func<TV, TVR> fr, Func<TE, TVE> fe, Func<TM, TVM> fm, Func<TF, TVF> ff)
        => new LetStatement<TVR, TVE, TVM, TVF>(fr(Result), fe(Expr));
}

