namespace DualDrill.CLSL.Language.Statement;

public sealed record class CallStatement<TV, TE, TM, TF>(TV Result, TF F, IReadOnlyList<TE> Arguments) : IStatement<TV, TE, TM, TF>
{
    public TR Evaluate<TX, TR>(IStatementSemantic<TX, TV, TE, TM, TF, TR> semantic, TX context)
        => semantic.Call(context, Result, F, Arguments);
}

