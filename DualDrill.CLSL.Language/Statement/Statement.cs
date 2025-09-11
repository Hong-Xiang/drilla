using DualDrill.CLSL.Language.Operation;
using DualDrill.Common;

namespace DualDrill.CLSL.Language.Statement;

[Obsolete]
public interface IStatementSemantic<in TV, in TE, in TM, in TF, out TO>
{
    TO Nop();
    TO Let(TV result, TE expr);
    TO Get(TV result, TM source);
    TO Set(TM target, TV source);
    TO Mov(TM target, TM source);
    TO Call(TV result, TF f, IReadOnlyList<TV> arguments);
    TO SetVecSwizzle(IVectorSwizzleSetOperation operation, TV target, TV value);
    TO Dup(TV result, TV source);
    TO Pop(TV target);
}

[Obsolete]
public interface IStatement<out TV, out TE, out TM, out TF>
{
    public TR Evaluate<TR>(IStatementSemantic<TV, TE, TM, TF, TR> semantic);
    public IStatement<TVR, TVE, TVM, TVF> Select<TVR, TVE, TVM, TVF>(Func<TV, TVR> fr, Func<TE, TVE> fe, Func<TM, TVM> fm, Func<TF, TVF> ff);
}

public static class Statement
{
    public static IStatementSemantic<TV, TE, TM, TF, IStatement<TV, TE, TM, TF>> Factory<TV, TE, TM, TF>()
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


