using DualDrill.CLSL.Language.ControlFlow;
using DualDrill.CLSL.Language.LinearInstruction;
using System.CodeDom.Compiler;

namespace DualDrill.CLSL.Language.FunctionBody;

public sealed record class ControlFlowGraphFunctionBody(
    ControlFlowGraph<BasicBlock<IStructuredStackInstruction>> Graph
) : IFunctionBody
{
    public void Dump(IndentedTextWriter writer)
    {
        throw new NotImplementedException();
    }
}
