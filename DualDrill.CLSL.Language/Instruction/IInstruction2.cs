using DualDrill.CLSL.Language.Operation;
using System.Collections.Immutable;

namespace DualDrill.CLSL.Language.Instruction;

public readonly record struct Instruction2<TV, TR>(
    IOperation Operation,
    int OperandCount,
    TR? Result,
    TV? Operand0,
    TV? Operand1,
    ImmutableArray<TV> Operands
)
{
    public TV? this[int index] =>
        index < OperandCount ?
        index switch
        {
            0 => Operand0,
            1 => Operand1,
            _ => index - 2 < Operands.Length ? Operands[index - 2] : default,
        } : throw new IndexOutOfRangeException($"Accessing {index} operand while instruction has {OperandCount} operands");
}
