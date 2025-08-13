namespace DualDrill.CLSL.Language.Statement;

public sealed record class MovStatement<TV, TE, TM, TF>(TM Target, TM Source) : IStatement<TV, TE, TM, TF>
{
    public TR Evaluate<TR>(IStatementSemantic<TV, TE, TM, TF, TR> semantic)
        => semantic.Mov(Target, Source);
}

