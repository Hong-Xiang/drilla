namespace DualDrill.CLSL.Language.Statement;

public sealed record class PopStatement<TV, TE, TM, TF>(TV Target) : IStatement<TV, TE, TM, TF>
{
    public TR Evaluate<TR>(IStatementSemantic<TV, TE, TM, TF, TR> semantic)
        => semantic.Pop(Target);
}

