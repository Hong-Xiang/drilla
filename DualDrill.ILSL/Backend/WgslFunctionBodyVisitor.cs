using DualDrill.CLSL.Language.AbstractSyntaxTree.Expression;
using DualDrill.CLSL.Language.AbstractSyntaxTree.Statement;
using DualDrill.CLSL.Language.Literal;
using DualDrill.CLSL.Language.Operation;
using DualDrill.Common.CodeTextWriter;
using System.CodeDom.Compiler;
using DualDrill.CLSL.Language.AbstractSyntaxTree;
using DualDrill.CLSL.Language.ControlFlow;
using DualDrill.CLSL.Language.Declaration;
using DualDrill.CLSL.Language.Types;
using DualDrill.Common.Nat;

namespace DualDrill.CLSL.Backend;

public sealed class WgslFunctionBodyVisitor(ILocalDeclarationContext Context, IndentedTextWriter Writer)
    : IStatementVisitor<ValueTask>
    , IExpressionVisitor<ValueTask>
{
    public async ValueTask VisitReturn(ReturnStatement stmt)
    {
        Writer.Write("return ");
        if (stmt.Expr is not null)
        {
            await stmt.Expr.Accept(this);
        }

        Writer.WriteLine(";");
    }

    string VariableIdentifier(VariableDeclaration variable)
    {
        return variable.DeclarationScope == DeclarationScope.Function
            ? $"loc_{Context.VariableIndex(variable)}"
            : variable.Name;
    }

    public async ValueTask VisitVariableOrValue(VariableOrValueStatement stmt)
    {
        Writer.Write("var ");
        Writer.Write(VariableIdentifier(stmt.Variable));
        Writer.Write(" : ");
        //await VisitTypeReference(stmt.Variable.Type);
        Writer.Write(stmt.Variable.Type.Name);
        //await OnTypeReference(stmt.Variable.Type);
        if (stmt.Variable.Initializer is not null)
        {
            Writer.Write(" = ");
            await stmt.Variable.Initializer.Accept(this);
        }

        Writer.WriteLine(";");
    }

    public async ValueTask VisitCompound(CompoundStatement stmt)
    {
        Writer.WriteLine('{');
        using (Writer.IndentedScope())
        {
            foreach (var s in stmt.Statements)
            {
                await s.Accept(this);
            }
        }

        Writer.WriteLine('}');
    }

    public async ValueTask VisitIf(IfStatement stmt)
    {
        Writer.Write("if (");
        await stmt.Expr.Accept(this);
        Writer.WriteLine(") {");
        if (stmt.TrueBody.Statements.Length > 0)
        {
            using (Writer.IndentedScope())
            {
                await stmt.TrueBody.Accept(this);
            }
        }

        if (stmt.FalseBody.Statements.Length > 0)
        {
            Writer.WriteLine("} else {");
            using (Writer.IndentedScope())
            {
                await stmt.FalseBody.Accept(this);
            }
        }

        Writer.WriteLine("}");
    }

    public async ValueTask VisitWhile(WhileStatement stmt)
    {
        Writer.Write("while ");
        await stmt.Expr.Accept(this);
        Writer.WriteLine();
        await stmt.Statement.Accept(this);
    }

    public async ValueTask VisitBreak(BreakStatement stmt)
    {
        Writer.WriteLine("break;");
    }

    public async ValueTask VisitFor(ForStatement stmt)
    {
        Writer.Write("for (");
        var header = stmt.ForHeader;
        if (header.Init != null)
        {
            await header.Init.AcceptVisitor(this);
        }

        Writer.Write("; ");
        if (header.Expr != null)
        {
            await header.Expr.Accept(this);
        }

        Writer.Write("; ");
        if (header.Update != null)
        {
            await header.Update.AcceptVisitor(this);
        }

        Writer.WriteLine(')');
        await stmt.Statement.AcceptVisitor(this);
    }

    public async ValueTask VisitSimpleAssignment(SimpleAssignmentStatement stmt)
    {
        var op = stmt.Op switch
        {
            AssignmentOp.Assign => "=",
            AssignmentOp.Add => "+=",
            AssignmentOp.Subtract => "-=",
            AssignmentOp.Multiply => "*=",
            AssignmentOp.Divide => "/=",
            AssignmentOp.Modulus => "%=",
            AssignmentOp.BitwiseAnd => "&=",
            AssignmentOp.BitwiseOr => "|=",
            AssignmentOp.ExclusiveOr => "^=",
            AssignmentOp.ShiftLeft => "<<=",
            AssignmentOp.ShiftRight => ">>=",
            _ => throw new NotSupportedException()
        };

        await stmt.L.Accept(this);
        Writer.Write(' ');
        Writer.Write(op);
        Writer.Write(' ');
        await stmt.R.Accept(this);
        Writer.WriteLine(";");
    }

    public async ValueTask VisitPhonyAssignment(PhonyAssignmentStatement stmt)
    {
        Writer.Write("_ = ");
        await stmt.Expr.Accept(this);
        Writer.WriteLine(";");
    }

    public async ValueTask VisitIncrement(IncrementStatement stmt)
    {
        await stmt.Expr.Accept(this);
        Writer.Write("++");
    }

    public async ValueTask VisitDecrement(DecrementStatement stmt)
    {
        await stmt.Expr.Accept(this);
        Writer.Write("--");
    }

    public async ValueTask VisitFunctionCallExpression(FunctionCallExpression expr)
    {
        Writer.Write(expr.Callee.Name);
        Writer.Write('(');
        bool firstArgument = true;
        foreach (var a in expr.Arguments)
        {
            if (!firstArgument)
            {
                Writer.Write(", ");
            }

            firstArgument = false;
            await a.Accept(this);
        }

        Writer.Write(')');
    }

    public async ValueTask VisitIndirectionExpression(IndirectionExpression expr)
    {
        Writer.Write("*(");
        await expr.Expr.Accept(this);
        Writer.Write(")");
    }

    public async ValueTask VisitAddressOfExpression(AddressOfExpression expr)
    {
        Writer.Write("&(");
        await expr.Base.Accept(this);
        Writer.Write(")");
    }

    public async ValueTask VisitBinaryExpression
        <TOperation>
        (BinaryOperationExpression<TOperation> expr)
        where TOperation : IBinaryExpressionOperation<TOperation>
    {
        if (TOperation.Instance.BinaryOp is ISymbolOp symbol)
        {
            Writer.Write("( ");
            await expr.L.Accept(this);
            Writer.Write(") ");
            Writer.Write(symbol.Symbol);
            Writer.Write("( ");
            await expr.R.Accept(this);
            Writer.Write(")");
        }
        else
        {
            throw new NotSupportedException($"Non symbol binary op {TOperation.Instance.Name} is not supported.");
        }
    }

    public async ValueTask VisitUnaryExpression
        <TOperation, TSourceType, TResultType, TOp>
        (UnaryOperationExpression<TOperation> expr)
        where TOperation : IUnaryExpressionOperation<TOperation, TSourceType, TResultType, TOp>
        where TSourceType : ISingletonShaderType<TSourceType>
        where TResultType : ISingletonShaderType<TResultType>
        where TOp : IUnaryOp<TOp>
    {
        if (TOp.Instance is ISymbolOp symbol)
        {
            Writer.Write(symbol.Symbol);
            Writer.Write("(");
            await expr.Source.Accept(this);
            Writer.Write(")");
        }
        else
        {
            throw new NotSupportedException($"Unary operation {TOperation.Instance.Name} is not supported.");
        }
    }

    public async ValueTask VisitConversionExpression<TTarget>(IUnaryExpression expr)
        where TTarget : ISingletonShaderType<TTarget>
    {
        Writer.Write(TTarget.Instance.Name);
        Writer.Write('(');
        await expr.Source.Accept(this);
        Writer.Write(')');
    }

    public async ValueTask VisitLiteralValueExpression(LiteralValueExpression expr)
    {
        var code = expr.Literal switch
        {
            BoolLiteral l => l.Value ? "true" : "false",
            I32Literal { Value: var value } => value + "i",
            I64Literal { Value: var value } => value + "i",
            U32Literal { Value: var value } => value + "u",
            U64Literal { Value: var value } => value + "u",
            F32Literal { Value: var value } => value + "f",
            F64Literal { Value: var value } => value + "f",
            _ => throw new NotSupportedException($"{nameof(VisitLiteralValueExpression)} not support {expr}")
        };
        Writer.Write(code);
    }

    public async ValueTask VisitVariableIdentifierExpression(VariableIdentifierExpression expr)
    {
        switch (expr.Variable)
        {
            case VariableDeclaration v:
                Writer.Write(VariableIdentifier(v));
                break;
            case ParameterDeclaration p:
                Writer.Write(p.Name);
                break;
            default:
                Writer.Write(expr.Variable.Name);
                break;
        }
    }

    public async ValueTask VisitFormalParameterExpression(FormalParameterExpression expr)
    {
        Writer.Write(expr.Parameter.Name);
    }


    public async ValueTask AppendSemicolon(ValueTask task)
    {
        await task;
        Writer.WriteLine(';');
    }

    public async ValueTask
        VisitVectorSwizzleSet<TRank, TElement, TPattern>(VectorSwizzleSetStatement<TRank, TElement, TPattern> stmt)
        where TRank : IRank<TRank>
        where TElement : IScalarType<TElement>
        where TPattern : Swizzle.ISizedPattern<TRank, TPattern>
    {
        switch (stmt.Target)
        {
            case AddressOfExpression { Base: var b }:
                await b.Accept(this);
                break;
            default:
                await SyntaxFactory.Indirection(stmt.Target).Accept(this);
                break;
        }

        Writer.Write('.');
        Writer.Write(TPattern.Instance.Name);
        Writer.Write(" = ");
        await stmt.Value.Accept(this);
        Writer.WriteLine(';');
    }

    public async ValueTask
        VisitVectorComponentSet<TRank, TElement, TComponent>(
            VectorComponentSetStatement<TRank, TElement, TComponent> stmt) where TRank : IRank<TRank>
                                                                           where TElement : IScalarType<TElement>
                                                                           where TComponent : Swizzle.ISizedComponent<
                                                                               TRank, TComponent>
    {
        switch (stmt.Target)
        {
            case AddressOfExpression { Base: var b }:
                await b.Accept(this);
                break;
            default:
                await SyntaxFactory.Indirection(stmt.Target).Accept(this);
                break;
        }

        Writer.Write(".");
        Writer.Write(TComponent.Instance.Name);
        Writer.Write(" = ");
        await stmt.Value.Accept(this);
        Writer.WriteLine(';');
    }


    public async ValueTask VisitVectorSwizzleGetExpression<TPattern, TElement>(IUnaryExpression expr)
        where TPattern : Swizzle.IPattern<TPattern>
        where TElement : IScalarType<TElement>
    {
        switch (expr.Source)
        {
            case AddressOfExpression { Base: var b }:
                await b.Accept(this);
                break;
            default:
                await SyntaxFactory.Indirection(expr).Accept(this);
                break;
        }

        Writer.Write('.');
        Writer.Write(TPattern.Instance.Name);
    }

    public async ValueTask VisitVectorComponentGetExpression<TRank, TVector, TComponent>(IUnaryExpression expr)
        where TRank : IRank<TRank>
        where TVector : ISizedVecType<TRank, TVector>
        where TComponent : Swizzle.ISizedComponent<TRank, TComponent>
    {
        switch (expr.Source)
        {
            case AddressOfExpression { Base: var b }:
                await b.Accept(this);
                break;
            default:
                await SyntaxFactory.Indirection(expr).Accept(this);
                break;
        }

        Writer.Write('.');
        Writer.Write(TComponent.Instance.Name);
    }

    public async ValueTask VisitNamedComponentExpression(NamedComponentExpression expr)
    {
        await expr.Base.Accept(this);
        Writer.Write(".");
        Writer.Write(expr.Component.Name);
    }

    public async ValueTask VisitLoop(LoopStatement stmt)
    {
        Writer.Write("loop ");
        using (Writer.IndentedScopeWithBracket())
        {
            await stmt.Body.AcceptVisitor(this);
        }
    }

    public async ValueTask VisitSwitch(SwitchStatement stmt)
    {
        Writer.Write("switch (");
        await stmt.Expr.Accept(this);
        Writer.WriteLine(") {");
        using (Writer.IndentedScope())
        {
            foreach (var c in stmt.Cases)
            {
                Writer.Write("case ");
                await c.Label.Accept(this);
                Writer.WriteLine(" : {");
                using (Writer.IndentedScope())
                {
                    await c.Body.AcceptVisitor(this);
                }

                Writer.WriteLine("}");
            }

            Writer.Write("default : {");
            using (Writer.IndentedScope())
            {
                await stmt.DefaultCase.AcceptVisitor(this);
            }

            Writer.Write("}");
        }

        Writer.WriteLine("}");
    }

    public ValueTask VisitContinue(ContinueStatement stmt)
    {
        Writer.WriteLine("continue;");
        return ValueTask.CompletedTask;
    }
}