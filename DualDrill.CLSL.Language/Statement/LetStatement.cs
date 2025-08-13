namespace DualDrill.CLSL.Language.Statement;

public sealed record class LetStatement<TV, TE, TM, TF>(TV Result, TE Expr) : IStatement<TV, TE, TM, TF>
{
    public TR Evaluate<TR>(IStatementSemantic<TV, TE, TM, TF, TR> semantic)
        => semantic.Let(Result, Expr);
}

