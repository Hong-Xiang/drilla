using DualDrill.CLSL.Language.ControlFlow;
using DualDrill.CLSL.Language.Declaration;
using DualDrill.CLSL.Language.LinearInstruction;
using DualDrill.CLSL.Language.Literal;
using DualDrill.CLSL.Language.Operation;
using DualDrill.CLSL.Language.Types;
using DualDrill.Common;
using System.Collections.Frozen;
using System.Diagnostics;
using System.Reflection;

namespace DualDrill.CLSL.Frontend;

public sealed class ValidationException(string message, MethodBase method)
    : Exception(message)
{
    public MethodBase Method { get; } = method;
}

sealed class RuntimeReflectionParserInstructionVisitor(
        RuntimeReflectionParser Parser,
        ICompilationContext Context,
        FunctionDeclaration Function,
        MethodBase Method,
        FrozenDictionary<int, (Label, int)> Labels
    ) : ICilInstructionVisitor<int[]>
{
    //public List<int> Nexts { get; private set; } = [];

    public List<IStackInstruction> Instructions { get; set; } = [];
    Stack<IShaderType>? ValidationStack { get; set; } = [];

    Stack<IShaderType> CurrentStack => ValidationStack ?? throw new NullReferenceException("Invalid null stack state");

    public Dictionary<Label, Stack<IShaderType>> JumpStack { get; } = [];

    public void AfterVisitInstruction(CilInstructionInfo info)
    {
        Console.Error.Write($"After inst#{info.Index}({info.Instruction.Offset}).({info.NextByteOffset}) - {info.Instruction.OpCode}");
        var opcode = info.Instruction.OpCode;
        if (opcode.FlowControl == System.Reflection.Emit.FlowControl.Branch
            || opcode.FlowControl == System.Reflection.Emit.FlowControl.Cond_Branch)
        {
            switch (info.Instruction.Operand)
            {
                case sbyte v:
                    Console.Error.Write($" -> {info.NextByteOffset + v}");
                    break;
                case int v:
                    Console.Error.Write($" -> {info.NextByteOffset + v}");
                    break;
                default:
                    break;

            }
        }
        Console.Error.Write($"\t{info.Instruction.Operand} - [");
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
        if (Labels.TryGetValue(info.ByteOffset, out var labelIndex))
        {
            var (label, _) = labelIndex;
            Instructions.Add(new LabelInstruction(label));
            if (ValidationStack is null)
            {
                if (JumpStack.TryGetValue(label, out var s))
                {
                    ValidationStack = s;
                }
                else
                {
                    throw new NotSupportedException();
                }
            }
            else
            {
                if (JumpStack.TryGetValue(label, out var jump))
                {
                    if (!ValidationStack.SequenceEqual(jump))
                    {
                        throw new ValidationException($"Stack state after jump is not consistent", Method);
                    }
                }
            }
        }
    }


    public int[] VisitBinaryArithmetic<TOp>(CilInstructionInfo inst, bool isUn = false, bool isChecked = false)
        where TOp : BinaryArithmetic.IOp<TOp>
    {
        var r = CurrentStack.Pop();
        var l = CurrentStack.Pop();
        var t = EnsureBinaryOpOprandsType(l, r);
        if (t is INumericType nt)
        {
            var c = ((IOperation)nt.GetBinaryOperation<TOp>()).Instruction;
            Instructions.Add(c);
            CurrentStack.Push(nt);
            return [inst.Index + 1];
        }
        throw new NotImplementedException();
    }

    public int[] VisitBinaryBitwise<TOp>(CilInstructionInfo inst, bool isUn = false, bool isChecked = false)
        where TOp : BinaryArithmetic.IOp<TOp>
    {
        throw new NotImplementedException();
    }

    public int[] VisitBinaryLogical<TOp>(CilInstructionInfo inst)
        where TOp : BinaryLogical.IOp<TOp>
    {
        var r = CurrentStack.Pop();
        var l = CurrentStack.Pop();
        var t = EnsureBinaryOpOprandsType(l, r);

        if (t is INumericType nt)
        {
            var c = ((IOperation)TOp.Instance.BitwiseOp.GetNumericBinaryOperation(nt)).Instruction;
            Instructions.Add(c);
            CurrentStack.Push(nt);
            return [inst.Index + 1];
        }
        throw new NotImplementedException();
    }

    IShaderType EnsureBinaryOpOprandsType(IShaderType l, IShaderType r)
    {
        if (l.Equals(r))
        {
            return l;
        }
        throw new ValidationException($"binary arithmeic validation mismatch {l}, {r}", Method);
    }

    public int[] VisitBinaryRelation<TOp>(CilInstructionInfo inst, bool isUn = false, bool isChecked = false) where TOp : BinaryRelation.IOp<TOp>
    {
        var r = CurrentStack.Pop();
        var l = CurrentStack.Pop();
        var t = EnsureBinaryOpOprandsType(l, r);
        if (t is INumericType nt)
        {
            var c = ((IOperation)nt.GetBinaryOperation<TOp>()).Instruction;
            Instructions.Add(c);
            CurrentStack.Push(nt);
            return [inst.Index + 1];
        }
        throw new NotImplementedException();
    }

    public int[] VisitBr(CilInstructionInfo inst, int jumpOffset)
    {
        var labelIndex = Labels[jumpOffset + inst.NextByteOffset];
        var (label, index) = labelIndex;
        Instructions.Add(ShaderInstruction.Br(label));
        if (JumpStack.TryGetValue(label, out var s))
        {
            Debug.Assert(s.SequenceEqual(CurrentStack));
            // TODO: check for consistency
        }
        else
        {
            JumpStack.TryAdd(label, new Stack<IShaderType>(CurrentStack));
        }
        ValidationStack = null;
        return [index];
    }

    public int[] VisitBreak(CilInstructionInfo inst)
    {
        throw new NotSupportedException($"{inst.Instruction.OpCode}");
    }

    public int[] VisitBrIf(CilInstructionInfo inst, int jumpOffset, bool value)
    {
        var v = CurrentStack.Pop();
        if (v is not BoolType)
        {
            // TODO: add conversion instruction
        }
        // bf.false
        if (value == false)
        {
            Instructions.Add(ShaderInstruction.LogicalNot());
        }

        var labelIndex = Labels[jumpOffset + inst.NextByteOffset];
        var (label, index) = labelIndex;
        Instructions.Add(ShaderInstruction.BrIf(label));
        if (JumpStack.TryGetValue(label, out var s))
        {
            if (!CurrentStack.SequenceEqual(s))
            {
                throw new ValidationException($"Stack state after jump is not consistent", Method);
            }
        }
        else
        {
            JumpStack.TryAdd(label, new Stack<IShaderType>(CurrentStack));
        }
        return [index, inst.Index + 1];
    }

    public int[] VisitBrIf<TOp>(CilInstructionInfo inst, int jumpOffset, bool isUn = false) where TOp : BinaryRelation.IOp<TOp>
    {
        var r = CurrentStack.Pop();
        var l = CurrentStack.Pop();
        var t = EnsureBinaryOpOprandsType(l, r);

        if (t is INumericType nt)
        {
            Instructions.Add(((IOperation)nt.GetBinaryOperation<TOp>()).Instruction);
        }
        else
        {
            throw new NotImplementedException();
        }


        var labelIndex = Labels[jumpOffset + inst.NextByteOffset];
        var (label, index) = labelIndex;
        Instructions.Add(ShaderInstruction.BrIf(label));
        if (JumpStack.TryGetValue(label, out var s))
        {
            // TODO: check for consistency
        }
        else
        {
            JumpStack.TryAdd(label, new Stack<IShaderType>(CurrentStack));
        }
        return [index, inst.Index + 1];
    }

    public int[] VisitCall(CilInstructionInfo info, MethodInfo method)
    {
        var f = Parser.ParseMethod(method);
        foreach (var (idx, p) in f.Parameters.Reverse().Index())
        {
            var vt = CurrentStack.Pop();
            if (!vt.Equals(p.Type))
            {
                throw new ValidationException($"parameter {p} not match: stack {vt.Name} declaration {p.Type.Name}", Method);
            }
        }
        Instructions.Add(ShaderInstruction.Call(f));
        if (f.Return.Type is not UnitType)
        {
            CurrentStack.Push(f.Return.Type);
        }
        return [info.Index + 1];
    }

    public int[] VisitLdArg(CilInstructionInfo inst, ParameterInfo info)
    {
        var p = Parser.ParseParameter(info);
        Instructions.Add(ShaderInstruction.Load(p));
        CurrentStack.Push(p.Type);
        return [inst.Index + 1];
    }

    public int[] VisitLdArgAddress(CilInstructionInfo inst, ParameterInfo info)
    {
        var p = Parser.ParseParameter(info);
        var c = ShaderInstruction.LoadAddress(p);
        Instructions.Add(c);
        CurrentStack.Push(p.Type.GetPtrType());
        return [inst.Index + 1];
    }

    public int[] VisitLdIndirect<TShaderType>(CilInstructionInfo inst) where TShaderType : IShaderType
    {
        throw new NotImplementedException();
    }

    public int[] VisitLdIndirectNativeInt(CilInstructionInfo inst)
    {
        throw new NotImplementedException();
    }

    public int[] VisitLdIndirectRef(CilInstructionInfo inst)
    {
        throw new NotImplementedException();
    }

    public int[] VisitLdLoc(CilInstructionInfo inst, LocalVariableInfo info)
    {

        var v = Context[Symbol.Variable(info)] ?? throw new KeyNotFoundException($"Failed to resolve local variable {info}");
        Instructions.Add(ShaderInstruction.Load(v));
        CurrentStack.Push(v.Type);
        return [inst.Index + 1];

    }

    public int[] VisitLdLocAddress(CilInstructionInfo inst, LocalVariableInfo info)
    {
        var v = Context[Symbol.Variable(info)] ?? throw new KeyNotFoundException($"Failed to resolve local variable {info}");
        Instructions.Add(ShaderInstruction.LoadAddress(v));
        CurrentStack.Push(v.Type.GetPtrType());
        return [inst.Index + 1];
    }

    public int[] VisitLdNull(CilInstructionInfo info)
    {
        throw new NotImplementedException();
    }

    public int[] VisitLiteral<TLiteral>(CilInstructionInfo info, TLiteral literal) where TLiteral : ILiteral
    {
        var c = ShaderInstruction.Const(literal);
        Instructions.Add(c);
        CurrentStack.Push(literal.Type);
        return [info.Index + 1];
    }

    public int[] VisitNewObj(CilInstructionInfo info, ConstructorInfo constructor)
    {
        var callee = Context[Symbol.Function(constructor)]
                     ?? throw new ValidationException($"Failed to resolve constructor {constructor}", Method);
        foreach (var p in callee.Parameters.Reverse())
        {
            var t = CurrentStack.Pop();
            Debug.Assert(t.Equals(p.Type));
        }
        Instructions.Add(new CallInstruction(callee));
        CurrentStack.Push(callee.Return.Type);
        return [info.Index + 1];
    }

    public int[] VisitNop(CilInstructionInfo inst)
    {
        Instructions.Add(ShaderInstruction.Nop());
        return [inst.Index + 1];
    }

    public int[] VisitReturn(CilInstructionInfo info)
    {
        switch (CurrentStack.Count)
        {
            case 0:
                if (Function.Return.Type is not UnitType)
                {
                    throw new ValidationException("return when stack is empty requires unit type", Method);
                }
                Instructions.Add(ShaderInstruction.Return());
                ValidationStack = null;
                return [];
            case 1:
                var vt = CurrentStack.Pop();
                var rt = Function.Return.Type;
                if (!rt.Equals(vt))
                {
                    throw new ValidationException($"return when statck type {vt.Name} is not consistent with function signature return {rt.Name}", Method);
                }
                Instructions.Add(ShaderInstruction.Return());
                ValidationStack = null;
                return [];
            default:
                throw new ValidationException("return when stack.Count > 1", Method);
        }
    }

    public int[] VisitStArg(CilInstructionInfo inst, ParameterInfo info)
    {
        var p = Context[info] ?? throw new KeyNotFoundException($"Failed to resolve parameter {inst}");
        var v = CurrentStack.Pop();
        // TODO: check for consistency, may emit convert instruction here
        Instructions.Add(ShaderInstruction.LoadAddress(p));
        return [inst.Index + 1];
    }

    public int[] VisitStIndirect<TShaderType>(CilInstructionInfo inst) where TShaderType : IShaderType
    {
        throw new NotImplementedException();
    }

    public int[] VisitStIndirectRef(CilInstructionInfo inst)
    {
        throw new NotImplementedException();
    }

    public int[] VisitStLoc(CilInstructionInfo inst, LocalVariableInfo info)
    {
        var v = CurrentStack.Pop();
        var varDecl = Context[Symbol.Variable(info)] ?? throw new KeyNotFoundException($"Failed to resolve local variable {info}, @{inst}({inst.Instruction.OpCode}) - {Method.Name}");
        // TODO: check for consistency, may emit convert instruction here
        Instructions.Add(ShaderInstruction.Store(varDecl));
        return [inst.Index + 1];
    }

    public int[] VisitSwitch(CilInstructionInfo inst)
    {
        throw new NotImplementedException();
    }

    public int[] VisitUnaryLogical<TOp>(CilInstructionInfo inst) where TOp : BinaryRelation.IOp<TOp>
    {
        throw new NotImplementedException();
    }

    public int[] VisitConversion<TTarget>(CilInstructionInfo inst) where TTarget : IScalarType<TTarget>
    {
        var t = CurrentStack.Pop();
        if (t is IScalarType nt)
        {
            Instructions.Add(nt.GetConversionToOperation<TTarget>().Instruction);
            CurrentStack.Push(TTarget.Instance);
            return [inst.Index + 1];
        }
        throw new NotImplementedException();
    }

    public int[] VisitLdThis(CilInstructionInfo inst)
    {
        var p = Function.Parameters[0];
        Instructions.Add(ShaderInstruction.Load(p));
        CurrentStack.Push(p.Type.GetPtrType());
        return [inst.Index + 1];
    }

    public int[] VisitStThis(CilInstructionInfo inst)
    {
        throw new NotImplementedException();
    }

    public int[] VisitLdFld(CilInstructionInfo inst, FieldInfo info)
    {
        var o = CurrentStack.Pop();
        if (o is not IPtrType)
        {
            throw new ValidationException("ldfld expect current stack to have ptr type", Method);
        }
        var m = Parser.ParseField(info);
        CurrentStack.Push(m.Type);
        return [inst.Index + 1];
    }

    public int[] VisitLdFldAddress(CilInstructionInfo inst, FieldInfo info)
    {
        var o = CurrentStack.Pop();
        if (o is not IPtrType)
        {
            throw new ValidationException("ldfld expect current stack to have ptr type", Method);
        }
        var m = Parser.ParseField(info);
        CurrentStack.Push(m.Type.GetPtrType());
        return [inst.Index + 1];
    }

    public int[] VisitStFld(CilInstructionInfo inst, FieldInfo info)
    {
        throw new NotImplementedException();
    }

    public int[] VisitLdsfld(CilInstructionInfo inst, FieldInfo info)
    {
        var v = Parser.ParseStaticField(info);
        CurrentStack.Push(v.Type);
        Instructions.Add(ShaderInstruction.Load(v));
        return [inst.Index + 1];
    }
}
