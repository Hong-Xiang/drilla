using System.Collections.Immutable;
using DualDrill.CLSL.Language.ControlFlow;
using DualDrill.CLSL.Language.Value;
using DualDrill.CLSL.Language.ValueInstruction;
using LLVMSharp;

namespace DualDrill.CLSL.Compiler;

public static class StackBasicBlockToValueBasicBlock
{
    public static ValueInstructionBasicBlock ToValueInstructionBasicBlock(this StackInstructionBasicBlock basicBlock)
    {
        Stack<IValue> stack = [];
        ImmutableArray<IBlockArgumentValue> inputs = [..basicBlock.Inputs.Select(v => v.CreateBlockArgumentValue())];
        foreach (var v in inputs)
        {
            stack.Push(v);
        }

        List<IValueInstruction> instructions = [];
        foreach (var e in basicBlock.Elements)
        {
            instructions.AddRange(e.CreateValueInstruction(stack));
        }

        ImmutableArray<IValue> outputs = [..stack.Reverse()];

        return new ValueInstructionBasicBlock(
            basicBlock.Label,
            [..instructions],
            inputs,
            outputs
        );
    }
}