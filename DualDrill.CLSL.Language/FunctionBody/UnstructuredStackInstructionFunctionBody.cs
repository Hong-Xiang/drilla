using DualDrill.CLSL.Language.ControlFlow;
using DualDrill.CLSL.Language.LinearInstruction;
using System.CodeDom.Compiler;
using System.Collections.Frozen;
using System.Collections.Immutable;

namespace DualDrill.CLSL.Language.FunctionBody;

public sealed class UnstructuredStackInstructionFunctionBody
    : IFunctionBody
{
    public void Dump(IndentedTextWriter writer)
    {
        throw new NotImplementedException();
    }

    public ImmutableArray<IStackInstruction> Instructions { get; }
    FrozenDictionary<Label, int> LabelInstructionIndices { get; }

    public UnstructuredStackInstructionFunctionBody(IEnumerable<IStackInstruction> instructions)
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
