using System.Collections.Frozen;
using System.Collections.Immutable;
using System.Reflection;
using System.Reflection.Emit;
using System.Reflection.Metadata;
using Lokad.ILPack.IL;

namespace DualDrill.CLSL.Frontend;

public sealed class CilMethodEnvironment
{
    internal CilMethodEnvironment(
        MethodBase method,
        MethodBody? body,
        ImmutableArray<ParameterInfo> parameters,
        ImmutableArray<LocalVariableInfo> localVariables,
        ImmutableArray<int> offsets)
    {
        Method = method;
        Body = body;
        Parameters = parameters;
        LocalVariables = localVariables;
        Offsets = offsets;
        OffsetsToIndex = offsets.Index().ToFrozenDictionary(item => item.Item, item => item.Index);
    }

    public MethodBase Method { get; }
    public MethodBody? Body { get; }
    public ImmutableArray<ParameterInfo> Parameters { get; }
    public ImmutableArray<LocalVariableInfo> LocalVariables { get; }
    public ImmutableArray<int> Offsets { get; }
    public FrozenDictionary<int, int> OffsetsToIndex { get; }
    public bool IsStatic => Method.IsStatic;
    public int InstructionCount => Offsets.Length - 1;
    public int CodeByteSize => Offsets[^1];

    internal void ValidateControlBoundaries(LinearCode<CilInstructionInfo> source)
    {
        foreach (var instruction in source.Instructions)
            ValidateControlBoundary(source, instruction);
    }

    internal IEnumerable<int> SuccessorInstructionIndices(
        LinearCode<CilInstructionInfo> source,
        CilInstructionInfo instruction)
    {
        var opCode = instruction.Instruction.OpCode.ToILOpCode();
        switch (instruction.Instruction.OpCode.FlowControl)
        {
            case FlowControl.Branch when CilControlFlow.IsUnconditionalBranch(opCode):
                yield return ResolveBranchTarget(instruction);
                yield break;
            case FlowControl.Cond_Branch when CilControlFlow.IsConditionalBranch(opCode):
                yield return ResolveBranchTarget(instruction);
                if (instruction.Index + 1 >= source.Count)
                    throw new InvalidProgramException(
                        $"Conditional branch at IL_{instruction.ByteOffset:X4} has no fallthrough instruction in {Method}.");
                yield return instruction.Index + 1;
                yield break;
            case FlowControl.Cond_Branch when CilControlFlow.IsSwitch(opCode):
                foreach (var target in ResolveSwitchTargets(instruction))
                    yield return target;
                yield return ResolveSwitchDefault(source, instruction);
                yield break;
            case FlowControl.Return when CilControlFlow.IsReturn(opCode):
                yield break;
            case FlowControl.Next:
            case FlowControl.Call:
                if (instruction.Index + 1 >= source.Count)
                    throw new InvalidProgramException(
                        $"Method {Method} reaches the end of CIL after IL_{instruction.ByteOffset:X4} without a return.");
                yield return instruction.Index + 1;
                yield break;
            default:
                throw UnsupportedControl(instruction, opCode);
        }
    }

    internal int ResolveBranchTarget(CilInstructionInfo instruction)
    {
        var jumpOffset = instruction.Instruction.Operand switch
        {
            sbyte value => value,
            int value => value,
            _ => throw new InvalidProgramException(
                $"Unsupported branch operand at IL_{instruction.ByteOffset:X4} in {Method}.")
        };

        int target;
        try
        {
            target = checked(instruction.NextByteOffset + jumpOffset);
        }
        catch (OverflowException exception)
        {
            throw new InvalidProgramException(
                $"Branch target overflows at IL_{instruction.ByteOffset:X4} in {Method}.",
                exception);
        }

        if (!OffsetsToIndex.TryGetValue(target, out var targetIndex) || targetIndex >= InstructionCount)
            throw new InvalidProgramException(
                $"Branch target IL_{target:X4} from IL_{instruction.ByteOffset:X4} in {Method} " +
                "is not an instruction boundary.");

        return targetIndex;
    }

