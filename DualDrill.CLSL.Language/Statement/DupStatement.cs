namespace DualDrill.CLSL.Language.Statement;

public sealed record class DupStatement<TV, TE, TM, TF>(TV Result, TV Source) : IStatement<TV, TE, TM, TF>
{
    public TR Evaluate<TR>(IStatementSemantic<TV, TE, TM, TF, TR> semantic)
        => semantic.Dup(Result, Source);
}

