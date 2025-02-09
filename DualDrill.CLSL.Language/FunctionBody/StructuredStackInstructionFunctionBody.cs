using DualDrill.CLSL.Language.ControlFlow;
using DualDrill.CLSL.Language.ControlFlowGraph;
using DualDrill.CLSL.Language.Declaration;
using DualDrill.CLSL.Language.LinearInstruction;
using System.CodeDom.Compiler;

namespace DualDrill.CLSL.Language.FunctionBody;

public sealed record class StructuredStackInstructionFunctionBody(
    IStructuredControlFlowRegion<IStructuredStackInstruction> Root
) : IFunctionBodyData
{
    public IEnumerable<VariableDeclaration> LocalVariables => throw new NotImplementedException();

    public IEnumerable<Label> Labels => throw new NotImplementedException();

    sealed class LabelVisitor : IStructuredControlFlowRegion<IStructuredStackInstruction>.IRegionVisitor<IEnumerable<Label>>
    {
        public IEnumerable<Label> VisitBlock(Block<IStructuredStackInstruction> block)
        {
            yield return block.Label;
        }

        public IEnumerable<Label> VisitIfThenElse(IfThenElse<IStructuredStackInstruction> ifThenElse)
        {
            foreach (var l in ifThenElse.TrueBlock.AcceptRegionVisitor(this))
            {
                yield return l;
            }
            foreach (var l in ifThenElse.FalseBlock.AcceptRegionVisitor(this))
            {
                yield return l;
            }
        }

        public IEnumerable<Label> VisitLoop(Loop<IStructuredStackInstruction> loop)
        {
            yield return loop.Label;
            foreach (var l in loop.Body.AcceptRegionVisitor(this))
            {
                yield return l;
            }
        }
    }

    public void Dump(IFunctionBody context, IndentedTextWriter writer)
    {
        throw new NotImplementedException();
    }
}