    internal ImmutableArray<int> ResolveSwitchTargets(CilInstructionInfo instruction)
    {
        if (instruction.Instruction.Operand is not int[] displacements)
            throw new InvalidProgramException(
                $"Malformed switch table at IL_{instruction.ByteOffset:X4} in {Method}: expected int[] operand.");

        var targets = ImmutableArray.CreateBuilder<int>(displacements.Length);
        foreach (var (ordinal, displacement) in displacements.Index())
        {
            int target;
            try
            {
                target = checked(instruction.NextByteOffset + displacement);
            }
            catch (OverflowException exception)
            {
                throw new InvalidProgramException(
                    $"Switch target overflows at IL_{instruction.ByteOffset:X4}, case {ordinal}, in {Method}.",
                    exception);
            }

            if (!OffsetsToIndex.TryGetValue(target, out var targetIndex) || targetIndex >= InstructionCount)
                throw new InvalidProgramException(
                    $"Switch target IL_{target:X4} from IL_{instruction.ByteOffset:X4}, case {ordinal}, in {Method} " +
                    "is not an instruction boundary.");
            targets.Add(targetIndex);
        }
        return targets.ToImmutable();
    }

    internal int ResolveSwitchDefault(
        LinearCode<CilInstructionInfo> source,
        CilInstructionInfo instruction)
    {
        if (instruction.Index + 1 >= source.Count)
            throw new InvalidProgramException(
                $"Switch at IL_{instruction.ByteOffset:X4} has no fallthrough/default instruction in {Method}.");
        return instruction.Index + 1;
    }

    private void ValidateControlBoundary(
        LinearCode<CilInstructionInfo> source,
        CilInstructionInfo instruction)
    {
        var opCode = instruction.Instruction.OpCode.ToILOpCode();
        switch (instruction.Instruction.OpCode.FlowControl)
        {
            case FlowControl.Branch when CilControlFlow.IsUnconditionalBranch(opCode):
                _ = ResolveBranchTarget(instruction);
                return;
            case FlowControl.Cond_Branch when CilControlFlow.IsConditionalBranch(opCode):
                _ = ResolveBranchTarget(instruction);
                if (instruction.Index + 1 >= source.Count)
                    throw new InvalidProgramException(
                        $"Conditional branch at IL_{instruction.ByteOffset:X4} has no fallthrough instruction in {Method}.");
                return;
            case FlowControl.Cond_Branch when CilControlFlow.IsSwitch(opCode):
                _ = ResolveSwitchTargets(instruction);
                _ = ResolveSwitchDefault(source, instruction);
                return;
            case FlowControl.Return when CilControlFlow.IsReturn(opCode):
            case FlowControl.Next:
            case FlowControl.Call:
                return;
            default:
                throw UnsupportedControl(instruction, opCode);
        }
    }

    private NotSupportedException UnsupportedControl(CilInstructionInfo instruction, ILOpCode opCode) =>
        new($"CIL control {opCode} at IL_{instruction.ByteOffset:X4} is not supported for method {Method}.");
}

public static class CilMethodDecoder
{
    public static LinearCode<CilInstructionInfo> Decode(MethodBase method)
    {
        ArgumentNullException.ThrowIfNull(method);
        var body = method.GetMethodBody();

        var decodedInstructions = (method.GetInstructions() ?? []).ToImmutableArray();
        var localVariables = (body?.LocalVariables ?? []).ToArray();
        var localVariablesFromInstructions = decodedInstructions.Select(instruction => instruction.Operand)
                                                                .OfType<LocalVariableInfo>()
                                                                .Distinct()
                                                                .OrderBy(variable => variable.LocalIndex);
        foreach (var local in localVariablesFromInstructions)
            localVariables[local.LocalIndex] = local;

        var offsets = decodedInstructions.Select(instruction => instruction.Offset).ToList();
        offsets.Add(body?.GetILAsByteArray()?.Length ?? 0);
        var environment = new CilMethodEnvironment(
            method,
            body,
            [.. method.GetParameters()],
            [.. localVariables],
            [.. offsets]);
        var instructions = decodedInstructions.Select((instruction, index) =>
            new CilInstructionInfo(index, offsets[index], offsets[index + 1], instruction)).ToImmutableArray();
        return new LinearCode<CilInstructionInfo>(
            environment,
            instructions,
            CilStagePrettyPrinter.PrintRawLinearCode);
    }
}
