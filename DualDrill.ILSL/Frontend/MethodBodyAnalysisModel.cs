using DualDrill.CLSL.Language.Literal;
using DualDrill.CLSL.Language.Operation;
using DualDrill.CLSL.Language.Types;
using DualDrill.Common.Nat;
using Lokad.ILPack.IL;
using System.Collections.Immutable;
using System.Reflection;
using System.Reflection.Metadata;

namespace DualDrill.CLSL.Frontend;

public sealed class MethodBodyAnalysisModel
{
    public ImmutableArray<Instruction> Instructions { get; }
    public ImmutableArray<ParameterInfo> Parameters { get; }
    public ImmutableArray<LocalVariableInfo> LocalVariables { get; }
    public ImmutableArray<int> Offsets { get; }
    public MethodBase Method { get; }
    public MethodBody? Body { get; }
    public bool IsStatic => Method.IsStatic;
    public MethodBodyAnalysisModel(MethodBase method)
    {
        Method = method;
        Body = method.GetMethodBody();
        Parameters = [.. method.GetParameters()];
        LocalVariables = [.. method.GetMethodBody()?.LocalVariables ?? []];
        Instructions = [.. (method.GetInstructions() ?? [])];
        var offsets = Instructions.Select(inst => inst.Offset).ToList();
        offsets.Add(method.GetMethodBody()?.GetILAsByteArray()?.Length ?? 0);
        Offsets = [.. offsets];
    }

    public IEnumerable<MethodBase> CalledMethods()
    {
        return Instructions.Select(op => op.Operand)
                           .OfType<MethodBase>();
    }

    public IEnumerable<int> GetJumpTargetOffsets()
    {
        foreach (var (index, inst) in Instructions.Index())
        {
            var flowControl = inst.OpCode.FlowControl;
            if (flowControl == System.Reflection.Emit.FlowControl.Branch ||
                flowControl == System.Reflection.Emit.FlowControl.Cond_Branch)
            {
                var nextOffset = Offsets[index + 1];
                int jumpOffset = inst.Operand switch
                {
                    sbyte v => v,
                    int v => v,
                    _ => throw new NotSupportedException()
                };
                var target = nextOffset + jumpOffset;
                yield return target;
            }
        }
    }

    ParameterInfo GetArg(int index)
    {
        return Parameters[IsStatic ? index : index - 1];
    }

    LocalVariableInfo GetLoc(int index)
    {
        return LocalVariables[index];
    }


