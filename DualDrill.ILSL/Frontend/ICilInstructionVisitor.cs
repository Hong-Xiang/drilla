using DualDrill.CLSL.Language.Declaration;
using DualDrill.CLSL.Language.Literal;
using DualDrill.CLSL.Language.Operation;
using DualDrill.CLSL.Language.Types;

namespace DualDrill.CLSL.Frontend;

public interface ICilInstructionVisitor<TResult>
{
    TResult VisitNop(CilInstructionInfo inst);
    TResult VisitBreak(CilInstructionInfo inst);
    TResult VisitLoadArgument(CilInstructionInfo inst, ParameterDeclaration p);
    TResult VisitLoadArgumentAddress(CilInstructionInfo inst, ParameterDeclaration p);
    TResult VisitStoreArgument(CilInstructionInfo inst, ParameterDeclaration p);
    TResult VisitLdThis(CilInstructionInfo inst);
    TResult VisitStThis(CilInstructionInfo inst);
    TResult VisitLoadLocal(CilInstructionInfo inst, VariableDeclaration v);
    TResult VisitLoadLocalAddress(CilInstructionInfo inst, VariableDeclaration v);
    TResult VisitStoreLocal(CilInstructionInfo inst, VariableDeclaration v);
    TResult VisitLoadField(CilInstructionInfo inst, MemberDeclaration m);
    TResult VisitLoadFieldAddress(CilInstructionInfo inst, MemberDeclaration m);
    TResult VisitStoreField(CilInstructionInfo inst, MemberDeclaration m);
    TResult VisitLoadStaticField(CilInstructionInfo inst, VariableDeclaration v);
    TResult VisitLoadStaticFieldAddress(CilInstructionInfo inst, VariableDeclaration v);
    TResult VisitLoadNull(CilInstructionInfo info);
    TResult VisitCall(CilInstructionInfo info, FunctionDeclaration f);
    TResult VisitNewObject(CilInstructionInfo info, FunctionDeclaration f);
    TResult VisitReturn(CilInstructionInfo info);
    TResult VisitLiteral<TLiteral>(CilInstructionInfo info, TLiteral literal) where TLiteral : ILiteral;
    TResult VisitBranch(CilInstructionInfo inst, int jumpOffset);
    TResult VisitBranchIf(CilInstructionInfo inst, int jumpOffset, bool value);

    TResult VisitBranchIf<TOp>(CilInstructionInfo inst, int jumpOffset, bool isUn = false)
        where TOp : BinaryRelational.IOp<TOp>;

    TResult VisitSwitch(CilInstructionInfo inst);

    TResult VisitBinaryArithmetic<TOp>(CilInstructionInfo inst, bool isUn = false, bool isChecked = false)
        where TOp : BinaryArithmetic.IOp<TOp>;

    TResult VisitBinaryLogical<TOp>(CilInstructionInfo inst)
        where TOp : BinaryLogical.IOp<TOp>;

    TResult VisitBinaryRelation<TOp>(CilInstructionInfo inst, bool isUn = false, bool isChecked = false)
        where TOp : BinaryRelational.IOp<TOp>;

    TResult VisitConversion<TTarget>(CilInstructionInfo inst) where TTarget : IScalarType<TTarget>;
    TResult VisitLogicalNot(CilInstructionInfo inst);
    TResult VisitUnaryArithmetic<TOp>(CilInstructionInfo inst) where TOp : UnaryArithmetic.IOp<TOp>;

    TResult VisitLoadIndirect<TShaderType>(CilInstructionInfo inst) where TShaderType : IShaderType;
    TResult VisitStoreIndirect<TShaderType>(CilInstructionInfo inst) where TShaderType : IShaderType;
    TResult VisitLoadIndirectNativeInt(CilInstructionInfo inst);
    TResult VisitLoadIndirectRef(CilInstructionInfo inst);
    TResult VisitStoreIndirectRef(CilInstructionInfo inst);
    TResult VisitDup(CilInstructionInfo inst);
    TResult VisitPop(CilInstructionInfo inst);
}