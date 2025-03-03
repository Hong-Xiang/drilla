using DualDrill.CLSL.Language.Declaration;
using DualDrill.CLSL.Language.LinearInstruction;
using DualDrill.CLSL.Language.Literal;
using DualDrill.CLSL.Language.Operation;
using DualDrill.CLSL.Language.Types;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using DualDrill.CLSL.Language.AbstractSyntaxTree;
using DualDrill.CLSL.Language.AbstractSyntaxTree.Expression;
using DualDrill.CLSL.Language.AbstractSyntaxTree.Statement;
using DualDrill.CLSL.Language.ControlFlow;
using DualDrill.CLSL.Language.ShaderAttribute;
using DualDrill.Common;
using DualDrill.Common.Nat;

namespace DualDrill.CLSL.Frontend;

public sealed class ValidationException(string message, MethodBase method)
    : Exception(message + $" @ {method.Name}")
{
    public MethodBase Method { get; } = method;
}

sealed class RuntimeReflectionParserInstructionVisitor(
    FunctionDeclaration Function,
    MethodBodyAnalysisModel Model,
    Label Label,
    ImmutableStack<VariableDeclaration> Inputs)
    : ICilInstructionVisitor<Unit>
{
    //public List<int> Nexts { get; private set; } = [];

    public List<IStackStatement> Statements { get; set; } = [];


    Stack<IExpression> CurrentStack = new(Inputs.Select(v => SyntaxFactory.VarIdentifier(v)));

    public ImmutableStack<VariableDeclaration> Outputs { get; private set; } = [];


    static IExpression ToBinaryArithmeticExpression<TOp>(IExpression left, IExpression right)
        where TOp : BinaryArithmetic.IOp<TOp>
    {
        (IExpression l, IExpression r, IBinaryExpressionOperation op) = (left, right) switch
        {
            ({ Type: INumericType tl }, { Type: INumericType tr }) when tl.Equals(tr) => (left, right,
                tl.ArithmeticOperation<TOp>()),
            ({ Type: UIntType<N32> ut }, LiteralValueExpression { Literal: I32Literal { Value: var value } })
                => (left, SyntaxFactory.Literal(Literal.Create((uint)value)),
                    ((INumericType)ut).ArithmeticOperation<TOp>()),
            _ => throw new NotSupportedException()
        };
        return op.CreateExpression(l, r);
    }

    static IExpression HandleBinaryRelationalExpression<TOp>(IExpression left, IExpression right)
        where TOp : BinaryRelational.IOp<TOp>
    {
        switch (left, right)
        {
            case ({ Type: INumericType nt }, _) when left.Type.Equals(right.Type):
                IBinaryExpressionOperation op = nt.RelationalOperation<TOp>();
                return op.CreateExpression(left, right);
            case ({ Type: UIntType<N32> lt }, LiteralValueExpression { Literal: I32Literal { Value: var value } }):
                var ur = new LiteralValueExpression(Literal.Create((uint)value));
                return ((INumericType)lt).RelationalOperation<TOp>().CreateExpression(left, ur);
            case ({ Type: BoolType }, LiteralValueExpression):
            case (LiteralValueExpression, { Type: BoolType }):
                switch (TOp.Instance)
                {
                    case BinaryRelational.Eq:
                        return ToLogicalBinaryExpression<BinaryRelational.Eq>(left, right);
                    case BinaryRelational.Ne:
                        return ToLogicalBinaryExpression<BinaryRelational.Ne>(left, right);
                }

                break;
        }


        throw new NotSupportedException(
            $"relational operator {TOp.Instance.Name} for {left.Type.Name} and {right.Type.Name} is not supported");
    }

    static IExpression ToLogicalBinaryExpression<TOp>(IExpression left, IExpression right)
        where TOp : BinaryLogical.IOp<TOp>
    {
        IBinaryExpressionOperation op = LogicalBinaryOperation<TOp>.Instance;
        switch (left, right)
        {
            case ({ Type: BoolType }, _):
            case (_, { Type: BoolType }):
                return op.CreateExpression(ToBoolExpr(left), ToBoolExpr(right));
            case ({ Type: INumericType lt }, { Type: INumericType rt }) when lt.Equals(rt):
                {
                    if (TOp.Instance is BinaryLogical.IWithBitwiseOp withBitwiseOp)
                    {
                        var bitwiseOp = withBitwiseOp.BitwiseOp;
                        return bitwiseOp.GetNumericBinaryOperation(lt).CreateExpression(left, right);
                    }

                    break;
                }
            default:
                throw new NotImplementedException();
        }

        throw new NotImplementedException();
    }

    public Unit VisitBinaryArithmetic<TOp>(CilInstructionInfo inst, bool isUn = false, bool isChecked = false)
        where TOp : BinaryArithmetic.IOp<TOp>
    {
        var r = CurrentStack.Pop();
        var l = CurrentStack.Pop();
        var e = ToBinaryArithmeticExpression<TOp>(l, r);
        CurrentStack.Push(e);
        return default;
    }


    static IExpression ToBoolExpr(IExpression expr)
    {
        return expr switch
        {
            LiteralValueExpression { Literal: I32Literal { Value: 0 } } => new LiteralValueExpression(
                Literal.Create(false)),
            LiteralValueExpression { Literal: I32Literal } => new LiteralValueExpression(
                Literal.Create(true)),
            { Type: BoolType } _ => expr,
            _ => throw new NotSupportedException()
        };
    }


    public Unit VisitBinaryLogical<TOp>(CilInstructionInfo inst)
        where TOp : BinaryLogical.IOp<TOp>
    {
        var r = CurrentStack.Pop();
        var l = CurrentStack.Pop();
        var e = ToLogicalBinaryExpression<TOp>(l, r);
        CurrentStack.Push(e);
        return default;
    }


    public Unit VisitBinaryRelation<TOp>(CilInstructionInfo inst, bool isUn = false, bool isChecked = false)
        where TOp : BinaryRelational.IOp<TOp>
    {
        var r = CurrentStack.Pop();
        var l = CurrentStack.Pop();
        var e = HandleBinaryRelationalExpression<TOp>(l, r);
        CurrentStack.Push(e);
        return default;
    }

    public void FlushOutputs()
    {
        if (CurrentStack.Count == 0)
            return;
        if (!Outputs.IsEmpty)
        {
            throw new NotSupportedException("multiple flush outputs are not expected");
        }

        Outputs = ImmutableStack.Create<VariableDeclaration>();
        foreach (var (index, expr) in CurrentStack.Index().Reverse())
        {
            var v = new VariableDeclaration(DeclarationScope.Function, $"output({Label.Name})#{index}", expr.Type, []);
            Statements.Add(SyntaxFactory.AssignStatement(
                SyntaxFactory.VarIdentifier(v),
                expr));
            Outputs = Outputs.Push(v);
        }

        CurrentStack.Clear();
    }

    public Unit VisitBranch(CilInstructionInfo inst, int jumpOffset)
    {
        FlushOutputs();
        return default;
    }

    public Unit VisitBreak(CilInstructionInfo inst)
    {
        throw new NotSupportedException($"{inst.Instruction.OpCode}");
    }

    public Unit VisitBranchIf(CilInstructionInfo inst, int jumpOffset, bool value)
    {
        var v = ToBoolExpr(CurrentStack.Pop());

        // bf.false
        if (value == false)
        {
            v = LogicalNotOperation.Instance.CreateExpression(v);
        }

        if (CurrentStack.Count > 0)
        {
            FlushOutputs();
        }

        Statements.Add(new PushStatement(v));


        return default;
    }

    public Unit VisitBranchIf<TOp>(CilInstructionInfo inst, int jumpOffset, bool isUn = false)
        where TOp : BinaryRelational.IOp<TOp>
    {
        var r = CurrentStack.Pop();
        var l = CurrentStack.Pop();
        // TODO: handle isUn = true
        var e = HandleBinaryRelationalExpression<TOp>(l, r);

        if (CurrentStack.Count > 0)
        {
            FlushOutputs();
        }

        Statements.Add(new PushStatement(e));
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
                        var r = CurrentStack.Pop();
                        var l = CurrentStack.Pop();
                        var e = be.CreateExpression(r, l);
                        CurrentStack.Push(e);
                        return;
                    }
                case IBinaryStatementOperation bs:
                    {
                        var r = CurrentStack.Pop();
                        var l = CurrentStack.Pop();
                        var s = bs.CreateStatement(r, l);
                        Statements.Add((IStackStatement)s);
                        return;
                    }
                case IUnaryExpressionOperation ue:
                    {
                        var s = CurrentStack.Pop();
                        var e = ue.CreateExpression(s);
                        CurrentStack.Push(e);
                        return;
                    }
            }
        }

        var args = new List<IExpression>(func.Parameters.Length);
        foreach (var p in func.Parameters.Reverse())
        {
            var ve = CurrentStack.Pop();
            if (!ve.Type.Equals(p.Type))
            {
                throw new ValidationException(
                    $"parameter {p} not match: stack {ve.Type.Name} declaration {p.Type.Name}",
                    Model.Method);
            }

            args.Add(ve);
        }

        args.Reverse();

        var result = SyntaxFactory.Call(func, [.. args]);
        if (isExpression)
        {
            CurrentStack.Push(result);
        }
        else
        {
            Statements.Add(SyntaxFactory.ExpressionStatement(result));
        }
    }

    public Unit VisitCall(CilInstructionInfo info, FunctionDeclaration f)
    {
        VisitCallFunction(f, f.Return.Type is not UnitType);
        return default;
    }

    public Unit VisitLoadArgument(CilInstructionInfo inst, ParameterDeclaration p)
    {
        CurrentStack.Push(SyntaxFactory.ArgIdentifier(p));
        return default;
    }

    public Unit VisitLoadArgumentAddress(CilInstructionInfo inst, ParameterDeclaration p)
    {
        CurrentStack.Push(SyntaxFactory.AddressOf(SyntaxFactory.ArgIdentifier(p)));
        return default;
    }

    public Unit VisitUnaryArithmetic<TOp>(CilInstructionInfo inst) where TOp : UnaryArithmetic.IOp<TOp>
    {
        var v = CurrentStack.Pop();
        if (v.Type is INumericType type)
        {
            CurrentStack.Push(type.UnaryArithmeticOperation<TOp>().CreateExpression(v));
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
        CurrentStack.Push(SyntaxFactory.VarIdentifier(v));
        return default;
    }

    public Unit VisitLoadLocalAddress(CilInstructionInfo inst, VariableDeclaration v)
    {
        CurrentStack.Push(SyntaxFactory.AddressOf(SyntaxFactory.VarIdentifier(v)));
        return default;
    }

    public Unit VisitLoadNull(CilInstructionInfo info)
    {
        throw new NotImplementedException();
    }

    public Unit VisitLiteral<TLiteral>(CilInstructionInfo info, TLiteral literal) where TLiteral : ILiteral
    {
        CurrentStack.Push(SyntaxFactory.Literal(literal));
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
        switch (CurrentStack.Count)
        {
            case 0:
                if (Function.Return.Type is not UnitType)
                {
                    throw new ValidationException("return when stack is empty requires unit type", Model.Method);
                }

                return default;
            case 1:
                var e = CurrentStack.Pop();
                var rt = Function.Return.Type;
                if (!rt.Equals(e.Type))
                {
                    throw new ValidationException(
                        $"return when statck type {e.Type.Name} is not consistent with function signature return {rt.Name}",
                        Model.Method);
                }

                Statements.Add(new PushStatement(e));
                return default;
            default:
                throw new ValidationException("return when stack.Count > 1", Model.Method);
        }
    }

    public Unit VisitStoreArgument(CilInstructionInfo inst, ParameterDeclaration p)
    {
        var v = CurrentStack.Pop();
        if (p.Type.Equals(v.Type))
        {
            Statements.Add(SyntaxFactory.AssignStatement(
                SyntaxFactory.ArgIdentifier(p),
                v
            ));
        }
        else
        {
            throw new NotSupportedException($"store {v.Type.Name} to {p.Type.Name} arg");
        }

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
        var val = CurrentStack.Pop();

        if (!v.Type.Equals(val.Type))
        {
            switch (v.Type, val)
            {
                case (BoolType, LiteralValueExpression):
                    val = ToBoolExpr(val);
                    break;
                case (UIntType<N32>, LiteralValueExpression { Literal: I32Literal { Value: var i32Value } }):
                    {
                        var u32Value = BitConverter.ToUInt32(BitConverter.GetBytes(i32Value));
                        val = SyntaxFactory.Literal(u32Value);
                        break;
                    }
                case (IntType<N32>, IExpression { Type: UIntType<N32> }):
                    {
                        val = ScalarConversionOperation<UIntType<N32>, IntType<N32>>.Instance.CreateExpression(val);
                        break;
                    }
                case (UIntType<N32>, IExpression { Type: IntType<N32> }):
                    {
                        val = ScalarBitCastOperation<IntType<N32>, UIntType<N32>>.Instance.CreateExpression(val);
                        break;
                    }
                default:
                    throw new NotSupportedException($"store {val.Type.Name} to loc : {v.Type.Name} is not supported");
            }
        }

        Statements.Add(SyntaxFactory.AssignStatement(
            SyntaxFactory.VarIdentifier(v),
            val
        ));
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
        if (CurrentStack.Count == 0)
            throw new ValidationException("Cannot dup when stack is empty", Model.Method);

        var top = CurrentStack.Pop();
        var v = new VariableDeclaration(DeclarationScope.Function, "", top.Type, []);
        Statements.Add(SyntaxFactory.AssignStatement(SyntaxFactory.VarIdentifier(v), top));
        CurrentStack.Push(SyntaxFactory.VarIdentifier(v));
        CurrentStack.Push(SyntaxFactory.VarIdentifier(v));
        return default;
    }

    public Unit VisitPop(CilInstructionInfo inst)
    {
        if (CurrentStack.Count == 0)
            throw new ValidationException("Cannot pop when stack is empty", Model.Method);

        var e = CurrentStack.Pop();
        Statements.Add(SyntaxFactory.ExpressionStatement(e));
        return default;
    }

    public Unit VisitConversion<TTarget>(CilInstructionInfo inst) where TTarget : IScalarType<TTarget>
    {
        var e = CurrentStack.Pop();
        if (e.Type is IScalarType nt)
        {
            CurrentStack.Push(nt.GetConversionToOperation<TTarget>().CreateExpression(e));
            return default;
        }

        throw new NotImplementedException();
    }

    public Unit VisitLogicalNot(CilInstructionInfo inst)
    {
        var v = CurrentStack.Pop();
        switch (v.Type)
        {
            case BoolType:
                CurrentStack.Push(LogicalNotOperation.Instance.CreateExpression(v));
                break;
            default:
                throw new NotImplementedException();
        }

        return default;
    }

    public Unit VisitLdThis(CilInstructionInfo inst)
    {
        var p = Function.Parameters[0];
        CurrentStack.Push(SyntaxFactory.ArgIdentifier(p));
        return default;
    }

    public Unit VisitStThis(CilInstructionInfo inst)
    {
        throw new NotImplementedException();
    }

    public Unit VisitLoadField(CilInstructionInfo inst, MemberDeclaration m)
    {
        var o = CurrentStack.Pop();
        if (o.Type is not IPtrType)
        {
            throw new ValidationException("ldfld expect current stack to have ptr type", Model.Method);
        }

        CurrentStack.Push(SyntaxFactory.FieldIdentifier(o, m));
        return default;
    }

    public Unit VisitLoadFieldAddress(CilInstructionInfo inst, MemberDeclaration m)
    {
        var o = CurrentStack.Pop();
        if (o.Type is not IPtrType)
        {
            throw new ValidationException("ldfld expect current stack to have ptr type", Model.Method);
        }

        // TODO: check o is a structure type and m is member of it
        CurrentStack.Push(SyntaxFactory.AddressOf(SyntaxFactory.FieldIdentifier(o, m)));
        return default;
    }

    public Unit VisitStoreField(CilInstructionInfo inst, MemberDeclaration m)
    {
        throw new NotImplementedException();
    }


    public Unit VisitLoadStaticField(CilInstructionInfo inst, VariableDeclaration v)
    {
        CurrentStack.Push(SyntaxFactory.VarIdentifier(v));
        return default;
    }

    public Unit VisitLoadStaticFieldAddress(CilInstructionInfo inst, VariableDeclaration v)
    {
        CurrentStack.Push(SyntaxFactory.AddressOf(SyntaxFactory.VarIdentifier(v)));
        return default;
    }
}