using DualDrill.CLSL.Language.Expression;
using DualDrill.CLSL.Language.Statement;
using DualDrill.CLSL.Language.Symbol;
using DualDrill.Common;

namespace DualDrill.CLSL.Language;

public sealed record class StackInstructionBasicBlock<TM, TF>(
    Seq<IStatement<Unit, IExpression<Unit>, TM, TF>, ITerminator<Label, Unit>> Body
)
{
}

public static class StackInstructionBasicBlock
{
    public static StackInstructionBasicBlock<TM, TF> Create<TM, TF>(
        Func<IStatementSemantic<Unit, Unit, IExpression<Unit>, TM, TF, IStatement<Unit, IExpression<Unit>, TM, TF>>, IEnumerable<IStatement<Unit, IExpression<Unit>, TM, TF>>> makeStatements,
        Func<ITerminatorSemantic<Unit, Label, Unit, ITerminator<Label, Unit>>,
             ITerminator<Label, Unit>> makeTerminator
    )
    {
        var sf = Statement.Statement.Factory<Unit, IExpression<Unit>, TM, TF>();
        var tf = Terminator.Factory<Label, Unit>();
        return new(Seq.Create([.. makeStatements(sf)], makeTerminator(tf)));
    }
}
