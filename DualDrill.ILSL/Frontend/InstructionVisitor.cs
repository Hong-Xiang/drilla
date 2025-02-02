using DualDrill.CLSL.Language.ControlFlowGraph;
using DualDrill.CLSL.Language.Declaration;
using DualDrill.CLSL.Language.Literal;
using DualDrill.CLSL.Language.Operation;
using DualDrill.CLSL.Language.Types;
using DualDrill.CLSL.LinearInstruction;
using DualDrill.Common;
using System.Collections.Frozen;
using System.Reflection;

namespace DualDrill.CLSL.Frontend;

public sealed class ValidationException(string message)
    : Exception(message)
{
}

sealed class InstructionVisitor(
        RuntimeReflectionParser Parser,
        ICompilationContext Context,
        FunctionDeclaration Function,
        FrozenDictionary<int, Label> Labels
    ) : ICilInstructionVisitor<Unit>
{
    List<IStackInstruction> Instructions { get; } = [];

    public IReadOnlyList<IStackInstruction> Result => Instructions;

    Stack<IShaderType>? ValidationStack { get; set; } = [];

    Stack<IShaderType> CurrentStack => ValidationStack ?? throw new NullReferenceException("Invalid null stack state");

    public Dictionary<Label, Stack<IShaderType>> JumpStack { get; } = [];

    public void AfterVisitInstruction(CilInstructionInfo info)
    {
        Console.Error.Write($"After inst#{info.Index} - {info.Instruction.OpCode}\t{info.Instruction.Operand} - [");
        if (ValidationStack is not null)
        {
            foreach (var s in CurrentStack)
            {
                Console.Error.Write(s.Name);
                Console.Error.Write(", ");
            }
        }
        Console.Error.WriteLine("]");
    }

    public void BeforeVisitInstruction(CilInstructionInfo info)
    {
        if (Labels.TryGetValue(info.ByteOffset, out var label))
        {
            Instructions.Add(new LabelInstruction(label));
            if (ValidationStack is null)
            {
                ValidationStack = JumpStack[label];
            }
            else
            {
                // TODO: check for consistency
            }
        }
    }

    public Unit VisitBinaryArithmetic<TOp>(CilInstructionInfo inst, bool isUn = false, bool isChecked = false)
        where TOp : BinaryArithmetic.IOp<TOp>
    {
        var r = CurrentStack.Pop();
        var l = CurrentStack.Pop();
        var t = EnsureBinaryOpOprandsType(l, r);
        if (t is INumericType nt)
        {
            var c = nt.GetBinaryOperation<TOp>().Instruction;
            Instructions.Add(c);
            CurrentStack.Push(nt);
            return default;
        }
        throw new NotImplementedException();
    }

    public Unit VisitBinaryBitwise<TOp>(CilInstructionInfo inst, bool isUn = false, bool isChecked = false)
        where TOp : BinaryArithmetic.IOp<TOp>
    {
        throw new NotImplementedException();
    }

    public Unit VisitBinaryLogical<TOp>(CilInstructionInfo inst)
        where TOp : BinaryLogical.IOp<TOp>
    {
        var r = CurrentStack.Pop();
        var l = CurrentStack.Pop();
        var t = EnsureBinaryOpOprandsType(l, r);

        if (t is INumericType nt)
        {
            var c = nt.GetBinaryOperation<TOp>().Instruction;
            Instructions.Add(c);
            CurrentStack.Push(nt);
            return default;
        }
        throw new NotImplementedException();
    }

    IShaderType EnsureBinaryOpOprandsType(IShaderType l, IShaderType r)
    {
        if (l.Equals(r))
        {
            return l;
        }
        throw new ValidationException($"binary arithmeic validation mismatch {l}, {r}");
    }

    public Unit VisitBinaryRelation<TOp>(CilInstructionInfo inst, bool isUn = false, bool isChecked = false) where TOp : BinaryRelation.IOp<TOp>
    {
        var r = CurrentStack.Pop();
        var l = CurrentStack.Pop();
        var t = EnsureBinaryOpOprandsType(l, r);
        if (t is INumericType nt)
        {
            var c = nt.GetBinaryOperation<TOp>().Instruction;
            Instructions.Add(c);
            CurrentStack.Push(nt);
            return default;
        }
        throw new NotImplementedException();
    }

    public Unit VisitBr(CilInstructionInfo inst, int jumpOffset)
    {
        var label = Labels[jumpOffset + inst.NextByteOffset];
        Instructions.Add(ShaderInstruction.Br(label));
        if (JumpStack.TryGetValue(label, out var s))
        {
            // TODO: check for consistency
        }
        else
        {
            JumpStack.TryAdd(label, new Stack<IShaderType>(CurrentStack));
        }
        ValidationStack = null;
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
            var v = CurrentStack.Pop();
            if (v is not BoolType)
            {
                // TODO: add conversion instruction
            }
            Instructions.Add(ShaderInstruction.LogicalNot());
        }

        var label = Labels[jumpOffset + inst.NextByteOffset];
        Instructions.Add(ShaderInstruction.BrIf(label));
        if (JumpStack.TryGetValue(label, out var s))
        {
            // TODO: check for consistency
        }
        else
        {
            JumpStack.TryAdd(label, new Stack<IShaderType>(CurrentStack));
        }
        return default;
    }

    public Unit VisitBrIf<TOp>(CilInstructionInfo inst, int jumpOffset, bool isUn = false) where TOp : BinaryRelation.IOp<TOp>
    {
        var v = CurrentStack.Pop();

        if (v is INumericType nt)
        {
            Instructions.Add(nt.GetBinaryOperation<TOp>().Instruction);
        }
        else
        {
            throw new NotImplementedException();
        }


        var label = Labels[jumpOffset + inst.NextByteOffset];
        Instructions.Add(ShaderInstruction.BrIf(label));
        if (JumpStack.TryGetValue(label, out var s))
        {
            // TODO: check for consistency
        }
        else
        {
            JumpStack.TryAdd(label, new Stack<IShaderType>(CurrentStack));
        }
        return default;
    }

    public Unit VisitCall(CilInstructionInfo info, MethodInfo method)
    {
        var f = Context[Symbol.Function(method)] ?? throw new KeyNotFoundException($"called {method} not found in context");
        foreach (var (idx, p) in f.Parameters.Reverse().Index())
        {
            var vt = CurrentStack.Pop();
            if (!vt.Equals(p.Type))
            {
                throw new ValidationException($"parameter {p} not match: stack {vt} declaration {p.Type}");
            }
        }
        Instructions.Add(ShaderInstruction.Call(f));
        if (f.Return.Type is not UnitType)
        {
            CurrentStack.Push(f.Return.Type);
        }
        return default;
    }

    public Unit VisitLdArg(CilInstructionInfo inst, ParameterInfo info)
    {
        var p = Context[info] ?? throw new KeyNotFoundException($"Failed to resolve parameter {info}");
        Instructions.Add(ShaderInstruction.Load(p));
        CurrentStack.Push(p.Type);
        return default;
    }

    public Unit VisitLdArgAddress(CilInstructionInfo inst, ParameterInfo info)
    {
        var p = Context[info] ?? throw new KeyNotFoundException($"Failed to resolve parameter {info}");
        var c = ShaderInstruction.LoadAddress(p);
        Instructions.Add(c);
        CurrentStack.Push(p.Type.GetRefType());
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
        CurrentStack.Push(v.Type);
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
        var c = ShaderInstruction.Const(literal);
        Instructions.Add(c);
        CurrentStack.Push(literal.Type);
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
        switch (CurrentStack.Count)
        {
            case 0:
                if (Function.Return.Type is not UnitType)
                {
                    throw new ValidationException("return when stack is empty requires unit type");
                }
                Instructions.Add(ShaderInstruction.Return());
                ValidationStack = null;
                return default;
            case 1:
                var vt = CurrentStack.Pop();
                var rt = Function.Return.Type;
                if (!rt.Equals(vt))
                {
                    throw new ValidationException($"return when statck type {vt} is not consistent with function signature return {rt}");
                }
                Instructions.Add(ShaderInstruction.Return());
                ValidationStack = null;
                return default;
            default:
                throw new ValidationException("return when stack.Count > 1");
        }
    }

    public Unit VisitStArg(CilInstructionInfo inst, ParameterInfo info)
    {
        var p = Context[info] ?? throw new KeyNotFoundException($"Failed to resolve parameter {inst}");
        var v = CurrentStack.Pop();
        // TODO: check for consistency, may emit convert instruction here
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
        var v = CurrentStack.Pop();
        var varDecl = Context[Symbol.Variable(info)] ?? throw new KeyNotFoundException($"Failed to resolve local variable {info}");
        // TODO: check for consistency, may emit convert instruction here
        Instructions.Add(ShaderInstruction.Store(varDecl));
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

    public Unit VisitConversion<TTarget>(CilInstructionInfo inst) where TTarget : IScalarType<TTarget>
    {
        var t = CurrentStack.Pop();
        if (t is IScalarType nt)
        {
            Instructions.Add(nt.GetConversionToOperation<TTarget>().Instruction);
            CurrentStack.Push(TTarget.Instance);
            return default;
        }
        throw new NotImplementedException();
    }
}
