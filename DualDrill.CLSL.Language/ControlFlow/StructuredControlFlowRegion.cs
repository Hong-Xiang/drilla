using DualDrill.CLSL.Language.AbstractSyntaxTree.Statement;
using DualDrill.CLSL.Language.Analysis;
using DualDrill.CLSL.Language.ControlFlow;
using DualDrill.CLSL.Language.Symbol;

namespace DualDrill.CLSL.Language.ControlFlowGraph;

/// <summary>
/// Encoding of Loop | Block | IfThenElse
/// </summary>
[Obsolete]
public interface IStructuredControlFlowRegion : IStructuredControlFlowElement
{
    sealed class FuncVisitor<TResult>(
        Func<Block, TResult> Block,
        Func<Loop, TResult> Loop,
        Func<IfThenElse, TResult> IfThenElse
    ) : IRegionPatternVisitor<TResult>
    {
        public TResult VisitBlock(Block block) => Block(block);

        public TResult VisitIfThenElse(IfThenElse ifThenElse)
            => IfThenElse(ifThenElse);

        public TResult VisitLoop(Loop loop)
            => Loop(loop);
    }

    public interface IRegionPatternVisitor<TResult>
    {
        TResult VisitBlock(Block block);
        TResult VisitLoop(Loop loop);
        TResult VisitIfThenElse(IfThenElse ifThenElse);

        public static IRegionPatternVisitor<TResult> Create(
            Func<Block, TResult> block,
            Func<Loop, TResult> loop,
            Func<IfThenElse, TResult> ifThenElse
        ) => new FuncVisitor<TResult>(block, loop, ifThenElse);
    }

    public TResult Accept<TResult>(IRegionPatternVisitor<TResult> pattern);
}

/// <summary>
/// Encoding of Block | Loop
/// </summary>
/// <typeparam name="TInstruction"></typeparam>
[Obsolete]
public interface ILabeledStructuredControlFlowRegion
    : IStructuredControlFlowRegion
    , ILabeledEntity
{
    IStatement BrCurrentStatement();
}

