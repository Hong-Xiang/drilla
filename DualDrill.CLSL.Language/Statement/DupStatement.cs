namespace DualDrill.CLSL.Language.Statement;

public sealed record class DupStatement<TV, TE, TM, TF>(TV Result, TV Source) : IStatement<TV, TE, TM, TF>
{
    public TR Evaluate<TX, TR>(IStatementSemantic<TX, TV, TE, TM, TF, TR> semantic, TX context)
        => semantic.Dup(context, Result, Source);
}

