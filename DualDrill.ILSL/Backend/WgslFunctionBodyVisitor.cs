using DualDrill.CLSL.Language.AbstractSyntaxTree.Expression;
using DualDrill.CLSL.Language.AbstractSyntaxTree.Statement;
using DualDrill.CLSL.Language.Literal;
using DualDrill.CLSL.Language.Operation;
using DualDrill.Common.CodeTextWriter;
using System.CodeDom.Compiler;
using DualDrill.CLSL.Language.Types;

namespace DualDrill.CLSL.Backend;

public sealed class WgslFunctionBodyVisitor(IndentedTextWriter Writer)
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
    }

    public async ValueTask VisitVariableOrValue(VariableOrValueStatement stmt)
    {
        Writer.Write("var ");
        Writer.Write(stmt.Variable.Name);
        Writer.Write(" : ");
        //await VisitTypeReference(stmt.Variable.Type);
        Writer.Write(stmt.Variable.Type.Name);
        //await OnTypeReference(stmt.Variable.Type);
        if (stmt.Variable.Initializer is not null)
        {
            Writer.Write(" = ");
            await stmt.Variable.Initializer.Accept(this);
        }
    }

    public async ValueTask VisitCompound(CompoundStatement stmt)
    {
        Writer.WriteLine('{');
        using (Writer.IndentedScope())
        {
            foreach (var s in stmt.Statements)
            {
                await s.AcceptVisitor(this);
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
                await stmt.TrueBody.AcceptVisitor(this);
            }
        }

        Writer.WriteLine("} else {");
        if (stmt.FalseBody.Statements.Length > 0)
        {
            using (Writer.IndentedScope())
            {
                await stmt.FalseBody.AcceptVisitor(this);
            }
        }

        Writer.WriteLine("}");
    }

    public async ValueTask VisitWhile(WhileStatement stmt)
    {
        Writer.Write("while ");
        await stmt.Expr.Accept(this);
        Writer.WriteLine();
        await stmt.Statement.AcceptVisitor(this);
    }

    public async ValueTask VisitBreak(BreakStatement stmt)
    {
        Writer.Write("break");
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
    }

    public async ValueTask VisitPhonyAssignment(PhonyAssignmentStatement stmt)
    {
        Writer.Write("_ = ");
        await stmt.Expr.Accept(this);
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

    public async ValueTask VisitBinaryExpression
        <TOperation, TLeftType, TRightType, TResultType, TOp>
        (IBinaryExpressionOperation<TOperation, TLeftType, TRightType, TResultType, TOp>.Expression expr)
        where TOperation : IBinaryExpressionOperation<TOperation, TLeftType, TRightType, TResultType, TOp>
        where TLeftType : ISingletonShaderType<TLeftType>
        where TRightType : ISingletonShaderType<TRightType>
        where TResultType : ISingletonShaderType<TResultType>
        where TOp : IBinaryOp<TOp>
    {
        if (TOp.Instance is ISymbolOp symbol)
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
            throw new NotSupportedException($"Non symbol binary op {TOp.Instance.Name} is not supported.");
        }
    }

    public async ValueTask VisitUnaryExpression
        <TOperation, TSourceType, TResultType, TOp>
        (IUnaryExpressionOperation<TOperation, TSourceType, TResultType, TOp>.Expression expr)
        where TOperation : IUnaryExpressionOperation<TOperation, TSourceType, TResultType, TOp>
        where TSourceType : ISingletonShaderType<TSourceType>
        where TResultType : ISingletonShaderType<TResultType>
        where TOp : IUnaryOp<TOp>
    {
        if (TOp.Instance is ISymbolOp symbol)
        {
            Writer.Write(symbol.Symbol);
            Writer.Write("(");
            await expr.Expr.Accept(this);
            Writer.Write(")");
        }
        else
        {
            throw new NotSupportedException($"Unary operation {TOperation.Instance.Name} is not supported.");
        }
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
        Writer.Write(expr.Variable.Name);
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

    public async ValueTask VisitVectorSwizzleAccessExpression(VectorSwizzleAccessExpression expr)
    {
        await expr.Base.Accept(this);
        Writer.Write('.');
        foreach (SwizzleComponent c in expr.Components)
        {
            Writer.Write(c);
        }
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