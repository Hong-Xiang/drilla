namespace DualDrill.CLSL.Language.Statement;

public sealed record class NopStatement<TV, TE, TM, TF>() : IStatement<TV, TE, TM, TF>
{
    public TR Evaluate<TR>(IStatementSemantic<TV, TE, TM, TF, TR> semantic)
        => semantic.Nop();
}

