using DualDrill.CLSL.Language.ControlFlowGraph;
using DualDrill.CLSL.LinearInstruction;
using System.CodeDom.Compiler;

namespace DualDrill.CLSL.Language.FunctionBody;

public sealed record class StructuredStackInstructionFunctionBody(
    IStructuredControlFlowRegion<IStructuredStackInstruction> Root
) : IFunctionBody
{
    public void Dump(IndentedTextWriter writer)
    {
        throw new NotImplementedException();
    }
}
