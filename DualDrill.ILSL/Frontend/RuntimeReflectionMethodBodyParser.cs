using DualDrill.CLSL.ControlFlowGraph;
using DualDrill.CLSL.Language.AbstractSyntaxTree.Expression;
using DualDrill.CLSL.Language.Declaration;
using DualDrill.CLSL.Language.Literal;
using DualDrill.CLSL.Language.Operation;
using DualDrill.CLSL.Language.Types;
using DualDrill.CLSL.LinearInstruction;
using DualDrill.Common;
using DualDrill.Common.Nat;
using DualDrill.ILSL.Compiler;
using Lokad.ILPack.IL;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Reflection;
using System.Reflection.Metadata;
using static DualDrill.CLSL.Language.Types.Signedness;

namespace DualDrill.ILSL.Frontend;
public readonly record struct CilVisitorContext(
        int Index,
        int ByteOffset,
        int NextByteOffset,
        Instruction Instruction
    )
{
}

public interface ICilVisitor<TResult>
{
    public CilVisitorContext CurrentContext { set; }
    TResult VisitNop();
    TResult VisitBreak();
    TResult VisitLdArg(ParameterInfo info);
    TResult VisitLdArgAddress(ParameterInfo info);
    TResult VisitStArg(ParameterInfo index);
    //TResult VisitLdThis();
    //TResult VsitiLdThisAddress();
    TResult VisitLdLoc(LocalVariableInfo info);
    TResult VisitLdLocAddress(LocalVariableInfo info);
    TResult VisitStLoc(LocalVariableInfo info);
    TResult VisitLdNull();
    TResult VisitCall(MethodInfo method);
    TResult VisitNewObj(ConstructorInfo constructor);
    TResult VisitReturn();
    TResult VisitLiteral<TLiteral>(TLiteral literal) where TLiteral : ILiteral;
    TResult VisitBr(int jumpOffset);
    TResult VisitBrIf(int jumpOffset, bool value);
    TResult VisitBrIf<TOp>(int jumpOffset, bool isUn = false) where TOp : BinaryRelation.IOp<TOp>;
    TResult VisitSwitch();
    TResult VisitBinaryArithmetic<TOp>(bool isUn = false, bool isChecked = false) where TOp : BinaryArithmetic.IOp<TOp>;
    TResult VisitBinaryBitwise<TOp>(bool isUn = false, bool isChecked = false) where TOp : BinaryArithmetic.IOp<TOp>;
    TResult VisitBinaryRelation<TOp>(bool isUn = false, bool isChecked = false) where TOp : BinaryRelation.IOp<TOp>;
    TResult VisitBinaryLogical<TOp>() where TOp : BinaryRelation.IOp<TOp>;
    TResult VisitUnaryLogical<TOp>() where TOp : BinaryRelation.IOp<TOp>;
    TResult VisitLdIndirect<TShaderType>() where TShaderType : IShaderType;
    TResult VisitStIndirect<TShaderType>() where TShaderType : IShaderType;
    TResult VisitLdIndirectNativeInt();
    TResult VisitLdIndirectRef();
    TResult VisitStIndirectRef();
}

sealed class CilMethodBodyWalker
{
    ImmutableArray<Instruction> Instructions { get; }
    ImmutableArray<ParameterInfo> Parameters { get; }
    ImmutableArray<LocalVariableInfo> LocalVariables { get; }
    List<int> Offsets { get; }
    MethodBase Method { get; }
    bool IsStatic => Method.IsStatic;
    public CilMethodBodyWalker(MethodBase method)
    {
        Method = method;
        Parameters = [.. method.GetParameters()];
        LocalVariables = [.. method.GetMethodBody()?.LocalVariables ?? []];
        Instructions = [.. (method.GetInstructions() ?? [])];
        Offsets = Instructions.Select(inst => inst.Offset).ToList();
        Offsets.Add(method.GetMethodBody()?.GetILAsByteArray()?.Length ?? 0);
    }

    ParameterInfo GetArg(int index)
    {
        if (!IsStatic)
            index--;
        return Parameters[IsStatic ? index : index - 1];
    }

