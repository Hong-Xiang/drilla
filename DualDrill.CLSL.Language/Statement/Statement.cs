using DualDrill.CLSL.Language.Operation;
using DualDrill.Common;

namespace DualDrill.CLSL.Language.Statement;

public interface IStatementSemantic<in TX, in TV, in TE, in TM, in TF, out TO>
{
    TO Nop(TX context);
    TO Let(TX context, TV result, TE expr);
    TO Get(TX context, TV result, TM source);
    TO Set(TX context, TM target, TV source);
    TO Mov(TX context, TM target, TM source);
    TO Call(TX context, TV result, TF f, IReadOnlyList<TE> arguments);
    TO Dup(TX context, TV result, TV source);
    TO Pop(TX context, TV target);

    TO SetVecSwizzle(TX context, IVectorSwizzleSetOperation operation, TV target, TV value);
}

public interface IStatement<out TV, out TE, out TM, out TF>
{
    public TR Evaluate<TX, TR>(IStatementSemantic<TX, TV, TE, TM, TF, TR> semantic, TX context);
}

public static class Statement
{
    public static IStatementSemantic<Unit, TV, TE, TM, TF, IStatement<TV, TE, TM, TF>> Factory<TV, TE, TM, TF>()
        => new StatementFactorySemantic<TV, TE, TM, TF>();
}

public sealed class StatementSeqBuilder<TV, TE, TM, TF>
    where TV : new()
{
    public Seq<IStatement<TV, TE, TM, TF>, Unit> Nop() => Seq.Create<IStatement<TV, TE, TM, TF>, Unit>([
        new NopStatement<TV, TE, TM, TF>()
        ], default);

    public Seq<IStatement<TV, TE, TM, TF>, TV> Let(TE expr)
    {
        var value = new TV();
        return Seq.Create<IStatement<TV, TE, TM, TF>, TV>([
            new LetStatement<TV, TE, TM, TF>(value, expr)
        ], value);
    }
}
