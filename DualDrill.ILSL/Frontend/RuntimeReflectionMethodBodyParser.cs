using DualDrill.CLSL.ControlFlowGraph;
using DualDrill.CLSL.Language.AbstractSyntaxTree.Expression;
using DualDrill.CLSL.Language.Literal;
using DualDrill.CLSL.Language.Operation;
using DualDrill.CLSL.Language.Types;
using DualDrill.CLSL.LinearInstruction;
using DualDrill.ILSL.Compiler;
using Lokad.ILPack.IL;
using System.Collections.Frozen;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Reflection;
using System.Reflection.Emit;
using System.Reflection.Metadata;
using static DualDrill.CLSL.Language.Types.Signedness;

namespace DualDrill.ILSL.Frontend;

public sealed class RuntimeReflectionMethodBodyParser(
    ICompilationContextView Context
)
{
    public enum FlowKind
    {
        Next,
        Fallthrough,
        UnconditionalBranch,
        ConditionalBranch,
        Switch,
        Return,
    }

    public CLSL.ControlFlowGraph.ControlFlowGraph Run(MethodBase method)
    {
        var instructions = method.GetInstructions()?.ToImmutableArray() ?? [];
        if (instructions.Length == 0)
        {
            return new([]);
        }

        var nextOffsets = new int[instructions.Length];
        var offsetsToInstructionIndex = instructions.Index().ToFrozenDictionary((data) => data.Item.Offset, data => data.Index);

        {
            var methodBodyILByteSize = method.GetMethodBody()?.GetILAsByteArray()?.Length ?? 0;
            foreach (var (i, inst) in instructions.Index())
            {
                nextOffsets[i] = (i + 1) < instructions.Length ? instructions[i + 1].Offset : methodBodyILByteSize;
                var instSize = nextOffsets[i] - instructions[i].Offset;
                Debug.Assert(instSize > 0);
            }
        }

        var basicBlocks = new BasicBlock?[instructions.Length];
        var successors = new HashSet<IControlFlowEdge>?[instructions.Length];

        basicBlocks[0] = new BasicBlock(instructions[0].Offset);

        void AddEdge(int index, Func<BasicBlock, IControlFlowEdge> edgeFactory)
        {
            if (index < instructions.Length)
            {
                basicBlocks[index] ??= new BasicBlock(instructions[index].Offset)
                {
                    Index = index
                };
                successors[index] ??= [];
                successors[index]!.Add(edgeFactory(basicBlocks[index]!));
            }
        }

        void TryAddLead(int index)
        {
            if (index < instructions.Length)
            {
                basicBlocks[index] ??= new BasicBlock(instructions[index].Offset)
                {
                    Index = index
                };
            }
        }

        foreach (var (idx, inst) in instructions.Index())
        {
            successors[idx] ??= [];
            switch (inst.OpCode.FlowControl)
            {
                case FlowControl.Branch:
                    {
                        int jump = OpCodes.TakesSingleByteArgument(inst.OpCode) ? (sbyte)inst.Operand : (int)inst.Operand;
                        var instIndex = offsetsToInstructionIndex[nextOffsets[idx] + jump];
                        AddEdge(instIndex, static (bb) => new UnconditionalEdge(bb));
                        TryAddLead(idx + 1);
                        break;
                    }
                case FlowControl.Cond_Branch:
                    if (inst.OpCode.ToILOpCode() == System.Reflection.Metadata.ILOpCode.Switch)
                    {
                        throw new NotImplementedException();
                    }
                    else
                    {
                        int jump = OpCodes.TakesSingleByteArgument(inst.OpCode) ? (sbyte)inst.Operand : (int)inst.Operand;
                        var instIndex = offsetsToInstructionIndex[nextOffsets[idx] + jump];
                        AddEdge(instIndex, static (bb) => new ConditionalEdge(bb));
                        AddEdge(idx + 1, static (bb) => new FallthroughEdge(bb));
                        break;
                    }
                case FlowControl.Return:
                    {
                        successors[idx]!.Add(new ReturnEdge());
                        TryAddLead(idx + 1);
                        break;
                    }
                case FlowControl.Throw:
                    TryAddLead(idx + 1);
                    throw new NotSupportedException("throw is not supported");
                // TODO: handle switch
                default:
                    continue;
            }
        }

        int bbCount = 0;
        var bbInstCount = new int[instructions.Length];
        BasicBlock? basicBlock = null;
        foreach (var (idx, bb) in basicBlocks.Index())
        {
            if (bb is not null)
            {
                basicBlock = bb;
                bbCount++;
            }
            bbInstCount[bbCount - 1]++;
            basicBlock.Successors = [.. basicBlock.Successors, .. successors[idx]];
        }

        foreach (var (idx, bb) in basicBlocks.Index())
        {
            if (bb is not null)
            {
                bb.Instructions = [.. instructions.Slice(idx, bbInstCount[bb.Index]).SelectMany(ParseInstruction)];
            }
        }
        return new([.. basicBlocks.OfType<BasicBlock>()]);
    }

    IEnumerable<IInstruction> ParseInstruction(Lokad.ILPack.IL.Instruction instruction)
    {
        switch (instruction.OpCode.ToILOpCode())
        {
            case ILOpCode.Nop:
                return [ShaderInstruction.Nop];
            case ILOpCode.Beq:
            case ILOpCode.Beq_s:
                return [ShaderInstruction.Eq, ShaderInstruction.BrIf];
            case ILOpCode.Bge:
            case ILOpCode.Bge_s:
                return [ShaderInstruction.Ge, ShaderInstruction.BrIf];
            case ILOpCode.Bge_un:
                return Branch<Condition.Ge<U>>((int)instruction.Operand);
            case ILOpCode.Bge_un_s:
                return Branch<Condition.Ge<U>>((sbyte)instruction.Operand);
            case ILOpCode.Bgt:
                return Branch<Condition.Gt<S>>((int)instruction.Operand);
            case ILOpCode.Bgt_s:
                return Branch<Condition.Gt<S>>((sbyte)instruction.Operand);
            case ILOpCode.Bgt_un:
                return Branch<Condition.Gt<U>>((int)instruction.Operand);
            case ILOpCode.Bgt_un_s:
                return Branch<Condition.Gt<U>>((sbyte)instruction.Operand);
            case ILOpCode.Ble:
                return Branch<Condition.Le<S>>((int)instruction.Operand);
            case ILOpCode.Ble_s:
                return Branch<Condition.Le<S>>((sbyte)instruction.Operand);
            case ILOpCode.Ble_un:
                return Branch<Condition.Le<U>>((int)instruction.Operand);
            case ILOpCode.Ble_un_s:
                return Branch<Condition.Le<U>>((sbyte)instruction.Operand);
            case ILOpCode.Blt:
                return Branch<Condition.Lt<S>>((int)instruction.Operand);
            case ILOpCode.Blt_s:
                return Branch<Condition.Lt<S>>((sbyte)instruction.Operand);
            case ILOpCode.Blt_un:
                return Branch<Condition.Lt<U>>((int)instruction.Operand);
            case ILOpCode.Blt_un_s:
                return Branch<Condition.Lt<U>>((sbyte)instruction.Operand);
            case ILOpCode.Bne_un:
                return Branch<Condition.Ne>((int)instruction.Operand);
            case ILOpCode.Bne_un_s:
                return Branch<Condition.Ne>((sbyte)instruction.Operand);
            case ILOpCode.Br:
                return Branch<Condition.Unconditional>((int)instruction.Operand);
            case ILOpCode.Br_s:
                return Branch<Condition.Unconditional>((sbyte)instruction.Operand);
            case ILOpCode.Brfalse:
                return Branch<Condition.False>((int)instruction.Operand);
            case ILOpCode.Brfalse_s:
                return Branch<Condition.False>((sbyte)instruction.Operand);
            case ILOpCode.Brtrue:
                return [new BrIfInstruction()];
            case ILOpCode.Brtrue_s:
                return [new BrIfInstruction()];
            case ILOpCode.Switch:
                throw new NotImplementedException("compile switch instruction is not implemented yet");
            case ILOpCode.Ceq:
                return [new ConditionValueInstruction<Condition.Eq>()];

            case ILOpCode.And:
                return [new BinaryBitwiseInstruction(BinaryBitwiseOp.BitwiseAnd)];
            case ILOpCode.Or:
                return [new BinaryBitwiseInstruction(BinaryBitwiseOp.BitwiseOr)];
            case ILOpCode.Xor:
                return [new BinaryBitwiseInstruction(BinaryBitwiseOp.BitwiseExclusiveOr)];

            case ILOpCode.Ldc_i4:
                return [new Const<I32Literal>(new((int)instruction.Operand))];
            case ILOpCode.Ldc_i4_0:
                return [new Const<I32Literal>(new(0))];
            case ILOpCode.Ldc_i4_1:
                return [new Const<I32Literal>(new(1))];
            case ILOpCode.Ldc_i4_2:
                return [new Const<I32Literal>(new(2))];
            case ILOpCode.Ldc_i4_3:
                return [new Const<I32Literal>(new(3))];
            case ILOpCode.Ldc_i4_4:
                return [new Const<I32Literal>(new(4))];
            case ILOpCode.Ldc_i4_5:
                return [new Const<I32Literal>(new(5))];
            case ILOpCode.Ldc_i4_6:
                return [new Const<I32Literal>(new(6))];
            case ILOpCode.Ldc_i4_7:
                return [new Const<I32Literal>(new(7))];
            case ILOpCode.Ldc_i4_8:
                return [new Const<I32Literal>(new(8))];
            case ILOpCode.Ldc_i4_m1:
                return [new Const<I32Literal>(new(-1))];
            case ILOpCode.Ldc_i4_s:
                return [new Const<I32Literal>(new((sbyte)instruction.Operand))];
            case ILOpCode.Ldc_i8:
                return [new Const<I64Literal>(new((long)instruction.Operand))];
            case ILOpCode.Ldc_r4:
                return [new Const<F32Literal>(new((float)instruction.Operand))];
            case ILOpCode.Ldc_r8:
                return [new Const<F64Literal>(new((double)instruction.Operand))];

            case ILOpCode.Ldarg:
            case ILOpCode.Ldarg_s:
                return [new LoadArgumentInstruction(((ParameterInfo)instruction.Operand).Position + argumentPositionBase)];
            case ILOpCode.Ldarg_0:
                return [new LoadArgumentInstruction(0)];
            case ILOpCode.Ldarg_1:
                return [new LoadArgumentInstruction(1)];
            case ILOpCode.Ldarg_2:
                return [new LoadArgumentInstruction(2)];
            case ILOpCode.Ldarg_3:
                return [new LoadArgumentInstruction(3)];
            case ILOpCode.Ldarga:
            case ILOpCode.Ldarga_s:
                return [new LoadArgumentAddressInstruction(((ParameterInfo)instruction.Operand).Position + argumentPositionBase)];
            case ILOpCode.Starg:
            case ILOpCode.Starg_s:
                return [new StoreArgumentInstruction(((ParameterInfo)instruction.Operand).Position + argumentPositionBase)];

            case ILOpCode.Ldloc_0:
                return [new LoadLocalInstruction(LocVar(0))];
            case ILOpCode.Ldloc_1:
                return [new LoadLocalInstruction(LocVar(1))];
            case ILOpCode.Ldloc_2:
                return [new LoadLocalInstruction(LocVar(2))];
            case ILOpCode.Ldloc_3:
                return [new LoadLocalInstruction(LocVar(3))];
            case ILOpCode.Ldloc:
            case ILOpCode.Ldloc_s:
                return [new LoadLocalInstruction(LocVar(instruction))];
            case ILOpCode.Stloc_0:
                return [new StoreLocalInstruction(LocVar(0))];
            case ILOpCode.Stloc_1:
                return [new StoreLocalInstruction(LocVar(1))];
            case ILOpCode.Stloc_2:
                return [new StoreLocalInstruction(LocVar(2))];
            case ILOpCode.Stloc_3:
                return [new StoreLocalInstruction(LocVar(3))];
            case ILOpCode.Stloc:
                return [new StoreLocalInstruction((LocVar(instruction)))];
            case ILOpCode.Stloc_s:
                return [new StoreLocalInstruction(LocVar(instruction))];
            case ILOpCode.Ldloca:
                return [new LoadLocalAddressInstruction(LocVar(instruction))];
            case ILOpCode.Ldloca_s:
                return [new LoadLocalAddressInstruction(LocVar(instruction))];
            case ILOpCode.Add:
                return [new BinaryArithmeticInstruction<BinaryArithmetic.Add>()];
            case ILOpCode.Sub:
                return [new BinaryArithmeticInstruction<BinaryArithmetic.Sub>()];
            case ILOpCode.Mul:
                return [new BinaryArithmeticInstruction<BinaryArithmetic.Mul>()];
            case ILOpCode.Div:
                return [new BinaryArithmeticInstruction<BinaryArithmetic.Div>()];
            case ILOpCode.Rem:
                return [new BinaryArithmeticInstruction<BinaryArithmetic.Rem>()];

            case ILOpCode.Call:
                return [new CallInstruction((MethodInfo)instruction.Operand)];
            case ILOpCode.Newobj:
                return [new NewObjInstruction((ConstructorInfo)instruction.Operand)];

            case ILOpCode.Ret:
                return [new ReturnInstruction()];

            case ILOpCode.Ldsfld:
                return [new LoadStaticFieldInstruction((FieldInfo)instruction.Operand)];
            case ILOpCode.Ldfld:
                return [new LoadInstanceFieldInstruction((FieldInfo)instruction.Operand)];
            case ILOpCode.Ldflda:
                return [new LoadInstanceFieldAddressInstruction((FieldInfo)instruction.Operand)];
            case ILOpCode.Clt:
                return [new ConditionValueInstruction<Condition.Lt<S>>()];
            case ILOpCode.Clt_un:
                return [new ConditionValueInstruction<Condition.Lt<U>>()];
            case ILOpCode.Cgt:
                return [new ConditionValueInstruction<Condition.Gt<S>>()];
            case ILOpCode.Cgt_un:
                return [new ConditionValueInstruction<Condition.Gt<U>>()];

            case ILOpCode.Conv_r4:
                return [new ConvertInstruction(ShaderType.F32)];
            case ILOpCode.Conv_r8:
                return [new ConvertInstruction(ShaderType.F64)];

            case ILOpCode.Neg:
                return [new NegateInstruction()];

            default:
                throw new NotSupportedException($"Compile {instruction.OpCode}@{instruction.Offset}");
        }
    }
}
