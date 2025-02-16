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
    TResult VisitLoadArgument(CilInstructionInfo inst, ParameterInfo info);
    TResult VisitLoadArgumentAddress(CilInstructionInfo inst, ParameterInfo info);
    TResult VisitStoreArgument(CilInstructionInfo inst, ParameterInfo info);
    TResult VisitLdThis(CilInstructionInfo inst);
    TResult VisitStThis(CilInstructionInfo inst);
    TResult VisitLoadLocal(CilInstructionInfo inst, LocalVariableInfo info);
    TResult VisitLoadLocalAddress(CilInstructionInfo inst, LocalVariableInfo info);
    TResult VisitStoreLocal(CilInstructionInfo inst, LocalVariableInfo info);
    TResult VisitLoadField(CilInstructionInfo inst, FieldInfo info);
    TResult VisitLoadFieldAddress(CilInstructionInfo inst, FieldInfo info);
    TResult VisitStoreField(CilInstructionInfo inst, FieldInfo info);
    TResult VisitLoadStaticField(CilInstructionInfo inst, FieldInfo info);
    TResult VisitLoadStaticFieldAddress(CilInstructionInfo inst, FieldInfo info);
    TResult VisitLoadNull(CilInstructionInfo info);
    TResult VisitCall(CilInstructionInfo info, MethodInfo method);
    TResult VisitNewObject(CilInstructionInfo info, ConstructorInfo constructor);
    TResult VisitReturn(CilInstructionInfo info);
    TResult VisitLiteral<TLiteral>(CilInstructionInfo info, TLiteral literal) where TLiteral : ILiteral;
    TResult VisitBranch(CilInstructionInfo inst, int jumpOffset);
    TResult VisitBranchIf(CilInstructionInfo inst, int jumpOffset, bool value);

    TResult VisitBranchIf<TOp>(CilInstructionInfo inst, int jumpOffset, bool isUn = false)
        where TOp : BinaryRelation.IOp<TOp>;

    TResult VisitSwitch(CilInstructionInfo inst);

    TResult VisitBinaryArithmetic<TOp>(CilInstructionInfo inst, bool isUn = false, bool isChecked = false)
        where TOp : BinaryArithmetic.IOp<TOp>;

    TResult VisitBinaryBitwise<TOp>(CilInstructionInfo inst, bool isUn = false, bool isChecked = false)
        where TOp : BinaryArithmetic.IOp<TOp>;

    TResult VisitBinaryLogical<TOp>(CilInstructionInfo inst)
        where TOp : BinaryLogical.IOp<TOp>;

    TResult VisitBinaryRelation<TOp>(CilInstructionInfo inst, bool isUn = false, bool isChecked = false)
        where TOp : BinaryRelation.IOp<TOp>;

    TResult VisitConversion<TTarget>(CilInstructionInfo inst) where TTarget : IScalarType<TTarget>;
    TResult VisitUnaryLogical<TOp>(CilInstructionInfo inst) where TOp : BinaryRelation.IOp<TOp>;
    TResult VisitLoadIndirect<TShaderType>(CilInstructionInfo inst) where TShaderType : IShaderType;
    TResult VisitStoreIndirect<TShaderType>(CilInstructionInfo inst) where TShaderType : IShaderType;
    TResult VisitLoadIndirectNativeInt(CilInstructionInfo inst);
    TResult VisitLoadIndirectRef(CilInstructionInfo inst);
    TResult VisitStoreIndirectRef(CilInstructionInfo inst);
    TResult VisitDup(CilInstructionInfo inst);
    TResult VisitPop(CilInstructionInfo inst);
}

public interface ICilMethodBodyAbstractInterpreter<TResult>
{
    TResult Next(ICilInstructionVisitor<TResult> visitor);
    TResult GoTo(ICilInstructionVisitor<TResult> visitor, params ReadOnlySpan<int> offsets);
}