using System.CodeDom.Compiler;
using System.Collections.Immutable;
using DualDrill.CLSL.Language.Declaration;
using DualDrill.CLSL.Language.FunctionBody;
using DualDrill.CLSL.Language.Symbol;
using DualDrill.Common.CodeTextWriter;

namespace DualDrill.CLSL.Language.ControlFlow;

// ir element algebra
// Element = BasicBlock | IfThenElse | Block | Loop
// Block = Block(Sequence)
// Loop = Loop(Sequence)
// IfThenElse = IfThenElse(Sequence, Sequence)
// Sequence = Element[]

interface IFunctionBodyElementAlgebra<
    in TBasicBlock,
    TElement,
    TSequence>
{
    TElement BasicBlock(TBasicBlock basicBlock);
    TElement IfThenElse(TSequence trueBody, TSequence falseBody);
    TElement Block(TSequence body);
    TElement Loop(TSequence body);
    TSequence Sequence(ImmutableArray<TElement> elements);
}

interface FixT<TBasicBlock, T>
    where T : FixT<TBasicBlock, T>
{
}

public interface IStructuredControlFlowElement : IDeclarationUser
{
    // public IStructuredControlFlowElement ApplyTransform<TSourceBasicBlock, TResultBasicBlock>(
    //     IBasicBlockTransform<TSourceBasicBlock, TResultBasicBlock> transform)
    //     where TSourceBasicBlock : IBasicBlock2
    //     where TResultBasicBlock : IBasicBlock2;
    // IEnumerable<Label> ExternalBranchTargets { get; }
}

public interface IControlFlowElementVisitor<in TBasicBlock, out TResult>
    where TBasicBlock : IBasicBlock2
{
    TResult VisitBlock(Block block);
    TResult VisitLoop(Loop loop);
    TResult VisitIfThenElse(IfThenElse ifThenElse);
    TResult VisitBasicBlock(TBasicBlock basicBlock);
}

public interface IControlFlowElementSequenceTraverser<in TBasicBlock, TElementResult, out TSequenceResult>
    : IControlFlowElementVisitor<TBasicBlock, TElementResult>
    where TBasicBlock : IBasicBlock2
{
    TSequenceResult MergeSequenceResult(IEnumerable<TElementResult> elements);
}

public interface IControlFlowElement<TSelf> : IStructuredControlFlowElement
    where TSelf : IControlFlowElement<TSelf>
{
    public TSelf ApplyTransform<TSourceBasicBlock, TResultBasicBlock>(
        IBasicBlockTransform<TSourceBasicBlock, TResultBasicBlock> transform)
        where TSourceBasicBlock : IBasicBlock2
        where TResultBasicBlock : IBasicBlock2;
}

public readonly record struct StructuredControlFlowElementSequence(
    ImmutableArray<IStructuredControlFlowElement> Elements)
    : ITextDumpable<ILocalDeclarationContext>
{
    public IEnumerable<Label> Labels => Elements.SelectMany(e => e.ReferencedLabels);
    public IEnumerable<VariableDeclaration> LocalVariables => Elements.SelectMany(e => e.ReferencedLocalVariables);
    public IEnumerable<IShaderValue> LocalValues => Elements.SelectMany(e => e.ReferencedValues);

    public StructuredControlFlowElementSequence ApplyTransform<TSourceBasicBlock, TResultBasicBlock>(
        IBasicBlockTransform<TSourceBasicBlock, TResultBasicBlock> transform)
        where TSourceBasicBlock : IBasicBlock2 where TResultBasicBlock : IBasicBlock2
    {
        return new([
            .. Elements.Select(e => e switch
            {
                Loop l => (IStructuredControlFlowElement)l.ApplyTransform(transform),
                Block b => b.ApplyTransform(transform),
                IfThenElse ifThenElse => ifThenElse.ApplyTransform(transform),
                TSourceBasicBlock b => transform.Apply(b),
                _ => throw new NotSupportedException(),
            })
        ]);
    }

    public void Dump(ILocalDeclarationContext context, IndentedTextWriter writer)
    {
        foreach (var element in Elements)
        {
            element.Dump(context, writer);
        }
    }

    public TResult Traverse<TSourceBasicBlock, TElementResult, TResult>(
        IControlFlowElementSequenceTraverser<TSourceBasicBlock, TElementResult, TResult> traverser)
        where TSourceBasicBlock : IBasicBlock2
    {
        return traverser.MergeSequenceResult(
            Elements.Select(e => e switch
            {
                Loop l => traverser.VisitLoop(l),
                Block b => traverser.VisitBlock(b),
                IfThenElse ifThenElse => traverser.VisitIfThenElse(ifThenElse),
                TSourceBasicBlock b => traverser.VisitBasicBlock(b),
                _ => throw new NotSupportedException(),
            })
        );
    }
}

public static class StructuredControlFlowElementSequenceExtension
{
    public static IEnumerable<TBasicBlock> GetBasicBlocks<TBasicBlock>(
        this StructuredControlFlowElementSequence sequence)
        where TBasicBlock : IBasicBlock2
        => sequence.Traverse(new GetBasicBlocksTraverser<TBasicBlock>());
}

internal sealed class GetBasicBlocksTraverser<TBasicBlock>
    : IControlFlowElementSequenceTraverser<TBasicBlock, IEnumerable<TBasicBlock>, IEnumerable<TBasicBlock>>
    where TBasicBlock : IBasicBlock2
{
    public IEnumerable<TBasicBlock> VisitBlock(Block block)
        => block.Body.Traverse(this);

    public IEnumerable<TBasicBlock> VisitLoop(Loop loop)
        => loop.Body.Traverse(this);

    public IEnumerable<TBasicBlock> VisitIfThenElse(IfThenElse ifThenElse)
        => [..ifThenElse.TrueBody.Traverse(this), ..ifThenElse.FalseBody.Traverse(this)];

    public IEnumerable<TBasicBlock> VisitBasicBlock(TBasicBlock basicBlock)
        => [basicBlock];

    public IEnumerable<TBasicBlock> MergeSequenceResult(IEnumerable<IEnumerable<TBasicBlock>> elements)
        => elements.SelectMany(result => result);
}