﻿using DualDrill.CLSL.Language;
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
using ClangSharp;
using DualDrill.CLSL.Language.Expression;
using DualDrill.CLSL.Language.Operation.Pointer;
using DualDrill.CLSL.Language.Statement;
using LLVMSharp;
using ShaderValueDeclaration = DualDrill.CLSL.Language.Symbol.ShaderValueDeclaration;

namespace DualDrill.CLSL.Frontend;

using ShaderExpr = IExpressionTree<ShaderValue>;

sealed class RuntimeReflectionInstructionParserVisitor3
    : ICilInstructionVisitor<Unit>
{
    public MethodBodyAnalysisModel Model { get; }
    public FunctionDeclaration Function { get; }
    public ISuccessor Successor { get; }
    public ImmutableStack<ShaderValue> Stack { get; private set; }
    public IReadOnlyDictionary<VariableDeclaration, ShaderValueDeclaration> Locals { get; }
    public IReadOnlyDictionary<ParameterDeclaration, IParameterBinding> Parameters { get; }

    public ITerminator<RegionJump, ShaderValue>? Terminator { get; private set; } = null;
    public IReadOnlyList<ShaderStmt> Statements => statements;

    List<ShaderStmt> statements = [];

    private IStatementSemantic<Unit, ShaderValue, ShaderExpr, ShaderValue, FunctionDeclaration, ShaderStmt> Stmt { get; }
        = Statement.Factory<ShaderValue, ShaderExpr, ShaderValue, FunctionDeclaration>();

    private ITerminatorSemantic<Unit, RegionJump, ShaderValue, ITerminator<RegionJump, ShaderValue>> TermF { get; }
        = Language.Terminator.Factory<RegionJump, ShaderValue>();

    private IExpressionSemantic<Unit, IExpressionTree<ShaderValue>, IExpression<ShaderExpr>> ExprF { get; } =
        Language.Expression.Expression.Factory<IExpressionTree<ShaderValue>>();

    IExpressionSemantic<Unit, ShaderExpr, IExpression<ShaderExpr>> Expr = Expression.Factory<ShaderExpr>();
    private Dictionary<ShaderValue, IShaderType> ValueTypes = [];
    private PointerOperationFactory Pointer = new();
    public IShaderType GetValueType(ShaderValue value) => ValueTypes[value];


    public RuntimeReflectionInstructionParserVisitor3(
        MethodBodyAnalysisModel model,
        FunctionDeclaration function,
        ISuccessor successor,
        ImmutableStack<ShaderValueDeclaration> inputStack,
        IReadOnlyDictionary<VariableDeclaration, ShaderValueDeclaration> locals,
        IReadOnlyDictionary<ParameterDeclaration, IParameterBinding> parameters
    )
    {
        Model = model;
        Function = function;
        Successor = successor;
        Stack = ImmutableStack.CreateRange(inputStack.Select(x => x.Value));
        Locals = locals;
        Parameters = parameters;
        foreach (var p in parameters.Values)
        {
            ValueTypes.Add(p.Value, p.Type);
        }

        foreach (var v in locals.Values)
        {
            ValueTypes.Add(v.Value, v.Type);
        }

        foreach (var v in inputStack)
        {
            ValueTypes.Add(v.Value, v.Type);
        }
    }

    ShaderExpr CreateValueExpr(ShaderValue v) => ExpressionTree.Value(v);

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

    void DoStore(ShaderValue target, ShaderValue value)
    {
        var vt = GetValueType(value);
        var tt = GetValueType(target);
        if (tt is IPtrType ptt)
        {
            var conv = StoreConversion(ptt.BaseType, vt);
            if (conv is not null)
            {
                value = EmitLet(conv.ResultType, ExprF.Operation1(default, conv, CreateValueExpr(value)));
            }

            Emit(Stmt.Set(default, target, value));
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

    ShaderValue CreateValue(IShaderType t)
    {
        var v = ShaderValue.Create();
        ShaderValueDeclaration r = new(v, t);
        ValueTypes.Add(v, t);
        return v;
    }

    void Push(ShaderValue v)
    {
        var type = GetValueType(v);
        var stackType = ConvertToCilStackType(type);
        var conv = ConvertToCilStackTypeOperation(type);
        if (conv is not null)
        {
            var cv = EmitLet(stackType, ExprF.Operation1(default, conv, CreateValueExpr(v)));
            Stack = Stack.Push(cv);
        }
        else
        {
            Stack = Stack.Push(v);
        }
    }

    ShaderValue EvalExpr2(Func<IShaderType, IShaderType, IBinaryExpressionOperation> parseOp)
    {
        var rv = Pop();
        var lv = Pop();
        var r = GetValueType(rv);
        var l = GetValueType(lv);
        var op = parseOp(l, r);
        return EmitLet(op.ResultType, ExprF.Operation2(default, op, CreateValueExpr(lv), CreateValueExpr(rv)));
    }


    ShaderValue Pop()
    {
        Stack = Stack.Pop(out var r);
        return r;
    }

    void Emit(ShaderStmt inst)
    {
        statements.Add(inst);
    }

    ShaderValue EmitLet(IShaderType t, IExpression<ShaderExpr> e)
    {
        var value = CreateValue(t);
        statements.Add(Stmt.Let(default, value, e.AsNode()));
        return value;
    }


    void PushEmitLet2(IBinaryExpressionOperation op, ShaderValue l, ShaderValue r)
    {
        var v = EmitLet(op.ResultType, ExprF.Operation2(default, op, CreateValueExpr(l), CreateValueExpr(r)));
        Push(v);
    }


    void DoLoad(ShaderValue p)
    {
        var t = GetValueType(p);
        if (t is IPtrType pt)
        {
            var r = CreateValue(pt.BaseType);
            Emit(Stmt.Get(default, r, p));
            Push(r);
        }
        else
        {
            throw new ValidationException($"can not load from non-ptr type value {p} : {t}", Model.Method);
        }
    }

    public Unit VisitLiteral<TLiteral>(CilInstructionInfo info, TLiteral literal) where TLiteral : ILiteral
    {
        var val = EmitLet(literal.Type, ExprF.Literal(default, literal));
        Push(val);
        return default;
    }

    public Unit VisitPop(CilInstructionInfo inst)
    {
        Emit(Stmt.Pop(default, ShaderValue.Create()));
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

            Terminator = TermF.ReturnVoid(default);
        }
        else
        {
            if (Function.Return.Type is UnitType)
            {
                throw new ValidationException("Function return type is Unit, but stack is empty.", Model.Method);
            }

            var v = Pop();
            Terminator = TermF.ReturnExpr(default, v);
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
        Emit(Stmt.Nop(default));
        return default;
    }

    public Unit VisitBreak(CilInstructionInfo inst)
    {
        throw new NotSupportedException();
    }

    public Unit VisitLoadArgument(CilInstructionInfo inst, ParameterDeclaration p)
    {
        var val = Parameters[p];
        DoLoad(val.Value);
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
        var v = Parameters[p];
        Push(v.Value);
        return default;
    }

    public Unit VisitStoreArgument(CilInstructionInfo inst, ParameterDeclaration p)
    {
        var t = Parameters[p];
        var v = Pop();
        DoStore(t.Value, v);
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
        DoLoad(Locals[v].Value);
        return default;
    }

    public Unit VisitLoadLocalAddress(CilInstructionInfo inst, VariableDeclaration v)
    {
        Push(Locals[v].Value);
        return default;
    }

    public Unit VisitStoreLocal(CilInstructionInfo inst, VariableDeclaration v)
    {
        var t = Locals[v].Value;
        var val = Pop();
        DoStore(t, val);
        return default;
    }

    ShaderValue GetMemberPointer(ShaderValue o, MemberDeclaration m)
    {
        var t = GetValueType(o);
        if (!(t is IPtrType ptr && ptr.BaseType is StructureType))
        {
            throw new ValidationException($"can not load field from {o}, expected ptr to structure type", Model.Method);
        }

        return EmitLet(m.Type.GetPtrType(), Expr.Operation1(default, Pointer.Member(m), CreateValueExpr(o)));
    }

    public Unit VisitLoadField(CilInstructionInfo inst, MemberDeclaration m)
    {
        var o = Pop();
        var mp = GetMemberPointer(o, m);
        var rv = CreateValue(m.Type);
        Emit(Stmt.Get(default, rv, mp));
        Push(rv);
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
        Emit(Stmt.Set(default, mp, v));
        return default;
    }

    public Unit VisitLoadStaticField(CilInstructionInfo inst, VariableDeclaration v)
    {
        throw new NotImplementedException();
        // Emit(StackIR.Instruction.GetLocal(v));
        // Push(v.Type);
        // return default;
    }

    public Unit VisitLoadStaticFieldAddress(CilInstructionInfo inst, VariableDeclaration v)
    {
        throw new NotImplementedException();
        // Emit(StackIR.Instruction.GetLocalAddress(v));
        // Push(v.Type.GetPtrType());
        // return default;
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
                        var lt = GetValueType(l);
                        var rt = GetValueType(r);
                        if (!lt.Equals(be.LeftType) || !rt.Equals(be.RightType))
                        {
                            throw new ValidationException($"{be} operation stack : {l.Name}, {r.Name}", Model.Method);
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
                            throw new ValidationException($"{bs} operation stack : {l.Name}, {r.Name}", Model.Method);
                        }

                        if (bs is IVectorComponentSetOperation vcs)
                        {
                            var cp = EmitLet(vcs.ElementType.GetPtrType(), ExprF.AddressOfChain(default, new AddressOfVecComponentOperation(vcs.VecType, vcs.Component), CreateValueExpr(l)));
                            Emit(Stmt.Set(default, cp, r));
                            return;
                        }

                        if (bs is IVectorSwizzleSetOperation vss)
                        {
                            Emit(Stmt.SetVecSwizzle(default, vss, l, r));
                            return;
                        }

                        throw new NotSupportedException($"binary statement {bs.Name}");
                    }
                case IUnaryExpressionOperation ue:
                    {
                        var s = Pop();
                        if (!GetValueType(s).Equals(ue.SourceType))
                        {
                            throw new ValidationException($"{ue} operation stack : {s.Name}", Model.Method);
                        }

                        var v = EmitLet(ue.ResultType, ExprF.Operation1(default, ue, CreateValueExpr(s)));
                        Push(v);
                        return;

                        //Emit(StackIR.Instruction.Expr1(ue));
                        //Push(ue.ResultType);
                        //throw new NotImplementedException($"unary expression {ue} not implemented");
                    }
            }
        }

        List<ShaderValue> args = [];
        foreach (var p in func.Parameters.Reverse())
        {
            var ve = Pop();
            if (!GetValueType(ve).Equals(p.Type))
            {
                throw new ValidationException(
                    $"parameter {p} not match: stack {ve.Name} declaration {p.Type.Name}",
                    Model.Method);
            }
            args.Add(ve);
        }
        args.Reverse();
        var rv = CreateValue(UnitType.Instance);
        Emit(Stmt.Call(default, rv, func, [.. args.Select(v => CreateValueExpr(v))]));

        //throw new NotImplementedException();
        //Emit(StackIR.Instruction.Call(func));
        if (isExpression)
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

    public ImmutableArray<ShaderValue> GetStackOutput()
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
            Terminator = TermF.Br(default, new(target, args));
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
            if (value)
            {
                Terminator = TermF.BrIf(default, v, new(tt, args), new(ft, args));
            }
            else
            {
                Terminator = TermF.BrIf(default, v, new(ft, args), new(tt, args));
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
            Terminator = TermF.BrIf(default, v, new(tt, args), new(ft, args));
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
            var r = EmitLet(operation.ResultType, Expr.Operation1(default, operation, CreateValueExpr(v)));
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