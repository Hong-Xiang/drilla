using DualDrill.CLSL.Language.ControlFlowGraph;
using DualDrill.CLSL.Language.Declaration;
using DualDrill.CLSL.Language.Literal;
using DualDrill.CLSL.Language.Operation;
using DualDrill.CLSL.Language.Types;
using DualDrill.CLSL.LinearInstruction;
using DualDrill.Common;
using DualDrill.ILSL.Compiler;
using Lokad.ILPack.IL;
using System.Collections.Frozen;
using System.Reflection;

namespace DualDrill.ILSL.Frontend;

public sealed class RuntimeReflectionMethodBodyParser(
    ICompilationContextView Context
)
{
    public UnstructuredControlFlowInstructionFunctionBody Parse(FunctionDeclaration decl)
    {
        var methodContext = new CompilationContext(Context);

        var method = Context.GetFunctionDefinition(decl);

        var traverser = new MethodBodyCilInstructionTraverser(method);

        var body = method.GetMethodBody() ?? throw new NotSupportedException($"Failed to get method body");

        foreach (var v in body.LocalVariables)
        {
            methodContext.AddVariable(Symbol.Variable(v),
                index => new VariableDeclaration(
                    DeclarationScope.Function,
                    index,
                    $"loc_{v.LocalIndex}",
                    Context[v.LocalType] ?? throw new KeyNotFoundException($"Failed to resolve local varialbe {v}"),
                    []
                )
            );
        }

        var labels = new Dictionary<int, Label>();
        foreach (var (index, inst) in traverser.Instructions.Index())
        {
            var flowControl = inst.OpCode.FlowControl;
            if (flowControl == System.Reflection.Emit.FlowControl.Branch ||
                flowControl == System.Reflection.Emit.FlowControl.Cond_Branch)
            {
                var nextOffset = traverser.Offsets[index + 1];
                int jumpOffset = inst.Operand switch
                {
                    sbyte v => v,
                    int v => v,
                    _ => throw new NotSupportedException()
                };
                var target = nextOffset + jumpOffset;
                labels.TryAdd(target, Label.Create(target));
            }
        }

        var visitor = new InstructionVisitor(methodContext, labels.ToFrozenDictionary());
        traverser.Accept<InstructionVisitor, Unit>(visitor);
        return new(visitor.Instructions);
    }

    sealed class InstructionVisitor(
        CompilationContext Context,
        FrozenDictionary<int, Label> Labels
    ) : ICilInstructionVisitor<Unit>
    {
        public List<IStackInstruction> Instructions { get; } = [];

        public void AfterVisitInstruction(CilInstructionInfo info)
        {
        }

        public void BeforeVisitInstruction(CilInstructionInfo info)
        {
            if (Labels.TryGetValue(info.ByteOffset, out var label))
            {
                Instructions.Add(new LabelInstruction(label));
            }
        }

        public Unit VisitBinaryArithmetic<TOp>(CilInstructionInfo inst, bool isUn = false, bool isChecked = false) where TOp : BinaryArithmetic.IOp<TOp>
        {
            throw new NotImplementedException();
        }

        public Unit VisitBinaryBitwise<TOp>(CilInstructionInfo inst, bool isUn = false, bool isChecked = false) where TOp : BinaryArithmetic.IOp<TOp>
        {
            throw new NotImplementedException();
        }

        public Unit VisitBinaryLogical<TOp>(CilInstructionInfo inst) where TOp : BinaryRelation.IOp<TOp>
        {
            throw new NotImplementedException();
        }

        public Unit VisitBinaryRelation<TOp>(CilInstructionInfo inst, bool isUn = false, bool isChecked = false) where TOp : BinaryRelation.IOp<TOp>
        {
            throw new NotImplementedException();
        }

        public Unit VisitBr(CilInstructionInfo inst, int jumpOffset)
        {
            var label = Labels[jumpOffset + inst.NextByteOffset];
            Instructions.Add(ShaderInstruction.BrIf(label));
            return default;
        }

        public Unit VisitBreak(CilInstructionInfo inst)
        {
            throw new NotSupportedException($"{inst.Instruction.OpCode}");
        }

        public Unit VisitBrIf(CilInstructionInfo inst, int jumpOffset, bool value)
        {
            // bf.false
            if (value == false)
            {
                // TODO: add logical not instruction
            }

            var label = Labels[jumpOffset + inst.NextByteOffset];
            Instructions.Add(ShaderInstruction.BrIf(label));
            return default;
        }

        public Unit VisitBrIf<TOp>(CilInstructionInfo inst, int jumpOffset, bool isUn = false) where TOp : BinaryRelation.IOp<TOp>
        {
            // TODO: handle relation operation
            var label = Labels[jumpOffset + inst.NextByteOffset];
            Instructions.Add(ShaderInstruction.BrIf(label));
            return default;
        }

        public Unit VisitCall(CilInstructionInfo info, MethodInfo method)
        {
            var f = Context[Symbol.Function(method)] ?? throw new KeyNotFoundException($"called {method} not found in context");
            Instructions.Add(ShaderInstruction.Call(f));
            return default;
        }

        public Unit VisitLdArg(CilInstructionInfo inst, ParameterInfo info)
        {
            var p = Context[info] ?? throw new KeyNotFoundException($"Failed to resolve parameter {info}");
            Instructions.Add(ShaderInstruction.Load(p));
            return default;
        }

        public Unit VisitLdArgAddress(CilInstructionInfo inst, ParameterInfo info)
        {
            var p = Context[info] ?? throw new KeyNotFoundException($"Failed to resolve parameter {info}");
            Instructions.Add(ShaderInstruction.LoadAddress(p));
            return default;
        }

        public Unit VisitLdIndirect<TShaderType>(CilInstructionInfo inst) where TShaderType : IShaderType
        {
            throw new NotImplementedException();
        }

        public Unit VisitLdIndirectNativeInt(CilInstructionInfo inst)
        {
            throw new NotImplementedException();
        }

        public Unit VisitLdIndirectRef(CilInstructionInfo inst)
        {
            throw new NotImplementedException();
        }

        public Unit VisitLdLoc(CilInstructionInfo inst, LocalVariableInfo info)
        {
            var v = Context[Symbol.Variable(info)] ?? throw new KeyNotFoundException($"Failed to resolve local variable {info}");
            Instructions.Add(ShaderInstruction.Load(v));
            return default;

        }

        public Unit VisitLdLocAddress(CilInstructionInfo inst, LocalVariableInfo info)
        {
            var v = Context[Symbol.Variable(info)] ?? throw new KeyNotFoundException($"Failed to resolve local variable {info}");
            Instructions.Add(ShaderInstruction.LoadAddress(v));
            return default;
        }

        public Unit VisitLdNull(CilInstructionInfo info)
        {
            throw new NotImplementedException();
        }

        public Unit VisitLiteral<TLiteral>(CilInstructionInfo info, TLiteral literal) where TLiteral : ILiteral
        {
            Instructions.Add(ShaderInstruction.Const(literal));
            return default;
        }

        public Unit VisitNewObj(CilInstructionInfo info, ConstructorInfo constructor)
        {
            throw new NotImplementedException();
        }

        public Unit VisitNop(CilInstructionInfo inst)
        {
            Instructions.Add(ShaderInstruction.Nop());
            return default;
        }

        public Unit VisitReturn(CilInstructionInfo info)
        {
            Instructions.Add(ShaderInstruction.Return());
            return default;
        }

        public Unit VisitStArg(CilInstructionInfo inst, ParameterInfo info)
        {
            var p = Context[info] ?? throw new KeyNotFoundException($"Failed to resolve parameter {inst}");
            Instructions.Add(ShaderInstruction.LoadAddress(p));
            return default;
        }

        public Unit VisitStIndirect<TShaderType>(CilInstructionInfo inst) where TShaderType : IShaderType
        {
            throw new NotImplementedException();
        }

        public Unit VisitStIndirectRef(CilInstructionInfo inst)
        {
            throw new NotImplementedException();
        }

        public Unit VisitStLoc(CilInstructionInfo inst, LocalVariableInfo info)
        {
            var v = Context[Symbol.Variable(info)] ?? throw new KeyNotFoundException($"Failed to resolve local variable {info}");
            Instructions.Add(ShaderInstruction.Store(v));
            return default;
        }

        public Unit VisitSwitch(CilInstructionInfo inst)
        {
            throw new NotImplementedException();
        }

        public Unit VisitUnaryLogical<TOp>(CilInstructionInfo inst) where TOp : BinaryRelation.IOp<TOp>
        {
            throw new NotImplementedException();
        }
    }
}
