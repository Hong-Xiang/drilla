namespace DualDrill.CLSL.Language.Statement;

public sealed record class CallStatement<TV, TE, TM, TF>(TV Result, TF F, IReadOnlyList<TE> Arguments)
    : IStatement<TV, TE, TM, TF>
{
    public TR Evaluate<TR>(IStatementSemantic<TV, TE, TM, TF, TR> semantic)
        => semantic.Call(Result, F, Arguments);
}

