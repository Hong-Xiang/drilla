using DualDrill.CLSL.ControlFlowGraph;
using DualDrill.CLSL.Language.AbstractSyntaxTree.Expression;
using DualDrill.CLSL.Language.Declaration;
using DualDrill.CLSL.Language.Literal;
using DualDrill.CLSL.Language.Operation;
using DualDrill.CLSL.Language.Types;
using DualDrill.CLSL.LinearInstruction;
using DualDrill.ILSL.Compiler;
using System.Reflection;
using System.Reflection.Metadata;
using static DualDrill.CLSL.Language.Types.Signedness;

namespace DualDrill.ILSL.Frontend;

public sealed class RuntimeReflectionMethodBodyParser(
    ICompilationContextView Context
)
{

    public CLSL.ControlFlowGraph.ControlFlowGraph Run(MethodBase method)
    {
        var model = CilMethodSemanticModel.Create(method);
        var basicBlocks = model.BasicBlocks.Select(b =>
        {
            return new BasicBlock(b.ByteOffset)
            {
                Index = b.BlockIndex,
                Instructions = [.. b.Instructions.SelectMany(ParseInstruction)],
            };
        });
        foreach (var block in basicBlocks)
        {
            block.Successors = [..model.GetBasicBlockInfo(block.Index).Successors.Select(e => {
                throw new NotImplementedException();
                return (IControlFlowEdge) new ReturnEdge();
            })];
        }
        return new([.. basicBlocks.OfType<BasicBlock>()]);
    }

    VariableDeclaration GetLocalVariable(int index)
    {
        throw new NotImplementedException();
    }

    VariableDeclaration GetLocalVariable(IInstructionInfo instruction)
    {
        throw new NotImplementedException();
    }

    ParameterDeclaration GetParameter(int index)
    {
        throw new NotImplementedException();
    }
    ParameterDeclaration GetParameter(ParameterInfo info)
    {
        throw new NotImplementedException();
    }

    FunctionDeclaration GetMethod(MethodInfo method)
    {
        throw new NotImplementedException();
    }
    FunctionDeclaration GetMethod(ConstructorInfo method)
    {
        throw new NotImplementedException();
    }


    IEnumerable<IInstruction> ParseInstruction(IInstructionInfo instruction)
    {
        switch (instruction.ILOpCode)
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
                return [ShaderInstruction.Load(GetParameter((ParameterInfo)instruction.Operand))];
            case ILOpCode.Ldarg_0:
                return [ShaderInstruction.Load(GetParameter(0))];
            case ILOpCode.Ldarg_1:
                return [ShaderInstruction.Load(GetParameter(1))];
            case ILOpCode.Ldarg_2:
                return [ShaderInstruction.Load(GetParameter(2))];
            case ILOpCode.Ldarg_3:
                return [ShaderInstruction.Load(GetParameter(3))];
            case ILOpCode.Ldarga:
            case ILOpCode.Ldarga_s:
                return [ShaderInstruction.LoadAddress(GetParameter((ParameterInfo)instruction.Operand))];
            case ILOpCode.Starg:
            case ILOpCode.Starg_s:
                return [ShaderInstruction.Store(GetParameter((ParameterInfo)instruction.Operand))];

            case ILOpCode.Ldloc_0:
                return [new LoadLocalInstruction(GetLocalVariable(0))];
            case ILOpCode.Ldloc_1:
                return [new LoadLocalInstruction(GetLocalVariable(1))];
            case ILOpCode.Ldloc_2:
                return [new LoadLocalInstruction(GetLocalVariable(2))];
            case ILOpCode.Ldloc_3:
                return [new LoadLocalInstruction(GetLocalVariable(3))];
            case ILOpCode.Ldloc:
            case ILOpCode.Ldloc_s:
                return [new LoadLocalInstruction(GetLocalVariable(instruction))];
            case ILOpCode.Stloc_0:
                return [new StoreLocalInstruction(GetLocalVariable(0))];
            case ILOpCode.Stloc_1:
                return [new StoreLocalInstruction(GetLocalVariable(1))];
            case ILOpCode.Stloc_2:
                return [new StoreLocalInstruction(GetLocalVariable(2))];
            case ILOpCode.Stloc_3:
                return [new StoreLocalInstruction(GetLocalVariable(3))];
            case ILOpCode.Stloc:
                return [new StoreLocalInstruction((GetLocalVariable(instruction)))];
            case ILOpCode.Stloc_s:
                return [new StoreLocalInstruction(GetLocalVariable(instruction))];
            case ILOpCode.Ldloca:
                return [new LoadLocalAddressInstruction(GetLocalVariable(instruction))];
            case ILOpCode.Ldloca_s:
                return [new LoadLocalAddressInstruction(GetLocalVariable(instruction))];
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
                return [ShaderInstruction.Call(GetMethod((MethodInfo)instruction.Operand))];
            case ILOpCode.Newobj:
                return [ShaderInstruction.Call(GetMethod((ConstructorInfo)instruction.Operand))];

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
