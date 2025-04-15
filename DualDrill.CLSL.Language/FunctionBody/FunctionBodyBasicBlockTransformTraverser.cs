using DualDrill.CLSL.Language.ControlFlow;
using DualDrill.CLSL.Language.ControlFlowGraph;

namespace DualDrill.CLSL.Language.FunctionBody;

internal sealed class FunctionBodyBasicBlockTransformTraverser<TSourceBasicBlock, TResultBasicBlock>(
    IBasicBlockTransform<TSourceBasicBlock, TResultBasicBlock> basicBlockTransform
)
    : IControlFlowElementSequenceTraverser<
        TSourceBasicBlock,
        IStructuredControlFlowElement,
        StructuredControlFlowElementSequence>
    where TSourceBasicBlock : IBasicBlock2
    where TResultBasicBlock : IBasicBlock2
{
    public IStructuredControlFlowElement VisitBlock(Block block)
        => new Block(block.Label, block.Body.Traverse(this));

    public IStructuredControlFlowElement VisitLoop(Loop loop)
        => new Loop(loop.Label, loop.Body.Traverse(this));

    public IStructuredControlFlowElement VisitIfThenElse(IfThenElse ifThenElse)
        => new IfThenElse(
            ifThenElse.TrueBody.Traverse(this),
            ifThenElse.FalseBody.Traverse(this)
        );

    public IStructuredControlFlowElement VisitBasicBlock(TSourceBasicBlock basicBlock)
        => basicBlockTransform.Apply(basicBlock);

    public StructuredControlFlowElementSequence MergeSequenceResult(IEnumerable<IStructuredControlFlowElement> elements)
        => throw new NotImplementedException();
}