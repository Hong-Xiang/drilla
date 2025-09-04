namespace DualDrill.CLSL.Language.Statement;

public sealed record class MovStatement<TV, TE, TM, TF>(TM Target, TM Source) : IStatement<TV, TE, TM, TF>
{
    public TR Evaluate<TR>(IStatementSemantic<TV, TE, TM, TF, TR> semantic)
        => semantic.Mov(Target, Source);

    public IStatement<TVR, TVE, TVM, TVF> Select<TVR, TVE, TVM, TVF>(Func<TV, TVR> fr, Func<TE, TVE> fe, Func<TM, TVM> fm, Func<TF, TVF> ff)
        => new MovStatement<TVR, TVE, TVM, TVF>(fm(Target), fm(Source));
}

