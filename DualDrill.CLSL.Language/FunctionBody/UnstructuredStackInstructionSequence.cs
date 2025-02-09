using DualDrill.CLSL.Language.ControlFlow;
using DualDrill.CLSL.Language.Declaration;
using DualDrill.CLSL.Language.LinearInstruction;
using System.CodeDom.Compiler;
using System.Collections.Frozen;
using System.Collections.Immutable;

namespace DualDrill.CLSL.Language.FunctionBody;

public sealed class UnstructuredStackInstructionSequence
    : IFunctionBodyData
{
    public void Dump(IFunctionBody context, IndentedTextWriter writer)
    {
        throw new NotImplementedException();
    }

    public ImmutableArray<IStackInstruction> Instructions { get; }
    FrozenDictionary<Label, int> LabelInstructionIndices { get; }

    public IEnumerable<VariableDeclaration> LocalVariables => throw new NotImplementedException();

    public IEnumerable<Label> Labels => LabelInstructionIndices.Keys;

    public UnstructuredStackInstructionSequence(IEnumerable<IStackInstruction> instructions)
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
