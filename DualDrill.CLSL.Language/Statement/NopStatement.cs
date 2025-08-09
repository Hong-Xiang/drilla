namespace DualDrill.CLSL.Language.Statement;

public sealed record class NopStatement<TV, TE, TM, TF>() : IStatement<TV, TE, TM, TF>
{
    public TR Evaluate<TX, TR>(IStatementSemantic<TX, TV, TE, TM, TF, TR> semantic, TX context)
        => semantic.Nop(context);
}

