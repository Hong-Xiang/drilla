using DualDrill.CLSL.Language.Declaration;
using DualDrill.CLSL.Language.Literal;
using DualDrill.CLSL.Language.Operation;
using DualDrill.CLSL.Language.Types;
using DualDrill.CLSL.LinearInstruction;
using DualDrill.Common;
using DualDrill.ILSL.Compiler;
using Lokad.ILPack.IL;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Reflection;

namespace DualDrill.ILSL.Frontend;



public sealed class RuntimeReflectionMethodBodyParser(
    ICompilationContextView Context
)
{
    sealed class MethodBodyParseResult : ICilInstructionVisitor<Unit>
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

        //public CilVisitorContext CurrentContext { get; set; } = default;
        ICilInstructionVisitor<Unit>.Context ICilInstructionVisitor<Unit>.CurrentContext { set => throw new NotImplementedException(); }

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
