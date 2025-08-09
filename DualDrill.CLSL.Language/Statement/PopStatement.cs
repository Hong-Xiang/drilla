namespace DualDrill.CLSL.Language.Statement;

public sealed record class PopStatement<TV, TE, TM, TF>(TV Target) : IStatement<TV, TE, TM, TF>
{
    public TR Evaluate<TX, TR>(IStatementSemantic<TX, TV, TE, TM, TF, TR> semantic, TX context)
        => semantic.Pop(context, Target);
}

