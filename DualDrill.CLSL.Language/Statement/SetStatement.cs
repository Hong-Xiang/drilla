namespace DualDrill.CLSL.Language.Statement;

public sealed record class SetStatement<TV, TE, TM, TF>(TM Target, TV Source) : IStatement<TV, TE, TM, TF>
{
    public TR Evaluate<TR>(IStatementSemantic<TV, TE, TM, TF, TR> semantic)
        => semantic.Set(Target, Source);
}

