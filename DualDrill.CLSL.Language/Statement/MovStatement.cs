namespace DualDrill.CLSL.Language.Statement;

public sealed record class MovStatement<TV, TE, TM, TF>(TM Target, TM Source) : IStatement<TV, TE, TM, TF>
{
    public TR Evaluate<TX, TR>(IStatementSemantic<TX, TV, TE, TM, TF, TR> semantic, TX context)
        => semantic.Mov(context, Target, Source);
}

