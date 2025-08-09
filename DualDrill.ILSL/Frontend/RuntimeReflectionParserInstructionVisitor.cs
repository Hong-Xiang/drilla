using DualDrill.CLSL.Language.ControlFlow;
using DualDrill.CLSL.Language.Declaration;
using DualDrill.CLSL.Language.LinearInstruction;
using DualDrill.CLSL.Language.Literal;
using DualDrill.CLSL.Language.Operation;
using DualDrill.CLSL.Language.ShaderAttribute;
using DualDrill.CLSL.Language.Symbol;
using DualDrill.CLSL.Language.Types;
using DualDrill.Common;
using DualDrill.Common.Nat;
using System.Collections.Immutable;
using System.Reflection;

namespace DualDrill.CLSL.Frontend;

public sealed class ValidationException(string message, MethodBase method)
    : Exception(message + $" @ {method.Name}")
{
    public MethodBase Method { get; } = method;
}

public sealed class BinaryOperationEvaluationStackTypeNotMatchValidationException(
    IBinaryOp op,
    IShaderType lType,
    IShaderType rType,
    MethodBase method
) : Exception($"Binary op {op.Name} with {lType.Name} and {rType.Name} inputs is not valid @ {method.Name}")
{
}

sealed class RuntimeReflectionParserInstructionVisitor(
    FunctionDeclaration Function,
    MethodBodyAnalysisModel Model,
    Label Label,
    ImmutableArray<IShaderType> Inputs,
    ISuccessor successor)
    : ICilInstructionVisitor<Unit>
{
    //public List<int> Nexts { get; private set; } = [];

    public List<IInstruction> Instructions { get; } = [];

    Stack<IShaderType> EvaluationStack = new(Inputs);

    public ImmutableArray<IShaderType> Outputs { get; private set; } = [];
    public bool HasTerminatorInstruction { get; private set; } = false;

    void PushToEvaluationStack(IShaderType type)
    {
        switch (type)
        {
            case BoolType:
                EvaluationStack.Push(IntType<N32>.Instance);
                break;
            case IUIntType ui:
                EvaluationStack.Push(ui.SameWidthIntType);
                break;
            default:
                EvaluationStack.Push(type);
                break;
        }
    }

    bool IsImplicitCompatible(IShaderType value, IShaderType target)
    {
        return (value, target) switch
        {
            _ when value == target => true,
            (IntType<N32>, BoolType) => true,
            (IIntType st, IUIntType ut) when st.BitWidth == ut.BitWidth => true,
            _ => false
        };
    }

    Unit EvaluateBinaryOperation<TOp>(
        Func<TOp, IShaderType, IBinaryExpressionOperation?> factory
    )
        where TOp : IBinaryOp<TOp>
    {
        var r = EvaluationStack.Pop();
        var l = EvaluationStack.Pop();

        if (!l.Equals(r) || factory(TOp.Instance, l) is not { } operation)
        {
            throw new BinaryOperationEvaluationStackTypeNotMatchValidationException(TOp.Instance, l, r, Model.Method);
        }

        Instructions.Add(operation.Instruction);
        PushToEvaluationStack(operation.ResultType);
        return default;
    }

    public Unit VisitBinaryArithmetic<TOp>(CilInstructionInfo inst, bool isUn = false, bool isChecked = false)
        where TOp : BinaryArithmetic.IOp<TOp> =>
        EvaluateBinaryOperation<TOp>((_, t) =>
            t switch
            {
                IIntType it when isUn => ((INumericType)it.SameWidthUIntType).ArithmeticOperation<TOp>(),
                _ when isUn => null,
                INumericType nt => nt.ArithmeticOperation<TOp>(),
                _ => null
            });


    public Unit VisitBinaryLogical<TOp>(CilInstructionInfo inst)
        where TOp : BinaryLogical.IOp<TOp> =>
        EvaluateBinaryOperation<TOp>(static (_, t) =>
            t switch
            {
                BoolType => Operation.LogicalBinaryOperation<TOp>(),
                IntType<N32> _ when TOp.Instance is BinaryLogical.IWithBitwiseOp bOp => bOp.BitwiseOp
                    .GetNumericBinaryOperation(IntType<N32>.Instance),
                _ => null
            });


    public Unit VisitBinaryRelation<TOp>(CilInstructionInfo inst, bool isUn = false, bool isChecked = false)
        where TOp : BinaryRelational.IOp<TOp>
        => EvaluateBinaryOperation<TOp>((_, t) =>
            t switch
            {
                IIntType it when isUn => ((INumericType)it.SameWidthUIntType).RelationalOperation<TOp>(),
                _ when isUn => null,
                INumericType nt => nt.RelationalOperation<TOp>(),
                _ => null
            });

    public void FlushOutputs()
    {
        if (!HasTerminatorInstruction)
        {
            switch (successor)
            {
                case UnconditionalSuccessor { Target: var target }:
                    Instructions.Add(ShaderInstruction.Br(target));
                    HasTerminatorInstruction = true;
                    break;
                default:
                    throw new ValidationException($"successor mismatch, expected br, got {successor}", Model.Method);
            }
        }

        if (EvaluationStack.Count == 0)
            return;

        if (!Outputs.IsEmpty)
        {
            throw new NotSupportedException("multiple flush outputs are not expected");
        }

        Outputs = [..EvaluationStack.Reverse()];
        EvaluationStack.Clear();
    }

    public Unit VisitBranch(CilInstructionInfo inst, int jumpOffset)
    {
        if (successor is UnconditionalSuccessor { Target: var target })
        {
            Instructions.Add(ShaderInstruction.Br(target));
            HasTerminatorInstruction = true;
            FlushOutputs();
        }
        else
        {
            throw new ValidationException($"successor mismatch, expected br, got {successor}", Model.Method);
        }

        return default;
    }

    public Unit VisitBreak(CilInstructionInfo inst)
    {
        throw new NotSupportedException($"{inst.Instruction.OpCode}");
    }

    public Unit VisitBranchIf(CilInstructionInfo inst, int jumpOffset, bool value)
    {
        _ = EvaluationStack.Pop();
        // bf.false
        if (value == false)
        {
            Instructions.Add(LogicalNotOperation.Instance.Instruction);
        }

        if (successor is ConditionalSuccessor { TrueTarget: var trueTarget, FalseTarget: var falseTarget })
        {
            Instructions.Add(ShaderInstruction.BrIf(trueTarget, falseTarget));
            HasTerminatorInstruction = true;
            FlushOutputs();
        }
        else
        {
            throw new ValidationException($"successor mismatch, expected br, got {successor}", Model.Method);
        }

        return default;
    }

    public Unit VisitBranchIf<TOp>(CilInstructionInfo inst, int jumpOffset, bool isUn = false)
        where TOp : BinaryRelational.IOp<TOp>
    {
        EvaluateBinaryOperation<TOp>((_, t) =>
            t switch
            {
                IIntType it when isUn => ((INumericType)it.SameWidthUIntType).RelationalOperation<TOp>(),
                _ when isUn => null,
                INumericType nt => nt.RelationalOperation<TOp>(),
                _ => null
            });
        _ = EvaluationStack.Pop(); // condition
        if (successor is ConditionalSuccessor { TrueTarget: var trueTarget, FalseTarget: var falseTarget })
        {
            Instructions.Add(ShaderInstruction.BrIf(trueTarget, falseTarget));
            HasTerminatorInstruction = true;
            FlushOutputs();
        }
        else
        {
            throw new ValidationException($"successor mismatch, expected br, got {successor}", Model.Method);
        }

        return default;
    }


    void VisitCallFunction(FunctionDeclaration func, bool isExpression)
    {
        if (func.Attributes.OfType<IOperationMethodAttribute>().SingleOrDefault() is { } opAttr)
        {
            switch (opAttr.Operation)
            {
                case IBinaryExpressionOperation be:
                {
                    var r = EvaluationStack.Pop();
                    var l = EvaluationStack.Pop();
                    if (!l.Equals(be.LeftType) || !r.Equals(be.RightType))
                    {
                        throw new ValidationException($"{be} operation stack : {l.Name}, {r.Name}", Model.Method);
                    }

                    Instructions.Add(be.Instruction);
                    EvaluationStack.Push(be.ResultType);
                    return;
                }
                case IBinaryStatementOperation bs:
                {
                    var r = EvaluationStack.Pop();
                    var l = EvaluationStack.Pop();
                    if (!l.Equals(bs.LeftType) || !r.Equals(bs.RightType))
                    {
                        throw new ValidationException($"{bs} operation stack : {l.Name}, {r.Name}", Model.Method);
                    }

                    Instructions.Add(bs.Instruction);
                    return;
                }
                case IUnaryExpressionOperation ue:
                {
                    var s = EvaluationStack.Pop();
                    Instructions.Add(ue.Instruction);
                    PushToEvaluationStack(ue.ResultType);
                    return;
                }
            }
        }

        foreach (var p in func.Parameters.Reverse())
        {
            var ve = EvaluationStack.Pop();
            if (!ve.Equals(p.Type))
            {
                throw new ValidationException(
                    $"parameter {p} not match: stack {ve.Name} declaration {p.Type.Name}",
                    Model.Method);
            }
        }

        Instructions.Add(ShaderInstruction.Call(func));
        if (isExpression)
        {
            PushToEvaluationStack(func.Return.Type);
        }
    }

    public Unit VisitCall(CilInstructionInfo info, FunctionDeclaration f)
    {
        VisitCallFunction(f, f.Return.Type is not UnitType);
        return default;
    }

    public Unit VisitLoadArgument(CilInstructionInfo inst, ParameterDeclaration p)
    {
        Instructions.Add(ShaderInstruction.Load(p));
        PushToEvaluationStack(p.Type);
        return default;
    }

    public Unit VisitLoadArgumentAddress(CilInstructionInfo inst, ParameterDeclaration p)
    {
        Instructions.Add(ShaderInstruction.LoadAddress(p));
        PushToEvaluationStack(p.Type.GetPtrType());
        return default;
    }

    public Unit VisitUnaryArithmetic<TOp>(CilInstructionInfo inst) where TOp : UnaryArithmetic.IOp<TOp>
    {
        var v = EvaluationStack.Pop();
        if (v is INumericType type)
        {
            var operation = type.UnaryArithmeticOperation<TOp>();
            Instructions.Add(operation.Instruction);
            PushToEvaluationStack(operation.ResultType);
            return default;
        }

        throw new NotImplementedException();
    }

    public Unit VisitLoadIndirect<TShaderType>(CilInstructionInfo inst) where TShaderType : IShaderType
    {
        throw new NotImplementedException();
    }

    public Unit VisitLoadIndirectNativeInt(CilInstructionInfo inst)
    {
        throw new NotImplementedException();
    }

    public Unit VisitLoadIndirectRef(CilInstructionInfo inst)
    {
        throw new NotImplementedException();
    }

    public Unit VisitLoadLocal(CilInstructionInfo inst, VariableDeclaration v)
    {
        Instructions.Add(ShaderInstruction.Load(v));
        PushToEvaluationStack(v.Type);
        return default;
    }

    public Unit VisitLoadLocalAddress(CilInstructionInfo inst, VariableDeclaration v)
    {
        Instructions.Add(ShaderInstruction.LoadAddress(v));
        PushToEvaluationStack(v.Type.GetPtrType());
        return default;
    }

    public Unit VisitLoadNull(CilInstructionInfo info)
    {
        throw new NotImplementedException();
    }

    public Unit VisitLiteral<TLiteral>(CilInstructionInfo info, TLiteral literal) where TLiteral : ILiteral
    {
        Instructions.Add(ShaderInstruction.Const(literal));
        EvaluationStack.Push(literal.Type);
        return default;
    }

    public Unit VisitNewObject(CilInstructionInfo info, FunctionDeclaration callee)
    {
        VisitCallFunction(callee, true);
        return default;
    }

    public Unit VisitNop(CilInstructionInfo inst)
    {
        return default;
    }

    public Unit VisitReturn(CilInstructionInfo info)
    {
        switch (Function.Return.Type)
        {
            case UnitType:
                if (EvaluationStack.Count != 0)
                {
                    throw new ValidationException("return when stack is empty requires unit type", Model.Method);
                }

                Instructions.Add(ShaderInstruction.ReturnVoid());
                break;
            case { } r:
                var e = EvaluationStack.Pop();
                if (!IsImplicitCompatible(e, r))
                {
                    throw new ValidationException(
                        $"return when statck type {e.Name} is not consistent with function signature return {r.Name}",
                        Model.Method);
                }

                Instructions.Add(ShaderInstruction.ReturnResult());
                break;
            default:
                throw new ValidationException("return when stack.Count > 1", Model.Method);
        }

        HasTerminatorInstruction = true;
        FlushOutputs();
        return default;
    }

    bool IsValidToStore(IShaderType valType, IShaderType varType)
    {
        return (valType, varType) switch
        {
            (IntType<N32>, BoolType) => true,
            (IntType<N32> tv, IUIntType ui) when ui.BitWidth.Equals(tv.BitWidth) => true,
            _ when valType == varType => true,
            _ => false
        };
    }

    public Unit VisitStoreArgument(CilInstructionInfo inst, ParameterDeclaration p)
    {
        var v = EvaluationStack.Pop();
        if (!IsImplicitCompatible(v, p.Type))
        {
            throw new ValidationException($"store value of type {v.Name} to parameter of type{p.Type} is not valid",
                Model.Method);
        }


        Instructions.Add(ShaderInstruction.Store(p));
        return default;
    }

    public Unit VisitStoreIndirect<TShaderType>(CilInstructionInfo inst) where TShaderType : IShaderType
    {
        throw new NotImplementedException();
    }

    public Unit VisitStoreIndirectRef(CilInstructionInfo inst)
    {
        throw new NotImplementedException();
    }

    public Unit VisitStoreLocal(CilInstructionInfo inst, VariableDeclaration v)
    {
        var value = EvaluationStack.Pop();
        if (!IsValidToStore(value, v.Type))
        {
            throw new ValidationException($"store value of type {v.Name} to variable of type{v.Type} is not valid",
                Model.Method);
        }

        Instructions.Add(ShaderInstruction.Store(v));
        return default;
    }

    public Unit VisitSwitch(CilInstructionInfo inst)
    {
        throw new NotImplementedException();
    }

    public Unit VisitUnaryLogical<TOp>(CilInstructionInfo inst) where TOp : BinaryRelational.IOp<TOp>
    {
        throw new NotImplementedException();
    }

    public Unit VisitDup(CilInstructionInfo inst)
    {
        var t = EvaluationStack.Pop();
        Instructions.Add(ShaderInstruction.Dup());
        PushToEvaluationStack(t);
        PushToEvaluationStack(t);
        return default;
    }

    public Unit VisitPop(CilInstructionInfo inst)
    {
        _ = EvaluationStack.Pop();
        Instructions.Add(ShaderInstruction.Pop());
        return default;
    }

    public Unit VisitConversion<TTarget>(CilInstructionInfo inst) where TTarget : IScalarType<TTarget>
    {
        var e = EvaluationStack.Pop();
        if (e is IScalarType nt)
        {
            var operation = nt.GetConversionToOperation<TTarget>();
            Instructions.Add(operation.Instruction);
            PushToEvaluationStack(operation.ResultType);
            return default;
        }

        throw new NotImplementedException();
    }

    public Unit VisitLogicalNot(CilInstructionInfo inst)
    {
        var v = EvaluationStack.Pop();
        IUnaryExpressionOperation operation;
        switch (v)
        {
            case BoolType:
                operation = LogicalNotOperation.Instance;
                break;
            default:
                throw new NotImplementedException();
        }

        Instructions.Add(operation.Instruction);
        PushToEvaluationStack(operation.ResultType);
        return default;
    }

    // TODO: remove this method?
    public Unit VisitLdThis(CilInstructionInfo inst)
    {
        var p = Function.Parameters[0];
        Instructions.Add(ShaderInstruction.Load(p));
        EvaluationStack.Push(p.Type);
        return default;
    }

    public Unit VisitStThis(CilInstructionInfo inst)
    {
        throw new NotImplementedException();
    }

    public Unit VisitLoadField(CilInstructionInfo inst, MemberDeclaration m)
    {
        var o = EvaluationStack.Pop();
        if (o is not IPtrType)
        {
            throw new ValidationException("ldfld expect current stack to have ptr type", Model.Method);
        }

        Instructions.Add(ShaderInstruction.Load(m));
        PushToEvaluationStack(m.Type);
        return default;
    }


    public Unit VisitLoadFieldAddress(CilInstructionInfo inst, MemberDeclaration m)
    {
        var o = EvaluationStack.Pop();
        if (o is not IPtrType)
        {
            throw new ValidationException("ldfld expect current stack to have ptr type", Model.Method);
        }

        // TODO: check o is a structure type and m is member of it
        Instructions.Add(ShaderInstruction.LoadAddress(m));
        PushToEvaluationStack(m.Type.GetPtrType());
        return default;
    }

    public Unit VisitStoreField(CilInstructionInfo inst, MemberDeclaration m)
    {
        throw new NotImplementedException();
    }


    public Unit VisitLoadStaticField(CilInstructionInfo inst, VariableDeclaration v)
    {
        Instructions.Add(ShaderInstruction.Load(v));
        PushToEvaluationStack(v.Type);
        return default;
    }

    public Unit VisitLoadStaticFieldAddress(CilInstructionInfo inst, VariableDeclaration v)
    {
        Instructions.Add(ShaderInstruction.LoadAddress(v));
        PushToEvaluationStack(v.Type.GetPtrType());
        return default;
    }
}