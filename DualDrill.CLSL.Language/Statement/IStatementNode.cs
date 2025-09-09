using DualDrill.CLSL.Language.Operation;
using System.Collections.Immutable;
using System.Net.Http.Headers;

namespace DualDrill.CLSL.Language.Statement;

public interface IStatementNode<TV, TE>
{
    IStatementOperation Operation { get; }
    int OprandCount { get; }
    TV this[int index] { get; }
    IStatementNode<TR, TE> Select<TR>(Func<TV, TR> f);
    TR Evaluate<TS, TR>(TS semantic) where TS : IStatementSemantic<TV, TE, TR>;
    TE? Binding { get; }
}

public interface IStatementSemantic<in TI, in TE, out TO>
{
    TO Nop(NopOperation op);
    TO Let(TI value, TE expr);
}

sealed record class Stmt<TV, TE>(
    IStatementOperation Operation,
    ImmutableArray<TV> Operands
) : IStatementNode<TV, TE>
{
    public int OprandCount => Operands.Length;
    public TE? Binding => default;
    public TV this[int index] => Operands[index];
    public IStatementNode<TR, TE> Select<TR>(Func<TV, TR> f)
        => new Stmt<TR, TE>(Operation, [.. Operands.Select(f)]);
    public TR Evaluate<TS, TR>(TS semantic) where TS : IStatementSemantic<TV, TE, TR>
        => Operation.Evaluate<TV, TE, TS, TR>(this, semantic);
}

public static class StatementNode
{
}
