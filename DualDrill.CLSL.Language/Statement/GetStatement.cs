namespace DualDrill.CLSL.Language.Statement;

public sealed record class GetStatement<TV, TE, TM, TF>(TV Result, TM Source) : IStatement<TV, TE, TM, TF>
{
    public TR Evaluate<TX, TR>(IStatementSemantic<TX, TV, TE, TM, TF, TR> semantic, TX context)
        => semantic.Get(context, Result, Source);
}

