using DualDrill.CLSL.Language.Declaration;
using DualDrill.CLSL.Language.LinearInstruction;
using DualDrill.CLSL.Language.Literal;
using DualDrill.CLSL.Language.Operation;
using DualDrill.CLSL.Language.Types;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Reflection;
using DualDrill.CLSL.Language.AbstractSyntaxTree;
using DualDrill.CLSL.Language.AbstractSyntaxTree.Expression;
using DualDrill.CLSL.Language.AbstractSyntaxTree.Statement;
using DualDrill.CLSL.Language.ShaderAttribute;
using DualDrill.Common;
using DualDrill.Common.Nat;

namespace DualDrill.CLSL.Frontend;

public sealed class ValidationException(string message, MethodBase method)
    : Exception(message)
{
    public MethodBase Method { get; } = method;
}

sealed class RuntimeReflectionParserInstructionVisitor(
    FunctionDeclaration Function,
    MethodBodyAnalysisModel Model,
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

    static IExpression ToBinaryRelationalExpression<TOp>(IExpression left, IExpression right)
        where TOp : BinaryRelational.IOp<TOp>
    {
        if (left.Type.Equals(right.Type) && left.Type is INumericType nt)
        {
            IBinaryExpressionOperation op = nt.RelationalOperation<TOp>();
            return op.CreateExpression(left, right);
        }

        if (left.Type is UIntType<N32> u32t &&
            right is LiteralValueExpression { Literal: I32Literal { Value: var value } })
        {
            var ur = new LiteralValueExpression(Literal.Create((uint)value));
            return ((INumericType)u32t).RelationalOperation<TOp>().CreateExpression(left, ur);
        }

        throw new NotSupportedException();
    }

    static IExpression ToLogicalBinaryExpression<TOp>(IExpression left, IExpression right)
        where TOp : BinaryLogical.IOp<TOp>
    {
        IBinaryExpressionOperation op = LogicalBinaryOperation<TOp>.Instance;
        return op.CreateExpression(ToBoolExpr(left), ToBoolExpr(right));
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
        var e = ToBinaryRelationalExpression<TOp>(l, r);
        CurrentStack.Push(e);
        return default;
    }

    void FlushOutputs()
    {
        Outputs = ImmutableStack.Create<VariableDeclaration>();
        foreach (var expr in CurrentStack)
        {
            var v = new VariableDeclaration(DeclarationScope.Function, string.Empty, expr.Type, []);
            Statements.Add(SyntaxFactory.AssignStatement(
                SyntaxFactory.VarIdentifier(v),
                expr));
            Outputs = Outputs.Push(v);
        }
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
        var e = ToBinaryRelationalExpression<TOp>(l, r);

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
                    var e = be.CreateExpression(l, r);
                    CurrentStack.Push(e);
                    return;
                }
                case IBinaryStatementOperation bs:
                {
                    var r = CurrentStack.Pop();
                    var l = CurrentStack.Pop();
                    var s = bs.CreateStatement(l, r);
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

        var result = SyntaxFactory.Call(func, [..args]);
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

                Statements.Add(SyntaxFactory.Return(null));
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

                Statements.Add(SyntaxFactory.Return(e));
                return default;
            default:
                throw new ValidationException("return when stack.Count > 1", Model.Method);
        }
    }

    public Unit VisitStoreArgument(CilInstructionInfo inst, ParameterDeclaration p)
    {
        var v = CurrentStack.Pop();
        if (!p.Type.Equals(v.Type))
        {
            Statements.Add(SyntaxFactory.AssignStatement(
                SyntaxFactory.ArgIdentifier(p),
                v
            ));
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
            Statements.Add(SyntaxFactory.AssignStatement(
                SyntaxFactory.VarIdentifier(v),
                val
            ));
        }

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