    LocalVariableInfo GetLoc(int index)
    {
        return LocalVariables[index];
    }


    public List<TResult> Accept<TVisitor, TResult>(TVisitor visitor)
            where TVisitor : ICilVisitor<TResult>
    {
        var result = new List<TResult>(Instructions.Length);
        foreach (var (index, instruction) in Instructions.Index())
        {
            visitor.CurrentContext = new(index, Offsets[index], Offsets[index + 1], instruction);
            var code = instruction.OpCode.ToILOpCode();
            result.Add(code switch
            {
                ILOpCode.Nop => visitor.VisitNop(),
                ILOpCode.Break => visitor.VisitBreak(),
                ILOpCode.Ldarg_0 => visitor.VisitLdArg(GetArg(0)),
                ILOpCode.Ldarg_1 => visitor.VisitLdArg(GetArg(1)),
                ILOpCode.Ldarg_2 => visitor.VisitLdArg(GetArg(2)),
                ILOpCode.Ldarg_3 => visitor.VisitLdArg(GetArg(3)),
                ILOpCode.Ldloc_0 => visitor.VisitLdLoc(GetLoc(0)),
                ILOpCode.Ldloc_1 => visitor.VisitLdLoc(GetLoc(1)),
                ILOpCode.Ldloc_2 => visitor.VisitLdLoc(GetLoc(2)),
                ILOpCode.Ldloc_3 => visitor.VisitLdLoc(GetLoc(3)),
                ILOpCode.Stloc_0 => visitor.VisitStLoc(GetLoc(0)),
                ILOpCode.Stloc_1 => visitor.VisitStLoc(GetLoc(1)),
                ILOpCode.Stloc_2 => visitor.VisitStLoc(GetLoc(2)),
                ILOpCode.Stloc_3 => visitor.VisitStLoc(GetLoc(3)),
                ILOpCode.Ldarg_s => visitor.VisitLdArg((ParameterInfo)instruction.Operand),
                ILOpCode.Ldarga_s => visitor.VisitLdArgAddress((ParameterInfo)instruction.Operand),
                ILOpCode.Starg_s => visitor.VisitStArg((ParameterInfo)instruction.Operand),
                ILOpCode.Ldloc_s => visitor.VisitLdLoc((LocalVariableInfo)instruction.Operand),
                ILOpCode.Ldloca_s => visitor.VisitLdLocAddress((LocalVariableInfo)instruction.Operand),
                ILOpCode.Stloc_s => visitor.VisitStLoc((LocalVariableInfo)instruction.Operand),
                ILOpCode.Ldnull => visitor.VisitLdNull(),
                ILOpCode.Ldc_i4_m1 => visitor.VisitLiteral(Literal.Create(-1)),
                ILOpCode.Ldc_i4_0 => visitor.VisitLiteral(Literal.Create(0)),
                ILOpCode.Ldc_i4_1 => visitor.VisitLiteral(Literal.Create(1)),
                ILOpCode.Ldc_i4_2 => visitor.VisitLiteral(Literal.Create(2)),
                ILOpCode.Ldc_i4_3 => visitor.VisitLiteral(Literal.Create(3)),
                ILOpCode.Ldc_i4_4 => visitor.VisitLiteral(Literal.Create(4)),
                ILOpCode.Ldc_i4_5 => visitor.VisitLiteral(Literal.Create(5)),
                ILOpCode.Ldc_i4_6 => visitor.VisitLiteral(Literal.Create(6)),
                ILOpCode.Ldc_i4_7 => visitor.VisitLiteral(Literal.Create(7)),
                ILOpCode.Ldc_i4_8 => visitor.VisitLiteral(Literal.Create(8)),
                ILOpCode.Ldc_i4 => visitor.VisitLiteral(Literal.Create((int)instruction.Operand)),
                ILOpCode.Ldc_i8 => visitor.VisitLiteral(Literal.Create((long)instruction.Operand)),
                ILOpCode.Ldc_r4 => visitor.VisitLiteral(Literal.Create((float)instruction.Operand)),
                ILOpCode.Ldc_r8 => visitor.VisitLiteral(Literal.Create((double)instruction.Operand)),
                ILOpCode.Dup => throw new NotImplementedException(),
                ILOpCode.Pop => throw new NotImplementedException(),
                ILOpCode.Jmp => throw new NotImplementedException(),
                ILOpCode.Call => visitor.VisitCall((MethodInfo)instruction.Operand),
                ILOpCode.Calli => throw new NotImplementedException(),
                ILOpCode.Ret => visitor.VisitReturn(),
                ILOpCode.Br_s => visitor.VisitBr((sbyte)instruction.Operand),
                ILOpCode.Brfalse_s => visitor.VisitBrIf((sbyte)instruction.Operand, value: false),
                ILOpCode.Brtrue_s => visitor.VisitBrIf((sbyte)instruction.Operand, value: true),
                ILOpCode.Beq_s => visitor.VisitBrIf<BinaryRelation.Eq>((sbyte)instruction.Operand),
                ILOpCode.Bge_s => visitor.VisitBrIf<BinaryRelation.Ge>((sbyte)instruction.Operand),
                ILOpCode.Bgt_s => visitor.VisitBrIf<BinaryRelation.Gt>((sbyte)instruction.Operand),
                ILOpCode.Ble_s => visitor.VisitBrIf<BinaryRelation.Le>((sbyte)instruction.Operand),
                ILOpCode.Blt_s => visitor.VisitBrIf<BinaryRelation.Lt>((sbyte)instruction.Operand),
                ILOpCode.Bne_un_s => visitor.VisitBrIf<BinaryRelation.Ne>((sbyte)instruction.Operand, isUn: true),
                ILOpCode.Bge_un_s => visitor.VisitBrIf<BinaryRelation.Ge>((sbyte)instruction.Operand, isUn: true),
                ILOpCode.Bgt_un_s => visitor.VisitBrIf<BinaryRelation.Gt>((sbyte)instruction.Operand, isUn: true),
                ILOpCode.Ble_un_s => visitor.VisitBrIf<BinaryRelation.Le>((sbyte)instruction.Operand, isUn: true),
                ILOpCode.Blt_un_s => visitor.VisitBrIf<BinaryRelation.Lt>((sbyte)instruction.Operand, isUn: true),
                ILOpCode.Br => visitor.VisitBr((int)instruction.Operand),
                ILOpCode.Brfalse => visitor.VisitBrIf((int)instruction.Operand, value: false),
                ILOpCode.Brtrue => visitor.VisitBrIf((int)instruction.Operand, value: true),
                ILOpCode.Beq => visitor.VisitBrIf<BinaryRelation.Eq>((int)instruction.Operand),
                ILOpCode.Bge => visitor.VisitBrIf<BinaryRelation.Ge>((int)instruction.Operand),
                ILOpCode.Bgt => visitor.VisitBrIf<BinaryRelation.Gt>((int)instruction.Operand),
                ILOpCode.Ble => visitor.VisitBrIf<BinaryRelation.Le>((int)instruction.Operand),
                ILOpCode.Blt => visitor.VisitBrIf<BinaryRelation.Lt>((int)instruction.Operand),
                ILOpCode.Bne_un => visitor.VisitBrIf<BinaryRelation.Ne>((int)instruction.Operand, isUn: true),
                ILOpCode.Bge_un => visitor.VisitBrIf<BinaryRelation.Ge>((int)instruction.Operand, isUn: true),
                ILOpCode.Bgt_un => visitor.VisitBrIf<BinaryRelation.Gt>((int)instruction.Operand, isUn: true),
                ILOpCode.Ble_un => visitor.VisitBrIf<BinaryRelation.Le>((int)instruction.Operand, isUn: true),
                ILOpCode.Blt_un => visitor.VisitBrIf<BinaryRelation.Lt>((int)instruction.Operand, isUn: true),
                ILOpCode.Switch => visitor.VisitSwitch(),
                ILOpCode.Ldind_i1 => visitor.VisitLdIndirect<IntType<N8>>(),
                ILOpCode.Ldind_u1 => visitor.VisitLdIndirect<UIntType<N8>>(),
                ILOpCode.Ldind_i2 => throw new NotImplementedException(),
                ILOpCode.Ldind_u2 => throw new NotImplementedException(),
                ILOpCode.Ldind_i4 => throw new NotImplementedException(),
                ILOpCode.Ldind_u4 => throw new NotImplementedException(),
                ILOpCode.Ldind_i8 => throw new NotImplementedException(),
                ILOpCode.Ldind_i => visitor.VisitLdIndirectNativeInt(),
                ILOpCode.Ldind_r4 => visitor.VisitLdIndirect<FloatType<N32>>(),
                ILOpCode.Ldind_r8 => throw new NotImplementedException(),
                ILOpCode.Ldind_ref => visitor.VisitLdIndirectRef(),
                ILOpCode.Stind_ref => visitor.VisitStIndirectRef(),
                ILOpCode.Stind_i1 => throw new NotImplementedException(),
                ILOpCode.Stind_i2 => throw new NotImplementedException(),
                ILOpCode.Stind_i4 => throw new NotImplementedException(),
                ILOpCode.Stind_i8 => throw new NotImplementedException(),
                ILOpCode.Stind_r4 => visitor.VisitStIndirect<FloatType<N32>>(),
                ILOpCode.Stind_r8 => throw new NotImplementedException(),
                ILOpCode.Add => visitor.VisitBinaryArithmetic<BinaryArithmetic.Add>(),
                ILOpCode.Sub => throw new NotImplementedException(),
                ILOpCode.Mul => throw new NotImplementedException(),
                ILOpCode.Div => visitor.VisitBinaryArithmetic<BinaryArithmetic.Div>(),
                ILOpCode.Div_un => visitor.VisitBinaryArithmetic<BinaryArithmetic.Div>(true),
                ILOpCode.Rem => throw new NotImplementedException(),
                ILOpCode.Rem_un => throw new NotImplementedException(),
                ILOpCode.And => throw new NotImplementedException(),
                ILOpCode.Or => throw new NotImplementedException(),
                ILOpCode.Xor => throw new NotImplementedException(),
                ILOpCode.Shl => throw new NotImplementedException(),
                ILOpCode.Shr => throw new NotImplementedException(),
                ILOpCode.Shr_un => throw new NotImplementedException(),
                ILOpCode.Neg => throw new NotImplementedException(),
                ILOpCode.Not => throw new NotImplementedException(),
                ILOpCode.Conv_i1 => throw new NotImplementedException(),
                ILOpCode.Conv_i2 => throw new NotImplementedException(),
                ILOpCode.Conv_i4 => throw new NotImplementedException(),
                ILOpCode.Conv_i8 => throw new NotImplementedException(),
                ILOpCode.Conv_r4 => throw new NotImplementedException(),
                ILOpCode.Conv_r8 => throw new NotImplementedException(),
                ILOpCode.Conv_u4 => throw new NotImplementedException(),
                ILOpCode.Conv_u8 => throw new NotImplementedException(),
                ILOpCode.Callvirt => throw new NotImplementedException(),
                ILOpCode.Cpobj => throw new NotImplementedException(),
                ILOpCode.Ldobj => throw new NotImplementedException(),
                ILOpCode.Ldstr => throw new NotImplementedException(),
                ILOpCode.Newobj => throw new NotImplementedException(),
                ILOpCode.Castclass => throw new NotImplementedException(),
                ILOpCode.Isinst => throw new NotImplementedException(),
                ILOpCode.Conv_r_un => throw new NotImplementedException(),
                ILOpCode.Unbox => throw new NotImplementedException(),
                ILOpCode.Throw => throw new NotImplementedException(),
                ILOpCode.Ldfld => throw new NotImplementedException(),
                ILOpCode.Ldflda => throw new NotImplementedException(),
                ILOpCode.Stfld => throw new NotImplementedException(),
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
                ILOpCode.Ceq => throw new NotImplementedException(),
                ILOpCode.Cgt => throw new NotImplementedException(),
                ILOpCode.Cgt_un => throw new NotImplementedException(),
                ILOpCode.Clt => throw new NotImplementedException(),
                ILOpCode.Clt_un => throw new NotImplementedException(),
                ILOpCode.Ldftn => throw new NotImplementedException(),
                ILOpCode.Ldvirtftn => throw new NotImplementedException(),
                ILOpCode.Ldarg => throw new NotImplementedException(),
                ILOpCode.Ldarga => throw new NotImplementedException(),
                ILOpCode.Starg => throw new NotImplementedException(),
                ILOpCode.Ldloc => throw new NotImplementedException(),
                ILOpCode.Ldloca => throw new NotImplementedException(),
                ILOpCode.Stloc => throw new NotImplementedException(),
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
        }
        return result;
    }
}

public sealed class RuntimeReflectionMethodBodyParser(
    ICompilationContextView Context
)
{
    sealed class MethodBodyParseResult : ICilVisitor<Unit>
    {
        public int CodeSize { get; }
        public List<IInstruction> Instructions { get; private set; } = [];
        public List<int> InstructionsOriginalByteOffset { get; private set; } = [];
        ICompilationContextView Context { get; }
        IReadOnlyList<Instruction> CilInstructions { get; }
        List<int> ByteOffset { get; }
        Stack<IShaderType>? CurrentStack { get; set; } = null;
        Stack<IShaderType> EvaluationStack => CurrentStack ?? throw new NullReferenceException();
        Dictionary<int, Stack<IShaderType>> JumpStack { get; } = [];
        int CurrentInstructionByteOffset { get; set; } = 0;
        int CurrentInstructionIndex { get; set; } = 0;

        public CilVisitorContext CurrentContext { get; set; } = default;

        public MethodBodyParseResult(MethodBase method, ICompilationContextView context)
        {
            CodeSize = method.GetMethodBody()?.GetILAsByteArray()?.Length ?? 0;
            Context = context;
            CilInstructions = method.GetInstructions() ?? [];
            ByteOffset = CilInstructions.Select(inst => inst.Offset).ToList();
            ByteOffset.Add(CodeSize);
            var walker = new CilMethodBodyWalker(method);
            _ = walker.Accept<MethodBodyParseResult, Unit>(this);
        }

        void Emit(IInstruction instruction)
        {
            Instructions.Add(instruction);
            InstructionsOriginalByteOffset.Add(CurrentInstructionByteOffset);
        }

        void EmitBrS(Instruction instruction)
        {
            MarkNextInstructionAsLead();
            throw new NotImplementedException();
        }
        void EmitBr(Instruction instruction)
        {
            MarkNextInstructionAsLead();
            throw new NotImplementedException();
        }

        void EmitBrIf(Instruction instruction)
        {
            MarkNextInstructionAsLead();
            throw new NotImplementedException();
        }
        void EmitBrIfS(Instruction instruction)
        {
            MarkNextInstructionAsLead();
            throw new NotImplementedException();
        }
        void MarkNextInstructionAsLead()
        {
            throw new NotImplementedException();
        }

        VariableDeclaration GetLocalVariable(LocalVariableInfo info)
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

        void EmitBinaryArithmetic<TOp>()
            where TOp : BinaryArithmetic.IOp<TOp>
        {
            throw new NotImplementedException();
        }

        void EmitBinaryArithmeticUn<TOp>()
            where TOp : BinaryArithmetic.IOp<TOp>
        {
            throw new NotImplementedException();
        }

        void EmitBinaryRelation<TOp>()
            where TOp : BinaryRelation.IOp<TOp>
        {
            throw new NotImplementedException();
        }

        void EmitBinaryRelationUn<TOp>()
            where TOp : BinaryRelation.IOp<TOp>
        {
            throw new NotImplementedException();
        }

        void EmitConst<TLiteral>(TLiteral literal)
            where TLiteral : ILiteral
        {
            Emit(ShaderInstruction.Const(literal));
            EvaluationStack.Push(literal.Type);
        }

        public Unit VisitNop()
        {
            Emit(ShaderInstruction.Nop());
            return default;
        }

        public Unit VisitBreak()
        {
            throw new NotSupportedException("break instruction is not supported");
        }

        public Unit VisitLdArg(ParameterInfo info)
        {
            Emit(ShaderInstruction.Load(GetParameter(info)));
            return default;
        }

        public Unit VisitLdArgAddress(ParameterInfo info)
        {
            Emit(ShaderInstruction.LoadAddress(GetParameter(info)));
            return default;
        }

        public Unit VisitStArg(ParameterInfo info)
        {
            Emit(ShaderInstruction.Store(GetParameter(info)));
            return default;
        }

        public Unit VisitLdLoc(LocalVariableInfo info)
        {
            Emit(ShaderInstruction.Load(GetLocalVariable(info)));
            return default;
        }

        public Unit VisitLdLocAddress(LocalVariableInfo info)
        {
            throw new NotImplementedException();
        }

        public Unit VisitStLoc(LocalVariableInfo info)
        {
            var sType = EvaluationStack.Pop();
            var decl = GetLocalVariable(info);
            Debug.Assert(decl.Type.Equals(sType));
            Emit(ShaderInstruction.Store(decl));
            return default;
        }

        public Unit VisitLdNull()
        {
            throw new NotImplementedException();
        }

        public Unit VisitCall(MethodInfo method)
        {
            throw new NotImplementedException();
        }

        public Unit VisitNewObj(ConstructorInfo constructor)
        {
            throw new NotImplementedException();
        }

        public Unit VisitReturn()
        {
            throw new NotImplementedException();
        }

        public Unit VisitLiteral<TLiteral>(TLiteral literal) where TLiteral : ILiteral
        {
            throw new NotImplementedException();
        }

        public Unit VisitBr(int jumpOffset)
        {
            throw new NotImplementedException();
        }

        public Unit VisitBrIf(int jumpOffset, bool value)
        {
            throw new NotImplementedException();
        }

        public Unit VisitBrIf<TOp>(int jumpOffset, bool isUn = false) where TOp : BinaryRelation.IOp<TOp>
        {
            throw new NotImplementedException();
        }

        public Unit VisitSwitch()
        {
            throw new NotImplementedException();
        }

        public Unit VisitBinaryArithmetic<TOp>(bool isUn = false, bool isChecked = false) where TOp : BinaryArithmetic.IOp<TOp>
        {
            throw new NotImplementedException();
        }

        public Unit VisitBinaryBitwise<TOp>(bool isUn = false, bool isChecked = false) where TOp : BinaryArithmetic.IOp<TOp>
        {
            throw new NotImplementedException();
        }

        public Unit VisitBinaryRelation<TOp>(bool isUn = false, bool isChecked = false) where TOp : BinaryRelation.IOp<TOp>
        {
            throw new NotImplementedException();
        }

        public Unit VisitBinaryLogical<TOp>() where TOp : BinaryRelation.IOp<TOp>
        {
            throw new NotImplementedException();
        }

        public Unit VisitUnaryLogical<TOp>() where TOp : BinaryRelation.IOp<TOp>
        {
            throw new NotImplementedException();
        }

        public Unit VisitLdIndirect<TShaderType>() where TShaderType : IShaderType
        {
            throw new NotImplementedException();
        }

        public Unit VisitStIndirect<TShaderType>() where TShaderType : IShaderType
        {
            throw new NotImplementedException();
        }

        public Unit VisitLdIndirectNativeInt()
        {
            throw new NotImplementedException();
        }

        public Unit VisitLdIndirectRef()
        {
            throw new NotImplementedException();
        }

        public Unit VisitStIndirectRef()
        {
            throw new NotImplementedException();
        }
    }


    public ImmutableArray<IInstruction> Run(MethodBase method)
    {
        var result = new MethodBodyParseResult(method, Context);
        return [.. result.Instructions];
    }
}
