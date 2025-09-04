using DualDrill.CLSL.Language.Operation;

namespace DualDrill.CLSL.Language.Statement;

public sealed record class SetVectorSwizzleStatement<TV, TE, TM, TF>(IVectorSwizzleSetOperation Operation, TV Target, TV Value)
    : IStatement<TV, TE, TM, TF>
{
    public TR Evaluate<TR>(IStatementSemantic<TV, TE, TM, TF, TR> semantic)
        => semantic.SetVecSwizzle(Operation, Target, Value);

    public IStatement<TVR, TVE, TVM, TVF> Select<TVR, TVE, TVM, TVF>(Func<TV, TVR> fr, Func<TE, TVE> fe, Func<TM, TVM> fm, Func<TF, TVF> ff)
        => new SetVectorSwizzleStatement<TVR, TVE, TVM, TVF>(Operation, fr(Target), fr(Value));
}

