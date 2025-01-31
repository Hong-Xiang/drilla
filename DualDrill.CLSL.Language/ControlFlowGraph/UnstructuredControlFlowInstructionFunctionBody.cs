using DualDrill.CLSL.Language.Declaration;
using DualDrill.CLSL.LinearInstruction;
using System.CodeDom.Compiler;
using System.Collections.Frozen;
using System.Collections.Immutable;

namespace DualDrill.CLSL.Language.ControlFlowGraph;

public sealed class UnstructuredControlFlowInstructionFunctionBody
    : IFunctionBody
{
    public void Dump(IndentedTextWriter writer)
    {
        throw new NotImplementedException();
    }

    public ImmutableArray<IStackInstruction> Instructions { get; }
    FrozenDictionary<Label, int> LabelInstructionIndices { get; }

    public UnstructuredControlFlowInstructionFunctionBody(
        IEnumerable<IStackInstruction> instructions)
    {
        Instructions = [.. instructions];
        Dictionary<Label, int> labelInstructionIndices = [];
        foreach (var (index, inst) in Instructions.Index())
        {
            if (inst is LabelInstruction l)
            {
                labelInstructionIndices.Add(l.Label, index);
            }
        }
        LabelInstructionIndices = labelInstructionIndices.ToFrozenDictionary();
    }
    public int this[Label label] => LabelInstructionIndices[label];
}
