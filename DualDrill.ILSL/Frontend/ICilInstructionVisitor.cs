using DualDrill.CLSL.Language.Literal;
using DualDrill.CLSL.Language.Operation;
using DualDrill.CLSL.Language.Types;
using Lokad.ILPack.IL;
using System.Reflection;

namespace DualDrill.CLSL.Frontend;

public readonly record struct CilInstructionInfo(int Index, int ByteOffset, int NextByteOffset, Instruction Instruction)
{
}
public interface ICilInstructionVisitor<TResult>
{
    void BeforeVisitInstruction(CilInstructionInfo info);
    void AfterVisitInstruction(CilInstructionInfo info);
    TResult VisitNop(CilInstructionInfo inst);
    TResult VisitBreak(CilInstructionInfo inst);
    TResult VisitLdArg(CilInstructionInfo inst, ParameterInfo info);
    TResult VisitLdArgAddress(CilInstructionInfo inst, ParameterInfo info);
    TResult VisitStArg(CilInstructionInfo inst, ParameterInfo info);
    TResult VisitLdThis(CilInstructionInfo inst);
    TResult VisitStThis(CilInstructionInfo inst);
    TResult VisitLdLoc(CilInstructionInfo inst, LocalVariableInfo info);
    TResult VisitLdLocAddress(CilInstructionInfo inst, LocalVariableInfo info);
    TResult VisitStLoc(CilInstructionInfo inst, LocalVariableInfo info);
    TResult VisitLdFld(CilInstructionInfo inst, FieldInfo info);
    TResult VisitLdFldAddress(CilInstructionInfo inst, FieldInfo info);
    TResult VisitStFld(CilInstructionInfo inst, FieldInfo info);
    TResult VisitLdsfld(CilInstructionInfo inst, FieldInfo info);
    TResult VisitLdsflda(CilInstructionInfo inst, FieldInfo info);
    TResult VisitLdNull(CilInstructionInfo info);
    TResult VisitCall(CilInstructionInfo info, MethodInfo method);
    TResult VisitNewObj(CilInstructionInfo info, ConstructorInfo constructor);
    TResult VisitReturn(CilInstructionInfo info);
    TResult VisitLiteral<TLiteral>(CilInstructionInfo info, TLiteral literal) where TLiteral : ILiteral;
    TResult VisitBr(CilInstructionInfo inst, int jumpOffset);
    TResult VisitBrIf(CilInstructionInfo inst, int jumpOffset, bool value);
    TResult VisitBrIf<TOp>(CilInstructionInfo inst, int jumpOffset, bool isUn = false) where TOp : BinaryRelation.IOp<TOp>;
    TResult VisitSwitch(CilInstructionInfo inst);
    TResult VisitBinaryArithmetic<TOp>(CilInstructionInfo inst, bool isUn = false, bool isChecked = false)
        where TOp : BinaryArithmetic.IOp<TOp>;
    TResult VisitBinaryBitwise<TOp>(CilInstructionInfo inst, bool isUn = false, bool isChecked = false)
        where TOp : BinaryArithmetic.IOp<TOp>;
    TResult VisitBinaryLogical<TOp>(CilInstructionInfo inst)
        where TOp : BinaryLogical.IOp<TOp>;
    TResult VisitBinaryRelation<TOp>(CilInstructionInfo inst, bool isUn = false, bool isChecked = false) where TOp : BinaryRelation.IOp<TOp>;
    TResult VisitConversion<TTarget>(CilInstructionInfo inst) where TTarget : IScalarType<TTarget>;
    TResult VisitUnaryLogical<TOp>(CilInstructionInfo inst) where TOp : BinaryRelation.IOp<TOp>;
    TResult VisitLdIndirect<TShaderType>(CilInstructionInfo inst) where TShaderType : IShaderType;
    TResult VisitStIndirect<TShaderType>(CilInstructionInfo inst) where TShaderType : IShaderType;
    TResult VisitLdIndirectNativeInt(CilInstructionInfo inst);
    TResult VisitLdIndirectRef(CilInstructionInfo inst);
    TResult VisitStIndirectRef(CilInstructionInfo inst);
    TResult VisitDup(CilInstructionInfo inst);
    TResult VisitPop(CilInstructionInfo inst);
}
