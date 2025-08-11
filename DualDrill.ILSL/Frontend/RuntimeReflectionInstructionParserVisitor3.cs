using DualDrill.CLSL.Language;
using DualDrill.CLSL.Language.ControlFlow;
using DualDrill.CLSL.Language.Declaration;
using DualDrill.CLSL.Language.FunctionBody;
using DualDrill.CLSL.Language.Literal;
using DualDrill.CLSL.Language.Operation;
using DualDrill.CLSL.Language.ShaderAttribute;
using DualDrill.CLSL.Language.Symbol;
using DualDrill.CLSL.Language.Types;
using DualDrill.Common;
using DualDrill.Common.Nat;
using System.Collections.Immutable;
using ValueDeclaration = DualDrill.CLSL.Language.Symbol.ValueDeclaration;

namespace DualDrill.CLSL.Frontend;

sealed class RuntimeReflectionInstructionParserVisitor3
    : ICilInstructionVisitor<Unit>
{
    public MethodBodyAnalysisModel Model { get; }
    public FunctionDeclaration Function { get; }
    public ISuccessor Successor { get; }
    public ImmutableStack<IShaderType> Stack { get; private set; }
    public IReadOnlyDictionary<VariableDeclaration, ValueDeclaration> Locals { get; }
    public IReadOnlyDictionary<ParameterDeclaration, IParameterBinding> Parameters { get; }

    public ITerminator<Label, Unit>? Terminator { get; private set; } = null;
    public IReadOnlyList<StackIRInstruction> Instructions => instructions;

    List<StackIRInstruction> instructions = [];

    public RuntimeReflectionInstructionParserVisitor3(
        MethodBodyAnalysisModel model,
        FunctionDeclaration function,
        ISuccessor successor,
        ImmutableStack<IShaderType> inputStack,
        IReadOnlyDictionary<VariableDeclaration, ValueDeclaration> locals,
        IReadOnlyDictionary<ParameterDeclaration, IParameterBinding> parameters
    )
    {
        Model = model;
        Function = function;
        Successor = successor;
        Stack = inputStack;
        Locals = locals;
        Parameters = parameters;
    }

    IShaderType ConvertToCilStackType(IShaderType type)
    {
        return type switch
        {
            BoolType => IntType<N32>.Instance,
            IntType<N8> => IntType<N32>.Instance,
            IntType<N16> => IntType<N32>.Instance,
            UIntType<N8> => IntType<N32>.Instance,
            UIntType<N16> => IntType<N32>.Instance,
            UIntType<N32> => IntType<N32>.Instance,
            UIntType<N64> => IntType<N64>.Instance,
            _ => type
        };
    }

    IUnaryExpressionOperation? StoreConversion(IShaderType target, IShaderType source)
    {
        if (source.Equals(target))
        {
            return null;
        }
        return (target, source) switch
        {
            (BoolType, IntType<N32>) => ScalarConversionOperation<IntType<N32>, BoolType>.Instance,
            (IntType<N8>, IntType<N32>) => ScalarConversionOperation<IntType<N32>, IntType<N8>>.Instance,
            (IntType<N16>, IntType<N32>) => ScalarConversionOperation<IntType<N32>, IntType<N16>>.Instance,
            (UIntType<N8>, IntType<N32>) => ScalarConversionOperation<IntType<N32>, UIntType<N8>>.Instance,
            (UIntType<N16>, IntType<N32>) => ScalarConversionOperation<IntType<N32>, UIntType<N16>>.Instance,
            (UIntType<N32>, IntType<N32>) => ScalarConversionOperation<IntType<N32>, UIntType<N32>>.Instance,
            (UIntType<N64>, IntType<N64>) => ScalarConversionOperation<IntType<N64>, UIntType<N64>>.Instance,
            _ => throw new ValidationException($"can not store {source} to {target}", Model.Method)
        };
    }

    void PrepareStore(IShaderType source, IShaderType target)
    {
        var conv = StoreConversion(target, source);
        if (conv is not null)
        {
            //Emit(StackIR.Instruction.Expr1(conv));
        }
    }


    IUnaryExpressionOperation? ConvertToCilStackTypeOperation(IShaderType type)
    {
        return type switch
        {
            BoolType => ScalarConversionOperation<BoolType, IntType<N32>>.Instance,
            IntType<N8> => ScalarConversionOperation<IntType<N8>, IntType<N32>>.Instance,
            IntType<N16> => ScalarConversionOperation<IntType<N16>, IntType<N32>>.Instance,
            UIntType<N8> => ScalarConversionOperation<UIntType<N8>, IntType<N32>>.Instance,
            UIntType<N16> => ScalarConversionOperation<UIntType<N16>, IntType<N32>>.Instance,
            UIntType<N32> => ScalarConversionOperation<UIntType<N32>, IntType<N32>>.Instance,
            UIntType<N64> => ScalarConversionOperation<UIntType<N64>, IntType<N64>>.Instance,
            _ => null
        };
    }

    void Push(IShaderType type)
    {
        var stackType = ConvertToCilStackType(type);
        Stack = Stack.Push(stackType);
        var conv = ConvertToCilStackTypeOperation(type);
        if (conv is not null)
        {
            //Emit(StackIR.Instruction.Expr1(conv));
        }
    }

    void Emit(StackIRInstruction inst)
    {
        instructions.Add(inst);
    }

    public Unit VisitLiteral<TLiteral>(CilInstructionInfo info, TLiteral literal) where TLiteral : ILiteral
    {
        Emit(StackIR.Instruction.Literal(literal));
        Push(literal.Type);
        return default;
    }

    public Unit VisitPop(CilInstructionInfo inst)
    {
        Emit(StackIR.Instruction.Pop());
        Pop();
        return default;
    }

    public Unit VisitReturn(CilInstructionInfo info)
    {
        if (Stack.IsEmpty)
        {
            if (Function.Return.Type is not UnitType)
            {
                throw new ValidationException("Function return type is not Unit, but stack is empty.", Model.Method);
            }
            Terminator = StackIR.Terminator.ReturnVoid();
        }
        else
        {
            if (Function.Return.Type is UnitType)
            {
                throw new ValidationException("Function return type is Unit, but stack is empty.", Model.Method);
            }
            Pop();
            Terminator = StackIR.Terminator.ReturnExpr();
            if (!Stack.IsEmpty)
            {
                throw new ValidationException("Function return type is Unit, but stack has more than one element.", Model.Method);
            }
        }
        return default;
    }

    public Unit VisitNop(CilInstructionInfo inst)
    {
        Emit(StackIR.Instruction.Nop());
        return default;
    }

    public Unit VisitBreak(CilInstructionInfo inst)
    {
        throw new NotSupportedException();
    }

    public Unit VisitLoadArgument(CilInstructionInfo inst, ParameterDeclaration p)
    {
        Emit(StackIR.Instruction.GetParameter(p));
        Push(p.Type);
        return default;
    }


    IShaderType Pop()
    {
        Stack = Stack.Pop(out var r);
        return r;
    }

    public Unit VisitBinaryArithmetic<TOp>(CilInstructionInfo inst, bool isUn = false, bool isChecked = false) where TOp : BinaryArithmetic.IOp<TOp>
    {
        var r = Pop();
        var l = Pop();
        if (!l.Equals(r))
        {
            throw new ValidationException($"binary op type not match {l} != {r}", Model.Method);
        }
        switch (l)
        {
            case IntType<N32> when isUn:
                Emit(StackIR.Instruction.Expr2(NumericBinaryArithmeticOperation<UIntType<N32>, TOp>.Instance));
                break;
            case IntType<N32>:
                Emit(StackIR.Instruction.Expr2(NumericBinaryArithmeticOperation<IntType<N32>, TOp>.Instance));
                break;
            case IntType<N64> when isUn:
                Emit(StackIR.Instruction.Expr2(NumericBinaryArithmeticOperation<UIntType<N64>, TOp>.Instance));
                break;
            case IntType<N64>:
                Emit(StackIR.Instruction.Expr2(NumericBinaryArithmeticOperation<IntType<N64>, TOp>.Instance));
                break;
            case FloatType<N32>:
                Emit(StackIR.Instruction.Expr2(NumericBinaryArithmeticOperation<FloatType<N32>, TOp>.Instance));
                break;
            case FloatType<N64>:
                Emit(StackIR.Instruction.Expr2(NumericBinaryArithmeticOperation<FloatType<N64>, TOp>.Instance));
                break;
            default:
                throw new ValidationException($"op {TOp.Instance.Name} not support in type {l}", Model.Method);
        }

        Push(l);
        return default;
    }

    public Unit VisitLoadArgumentAddress(CilInstructionInfo inst, ParameterDeclaration p)
    {
        Emit(StackIR.Instruction.GetParameterAddress(p));
        Push(p.Type.GetPtrType());
        return default;
    }

    public Unit VisitStoreArgument(CilInstructionInfo inst, ParameterDeclaration p)
    {
        var t = Pop();
        PrepareStore(t, p.Type);
        Emit(StackIR.Instruction.SetParameter(p));
        return default;
    }

    public Unit VisitLdThis(CilInstructionInfo inst)
    {
        throw new NotImplementedException();
    }

    public Unit VisitStThis(CilInstructionInfo inst)
    {
        throw new NotImplementedException();
    }

    public Unit VisitLoadLocal(CilInstructionInfo inst, VariableDeclaration v)
    {
        Emit(StackIR.Instruction.GetLocal(v));
        Push(v.Type);
        return default;
    }

    public Unit VisitLoadLocalAddress(CilInstructionInfo inst, VariableDeclaration v)
    {
        Emit(StackIR.Instruction.GetLocalAddress(v));
        Push(v.Type.GetPtrType());
        return default;
    }

    public Unit VisitStoreLocal(CilInstructionInfo inst, VariableDeclaration v)
    {
        var t = Pop();
        PrepareStore(t, v.Type);
        Emit(StackIR.Instruction.SetLocal(v));
        return default;
    }


    public Unit VisitLoadField(CilInstructionInfo inst, MemberDeclaration m)
    {
        var t = Pop();
        if (!(t is IPtrType ptr && ptr.BaseType is StructureType))
        {
            throw new ValidationException($"can not load field from {t}, expected ptr to structure type", Model.Method);
        }
        Emit(StackIR.Instruction.GetMember(m));
        Push(m.Type);
        return default;
    }

    public Unit VisitLoadFieldAddress(CilInstructionInfo inst, MemberDeclaration m)
    {
        var t = Pop();
        if (!(t is IPtrType ptr && ptr.BaseType is StructureType))
        {
            throw new ValidationException($"can not load field from {t}, expected ptr to structure type", Model.Method);
        }
        Emit(StackIR.Instruction.GetMemberAddress(m));
        Push(m.Type.GetPtrType());
        return default;
    }

    public Unit VisitStoreField(CilInstructionInfo inst, MemberDeclaration m)
    {
        var t = Pop();
        if (!(t is IPtrType ptr && ptr.BaseType is StructureType))
        {
            throw new ValidationException($"can not load field from {t}, expected ptr to structure type", Model.Method);
        }
        Emit(StackIR.Instruction.SetMember(m));
        Push(m.Type);
        return default;
    }

    public Unit VisitLoadStaticField(CilInstructionInfo inst, VariableDeclaration v)
    {
        Emit(StackIR.Instruction.GetLocal(v));
        Push(v.Type);
        return default;
    }

    public Unit VisitLoadStaticFieldAddress(CilInstructionInfo inst, VariableDeclaration v)
    {
        Emit(StackIR.Instruction.GetLocalAddress(v));
        Push(v.Type.GetPtrType());
        return default;
    }

    public Unit VisitLoadNull(CilInstructionInfo info)
    {
        throw new NotImplementedException();
    }

    void VisitCallFunction(FunctionDeclaration func, bool isExpression)
    {
        if (func.Attributes.OfType<IOperationMethodAttribute>().SingleOrDefault() is { } opAttr)
        {
            switch (opAttr.Operation)
            {
                case IBinaryExpressionOperation be:
                    {
                        var r = Pop();
                        var l = Pop();
                        if (!l.Equals(be.LeftType) || !r.Equals(be.RightType))
                        {
                            throw new ValidationException($"{be} operation stack : {l.Name}, {r.Name}", Model.Method);
                        }
                        Emit(StackIR.Instruction.Expr2(be));
                        Push(be.ResultType);
                        return;
                    }
                case IBinaryStatementOperation bs:
                    {
                        var r = Pop();
                        var l = Pop();
                        if (!l.Equals(bs.LeftType) || !r.Equals(bs.RightType))
                        {
                            throw new ValidationException($"{bs} operation stack : {l.Name}, {r.Name}", Model.Method);
                        }
                        if (bs is IVectorComponentSetOperation vcs)
                        {
                            Emit(StackIR.Instruction.SetVecComponent(vcs));
                            return;
                        }
                        if (bs is IVectorSwizzleSetOperation vss)
                        {
                            Emit(StackIR.Instruction.SetVecSwizzle(vss));
                            return;

                        }
                        throw new NotSupportedException($"binary statement {bs.Name}");
                    }
                case IUnaryExpressionOperation ue:
                    {
                        var s = Pop();
                        if (!s.Equals(ue.SourceType))
                        {
                            throw new ValidationException($"{ue} operation stack : {s.Name}", Model.Method);
                        }
                        Emit(StackIR.Instruction.Expr1(ue));
                        Push(ue.ResultType);
                        return;
                    }
            }
        }

        foreach (var p in func.Parameters.Reverse())
        {
            var ve = Pop();
            if (!ve.Equals(p.Type))
            {
                throw new ValidationException(
                    $"parameter {p} not match: stack {ve.Name} declaration {p.Type.Name}",
                    Model.Method);
            }
        }

        Emit(StackIR.Instruction.Call(func));
        if (isExpression)
        {
            Push(func.Return.Type);
        }

    }

    public Unit VisitCall(CilInstructionInfo info, FunctionDeclaration func)
    {
        VisitCallFunction(func, func.Return.Type is not UnitType);
        return default;
    }

    public Unit VisitNewObject(CilInstructionInfo info, FunctionDeclaration f)
    {
        VisitCallFunction(f, true);
        return default;
    }

    public Unit VisitBranch(CilInstructionInfo inst, int jumpOffset)
    {
        if (Successor is UnconditionalSuccessor { Target: var target })
        {
            Terminator = StackIR.Terminator.Br(target);
            return default;
        }
        else
        {
            throw new ValidationException($"successor mismatch, expected br, got {Successor}", Model.Method);
        }
    }

    public Unit VisitBranchIf(CilInstructionInfo inst, int jumpOffset, bool value)
    {
        var t = Pop();
        if (t is not IntType<N32>)
        {
            throw new ValidationException($"br if expecte a i32 stack value, got {t}", Model.Method);
        }
        if (Successor is ConditionalSuccessor { TrueTarget: var tt, FalseTarget: var ft })
        {
            if (value)
            {
                Terminator = StackIR.Terminator.BrIf(tt, ft);
            }
            else
            {
                Terminator = StackIR.Terminator.BrIf(ft, tt);
            }
            return default;
        }
        else
        {
            throw new ValidationException($"successor mismatch, expected br_if, got {Successor}", Model.Method);
        }
    }

    public Unit VisitBranchIf<TOp>(CilInstructionInfo inst, int jumpOffset, bool isUn = false) where TOp : BinaryRelational.IOp<TOp>
    {
        var r = Pop();
        var l = Pop();
        switch (l, r)
        {
            case (IntType<N32>, IntType<N32>) when isUn:
                Emit(StackIR.Instruction.Expr2(NumericBinaryRelationalOperation<UIntType<N32>, TOp>.Instance));
                break;
            case (IntType<N32>, IntType<N32>):
                Emit(StackIR.Instruction.Expr2(NumericBinaryRelationalOperation<IntType<N32>, TOp>.Instance));
                break;
            case (IntType<N64>, IntType<N64>) when isUn:
                Emit(StackIR.Instruction.Expr2(NumericBinaryRelationalOperation<UIntType<N64>, TOp>.Instance));
                break;
            case (IntType<N64>, IntType<N64>):
                Emit(StackIR.Instruction.Expr2(NumericBinaryRelationalOperation<IntType<N64>, TOp>.Instance));
                break;
            case (FloatType<N32>, FloatType<N32>):
                Emit(StackIR.Instruction.Expr2(NumericBinaryRelationalOperation<FloatType<N32>, TOp>.Instance));
                break;
            case (FloatType<N64>, FloatType<N64>):
                Emit(StackIR.Instruction.Expr2(NumericBinaryRelationalOperation<FloatType<N64>, TOp>.Instance));
                break;
            default:
                throw new ValidationException($"Unsupported relational operand {l}, {r} for op {TOp.Instance.Name}", Model.Method);
        }
        if (Successor is ConditionalSuccessor { TrueTarget: var tt, FalseTarget: var ft })
        {
            Terminator = StackIR.Terminator.BrIf(tt, ft);
        }
        else
        {
            throw new ValidationException($"successor mismatch, expected br_if, got {Successor}", Model.Method);
        }
        return default;
    }

    public Unit VisitSwitch(CilInstructionInfo inst)
    {
        throw new NotImplementedException();
    }

    public Unit VisitBinaryLogical<TOp>(CilInstructionInfo inst) where TOp : BinaryLogical.IOp<TOp>
    {
        var r = Pop();
        var l = Pop();
        if (!(l is IntType<N32> && r is IntType<N32>))
        {
            throw new ValidationException($"logical op requires i32 operands, got {l}, {r}", Model.Method);
        }
        if (TOp.Instance is BinaryLogical.IWithBitwiseOp bOp)
        {
            Emit(StackIR.Instruction.Expr2(bOp.BitwiseOp.GetNumericBinaryOperation(IntType<N32>.Instance)));
        }
        else
        {
            throw new ValidationException($"Not support op {TOp.Instance}", Model.Method);
        }
        return default;
    }



    IBinaryExpressionOperation BinaryRelationalOperation<TOp>(IShaderType l, IShaderType r, bool isUn)
        where TOp : BinaryRelational.IOp<TOp>
    {
        return (l, r, isUn) switch
        {
            (IntType<N32>, IntType<N32>, false) => NumericBinaryRelationalOperation<IntType<N32>, TOp>.Instance,
            (IntType<N64>, IntType<N64>, false) => NumericBinaryRelationalOperation<IntType<N64>, TOp>.Instance,
            (UIntType<N32>, UIntType<N32>, false) => NumericBinaryRelationalOperation<UIntType<N32>, TOp>.Instance,
            (UIntType<N64>, UIntType<N64>, false) => NumericBinaryRelationalOperation<UIntType<N64>, TOp>.Instance,
            (FloatType<N32>, FloatType<N32>, false) => NumericBinaryRelationalOperation<FloatType<N32>, TOp>.Instance,
            (FloatType<N64>, FloatType<N64>, false) => NumericBinaryRelationalOperation<FloatType<N64>, TOp>.Instance,
            (IntType<N32>, IntType<N32>, true) => NumericBinaryRelationalOperation<UIntType<N32>, TOp>.Instance,
            (IntType<N64>, IntType<N64>, true) => NumericBinaryRelationalOperation<UIntType<N64>, TOp>.Instance,
            _ => throw new ValidationException($"binary relational op {TOp.Instance.Name} not support in type {l}, {r}", Model.Method)
        };
    }

    public Unit VisitBinaryRelation<TOp>(CilInstructionInfo inst, bool isUn = false, bool isChecked = false) where TOp : BinaryRelational.IOp<TOp>
    {
        var r = Pop();
        var l = Pop();
        var op = BinaryRelationalOperation<TOp>(l, r, isUn);
        Emit(StackIR.Instruction.Expr2(op));
        Push(op.ResultType);
        return default;
    }

    public Unit VisitConversion<TTarget>(CilInstructionInfo inst) where TTarget : IScalarType<TTarget>
    {
        var t = Pop();
        if (t is IScalarType nt)
        {
            var operation = nt.GetConversionToOperation<TTarget>();
            Emit(StackIR.Instruction.Expr1(operation));
            Push(TTarget.Instance);
            return default;
        }
        throw new NotImplementedException();
    }

    public Unit VisitLogicalNot(CilInstructionInfo inst)
    {
        throw new NotImplementedException();
    }

    public Unit VisitUnaryArithmetic<TOp>(CilInstructionInfo inst) where TOp : UnaryArithmetic.IOp<TOp>
    {
        throw new NotImplementedException();
    }

    public Unit VisitLoadIndirect<TShaderType>(CilInstructionInfo inst) where TShaderType : IShaderType
    {
        throw new NotImplementedException();
    }

    public Unit VisitStoreIndirect<TShaderType>(CilInstructionInfo inst) where TShaderType : IShaderType
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

    public Unit VisitStoreIndirectRef(CilInstructionInfo inst)
    {
        throw new NotImplementedException();
    }

    public Unit VisitDup(CilInstructionInfo inst)
    {
        throw new NotImplementedException();
    }

    Unit ICilInstructionVisitor<Unit>.VisitPop(CilInstructionInfo inst)
    {
        throw new NotImplementedException();
    }
}
