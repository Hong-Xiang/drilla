namespace DualDrill.CLSL.Language.Statement;

public sealed record class GetStatement<TV, TE, TM, TF>(TV Result, TM Source) : IStatement<TV, TE, TM, TF>
{
    public TR Evaluate<TR>(IStatementSemantic<TV, TE, TM, TF, TR> semantic)
        => semantic.Get(Result, Source);

    public IStatement<TVR, TVE, TVM, TVF> Select<TVR, TVE, TVM, TVF>(Func<TV, TVR> fr, Func<TE, TVE> fe, Func<TM, TVM> fm, Func<TF, TVF> ff)
        => new GetStatement<TVR, TVE, TVM, TVF>(fr(Result), fm(Source));
}

