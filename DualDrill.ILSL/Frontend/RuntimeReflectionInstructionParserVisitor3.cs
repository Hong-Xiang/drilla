using DualDrill.CLSL.Language;
using DualDrill.CLSL.Language.ControlFlow;
using DualDrill.CLSL.Language.Declaration;
using DualDrill.CLSL.Language.Expression;
using DualDrill.CLSL.Language.FunctionBody;
using DualDrill.CLSL.Language.Instruction;
using DualDrill.CLSL.Language.Literal;
using DualDrill.CLSL.Language.Operation;
using DualDrill.CLSL.Language.Operation.Pointer;
using DualDrill.CLSL.Language.ShaderAttribute;
using DualDrill.CLSL.Language.Statement;
using DualDrill.CLSL.Language.Symbol;
using DualDrill.CLSL.Language.Types;
using DualDrill.Common;
using DualDrill.Common.Nat;
using System.Collections.Immutable;

namespace DualDrill.CLSL.Frontend;

using ShaderExpr = IExpressionTree<IShaderValue>;

sealed class RuntimeReflectionInstructionParserVisitor3
    : ICilInstructionVisitor<Unit>
{
    public MethodBodyAnalysisModel Model { get; }
    public FunctionDeclaration Function { get; }
    public ISuccessor Successor { get; }
    public ImmutableStack<IShaderValue> Stack { get; private set; }

    public ITerminator<RegionJump, IShaderValue>? Terminator { get; private set; } = null;
    public IReadOnlyList<ShaderStmt> Statements => statements;

    List<ShaderStmt> statements = [];

    public IReadOnlyList<Instruction2<IShaderValue, IShaderValue>> Instructions => instructions;
    List<Instruction2<IShaderValue, IShaderValue>> instructions = [];

    private IStatementSemantic<IShaderValue, ShaderExpr, IShaderValue, FunctionDeclaration, ShaderStmt> Stmt { get; }
        = Statement.Factory<IShaderValue, ShaderExpr, IShaderValue, FunctionDeclaration>();

    private ITerminatorSemantic<RegionJump, IShaderValue, ITerminator<RegionJump, IShaderValue>> TermF { get; }
        = Language.Terminator.Factory<RegionJump, IShaderValue>();

    private IExpressionSemantic<IExpressionTree<IShaderValue>, IExpression<ShaderExpr>> ExprF { get; } =
        Language.Expression.Expression.Factory<IExpressionTree<IShaderValue>>();

    IOperationSemantic<Unit, IShaderValue, IShaderValue, Instruction2<IShaderValue, IShaderValue>> InstF { get; }
        = Instruction2.Factory;


    IExpressionSemantic<ShaderExpr, IExpression<ShaderExpr>> Expr = Expression.Factory<ShaderExpr>();
    private PointerOperationFactory Pointer = new();
    //public IShaderType GetValueType(IShaderValue value) => ValueTypes[value];
    public IShaderType GetValueType(IShaderValue value) => value.Type;


    public RuntimeReflectionInstructionParserVisitor3(
        MethodBodyAnalysisModel model,
        FunctionDeclaration function,
        ISuccessor successor,
        ImmutableStack<IShaderValue> inputStack)
    {
        Model = model;
        Function = function;
        Successor = successor;
        Stack = inputStack;
    }

    ShaderExpr CreateValueExpr(IShaderValue v) => ExpressionTree.Value(v);

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

    void DoStore(IShaderValue target, IShaderValue value)
    {
        var vt = GetValueType(value);
        var tt = GetValueType(target);
        if (tt is IPtrType ptt)
        {
            var conv = StoreConversion(ptt.BaseType, vt);
            if (conv is not null)
            {
                //value = EmitLet(conv.ResultType, ExprF.Operation1(conv, CreateValueExpr(value)));
                value = EmitLet(conv.ResultType, r => InstF.Operation1(default, conv, r, value));
            }

            Emit(InstF.Store(default, new StoreOperation(), target, value));
            //Emit(Stmt.Set(target, value));
        }
        else
        {
            throw new ValidationException($"can not store to non ptr type value {tt}", Model.Method);
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

    IShaderValue CreateValue(IShaderType t)
    {
        //var v = IShaderValue.Create(t);
        //ShaderValueDeclaration r = new(v, t);
        //ValueTypes.Add(v, t);
        //return v;
        return ShaderValue.Create(t);
    }

    void Push(IShaderValue v)
    {
        var type = GetValueType(v);
        var stackType = ConvertToCilStackType(type);
        var conv = ConvertToCilStackTypeOperation(type);
        if (conv is not null)
        {
            var cv = EmitLet(stackType, r => InstF.Operation1(default, conv, r, v));
            Stack = Stack.Push(cv);
        }
        else
        {
            Stack = Stack.Push(v);
        }
    }

    IShaderValue EvalExpr2(Func<IShaderType, IShaderType, IBinaryExpressionOperation> parseOp)
    {
        var r = Pop();
        var l = Pop();
        var op = parseOp(l.Type, r.Type);
        return EmitLet(op.ResultType, res => InstF.Operation2(default, op, res, l, r));
    }


    IShaderValue Pop()
    {
        Stack = Stack.Pop(out var r);
        return r;
    }

    void Emit(Instruction2<IShaderValue, IShaderValue> inst)
    {
        instructions.Add(inst);
    }

    IShaderValue EmitLet(IShaderType t, Func<IShaderValue, Instruction2<IShaderValue, IShaderValue>> e)
    {
        var value = CreateValue(t);
        instructions.Add(e(value));
        return value;
    }

    void PushEmitLet2(IBinaryExpressionOperation op, IShaderValue l, IShaderValue r)
    {
        var res = ShaderValue.Create(op.ResultType);
        //var v = EmitLet(op.ResultType, ExprF.Operation2(op, CreateValueExpr(l), CreateValueExpr(r)));
        //var v = EmitLet(op.ResultType, r => InstF.Operation2(default, op, r, l, r));
        Emit(InstF.Operation2(default, op, res, l, r));
        Push(res);
    }


    void DoLoad(IShaderValue p)
    {
        if (p.Type is IPtrType pt)
        {
            var r = CreateValue(pt.BaseType);
            Emit(InstF.Load(default, new LoadOperation(), r, p));
            //Emit(Stmt.Get(r, p));
            Push(r);
        }
        else
        {
            throw new ValidationException($"can not load from non-ptr type value {p} : {p.Type}", Model.Method);
        }
    }

    public Unit VisitLiteral<TLiteral>(CilInstructionInfo info, TLiteral literal) where TLiteral : ILiteral
    {
        //var val = EmitLet(literal.Type, ExprF.Literal(literal));
        var val = EmitLet(literal.Type, r => InstF.Literal(default, new LiteralOperation(), r, literal));
        Push(val);
        return default;
    }

    public Unit VisitPop(CilInstructionInfo inst)
    {
        var v = Pop();
        //Emit(Stmt.Pop(ShaderValue.Create(v.Type)));
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

            Terminator = TermF.ReturnVoid();
        }
        else
        {
            if (Function.Return.Type is UnitType)
            {
                throw new ValidationException("Function return type is Unit, but stack is empty.", Model.Method);
            }

            var v = Pop();
            Terminator = TermF.ReturnExpr(v);
            if (!Stack.IsEmpty)
            {
                throw new ValidationException("Function return type is Unit, but stack has more than one element.",
                    Model.Method);
            }
        }

        return default;
    }

    public Unit VisitNop(CilInstructionInfo inst)
    {
        //Emit(Stmt.Nop());
        Emit(InstF.Nop(default, NopOperation.Instance));
        return default;
    }

    public Unit VisitBreak(CilInstructionInfo inst)
    {
        throw new NotSupportedException();
    }

    public Unit VisitLoadArgument(CilInstructionInfo inst, ParameterDeclaration p)
    {
        DoLoad(p.Value);
        return default;
    }


    public Unit VisitBinaryArithmetic<TOp>(CilInstructionInfo inst, bool isUn = false, bool isChecked = false)
        where TOp : BinaryArithmetic.IOp<TOp>
    {
        var r = Pop();
        var l = Pop();
        var lt = GetValueType(l);
        var rt = GetValueType(r);
        if (!lt.Equals(rt))
        {
            throw new ValidationException($"binary op type not match {l} != {r}", Model.Method);
        }

        switch (lt)
        {
            case IntType<N32> when isUn:
                PushEmitLet2(NumericBinaryArithmeticOperation<UIntType<N32>, TOp>.Instance, l, r);
                break;
            case IntType<N32>:
                PushEmitLet2(NumericBinaryArithmeticOperation<IntType<N32>, TOp>.Instance, l, r);
                break;
            case IntType<N64> when isUn:
                PushEmitLet2(NumericBinaryArithmeticOperation<UIntType<N64>, TOp>.Instance, l, r);
                break;
            case IntType<N64>:
                PushEmitLet2(NumericBinaryArithmeticOperation<IntType<N64>, TOp>.Instance, l, r);
                break;
            case FloatType<N32>:
                PushEmitLet2(NumericBinaryArithmeticOperation<FloatType<N32>, TOp>.Instance, l, r);
                break;
            case FloatType<N64>:
                PushEmitLet2(NumericBinaryArithmeticOperation<FloatType<N64>, TOp>.Instance, l, r);
                break;
            default:
                throw new ValidationException($"op {TOp.Instance.Name} not support in type {l}", Model.Method);
        }

        return default;
    }

    public Unit VisitLoadArgumentAddress(CilInstructionInfo inst, ParameterDeclaration p)
    {
        Push(p.Value);
        return default;
    }

    public Unit VisitStoreArgument(CilInstructionInfo inst, ParameterDeclaration p)
    {
        var v = Pop();
        DoStore(p.Value, v);
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
        DoLoad(v.Value);
        return default;
    }

    public Unit VisitLoadLocalAddress(CilInstructionInfo inst, VariableDeclaration v)
    {
        Push(v.Value);
        return default;
    }

    public Unit VisitStoreLocal(CilInstructionInfo inst, VariableDeclaration v)
    {
        var t = v.Value;
        var val = Pop();
        DoStore(t, val);
        return default;
    }

    IShaderValue GetMemberPointer(IShaderValue o, MemberDeclaration m)
    {
        if (!(o.Type is IPtrType ptr && ptr.BaseType is StructureType))
        {
            throw new ValidationException($"can not load field from {o}, expected ptr to structure type", Model.Method);
        }

        //return EmitLet(m.Type.GetPtrType(), Expr.Operation1(Pointer.Member(m), CreateValueExpr(o)));
        var pa = (IPtrType)o.Type;

        return EmitLet(m.Type.GetPtrType(pa.AddressSpace), r => InstF.Operation1(default, Pointer.Member(m), r, o));
    }

    public Unit VisitLoadField(CilInstructionInfo inst, MemberDeclaration m)
    {
        var o = Pop();
        var mp = GetMemberPointer(o, m);
        //var rv = CreateValue(m.Type);
        DoLoad(mp);
        //Emit(Stmt.Get(rv, mp));
        //Push(rv);
        return default;
    }

    public Unit VisitLoadFieldAddress(CilInstructionInfo inst, MemberDeclaration m)
    {
        var o = Pop();
        var mp = GetMemberPointer(o, m);
        Push(mp);
        return default;
    }

    public Unit VisitStoreField(CilInstructionInfo inst, MemberDeclaration m)
    {
        var v = Pop();
        var o = Pop();
        var mp = GetMemberPointer(o, m);
        DoStore(mp, v);
        //Emit(Stmt.Set(mp, v));
        return default;
    }

    public Unit VisitLoadStaticField(CilInstructionInfo inst, VariableDeclaration v)
    {
        DoLoad(v.Value);
        return default;
    }

    public Unit VisitLoadStaticFieldAddress(CilInstructionInfo inst, VariableDeclaration v)
    {
        Push(v.Value);
        return default;
    }

    public Unit VisitLoadNull(CilInstructionInfo info)
    {
        throw new NotImplementedException();
    }

    void VisitCallFunction(FunctionDeclaration func, bool hasReturnValue)
    {
        // TODO: move this operation handle fully properly to FunctionToOperationPass
        if (func.Attributes.OfType<IOperationMethodAttribute>().SingleOrDefault() is { } opAttr)
        {
            switch (opAttr.Operation)
            {
                case IBinaryExpressionOperation be:
                    {
                        var r = Pop();
                        var l = Pop();
                        var lt = GetValueType(l);
                        var rt = GetValueType(r);
                        if (!lt.Equals(be.LeftType) || !rt.Equals(be.RightType))
                        {
                            throw new ValidationException($"{be} operation stack : {l}, {r}", Model.Method);
                        }
                        PushEmitLet2(be, l, r);
                        return;
                    }
                case IBinaryStatementOperation bs:
                    {
                        var r = Pop();
                        var l = Pop();
                        if (!GetValueType(l).Equals(bs.LeftType) || !GetValueType(r).Equals(bs.RightType))
                        {
                            if (l.Type is IPtrType lp && bs.LeftType is IPtrType bp && lp.BaseType.Equals(bp.BaseType))
                            {
                                // TODO: correct handling of address space equality
                            }
                            else
                            {
                                throw new ValidationException($"{bs.Name} {bs.LeftType.Name} {bs.RightType.Name} operation stack : {l.Type.Name}, {r.Type.Name}", Model.Method);
                            }
                        }

                        if (bs is IVectorComponentSetOperation vcs)
                        {
                            //var cp = EmitLet(vcs.ElementType.GetPtrType(), ExprF.AddressOfChain(new AddressOfVecComponentOperation(vcs.VecType, vcs.Component), CreateValueExpr(l)));
                            var cp = EmitLet(vcs.ElementType.GetPtrType(((IPtrType)l.Type).AddressSpace), res => InstF.AddressOfChain(default, new AddressOfVecComponentOperation(vcs.VecType, vcs.Component), res, l));
                            DoStore(cp, r);
                            //Emit(Stmt.Set(cp, r));
                            return;
                        }

                        if (bs is IVectorSwizzleSetOperation vss)
                        {
                            Emit(InstF.VectorSwizzleSet(default, vss, l, r));
                            //Emit(Stmt.SetVecSwizzle(vss, l, r));
                            return;
                        }

                        throw new NotSupportedException($"binary statement {bs.Name}");
                    }
                case IUnaryExpressionOperation ue:
                    {
                        var s = Pop();
                        if (!GetValueType(s).Equals(ue.SourceType))
                        {
                            if (s.Type is IPtrType ps && ue.SourceType is IPtrType pu && ps.BaseType.Equals(pu.BaseType))
                            {
                                // TODO: modify operation to support precise control on address space 
                            }
                            else
                            {
                                throw new ValidationException($"{ue} operation stack : {s}", Model.Method);
                            }
                        }

                        var v = EmitLet(ue.ResultType, r => InstF.Operation1(default, ue, r, s));

                        //var v = EmitLet(ue.ResultType, ExprF.Operation1(ue, CreateValueExpr(s)));
                        Push(v);
                        return;

                        //Emit(StackIR.Instruction.Expr1(ue));
                        //Push(ue.ResultType);
                        //throw new NotImplementedException($"unary expression {ue} not implemented");
                    }
            }
        }

        List<IShaderValue> args = [];
        foreach (var p in func.Parameters.Reverse())
        {
            var ve = Pop();
            if (!GetValueType(ve).Equals(p.Type))
            {
                throw new ValidationException(
                    $"parameter {p} not match: stack {ve} declaration {p.Type.Name}",
                    Model.Method);
            }
            args.Add(ve);
        }
        args.Reverse();
        var rv = CreateValue(func.Return.Type);
        Emit(InstF.Call(default, new CallOperation((FunctionType)func.Type), rv, func, args));
        //Emit(Stmt.Call(rv, func, [.. args]));

        if (hasReturnValue)
        {
            Push(rv);
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

    public ImmutableArray<IShaderValue> GetStackOutput()
    {
        var result = Stack.Reverse().ToImmutableArray();
        //Stack = [];
        return result;
    }

    public Unit VisitBranch(CilInstructionInfo inst, int jumpOffset)
    {
        if (Successor is UnconditionalSuccessor { Target: var target })
        {
            var args = GetStackOutput();
            Terminator = TermF.Br(new(target, args));
            return default;
        }
        else
        {
            throw new ValidationException($"successor mismatch, expected br, got {Successor}", Model.Method);
        }
    }

    public Unit VisitBranchIf(CilInstructionInfo inst, int jumpOffset, bool value)
    {
        var v = Pop();
        var t = GetValueType(v);
        if (t is not IntType<N32>)
        {
            throw new ValidationException($"br if expecte a i32 stack value, got {t}", Model.Method);
        }

        if (Successor is ConditionalSuccessor { TrueTarget: var tt, FalseTarget: var ft })
        {
            var args = GetStackOutput();
            //var vb = EmitLet(ShaderType.Bool, Expr.Operation1(ScalarConversionOperation<IntType<N32>, BoolType>.Instance, CreateValueExpr(v)));
            var vb = EmitLet(ShaderType.Bool, res => InstF.Operation1(default, ScalarConversionOperation<IntType<N32>, BoolType>.Instance, res, v));
            if (value)
            {
                Terminator = TermF.BrIf(vb, new(tt, args), new(ft, args));
            }
            else
            {
                Terminator = TermF.BrIf(vb, new(ft, args), new(tt, args));
            }

            return default;
        }
        else
        {
            throw new ValidationException($"successor mismatch, expected br_if, got {Successor}", Model.Method);
        }
    }

    public Unit VisitBranchIf<TOp>(CilInstructionInfo inst, int jumpOffset, bool isUn = false)
        where TOp : BinaryRelational.IOp<TOp>
    {
        var v = EvalExpr2((lt, rt) =>
        {
            if (!lt.Equals(rt))
            {
                throw new ValidationException($"binary relational op type not match {lt.Name} {rt.Name}", Model.Method);
            }
            return lt switch
            {
                IntType<N32> when isUn =>
                    NumericBinaryRelationalOperation<UIntType<N32>, TOp>.Instance,
                IntType<N32> =>
                NumericBinaryRelationalOperation<IntType<N32>, TOp>.Instance,
                IntType<N64> when isUn =>
                   NumericBinaryRelationalOperation<UIntType<N64>, TOp>.Instance,
                IntType<N64> =>
                    NumericBinaryRelationalOperation<IntType<N64>, TOp>.Instance,
                FloatType<N32> =>
                    NumericBinaryRelationalOperation<FloatType<N32>, TOp>.Instance,
                FloatType<N64> =>
                    NumericBinaryRelationalOperation<FloatType<N64>, TOp>.Instance,
                _ =>
                    throw new ValidationException($"Unsupported relational operand {lt}, {rt} for op {TOp.Instance.Name}", Model.Method)
            };
        });

        if (Successor is ConditionalSuccessor { TrueTarget: var tt, FalseTarget: var ft })
        {
            var args = GetStackOutput();
            var vb = EmitLet(ShaderType.Bool, r => InstF.Operation1(default, ScalarConversionOperation<IntType<N32>, BoolType>.Instance, r, v));
            //var vb = EmitLet(ShaderType.Bool, Expr.Operation1(ScalarConversionOperation<IntType<N32>, BoolType>.Instance, CreateValueExpr(v)));
            Terminator = TermF.BrIf(vb, new(tt, args), new(ft, args));
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
        if (!(l.Type is IntType<N32> && r.Type is IntType<N32>))
        {
            throw new ValidationException($"logical op requires i32 operands, got {l}, {r}", Model.Method);
        }

        if (TOp.Instance is BinaryLogical.IWithBitwiseOp bOp)
        {
            PushEmitLet2(bOp.BitwiseOp.GetNumericBinaryOperation(IntType<N32>.Instance), l, r);
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
            // TODO handle unorder comparison
            (FloatType<N32>, FloatType<N32>, true) => NumericBinaryRelationalOperation<FloatType<N32>, TOp>.Instance,
            (FloatType<N64>, FloatType<N64>, true) => NumericBinaryRelationalOperation<FloatType<N64>, TOp>.Instance,
            _ => throw new ValidationException($"binary relational op {TOp.Instance.Name} not support in type {l}, {r}",
                Model.Method)
        };
    }

    public Unit VisitBinaryRelation<TOp>(CilInstructionInfo inst, bool isUn = false, bool isChecked = false)
        where TOp : BinaryRelational.IOp<TOp>
    {
        var r = Pop();
        var l = Pop();
        var lt = GetValueType(l);
        var rt = GetValueType(r);
        var op = BinaryRelationalOperation<TOp>(lt, rt, isUn);
        PushEmitLet2(op, l, r);
        return default;
    }

    public Unit VisitConversion<TTarget>(CilInstructionInfo inst) where TTarget : IScalarType<TTarget>
    {
        var v = Pop();
        var t = GetValueType(v);
        if (t is IScalarType nt)
        {
            var operation = nt.GetConversionToOperation<TTarget>();
            //var r = EmitLet(operation.ResultType, Expr.Operation1(operation, CreateValueExpr(v)));
            var r = EmitLet(operation.ResultType, res => InstF.Operation1(default, operation, res, v));
            Push(r);
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
        var v = Pop();
        var t = v.Type;
        if (t is IScalarType nt)
        {
            IUnaryExpressionOperation op;
            switch (t)
            {
                case FloatType<N32>:
                    op = UnaryNumericArithmeticExpressionOperation<FloatType<N32>, TOp>.Instance;
                    break;
                default:
                    throw new NotImplementedException();
                    break;
            }

            //var r = EmitLet(op.ResultType, Expr.Operation1(op, CreateValueExpr(v)));
            var r = EmitLet(op.ResultType, res => InstF.Operation1(default, op, res, v));
            Push(r);
            return default;
        }
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
}