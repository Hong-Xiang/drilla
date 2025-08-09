namespace DualDrill.CLSL.Language.Statement;

public sealed record class SetStatement<TV, TE, TM, TF>(TM Target, TV Source) : IStatement<TV, TE, TM, TF>
{
    public TR Evaluate<TX, TR>(IStatementSemantic<TX, TV, TE, TM, TF, TR> semantic, TX context)
        => semantic.Set(context, Target, Source);
}

