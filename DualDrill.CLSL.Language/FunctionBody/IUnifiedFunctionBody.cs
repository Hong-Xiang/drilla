using DualDrill.CLSL.Language.ControlFlow;

namespace DualDrill.CLSL.Language.FunctionBody;

public interface IUnifiedFunctionBody<TBasicBlock> : IFunctionBody
    where TBasicBlock : IBasicBlock2
{
    IUnifiedFunctionBody<TResultBasicBlock> ApplyTransform<TResultBasicBlock>(
        IBasicBlockTransform<TBasicBlock, TResultBasicBlock> transform)
        where TResultBasicBlock : IBasicBlock2;

    TResult Traverse<TElementResult, TResult>(
        IControlFlowElementSequenceTraverser<TBasicBlock, TElementResult, TResult> traverser);

    public Label Entry { get; }
    public TBasicBlock this[Label label] { get; }
    public ISuccessor Successor(Label label);
}