using DualDrill.CLSL.Language.ControlFlowGraph;
using DualDrill.CLSL.Language.Declaration;
using DualDrill.CLSL.Language.LinearInstruction;
using System.CodeDom.Compiler;
using System.Collections.Immutable;

namespace DualDrill.CLSL.Language.ControlFlow;

public sealed class Block<TInstruction>(
    Label Label,
    ImmutableArray<Block<TInstruction>.IElement> Body
) : ILabeledEntity
  , ILabeledStructuredControlFlowRegion<TInstruction>
    where TInstruction : IInstruction
{
    /// <summary>
    /// Encoding of Loop | Block | IfThenElse | BasicBlock
    /// </summary>
    public interface IElement
    {
        public interface IElementVisitor<TResult>
        {
            TResult VisitBlock(Block<TInstruction> block);
            TResult VisitLoop(Loop<TInstruction> loop);
            TResult VisitIfThenElse(IfThenElse<TInstruction> ifThenElse);
            TResult VisitBasicBlock(BasicBlock<TInstruction> basicBlock);
        }
        public TResult AcceptElementVisitor<TResult>(IElementVisitor<TResult> visitor);
    }

    public Label Label { get; } = Label;
    public ImmutableArray<IElement> Body { get; } = Body;

    public IEnumerable<TInstruction> Instructions => from e in Body
                                                     from i in (e switch
                                                     {
                                                         BasicBlock<TInstruction> bb => bb.Instructions.ToArray(),
                                                         IStructuredControlFlowRegion<TInstruction> r => r.Instructions,
                                                         _ => throw new NotSupportedException()
                                                     })
                                                     select i;

    public IEnumerable<Label> Labels => [Label, ..from e in Body
                                                  from l in (e switch
                                                     {
                                                         BasicBlock<TInstruction> bb => [],
                                                         IStructuredControlFlowRegion<TInstruction> r => r.Labels,
                                                         _ => throw new NotSupportedException()
                                                     })
                                                  select l];

    public IEnumerable<VariableDeclaration> LocalVariables =>
        from e in Body
        from v in (e switch
        {
            BasicBlock<TInstruction> bb => bb.Instructions.ToArray().SelectMany(x => x.ReferencedLocalVariable),
            IStructuredControlFlowRegion<TInstruction> r => r.LocalVariables,
            _ => throw new NotSupportedException()
        })
        select v;


    public TResult AcceptRegionVisitor<TResult>(IStructuredControlFlowRegion<TInstruction>.IRegionVisitor<TResult> visitor)
        => visitor.VisitBlock(this);

    public static Block<TInstruction> Empty() => new Block<TInstruction>(Label.Create(), []);
    public static Block<TInstruction> Create(ReadOnlySpan<BasicBlock<TInstruction>> body) => new Block<TInstruction>(Label.Create(), [.. body]);

    public TResult AcceptElementVisitor<TResult>(IElement.IElementVisitor<TResult> visitor) => visitor.VisitBlock(this);
}
