namespace DualDrill.CLSL.Language.Statement;

public sealed record class DupStatement<TV, TE, TM, TF>(TV Result, TV Source) : IStatement<TV, TE, TM, TF>
{
    public TR Evaluate<TR>(IStatementSemantic<TV, TE, TM, TF, TR> semantic)
        => semantic.Dup(Result, Source);

    public IStatement<TVR, TVE, TVM, TVF> Select<TVR, TVE, TVM, TVF>(Func<TV, TVR> fr, Func<TE, TVE> fe, Func<TM, TVM> fm, Func<TF, TVF> ff)
        => new DupStatement<TVR, TVE, TVM, TVF>(fr(Result), fr(Source));
}

