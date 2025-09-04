namespace DualDrill.CLSL.Language.Statement;

public sealed record class NopStatement<TV, TE, TM, TF>() : IStatement<TV, TE, TM, TF>
{
    public TR Evaluate<TR>(IStatementSemantic<TV, TE, TM, TF, TR> semantic)
        => semantic.Nop();

    public IStatement<TVR, TVE, TVM, TVF> Select<TVR, TVE, TVM, TVF>(Func<TV, TVR> fr, Func<TE, TVE> fe, Func<TM, TVM> fm, Func<TF, TVF> ff)
        => new NopStatement<TVR, TVE, TVM, TVF>();
}