    public List<TResult> Accept<TVisitor, TResult>(TVisitor visitor)
            where TVisitor : ICilInstructionVisitor<TResult>
    {
        var result = new List<TResult>(Instructions.Length);
        foreach (var (index, instruction) in Instructions.Index())
        {
            var info = new CilInstructionInfo(index, Offsets[index], Offsets[index + 1], instruction);
            visitor.BeforeVisitInstruction(info);
            var code = instruction.OpCode.ToILOpCode();
            result.Add(code switch
            {
                ILOpCode.Nop => visitor.VisitNop(info),
                ILOpCode.Break => visitor.VisitBreak(info),
                ILOpCode.Ldarg_0 => IsStatic ? visitor.VisitLdArg(info, GetArg(0))
                                             : visitor.VisitLdThis(),
                ILOpCode.Ldarg_1 => visitor.VisitLdArg(info, GetArg(1)),
                ILOpCode.Ldarg_2 => visitor.VisitLdArg(info, GetArg(2)),
                ILOpCode.Ldarg_3 => visitor.VisitLdArg(info, GetArg(3)),
                ILOpCode.Ldloc_0 => visitor.VisitLdLoc(info, GetLoc(0)),
                ILOpCode.Ldloc_1 => visitor.VisitLdLoc(info, GetLoc(1)),
                ILOpCode.Ldloc_2 => visitor.VisitLdLoc(info, GetLoc(2)),
                ILOpCode.Ldloc_3 => visitor.VisitLdLoc(info, GetLoc(3)),
                ILOpCode.Stloc_0 => visitor.VisitStLoc(info, GetLoc(0)),
                ILOpCode.Stloc_1 => visitor.VisitStLoc(info, GetLoc(1)),
                ILOpCode.Stloc_2 => visitor.VisitStLoc(info, GetLoc(2)),
                ILOpCode.Stloc_3 => visitor.VisitStLoc(info, GetLoc(3)),
                ILOpCode.Ldarg_s => visitor.VisitLdArg(info, (ParameterInfo)instruction.Operand),
                ILOpCode.Ldarga_s => visitor.VisitLdArgAddress(info, (ParameterInfo)instruction.Operand),
                ILOpCode.Starg_s => visitor.VisitStArg(info, (ParameterInfo)instruction.Operand),
                ILOpCode.Ldloc_s => visitor.VisitLdLoc(info, (LocalVariableInfo)instruction.Operand),
                ILOpCode.Ldloca_s => visitor.VisitLdLocAddress(info, (LocalVariableInfo)instruction.Operand),
                ILOpCode.Stloc_s => visitor.VisitStLoc(info, (LocalVariableInfo)instruction.Operand),
                ILOpCode.Ldnull => visitor.VisitLdNull(info),
                ILOpCode.Ldc_i4_m1 => visitor.VisitLiteral(info, Literal.Create(-1)),
                ILOpCode.Ldc_i4_0 => visitor.VisitLiteral(info, Literal.Create(0)),
                ILOpCode.Ldc_i4_1 => visitor.VisitLiteral(info, Literal.Create(1)),
                ILOpCode.Ldc_i4_2 => visitor.VisitLiteral(info, Literal.Create(2)),
                ILOpCode.Ldc_i4_3 => visitor.VisitLiteral(info, Literal.Create(3)),
                ILOpCode.Ldc_i4_4 => visitor.VisitLiteral(info, Literal.Create(4)),
                ILOpCode.Ldc_i4_5 => visitor.VisitLiteral(info, Literal.Create(5)),
                ILOpCode.Ldc_i4_6 => visitor.VisitLiteral(info, Literal.Create(6)),
                ILOpCode.Ldc_i4_7 => visitor.VisitLiteral(info, Literal.Create(7)),
                ILOpCode.Ldc_i4_8 => visitor.VisitLiteral(info, Literal.Create(8)),
                ILOpCode.Ldc_i4_s => visitor.VisitLiteral(info, Literal.Create((int)(sbyte)instruction.Operand)),
                ILOpCode.Ldc_i4 => visitor.VisitLiteral(info, Literal.Create((int)instruction.Operand)),
                ILOpCode.Ldc_i8 => visitor.VisitLiteral(info, Literal.Create((long)instruction.Operand)),
                ILOpCode.Ldc_r4 => visitor.VisitLiteral(info, Literal.Create((float)instruction.Operand)),
                ILOpCode.Ldc_r8 => visitor.VisitLiteral(info, Literal.Create((double)instruction.Operand)),
                ILOpCode.Dup => throw new NotImplementedException(),
                ILOpCode.Pop => throw new NotImplementedException(),
                ILOpCode.Jmp => throw new NotImplementedException(),
                ILOpCode.Call => visitor.VisitCall(info, (MethodInfo)instruction.Operand),
                ILOpCode.Calli => throw new NotSupportedException(),
                ILOpCode.Ret => visitor.VisitReturn(info),
                ILOpCode.Br_s => visitor.VisitBr(info, (sbyte)instruction.Operand),
                ILOpCode.Brfalse_s => visitor.VisitBrIf(info, (sbyte)instruction.Operand, value: false),
                ILOpCode.Brtrue_s => visitor.VisitBrIf(info, (sbyte)instruction.Operand, value: true),
                ILOpCode.Beq_s => visitor.VisitBrIf<BinaryRelation.Eq>(info, (sbyte)instruction.Operand),
                ILOpCode.Bge_s => visitor.VisitBrIf<BinaryRelation.Ge>(info, (sbyte)instruction.Operand),
                ILOpCode.Bgt_s => visitor.VisitBrIf<BinaryRelation.Gt>(info, (sbyte)instruction.Operand),
                ILOpCode.Ble_s => visitor.VisitBrIf<BinaryRelation.Le>(info, (sbyte)instruction.Operand),
                ILOpCode.Blt_s => visitor.VisitBrIf<BinaryRelation.Lt>(info, (sbyte)instruction.Operand),
                ILOpCode.Bne_un_s => visitor.VisitBrIf<BinaryRelation.Ne>(info, (sbyte)instruction.Operand, isUn: true),
                ILOpCode.Bge_un_s => visitor.VisitBrIf<BinaryRelation.Ge>(info, (sbyte)instruction.Operand, isUn: true),
                ILOpCode.Bgt_un_s => visitor.VisitBrIf<BinaryRelation.Gt>(info, (sbyte)instruction.Operand, isUn: true),
                ILOpCode.Ble_un_s => visitor.VisitBrIf<BinaryRelation.Le>(info, (sbyte)instruction.Operand, isUn: true),
                ILOpCode.Blt_un_s => visitor.VisitBrIf<BinaryRelation.Lt>(info, (sbyte)instruction.Operand, isUn: true),
                ILOpCode.Br => visitor.VisitBr(info, (int)instruction.Operand),
                ILOpCode.Brfalse => visitor.VisitBrIf(info, (int)instruction.Operand, value: false),
                ILOpCode.Brtrue => visitor.VisitBrIf(info, (int)instruction.Operand, value: true),
                ILOpCode.Beq => visitor.VisitBrIf<BinaryRelation.Eq>(info, (int)instruction.Operand),
                ILOpCode.Bge => visitor.VisitBrIf<BinaryRelation.Ge>(info, (int)instruction.Operand),
                ILOpCode.Bgt => visitor.VisitBrIf<BinaryRelation.Gt>(info, (int)instruction.Operand),
                ILOpCode.Ble => visitor.VisitBrIf<BinaryRelation.Le>(info, (int)instruction.Operand),
                ILOpCode.Blt => visitor.VisitBrIf<BinaryRelation.Lt>(info, (int)instruction.Operand),
                ILOpCode.Bne_un => visitor.VisitBrIf<BinaryRelation.Ne>(info, (int)instruction.Operand, isUn: true),
                ILOpCode.Bge_un => visitor.VisitBrIf<BinaryRelation.Ge>(info, (int)instruction.Operand, isUn: true),
                ILOpCode.Bgt_un => visitor.VisitBrIf<BinaryRelation.Gt>(info, (int)instruction.Operand, isUn: true),
                ILOpCode.Ble_un => visitor.VisitBrIf<BinaryRelation.Le>(info, (int)instruction.Operand, isUn: true),
                ILOpCode.Blt_un => visitor.VisitBrIf<BinaryRelation.Lt>(info, (int)instruction.Operand, isUn: true),
                ILOpCode.Switch => visitor.VisitSwitch(info),
                ILOpCode.Ldind_i1 => visitor.VisitLdIndirect<IntType<N8>>(info),
                ILOpCode.Ldind_u1 => visitor.VisitLdIndirect<UIntType<N8>>(info),
                ILOpCode.Ldind_i2 => throw new NotImplementedException(),
                ILOpCode.Ldind_u2 => throw new NotImplementedException(),
                ILOpCode.Ldind_i4 => throw new NotImplementedException(),
                ILOpCode.Ldind_u4 => throw new NotImplementedException(),
                ILOpCode.Ldind_i8 => throw new NotImplementedException(),
                ILOpCode.Ldind_i => visitor.VisitLdIndirectNativeInt(info),
                ILOpCode.Ldind_r4 => visitor.VisitLdIndirect<FloatType<N32>>(info),
                ILOpCode.Ldind_r8 => throw new NotImplementedException(),
                ILOpCode.Ldind_ref => visitor.VisitLdIndirectRef(info),
                ILOpCode.Stind_ref => visitor.VisitStIndirectRef(info),
                ILOpCode.Stind_i1 => throw new NotImplementedException(),
                ILOpCode.Stind_i2 => throw new NotImplementedException(),
                ILOpCode.Stind_i4 => throw new NotImplementedException(),
                ILOpCode.Stind_i8 => throw new NotImplementedException(),
                ILOpCode.Stind_r4 => visitor.VisitStIndirect<FloatType<N32>>(info),
                ILOpCode.Stind_r8 => throw new NotImplementedException(),
                ILOpCode.Add => visitor.VisitBinaryArithmetic<BinaryArithmetic.Add>(info),
                ILOpCode.Sub => visitor.VisitBinaryArithmetic<BinaryArithmetic.Sub>(info),
                ILOpCode.Mul => visitor.VisitBinaryArithmetic<BinaryArithmetic.Mul>(info),
                ILOpCode.Div => visitor.VisitBinaryArithmetic<BinaryArithmetic.Div>(info),
                ILOpCode.Div_un => visitor.VisitBinaryArithmetic<BinaryArithmetic.Div>(info, true),
                ILOpCode.Rem => throw new NotImplementedException(),
                ILOpCode.Rem_un => throw new NotImplementedException(),
                ILOpCode.And => visitor.VisitBinaryLogical<OpAnd>(info),
                ILOpCode.Or => visitor.VisitBinaryLogical<OpOr>(info),
                ILOpCode.Xor => visitor.VisitBinaryLogical<OpXor>(info),
                ILOpCode.Shl => throw new NotImplementedException(),
                ILOpCode.Shr => throw new NotImplementedException(),
                ILOpCode.Shr_un => throw new NotImplementedException(),
                ILOpCode.Neg => throw new NotImplementedException(),
                ILOpCode.Not => throw new NotImplementedException(),
                ILOpCode.Conv_i1 => visitor.VisitConversion<IntType<N8>>(info),
                ILOpCode.Conv_i2 => visitor.VisitConversion<IntType<N16>>(info),
                ILOpCode.Conv_i4 => visitor.VisitConversion<IntType<N32>>(info),
                ILOpCode.Conv_i8 => visitor.VisitConversion<IntType<N64>>(info),
                ILOpCode.Conv_r4 => visitor.VisitConversion<FloatType<N32>>(info),
                ILOpCode.Conv_r8 => visitor.VisitConversion<FloatType<N64>>(info),
                ILOpCode.Conv_u4 => visitor.VisitConversion<UIntType<N32>>(info),
                ILOpCode.Conv_u8 => visitor.VisitConversion<UIntType<N64>>(info),
                ILOpCode.Callvirt => throw new NotImplementedException(),
                ILOpCode.Cpobj => throw new NotImplementedException(),
                ILOpCode.Ldobj => throw new NotImplementedException(),
                ILOpCode.Ldstr => throw new NotImplementedException(),
                ILOpCode.Newobj => visitor.VisitNewObj(info, (ConstructorInfo)instruction.Operand),
                ILOpCode.Castclass => throw new NotImplementedException(),
                ILOpCode.Isinst => throw new NotImplementedException(),
                ILOpCode.Conv_r_un => throw new NotImplementedException(),
                ILOpCode.Unbox => throw new NotImplementedException(),
                ILOpCode.Throw => throw new NotImplementedException(),
                ILOpCode.Ldfld => visitor.VisitLdFld(info, (FieldInfo)instruction.Operand),
                ILOpCode.Ldflda => visitor.VisitLdFldAddress(info, (FieldInfo)instruction.Operand),
                ILOpCode.Stfld => visitor.VisitStFld(info, (FieldInfo)instruction.Operand),
                ILOpCode.Ldsfld => throw new NotImplementedException(),
                ILOpCode.Ldsflda => throw new NotImplementedException(),
                ILOpCode.Stsfld => throw new NotImplementedException(),
                ILOpCode.Stobj => throw new NotImplementedException(),
                ILOpCode.Conv_ovf_i1_un => throw new NotImplementedException(),
                ILOpCode.Conv_ovf_i2_un => throw new NotImplementedException(),
                ILOpCode.Conv_ovf_i4_un => throw new NotImplementedException(),
                ILOpCode.Conv_ovf_i8_un => throw new NotImplementedException(),
                ILOpCode.Conv_ovf_u1_un => throw new NotImplementedException(),
                ILOpCode.Conv_ovf_u2_un => throw new NotImplementedException(),
                ILOpCode.Conv_ovf_u4_un => throw new NotImplementedException(),
                ILOpCode.Conv_ovf_u8_un => throw new NotImplementedException(),
                ILOpCode.Conv_ovf_i_un => throw new NotImplementedException(),
                ILOpCode.Conv_ovf_u_un => throw new NotImplementedException(),
                ILOpCode.Box => throw new NotImplementedException(),
                ILOpCode.Newarr => throw new NotImplementedException(),
                ILOpCode.Ldlen => throw new NotImplementedException(),
                ILOpCode.Ldelema => throw new NotImplementedException(),
                ILOpCode.Ldelem_i1 => throw new NotImplementedException(),
                ILOpCode.Ldelem_u1 => throw new NotImplementedException(),
                ILOpCode.Ldelem_i2 => throw new NotImplementedException(),
                ILOpCode.Ldelem_u2 => throw new NotImplementedException(),
                ILOpCode.Ldelem_i4 => throw new NotImplementedException(),
                ILOpCode.Ldelem_u4 => throw new NotImplementedException(),
                ILOpCode.Ldelem_i8 => throw new NotImplementedException(),
                ILOpCode.Ldelem_i => throw new NotImplementedException(),
                ILOpCode.Ldelem_r4 => throw new NotImplementedException(),
                ILOpCode.Ldelem_r8 => throw new NotImplementedException(),
                ILOpCode.Ldelem_ref => throw new NotImplementedException(),
                ILOpCode.Stelem_i => throw new NotImplementedException(),
                ILOpCode.Stelem_i1 => throw new NotImplementedException(),
                ILOpCode.Stelem_i2 => throw new NotImplementedException(),
                ILOpCode.Stelem_i4 => throw new NotImplementedException(),
                ILOpCode.Stelem_i8 => throw new NotImplementedException(),
                ILOpCode.Stelem_r4 => throw new NotImplementedException(),
                ILOpCode.Stelem_r8 => throw new NotImplementedException(),
                ILOpCode.Stelem_ref => throw new NotImplementedException(),
                ILOpCode.Ldelem => throw new NotImplementedException(),
                ILOpCode.Stelem => throw new NotImplementedException(),
                ILOpCode.Unbox_any => throw new NotImplementedException(),
                ILOpCode.Conv_ovf_i1 => throw new NotImplementedException(),
                ILOpCode.Conv_ovf_u1 => throw new NotImplementedException(),
                ILOpCode.Conv_ovf_i2 => throw new NotImplementedException(),
                ILOpCode.Conv_ovf_u2 => throw new NotImplementedException(),
                ILOpCode.Conv_ovf_i4 => throw new NotImplementedException(),
                ILOpCode.Conv_ovf_u4 => throw new NotImplementedException(),
                ILOpCode.Conv_ovf_i8 => throw new NotImplementedException(),
                ILOpCode.Conv_ovf_u8 => throw new NotImplementedException(),
                ILOpCode.Refanyval => throw new NotImplementedException(),
                ILOpCode.Ckfinite => throw new NotImplementedException(),
                ILOpCode.Mkrefany => throw new NotImplementedException(),
                ILOpCode.Ldtoken => throw new NotImplementedException(),
                ILOpCode.Conv_u2 => throw new NotImplementedException(),
                ILOpCode.Conv_u1 => throw new NotImplementedException(),
                ILOpCode.Conv_i => throw new NotImplementedException(),
                ILOpCode.Conv_ovf_i => throw new NotImplementedException(),
                ILOpCode.Conv_ovf_u => throw new NotImplementedException(),
                ILOpCode.Add_ovf => throw new NotImplementedException(),
                ILOpCode.Add_ovf_un => throw new NotImplementedException(),
                ILOpCode.Mul_ovf => throw new NotImplementedException(),
                ILOpCode.Mul_ovf_un => throw new NotImplementedException(),
                ILOpCode.Sub_ovf => throw new NotImplementedException(),
                ILOpCode.Sub_ovf_un => throw new NotImplementedException(),
                ILOpCode.Endfinally => throw new NotImplementedException(),
                ILOpCode.Leave => throw new NotImplementedException(),
                ILOpCode.Leave_s => throw new NotImplementedException(),
                ILOpCode.Stind_i => throw new NotImplementedException(),
                ILOpCode.Conv_u => throw new NotImplementedException(),

                ILOpCode.Arglist => throw new NotImplementedException(),
                ILOpCode.Ceq => visitor.VisitBinaryRelation<BinaryRelation.Eq>(info),
                ILOpCode.Cgt => visitor.VisitBinaryRelation<BinaryRelation.Gt>(info),
                ILOpCode.Cgt_un => visitor.VisitBinaryRelation<BinaryRelation.Gt>(info, isUn: true),
                ILOpCode.Clt => visitor.VisitBinaryRelation<BinaryRelation.Lt>(info),
                ILOpCode.Clt_un => visitor.VisitBinaryRelation<BinaryRelation.Lt>(info, isUn: true),
                ILOpCode.Ldftn => throw new NotImplementedException(),
                ILOpCode.Ldvirtftn => throw new NotImplementedException(),
                ILOpCode.Ldarg => visitor.VisitLdArg(info, (ParameterInfo)instruction.Operand),
                ILOpCode.Ldarga => visitor.VisitLdArgAddress(info, (ParameterInfo)instruction.Operand),
                ILOpCode.Starg => visitor.VisitStArg(info, (ParameterInfo)instruction.Operand),
                ILOpCode.Ldloc => visitor.VisitLdLoc(info, (LocalVariableInfo)instruction.Operand),
                ILOpCode.Ldloca => visitor.VisitLdLocAddress(info, (LocalVariableInfo)instruction.Operand),
                ILOpCode.Stloc => visitor.VisitStLoc(info, (LocalVariableInfo)instruction.Operand),
                ILOpCode.Localloc => throw new NotImplementedException(),
                ILOpCode.Endfilter => throw new NotImplementedException(),
                ILOpCode.Unaligned => throw new NotImplementedException(),
                ILOpCode.Volatile => throw new NotImplementedException(),
                ILOpCode.Tail => throw new NotImplementedException(),
                ILOpCode.Initobj => throw new NotImplementedException(),
                ILOpCode.Constrained => throw new NotImplementedException(),
                ILOpCode.Cpblk => throw new NotImplementedException(),
                ILOpCode.Initblk => throw new NotImplementedException(),
                ILOpCode.Rethrow => throw new NotImplementedException(),
                ILOpCode.Sizeof => throw new NotImplementedException(),
                ILOpCode.Refanytype => throw new NotImplementedException(),
                ILOpCode.Readonly => throw new NotImplementedException(),
                _ => throw new NotSupportedException($"Unsupported ILOpCode {code}")
            });
            visitor.AfterVisitInstruction(info);
        }
        return result;
    }
}
