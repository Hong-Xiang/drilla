using DualDrill.CLSL.Language.ControlFlow;
using DualDrill.CLSL.Language.Declaration;
using DualDrill.CLSL.Language.LinearInstruction;
using System.CodeDom.Compiler;
using System.Collections.Immutable;
using DualDrill.CLSL.Language.ControlFlowGraph;

namespace DualDrill.CLSL.Language.FunctionBody;

// TODO: refactor to simply FunctionBody<IStructuredControlFlowRegion<IStructuredStackInstruction>>
public sealed class StructuredStackInstructionFunctionBody : IFunctionBody
{
    public IStructuredControlFlowRegion<IStructuredStackInstruction> Root { get; }

    public StructuredStackInstructionFunctionBody(IStructuredControlFlowRegion<IStructuredStackInstruction> root)
    {
        Root = root;
        Labels = [..Root.ReferencedLabels.Distinct()];
        LocalVariables = [..Root.ReferencedLocalVariables.Distinct()];
    }

    public IEnumerable<VariableDeclaration> FunctionBodyDataLocalVariables => Root.ReferencedLocalVariables;

    public IEnumerable<Label> FunctionBodyDataLabels => Root.ReferencedLabels;

    public void Dump(IFunctionBody context, IndentedTextWriter writer)
    {
        throw new NotImplementedException();
    }

    public int LabelIndex(Label label)
    {
        throw new NotImplementedException();
    }

    public int VariableIndex(VariableDeclaration variable)
    {
        throw new NotImplementedException();
    }

    public ImmutableArray<VariableDeclaration> LocalVariables { get; }
    public ImmutableArray<Label> Labels { get; }
}