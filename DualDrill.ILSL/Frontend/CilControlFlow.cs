using System.Collections.Immutable;
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
            RequireSupportedOpCode(instruction, IsReturn, "return");
            Instruction = instruction;
        }

        public CilInstructionInfo Instruction { get; }

        public override ISuccessor ToSuccessor() => Successor.Terminate();
    }

    public sealed record Branch : CilControlFlow
    {
        public Branch(CilInstructionInfo instruction, Label target)
        {
            RequireSupportedOpCode(instruction, IsUnconditionalBranch, "unconditional branch");
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
            RequireSupportedOpCode(instruction, IsConditionalBranch, "conditional branch");
            Instruction = instruction;
            BranchTarget = branchTarget;
            FallThroughTarget = fallThroughTarget;
        }

        public CilInstructionInfo Instruction { get; }
        public Label BranchTarget { get; }
        public Label FallThroughTarget { get; }

        public override ISuccessor ToSuccessor() => Successor.Conditional(BranchTarget, FallThroughTarget);
    }

    public sealed record Switch : CilControlFlow
    {
        public Switch(
            CilInstructionInfo instruction,
            ImmutableArray<Label> caseTargets,
            Label defaultTarget)
        {
            RequireSupportedOpCode(instruction, IsSwitch, "switch");
            if (caseTargets.IsDefault)
                throw new ArgumentException("Switch case targets must be initialized.", nameof(caseTargets));
            Instruction = instruction;
            CaseTargets = caseTargets;
            DefaultTarget = defaultTarget;
        }

        public CilInstructionInfo Instruction { get; }
        public ImmutableArray<Label> CaseTargets { get; }
        public Label DefaultTarget { get; }

        public override ISuccessor ToSuccessor() =>
            Successor.Switch(CaseTargets, DefaultTarget);
    }

    public sealed record FallThrough(Label Target) : CilControlFlow
    {
        public override ISuccessor ToSuccessor() => Successor.Unconditional(Target);
    }

    public sealed record EndOfCode : CilControlFlow
    {
        public override ISuccessor ToSuccessor() => Successor.Terminate();
    }

    internal static bool IsReturn(ILOpCode opCode) => opCode == ILOpCode.Ret;

    internal static bool IsUnconditionalBranch(ILOpCode opCode) =>
        opCode is ILOpCode.Br or ILOpCode.Br_s;

    internal static bool IsSwitch(ILOpCode opCode) => opCode is ILOpCode.Switch;

    internal static bool IsConditionalBranch(ILOpCode opCode) =>
        opCode is
            ILOpCode.Brfalse or ILOpCode.Brfalse_s or
            ILOpCode.Brtrue or ILOpCode.Brtrue_s or
            ILOpCode.Beq or ILOpCode.Beq_s or
            ILOpCode.Bge or ILOpCode.Bge_s or
            ILOpCode.Bge_un or ILOpCode.Bge_un_s or
            ILOpCode.Bgt or ILOpCode.Bgt_s or
            ILOpCode.Bgt_un or ILOpCode.Bgt_un_s or
            ILOpCode.Ble or ILOpCode.Ble_s or
            ILOpCode.Ble_un or ILOpCode.Ble_un_s or
            ILOpCode.Blt or ILOpCode.Blt_s or
            ILOpCode.Blt_un or ILOpCode.Blt_un_s or
            ILOpCode.Bne_un or ILOpCode.Bne_un_s;

    private static void RequireSupportedOpCode(
        CilInstructionInfo instruction,
        Func<ILOpCode, bool> isSupported,
        string family)
    {
        var opCode = instruction.Instruction.OpCode.ToILOpCode();
        if (!isSupported(opCode))
            throw new ArgumentException(
                $"Expected a supported CIL {family} at IL_{instruction.ByteOffset:X4}, got {opCode}.",
                nameof(instruction));
    }
}
