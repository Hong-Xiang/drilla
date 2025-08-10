using DualDrill.CLSL.Language.Operation;

namespace DualDrill.CLSL.Language.Statement;

public sealed record class SetVectorSwizzleStatement<TV, TE, TM, TF>(IVectorSwizzleSetOperation Operation, TV Target, TV Value)
    : IStatement<TV, TE, TM, TF>
{
    public TR Evaluate<TX, TR>(IStatementSemantic<TX, TV, TE, TM, TF, TR> semantic, TX context)
        => semantic.SetVecSwizzle(context, Operation, Target, Value);
}

