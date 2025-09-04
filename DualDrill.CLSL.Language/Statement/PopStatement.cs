namespace DualDrill.CLSL.Language.Statement;

public sealed record class PopStatement<TV, TE, TM, TF>(TV Target) : IStatement<TV, TE, TM, TF>
{
    public TR Evaluate<TR>(IStatementSemantic<TV, TE, TM, TF, TR> semantic)
        => semantic.Pop(Target);

    public IStatement<TVR, TVE, TVM, TVF> Select<TVR, TVE, TVM, TVF>(Func<TV, TVR> fr, Func<TE, TVE> fe, Func<TM, TVM> fm, Func<TF, TVF> ff)
        => new PopStatement<TVR, TVE, TVM, TVF>(fr(Target));
}

