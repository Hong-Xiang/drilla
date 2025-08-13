namespace DualDrill.CLSL.Language.Statement;

public sealed record class GetStatement<TV, TE, TM, TF>(TV Result, TM Source) : IStatement<TV, TE, TM, TF>
{
    public TR Evaluate<TR>(IStatementSemantic<TV, TE, TM, TF, TR> semantic)
        => semantic.Get(Result, Source);
}

