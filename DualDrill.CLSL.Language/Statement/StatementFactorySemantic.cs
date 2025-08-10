using DualDrill.CLSL.Language.Operation;
using DualDrill.Common;

namespace DualDrill.CLSL.Language.Statement;

sealed class StatementFactorySemantic<TV, TE, TM, TF>
    : IStatementSemantic<Unit, TV, TE, TM, TF, IStatement<TV, TE, TM, TF>>
{
    public IStatement<TV, TE, TM, TF> Call(Unit context, TV result, TF f, IReadOnlyList<TE> arguments)
        => new CallStatement<TV, TE, TM, TF>(result, f, arguments);

    public IStatement<TV, TE, TM, TF> Dup(Unit context, TV result, TV source)
        => new DupStatement<TV, TE, TM, TF>(result, source);

    public IStatement<TV, TE, TM, TF> Get(Unit context, TV result, TM source)
        => new GetStatement<TV, TE, TM, TF>(result, source);
    public IStatement<TV, TE, TM, TF> Let(Unit context, TV result, TE expr)
        => new LetStatement<TV, TE, TM, TF>(result, expr);

    public IStatement<TV, TE, TM, TF> Mov(Unit context, TM target, TM source)
        => new MovStatement<TV, TE, TM, TF>(target, source);

    public IStatement<TV, TE, TM, TF> Nop(Unit context)
        => new NopStatement<TV, TE, TM, TF>();

    public IStatement<TV, TE, TM, TF> Pop(Unit context, TV target)
        => new PopStatement<TV, TE, TM, TF>(target);

    public IStatement<TV, TE, TM, TF> Set(Unit context, TM target, TV source)
        => new SetStatement<TV, TE, TM, TF>(target, source);

    public IStatement<TV, TE, TM, TF> SetVecSwizzle(Unit context, IVectorSwizzleSetOperation operation, TV target, TV value)
        => new SetVectorSwizzleStatement<TV, TE, TM, TF>(operation, target, value);
}

