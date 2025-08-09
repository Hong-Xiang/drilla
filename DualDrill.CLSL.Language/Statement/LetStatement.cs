namespace DualDrill.CLSL.Language.Statement;

public sealed record class LetStatement<TV, TE, TM, TF>(TV Result, TE Expr) : IStatement<TV, TE, TM, TF>
{
    public TR Evaluate<TX, TR>(IStatementSemantic<TX, TV, TE, TM, TF, TR> semantic, TX context)
        => semantic.Let(context, Result, Expr);
}

