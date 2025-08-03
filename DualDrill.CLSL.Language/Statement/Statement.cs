using DualDrill.CLSL.Language.Symbol;
using DualDrill.Common;

namespace DualDrill.CLSL.Language.Statement;

public interface IStatementSemantic<in TX, in TV, in TE, in TM, in TF, out TO>
{
    TO Nop(TX context);
    TO Let(TX context, TV result, TE expr);
    TO Get(TX context, TV result, TM source);
    TO Set(TX context, TM target, TV source);
    TO Mov(TX context, TM target, TM source);
    TO Dup(TX context, TV result, TV source);
    TO Pop(TX context, TV target);
    TO Call(TX context, TV result, TF f, IReadOnlyList<TE> arguments);
}

public interface IStatement<out TV, out TE, out TM, out TF>
{
    public TR Evaluate<TX, TR>(IStatementSemantic<TX, TV, TE, TM, TF, TR> semantic, TX context);
}

public sealed record class GetStatement<TV, TE, TM, TF>(TV Result, TM Source) : IStatement<TV, TE, TM, TF>
{
    public TR Evaluate<TX, TR>(IStatementSemantic<TX, TV, TE, TM, TF, TR> semantic, TX context)
        => semantic.Get(context, Result, Source);
}

public static class Statement
{
    public static IStatementSemantic<Unit, TV, TE, TM, TF, IStatement<TV, TE, TM, TF>> Factory<TV, TE, TM, TF>()
    {
        throw new NotImplementedException();
    }

    public static Seq<IStatement<TV, TE, TM, TF>, TV> ToSeq<TV, TE, TM, TF>(
        this IStatement<TV, TE, TM, TF> stmt
    )
        where TV : new()
    {
        throw new NotImplementedException();
    }
}
