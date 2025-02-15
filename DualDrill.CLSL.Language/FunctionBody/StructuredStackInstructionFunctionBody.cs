using DualDrill.CLSL.Language.ControlFlow;
using DualDrill.CLSL.Language.Declaration;
using DualDrill.CLSL.Language.LinearInstruction;
using System.CodeDom.Compiler;
using DualDrill.CLSL.Language.ControlFlowGraph;

namespace DualDrill.CLSL.Language.FunctionBody;

public sealed record class StructuredStackInstructionFunctionBody(
    IStructuredControlFlowRegion<IStructuredStackInstruction> Root
) : IFunctionBodyData
{
    public IEnumerable<VariableDeclaration> LocalVariables => Root.ReferencedLocalVariables;

    public IEnumerable<Label> Labels => Root.ReferencedLabels;

    public void Dump(IFunctionBody context, IndentedTextWriter writer)
    {
        throw new NotImplementedException();
    }
}