using System.Reflection.Emit;
using System.Reflection.Metadata;
using DualDrill.CLSL.Language.ControlFlow;
using Lokad.ILPack.IL;
using Label = DualDrill.CLSL.Language.Symbol.Label;

namespace DualDrill.CLSL.Frontend;

public abstract record CilControlFlow
{
    public abstract ISuccessor ToSuccessor();

    public sealed record Return : CilControlFlow
    {
        public Return(CilInstructionInfo instruction)
        {
            RequireFlowControl(instruction, FlowControl.Return);
            Instruction = instruction;
        }

        public CilInstructionInfo Instruction { get; }

        public override ISuccessor ToSuccessor() => Successor.Terminate();
    }

    public sealed record Branch : CilControlFlow
    {
        public Branch(CilInstructionInfo instruction, Label target)
        {
            RequireFlowControl(instruction, FlowControl.Branch);
            Instruction = instruction;
            Target = target;
        }

        public CilInstructionInfo Instruction { get; }
        public Label Target { get; }

        public override ISuccessor ToSuccessor() => Successor.Unconditional(Target);
    }

    public sealed record ConditionalBranch : CilControlFlow
    {
        public ConditionalBranch(
            CilInstructionInfo instruction,
            Label branchTarget,
            Label fallThroughTarget)
        {
            RequireFlowControl(instruction, FlowControl.Cond_Branch);
            if (instruction.Instruction.OpCode.ToILOpCode() == ILOpCode.Switch)
                throw new NotSupportedException("CIL switch control is not supported.");

            Instruction = instruction;
            BranchTarget = branchTarget;
            FallThroughTarget = fallThroughTarget;
        }

        public CilInstructionInfo Instruction { get; }
        public Label BranchTarget { get; }
        public Label FallThroughTarget { get; }

        public override ISuccessor ToSuccessor()
        {
            return Instruction.Instruction.OpCode.ToILOpCode() switch
            {
                ILOpCode.Brfalse or ILOpCode.Brfalse_s =>
                    Successor.Conditional(FallThroughTarget, BranchTarget),
                _ => Successor.Conditional(BranchTarget, FallThroughTarget)
            };
        }
    }

    public sealed record FallThrough(Label Target) : CilControlFlow
    {
        public override ISuccessor ToSuccessor() => Successor.Unconditional(Target);
    }

    public sealed record EndOfCode : CilControlFlow
    {
        public override ISuccessor ToSuccessor() => Successor.Terminate();
    }

    private static void RequireFlowControl(CilInstructionInfo instruction, FlowControl expected)
    {
        if (instruction.Instruction.OpCode.FlowControl != expected)
            throw new ArgumentException(
                $"Expected {expected} CIL control at IL_{instruction.ByteOffset:X4}, " +
                $"got {instruction.Instruction.OpCode.FlowControl}.",
                nameof(instruction));
    }
}
