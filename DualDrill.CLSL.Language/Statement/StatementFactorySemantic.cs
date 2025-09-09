using DualDrill.CLSL.Language.Operation;

namespace DualDrill.CLSL.Language.Statement;

sealed class StatementFactorySemantic<TV, TE, TM, TF>
    : IStatementSemantic<TV, TE, TM, TF, IStatement<TV, TE, TM, TF>>
{
    public IStatement<TV, TE, TM, TF> Call(TV result, TF f, IReadOnlyList<TV> arguments)
        => new CallStatement<TV, TE, TM, TF>(result, f, arguments);

    public IStatement<TV, TE, TM, TF> Dup(TV result, TV source)
        => new DupStatement<TV, TE, TM, TF>(result, source);

    public IStatement<TV, TE, TM, TF> Get(TV result, TM source)
        => new GetStatement<TV, TE, TM, TF>(result, source);
    public IStatement<TV, TE, TM, TF> Let(TV result, TE expr)
        => new LetStatement<TV, TE, TM, TF>(result, expr);

    public IStatement<TV, TE, TM, TF> Mov(TM target, TM source)
        => new MovStatement<TV, TE, TM, TF>(target, source);

    public IStatement<TV, TE, TM, TF> Nop()
        => new NopStatement<TV, TE, TM, TF>();

    public IStatement<TV, TE, TM, TF> Pop(TV target)
        => new PopStatement<TV, TE, TM, TF>(target);

    public IStatement<TV, TE, TM, TF> Set(TM target, TV source)
        => new SetStatement<TV, TE, TM, TF>(target, source);

    public IStatement<TV, TE, TM, TF> SetVecSwizzle(IVectorSwizzleSetOperation operation, TV target, TV value)
        => new SetVectorSwizzleStatement<TV, TE, TM, TF>(operation, target, value);
}

